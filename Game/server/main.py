"""
WebSocket server for boss AI training and inference.

Supports two modes:
- Training: Learn policy from gameplay (--mode=training)
- Inference: Run pre-trained policy without learning (--mode=inference --policy-path=checkpoints/policy.pt)
"""

import asyncio
import websockets
import sys
import os
import logging
import json
from typing import Optional
import torch

# Add current directory to path so relative imports work
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from network.connection import PacketHandler
from training.training_manager import TrainingManager
from training.orchestrator import TrainingOrchestrator
from ai.policy import HybridPPOPolicy

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Global state
training_manager = None
orchestrator: Optional[TrainingOrchestrator] = None
inference_policy: Optional[HybridPPOPolicy] = None  # For inference-only mode
instance_id_to_websocket = {}  # Map instance_id to websocket for messaging
server_mode = "training"  # "training" or "inference"
max_episodes: int = 0  # 0 = unlimited


async def do_shutdown(reason: str):
    """Save the final policy and terminate all Godot instances."""
    print(f"\n[SHUTDOWN] {reason}")
    if training_manager:
        final_path = os.path.join(training_manager.checkpoint_dir, "final_policy.pt")
        torch.save(training_manager.policy.state_dict(), final_path)
        print(f"[SHUTDOWN] Final policy saved → {final_path}")
    if orchestrator:
        await orchestrator.cleanup()
        print("[SHUTDOWN] Godot instances terminated")


async def connection_handler(websocket, path: str = ""):
    """
    Handle a new game client connection.
    
    Each client gets its own PacketHandler but shares the same policy
    via the global TrainingManager.
    
    Extracts instance_id from handshake or URL query.
    
    Args:
        websocket: The new WebSocket connection from a client
        path: Optional path from WebSocket connection
    """
    # Try to extract instance_id from various sources
    instance_id = 0
    
    # Check for instance_id in initial message (handshake)
    try:
        first_msg = await asyncio.wait_for(websocket.recv(), timeout=2.0)
        if isinstance(first_msg, str):
            try:
                data = json.loads(first_msg)
                instance_id = data.get("instance_id", 0)
            except:
                pass
        
        # Put message back by storing state
        initial_payload = first_msg if isinstance(first_msg, bytes) else first_msg.encode()
    except asyncio.TimeoutError:
        initial_payload = None
    
    handler = PacketHandler(
        shared_policy=training_manager.get_policy() if training_manager else inference_policy,
        training_manager=training_manager,
        instance_id=instance_id,
        orchestrator=orchestrator,
        instance_websockets=instance_id_to_websocket,
    )
    
    # Mark instance as connected if using orchestrator
    if orchestrator:
        orchestrator.mark_instance_connected(instance_id)
        instance_id_to_websocket[instance_id] = websocket
        logger.info(f"Instance {instance_id} connected")
    else:
        if server_mode == "inference":
            print(f"client connected (inference mode)")
        else:
            print("client connected")

    try:
        # Handle initial message if we captured it
        if initial_payload and len(initial_payload) > 1:
            msg_type = initial_payload[0]
            payload = initial_payload[1:]
            try:
                await handler.handle(websocket, msg_type, payload)
            except Exception as exc:
                import traceback
                print(f"[ERROR] Handler exception on initial message: {exc}")
                traceback.print_exc()

        # Handle remaining messages
        async for packet in websocket:
            if isinstance(packet, str):
                # Skip text messages
                continue

            msg_type = packet[0]
            payload = packet[1:]
            try:
                await handler.handle(websocket, msg_type, payload)
            except Exception as exc:
                import traceback
                print(f"[ERROR] Handler exception (msg_type={msg_type}): {exc}")
                traceback.print_exc()

    except websockets.ConnectionClosed:
        if orchestrator:
            logger.info(f"Instance {instance_id} disconnected")
            if instance_id in instance_id_to_websocket:
                del instance_id_to_websocket[instance_id]
        else:
            print("client disconnected")



