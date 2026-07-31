"""
WebSocket message handler for each game client connection.

Decodes game state from binary protocol, queries the AI for actions,
computes rewards, and queues batches for centralized training.
"""

from __future__ import annotations

from network.state import SessionState
from ai.agent import BossAI
from network.protocol import (
    STATIC_STATE,
    DYNAMIC_STATE,
    OUTCOME,
    COMMAND,
    decode_static_state,
    decode_dynamic_state,
    decode_outcome,
    decode_command,
    encode_action,
    encode_command,
)


class PacketHandler:
    """
    Handles all messages for a single game client connection.
    
    Responsibilities:
    - Decode binary game state packets
    - Query shared AI for actions
    - Track episode end and queue training batches
    - Handle connection reset/close commands
    """
    
    def __init__(self, shared_policy, training_manager, instance_id: int = 0,
                 orchestrator=None, instance_websockets: dict = None):
        """
        Initialize packet handler for a new connection.

        Args:
            shared_policy: The shared HybridPPOPolicy from TrainingManager
            training_manager: The TrainingManager to queue batches to
            instance_id: Unique identifier for this Godot instance (0 if single-instance)
            orchestrator: Optional TrainingOrchestrator for coordinating multi-instance training
            instance_websockets: Shared dict mapping instance_id -> websocket for broadcasting
        """
        self.state = SessionState()
        self.ai = BossAI(shared_policy)
        self.training_manager = training_manager
        self.instance_id = instance_id
        self.orchestrator = orchestrator
        self.instance_websockets = instance_websockets if instance_websockets is not None else {}

    async def handle(self, websocket, msg_type: int, payload: bytes):
        """
        Route incoming messages based on type and handle accordingly.
        
        Args:
            websocket: WebSocket connection to send responses to
            msg_type: Protocol message type (STATIC_STATE, DYNAMIC_STATE, OUTCOME, COMMAND)
            payload: Binary payload decoded from packet
        """
        if msg_type == STATIC_STATE:
            # Equipment and room bounds - store for later.
            # Also marks start of a new episode (game resends this after every restart).
            self.state.static = decode_static_state(payload)
            self.state.done = False

        elif msg_type == DYNAMIC_STATE:
            # Player/boss positions, health, cooldowns
            self.state.dynamic = decode_dynamic_state(payload)

            if self.state.static is None:
                # STATIC_STATE hasn't arrived yet for this connection/episode — nothing
                # to build an observation from. Should be rare now that MainHandler.cs
                # gates DYNAMIC_STATE sends on STATIC_STATE having gone out first, but
                # skip gracefully instead of crashing if it ever happens anyway.
                return

            # Query AI for action
            x, y, action_id = self.ai.choose_action(self.state.static, self.state.dynamic)
            # Send action back to game
            await websocket.send(encode_action(x, y, action_id))

        elif msg_type == OUTCOME:
            # Episode ended (player win or boss win)
            won = decode_outcome(payload)
            self.state.done = True

            # Read HP from the last dynamic state before anything resets it.
            final_player_hp = self.state.dynamic["player"]["Health"] if self.state.dynamic else 0.0
            final_boss_hp   = self.state.dynamic["boss"]["Health"]   if self.state.dynamic else 0.0

            # Send restart BEFORE batch processing so a crash in on_episode_end
            # never prevents the game from starting the next episode.
            if not self.orchestrator:
                print(f"[SERVER] Sending restart (non-orchestrator)...")
                await websocket.send(encode_command(0))
                print(f"[SERVER] Restart sent")
                static_backup = self.state.static
                self.state = SessionState()
                self.state.static = static_backup

            # Process the training batch (non-fatal if it fails)
            transitions_count = 0
            try:
                batch = self.ai.on_episode_end(won, final_player_hp, final_boss_hp)
                transitions_count = batch["obs"].shape[0] if batch else 0
                await self.training_manager.add_batch(batch)
            except Exception as exc:
                import traceback
                print(f"[WARNING] Error processing episode batch (restart already sent): {exc}")
                traceback.print_exc()

            if self.orchestrator:
                self.orchestrator.mark_episode_end(
                    instance_id=self.instance_id,
                    boss_wins=won,
                    transitions_count=transitions_count
                )
                # Wait for all instances to finish before training and restarting
                if self.orchestrator.all_episodes_done():
                    print(f"[SERVER] All episodes done — running training step...")
                    try:
                        await self.training_manager.training_step(force=True)
                        print(f"[SERVER] Training step complete")
                    except Exception as exc:
                        import traceback
                        print(f"[WARNING] Training step failed (restart still sent): {exc}")
                        traceback.print_exc()
                    self.orchestrator.mark_all_episode_start()
                    restart = encode_command(0)
                    print(f"[SERVER] Sending restart to {len(self.instance_websockets)} instance(s)...")
                    for ws in list(self.instance_websockets.values()):
                        try:
                            await ws.send(restart)
                            print(f"[SERVER] Restart sent")
                        except Exception as exc:
                            print(f"[WARNING] Failed to send restart to instance: {exc}")

        elif msg_type == COMMAND:
            # Control commands from client
            cmd = decode_command(payload)
            if cmd == 0:
                # Reset: new episode. Preserve static state (equipment/room bounds)
                # across the reset — the client doesn't necessarily resend STATIC_STATE
                # before the next DYNAMIC_STATE arrives on this connection, and losing
                # it here would crash flatten_observation() on the next dynamic packet.
                static_backup = self.state.static
                self.state = SessionState()
                self.state.static = static_backup
                self.ai.on_reset()
            elif cmd == 1:
                # Close: disconnect
                await websocket.close()
