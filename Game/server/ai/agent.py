"""
Boss AI agent that plays against the player.

Wraps a reinforcement learning policy and trajectory collector to:
1. Take game state and return actions (via policy forward pass)
2. Collect experience (observations, rewards) during gameplay
3. Produce training batches when episodes end
"""

from .policy import HybridPPOPolicy
from training.trajectory_collector import TrajectoryCollector


class BossAI:
    """
    The boss opponent controlled by a learned PPO policy.
    
    Responsibilities:
    - Maintains a policy (shared across all connections)
    - Collects trajectories during gameplay
    - Computes rewards using the RewardTracker
    - Returns batches ready for policy updates
    """
    
    def __init__(self, shared_policy: HybridPPOPolicy):
        """
        Initialize the boss AI with a shared policy.
        
        Args:
            shared_policy: The HybridPPOPolicy instance to use.
                          Should be the same for all connections.
        """
        self.policy = shared_policy
        self.collector = TrajectoryCollector(self.policy)

    def choose_action(self, static_state: dict, dynamic_state: dict) -> tuple[float, float, int]:
        """
        Choose an action given the current game state.
        
        Converts game state to observation vector, runs through policy,
        and returns movement + action_id to send to the game.
        
        Args:
            static_state: Equipment and room bounds (from protocol.decode_static_state)
            dynamic_state: Player/boss position, health, cooldowns (from protocol.decode_dynamic_state)
        
        Returns:
            Tuple of (movement_x, movement_y, action_id) to send to game
        """
        return self.collector.collect_step(static_state, dynamic_state)

    def on_episode_end(self, won: bool, final_player_hp: float, final_boss_hp: float) -> dict | None:
        """
        Called when the episode ends (player win or boss win).
        
        Computes terminal reward and returns a training batch.
        
        Args:
            won: True if boss won, False if player won
            final_player_hp: Player's HP at episode end
            final_boss_hp: Boss's HP at episode end
        
        Returns:
            Training batch (ready for PPOTrainer.update()) or None if trajectory empty
        """
        return self.collector.end_episode(won, final_player_hp, final_boss_hp)

    def on_reset(self):
        """Reset the collector for a new episode without training."""
        self.collector.reset()
