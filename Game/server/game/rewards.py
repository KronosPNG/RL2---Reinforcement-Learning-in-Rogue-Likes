"""
Reward computation for boss training.

Implements shaped rewards that incentivize the boss to deal damage,
avoid damage, and avoid action spam.
"""

from __future__ import annotations


class RewardTracker:
    """Tracks and computes step and terminal rewards during episodes."""
    
    def __init__(self):
        self.last_action_id = None
        self.same_action_streak = 0

    def reset(self):
        """Reset for a new episode."""
        self.last_action_id = None
        self.same_action_streak = 0

    def step_reward(
        self,
        player_damage: float,
        boss_damage: float,
        action_id: int,
    ) -> float:
        """
        Compute reward for a single step.
        
        Args:
            player_damage: Damage dealt TO the player
            boss_damage: Damage dealt TO the boss
            action_id: The action taken (for anti-spam penalty)
        
        Returns:
            Reward value
        """
        r = 0.0

        # main shaping: reward dealing damage, penalize taking damage
        r += 0.4 * player_damage
        r -= 0.6 * boss_damage

        # anti-spam: repeated identical action gets increasingly punished
        if self.last_action_id == action_id:
            self.same_action_streak += 1
        else:
            self.same_action_streak = 0

        r -= 0.005 * (self.same_action_streak ** 2)

        self.last_action_id = action_id
        return r

    def terminal_reward(self, boss_wins: bool, boss_hp: float, player_hp: float) -> float:
        """
        Compute terminal reward when episode ends.
        
        Args:
            boss_wins: True if boss won, False if player won
            boss_hp: Boss HP at episode end
            player_hp: Player HP at episode end
        
        Returns:
            Terminal reward (bonus for close wins, penalty for losses)
        """
        hp_gap = boss_hp - player_hp

        # reward a close win more than a steamroll
        target_gap = 0.15
        close_bonus = max(0.0, 1.0 - abs(hp_gap - target_gap) / 0.3)

        if boss_wins:
            return 10.0 + close_bonus
        return -10.0