async def training_loop():
    """
    Trigger training when enough experience accumulates (not on fixed timer).

    With real-time gameplay where episodes can be 2-10 minutes long,
    we train based on:
    - Episode count: when 4+ episodes are queued
    - OR transition count: when 512+ transitions accumulated

    This adapts to fight length instead of forcing training every 5 seconds.
    """
    while True:
        await asyncio.sleep(2.0)  # Check frequently but don't spam

        # Calculate total transitions in queue
        total_transitions = sum(
            batch["obs"].shape[0] for batch in training_manager.batch_queue
            if "obs" in batch
        ) if hasattr(training_manager.batch_queue, '__iter__') else 0

        # Train if:
        # - 4+ episodes queued (good batch for PPO)
        # - OR 512+ transitions accumulated (enough for 4 epochs of training)
        should_train = (
            len(training_manager.batch_queue) >= 4
            or total_transitions >= 512
        )

        if should_train and training_manager.batch_queue:
            batches_trained = await training_manager.training_step(force=False)
            
            if batches_trained > 0:
                stats = training_manager.get_stats()

                # Log training info with trigger reason
                if len(training_manager.batch_queue) >= 4:
                    trigger_reason = f"episode_count={len(training_manager.batch_queue)}"
                else:
                    trigger_reason = f"transition_count={total_transitions}"

                # Log basic stats
                print(f"\n{'='*60}")
                print(f"Training Step #{stats['training_steps_completed']} [trigger: {trigger_reason}]")
                print(f"Episodes Processed: {stats['episodes_processed']}")
                print(f"Batch Size: {stats.get('recent_metrics', {}).get('batch_size', 0)} transitions")
                print(f"Batches Pending: {stats['batches_pending']}")
                
                # Log orchestrator status if available
                if orchestrator:
                    print(f"\nOrchestrator Status:")
                    print(f"  Connected Instances: {len(orchestrator.get_connected_instances())}/{orchestrator.num_instances}")
                    print(f"  Total Episodes: {orchestrator.get_total_episodes()}")
                    print(f"  Total Transitions: {orchestrator.get_total_transitions()}")
                
                # Log detailed metrics if available
                if stats['recent_metrics']:
                    metrics = stats['recent_metrics']
                    print(f"\nLoss Metrics:")
                    print(f"  Policy Loss:  {metrics.get('policy_loss', 0.0):.6f}")
                    print(f"  Value Loss:   {metrics.get('value_loss', 0.0):.6f}")
                    print(f"  Entropy Loss: {metrics.get('entropy_loss', 0.0):.6f}")
                    print(f"  Total Loss:   {metrics.get('total_loss', 0.0):.6f}")
                    
                    print(f"\nAdvantage Statistics:")
                    print(f"  Mean: {metrics.get('advantage_mean', 0.0):.6f}")
                    print(f"  Std:  {metrics.get('advantage_std', 0.0):.6f}")
                
                # Log summary of recent training
                summary = training_manager.get_metrics_summary()
                if summary:
                    print(f"\nRecent Average (last 10 steps):")
                    print(f"  Policy Loss:  {summary.get('policy_loss_avg', 0.0):.6f} "
                          f"(min: {summary.get('policy_loss_min', 0.0):.6f}, "
                          f"max: {summary.get('policy_loss_max', 0.0):.6f})")
                    print(f"  Total Loss:   {summary.get('total_loss_avg', 0.0):.6f} "
                          f"(min: {summary.get('total_loss_min', 0.0):.6f}, "
                          f"max: {summary.get('total_loss_max', 0.0):.6f})")
                
                print(f"{'='*60}\n")

                # --- Stopping conditions ---
                stop_reason = None
                if max_episodes > 0 and training_manager.episodes_processed >= max_episodes:
                    stop_reason = f"reached max episodes ({max_episodes})"
                elif training_manager.check_plateau():
                    stop_reason = "performance plateau detected (loss not improving)"

                if stop_reason:
                    print(f"[TRAINING] Stopping: {stop_reason}")
                    return  # main() finally block handles save + cleanup



