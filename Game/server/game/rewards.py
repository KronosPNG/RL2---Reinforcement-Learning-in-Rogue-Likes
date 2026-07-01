"""
Reward computation for boss training.

Implements shaped rewards that incentivize the boss to deal damage,
avoid damage, and avoid action spam.
"""

from __future__ import annotations
import math

# Action IDs matching BossAction.cs ActionType enum
DASH = 1
ATTACK_IDS = {3, 4, 5, 6}  # Melee1, Melee2, Magic1, Magic2

MISS_THRESHOLD = math.pi / 4   # 45° — attacks beyond this are penalized
MIN_DASH_GAP = 30              # steps before dashing again without penalty
                               # dash lasts 2s = ~24 steps at 83ms/step


class RewardTracker:
    """Tracks and computes step and terminal rewards during episodes."""

    MAX_PLAYER_HP = 20
    MAX_BOSS_HP = 2000

    def __init__(self):
        self.last_action_id = None
        self.same_action_streak = 0
        self.steps_since_last_dash = 999

    def reset(self):
        """Reset for a new episode."""
        self.last_action_id = None
        self.same_action_streak = 0
        self.steps_since_last_dash = 999

    def step_reward(
        self,
        player_damage: float,
        boss_damage: float,
        action_id: int,
        angle_to_player: float = 0.0,
        on_cooldown: bool = False,
    ) -> float:
        """
        Compute reward for a single step.

        Args:
            player_damage: Damage dealt TO the player this step
            boss_damage: Damage dealt TO the boss this step
            action_id: The action taken this step
            angle_to_player: Boss facing angle to player in radians [-pi, pi]
            on_cooldown: True if the chosen attack is still on cooldown

        Returns:
            Reward value
        """
        reward = 0.0

        # Damage shaping — normalized by max HP so coefficients are comparable
        reward += 0.6 * (player_damage / self.MAX_PLAYER_HP)
        reward -= 0.4 * (boss_damage / self.MAX_BOSS_HP)

        # Detect the first step of a new attack
        is_new_attack = action_id in ATTACK_IDS and action_id != self.last_action_id

        # Cooldown penalty: attack chosen while still on cooldown (game ignores it)
        if is_new_attack and on_cooldown:
            reward -= 0.2

        # Miss penalty: new attack fired while not facing the player
        if is_new_attack:
            abs_angle = abs(angle_to_player)
            if abs_angle > MISS_THRESHOLD:
                miss_factor = (abs_angle - MISS_THRESHOLD) / (math.pi - MISS_THRESHOLD)
                reward -= 0.3 * miss_factor  # 0 at 45 deg, -0.3 at 180 deg

        # Dash spam: penalize dashing too soon after the previous dash.
        # Boss is immune (collision disabled) for the full 2s dash duration,
        # so chaining dashes creates near-permanent invulnerability.
        self.steps_since_last_dash += 1
        if action_id == DASH:
            if self.steps_since_last_dash < MIN_DASH_GAP:
                reward -= 0.1 * (1.0 - self.steps_since_last_dash / MIN_DASH_GAP)
            self.steps_since_last_dash = 0

        # Attack spam: penalize repeating the same attack back-to-back.
        # Idle and Walk are intentionally excluded — strategic waiting is fine.
        if action_id in ATTACK_IDS and action_id == self.last_action_id:
            self.same_action_streak += 1
        else:
            self.same_action_streak = 0

        reward -= 0.005 * (self.same_action_streak ** 2)

        self.last_action_id = action_id

        return reward

    def terminal_reward(self, boss_wins: bool, boss_hp: float, player_hp: float) -> float:
        """
        Compute terminal reward when episode ends.

        Args:
            boss_wins: True if boss won, False if player won
            boss_hp: Boss HP at episode end (raw)
            player_hp: Player HP at episode end (raw)

        Returns:
            Terminal reward (bonus for close wins, penalty for losses)
        """
        boss_hp_pct = boss_hp / self.MAX_BOSS_HP
        player_hp_pct = player_hp / self.MAX_PLAYER_HP
        hp_gap = boss_hp_pct - player_hp_pct

        # Reward a close win more than a steamroll
        target_gap = 0.15
        close_bonus = max(0.0, 1.0 - abs(hp_gap - target_gap) / 0.3)

        if boss_wins:
            return 10.0 + close_bonus
        return -10.0
