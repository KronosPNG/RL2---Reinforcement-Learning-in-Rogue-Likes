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
    
    def __init__(self, shared_policy, training_manager, instance_id: int = 0, orchestrator=None):
        """
        Initialize packet handler for a new connection.
        
        Args:
            shared_policy: The shared HybridPPOPolicy from TrainingManager
            training_manager: The TrainingManager to queue batches to
            instance_id: Unique identifier for this Godot instance (0 if single-instance)
            orchestrator: Optional TrainingOrchestrator for coordinating multi-instance training
        """
        self.state = SessionState()
        self.ai = BossAI(shared_policy)
        self.training_manager = training_manager
        self.instance_id = instance_id
        self.orchestrator = orchestrator

    async def handle(self, websocket, msg_type: int, payload: bytes):
        """
        Route incoming messages based on type and handle accordingly.
        
        Args:
            websocket: WebSocket connection to send responses to
            msg_type: Protocol message type (STATIC_STATE, DYNAMIC_STATE, OUTCOME, COMMAND)
            payload: Binary payload decoded from packet
        """
        if msg_type == STATIC_STATE:
            # Equipment and room bounds - store for later
            self.state.static = decode_static_state(payload)

        elif msg_type == DYNAMIC_STATE:
            # Player/boss positions, health, cooldowns
            self.state.dynamic = decode_dynamic_state(payload)

            # Query AI for action
            x, y, action_id = self.ai.choose_action(self.state.static, self.state.dynamic)
            # Send action back to game
            await websocket.send(encode_action(x, y, action_id))

        elif msg_type == OUTCOME:
            # Episode ended (player win or boss win)
            won = decode_outcome(payload)
            self.state.done = True
            
            # Get final HP for terminal reward computation
            final_player_hp = self.state.dynamic["player"]["Health"]
            final_boss_hp = self.state.dynamic["boss"]["Health"]
            
            # Get training batch and queue it
            batch = self.ai.on_episode_end(won, final_player_hp, final_boss_hp)
            transitions_count = batch["observations"].shape[0] if batch else 0
            await self.training_manager.add_batch(batch)
            
            # Notify orchestrator if available
            if self.orchestrator:
                self.orchestrator.mark_episode_end(
                    instance_id=self.instance_id,
                    boss_wins=won,
                    transitions_count=transitions_count
                )

        elif msg_type == COMMAND:
            # Control commands from client
            cmd = decode_command(payload)
            if cmd == 0:
                # Reset: new episode
                self.state = SessionState()
                self.ai.on_reset()
            elif cmd == 1:
                # Close: disconnect
                await websocket.close()