async def main():
    """
    Start the WebSocket server with optional training loop.
    
    Server listens on ws://localhost:7000 for game client connections.
    
    Supports command-line arguments:
    
    TRAINING MODE (default):
    - --mode=training: Enable training (default)
    - --num-instances N: Spawn N Godot instances (default: 1)
    - --godot-exec PATH: Path to Godot executable
    - --port N: Server port (default: 7000)
    
    INFERENCE MODE:
    - --mode=inference: Disable training, use pre-trained policy
    - --policy-path PATH: Path to saved policy checkpoint (required for inference)
    - --port N: Server port (default: 7000)
    """
    global training_manager, orchestrator, inference_policy, server_mode
    
    # Parse command-line arguments
    mode = "training"
    global max_episodes

    num_instances = 1
    godot_exec = ""
    server_port = 7000
    policy_path = ""
    debug_collisions = False
    debug_navigation = False

    for arg in sys.argv[1:]:
        if arg.startswith("--mode="):
            mode = arg.split("=")[1]
        elif arg.startswith("--num-instances="):
            num_instances = int(arg.split("=")[1])
        elif arg.startswith("--godot-exec="):
            godot_exec = arg.split("=", 1)[1]
        elif arg.startswith("--port="):
            server_port = int(arg.split("=")[1])
        elif arg.startswith("--policy-path="):
            policy_path = arg.split("=", 1)[1]
        elif arg.startswith("--max-episodes="):
            max_episodes = int(arg.split("=")[1])
        elif arg == "--debug-collisions":
            debug_collisions = True
        elif arg == "--debug-navigation":
            debug_navigation = True
    
    server_mode = mode
    
    print("="*60)
    print("Boss AI Server")
    print("="*60)
    print(f"Mode: {mode.upper()}")
    print(f"Port: {server_port}")
    
    # INFERENCE MODE
    if mode == "inference":
        if not policy_path:
            print("[ERROR] Inference mode requires --policy-path=PATH/TO/POLICY.pt")
            return
        
        try:
            print(f"Loading policy from: {policy_path}")
            inference_policy = torch.load(policy_path)
            inference_policy.eval()  # Set to eval mode (no dropout, etc)
            print(f"Policy loaded successfully")
            print("="*60)
        except Exception as e:
            print(f"[ERROR] Failed to load policy: {e}")
            return
    
    # TRAINING MODE
    else:
        # Initialize the centralized training manager
        training_manager = TrainingManager(
            obs_dim=56,
            num_actions=7,
            checkpoint_dir="checkpoints",
            checkpoint_interval=10
        )
        
        print(f"Checkpoint directory: {training_manager.checkpoint_dir}")
        print(f"Checkpoint interval: every {training_manager.checkpoint_interval} training steps")
        print("="*60)

    # Start server first so Godot can connect immediately after being spawned
    async with websockets.serve(connection_handler, "localhost", server_port):
        print(f"[SERVER] Listening on ws://localhost:{server_port}")

        if mode == "training":
            # Spawn Godot instances now that the server is ready
            try:
                server_dir = os.path.dirname(os.path.abspath(__file__))
                godot_project_path = os.path.normpath(os.path.join(server_dir, "..", "ai_boss"))
                orchestrator = TrainingOrchestrator(
                    num_instances=num_instances,
                    godot_executable=godot_exec,
                    godot_project_path=godot_project_path,
                    headless=False,
                    debug_collisions=debug_collisions,
                    debug_navigation=debug_navigation,
                )
                print(f"Spawning {num_instances} instance(s)")
                print(f"Godot executable: {orchestrator.godot_executable}")

                configs = orchestrator.create_instance_configs(base_seed=42)
                success = await orchestrator.spawn_instances(configs)

                if not success:
                    print("[ERROR] Failed to spawn instances")
                    return

                asyncio.create_task(orchestrator.monitor_instances())

                print("Waiting for instances to connect...")
                await asyncio.sleep(5)

                connected = orchestrator.get_connected_instances()
                print(f"Connected instances: {len(connected)}/{num_instances}")
            except Exception as e:
                print(f"[ERROR] Failed to initialize orchestrator: {e}")
                logger.exception(e)
                return

            if max_episodes > 0:
                print(f"Max episodes: {max_episodes}")
            else:
                print("Max episodes: unlimited (stops on plateau or Ctrl+C)")
            print(f"[SERVER] Training loop starting (checks every 2 seconds)\n")

            try:
                await training_loop()
            except (KeyboardInterrupt, asyncio.CancelledError):
                pass
            finally:
                await do_shutdown("Training ended")
        else:
            print(f"[SERVER] Inference mode active (no training)\n")
            try:
                await asyncio.Event().wait()
            except (KeyboardInterrupt, asyncio.CancelledError):
                pass


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass  # Clean exit, shutdown already handled inside main()
