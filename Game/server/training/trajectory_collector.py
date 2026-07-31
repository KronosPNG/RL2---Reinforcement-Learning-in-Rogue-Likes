"""
Trajectory collection and reward computation for boss AI training.

Collects experience (observations, actions, rewards) during gameplay and
produces training batches ready for PPO updates.
"""

from __future__ import annotations

import torch
import time

from game.rewards import RewardTracker, DASH
from game.collector import flatten_observation
from .trainer import PPOTrainer
from utils.device import DEVICE


class TrajectoryCollector:
    """
    Collects experience during an episode and produces training batches.
    
    Responsibilities:
    - Calls policy to get actions and values
    - Tracks state transitions and computes step rewards
    - Handles episode completion and terminal rewards
    - Computes Generalized Advantage Estimation (GAE)
    - Returns batches in format expected by PPOTrainer.update()
    """
    
    def __init__(self, policy):
        """
        Initialize the trajectory collector.
        
        Args:
            policy: The HybridPPOPolicy instance to sample actions from
        """
        self.policy = policy
        self.reward_tracker = RewardTracker()
        self.trajectory = []
        self.last_player_hp = None
        self.last_boss_hp = None
        self.last_action = (0.0, 0.0, 0)  # cached action resent while the boss is locked
        self.episode_start_time = None  # Track episode duration
        self.max_episode_duration = 600.0  # 10 minutes in seconds

    def collect_step(self, static_state: dict, dynamic_state: dict) -> tuple[float, float, int]:
        """
        Collect a single step of experience and return the action to execute.

        Flow:
        1. Convert game state to observation tensor
        2. Sample action from policy (movement + action_id)
        3. Compute reward based on damage since last step
        4. Store in trajectory
        5. Return action to send to game

        While the boss is mid-attack, mid-dash, or in post-dash cooldown
        (dynamic_state["boss"]["IsLocked"]), BossRL.ApplyAction() on the game side
        ignores whatever action it receives. Sampling and scoring a fresh "decision"
        on every one of those ticks would score the same in-flight attack many times
        over (e.g. via the anti-spam streak) for a choice that was already made and
        can't be changed. So those ticks are skipped entirely: no policy call, no
        trajectory entry, no reward — HP deltas simply accumulate until the boss is
        free to act again, and the whole commit window is scored as the single
        decision that caused it.

        Args:
            static_state: Equipment and room bounds (from protocol.decode_static_state)
            dynamic_state: Player/boss position, health, etc. (from protocol.decode_dynamic_state)

        Returns:
            Tuple of (movement_x, movement_y, action_id) to send to game
        """
        # Track episode start time on first step
        if self.episode_start_time is None:
            self.episode_start_time = time.time()

        if dynamic_state["boss"]["IsLocked"]:
            return self.last_action

        obs = flatten_observation(static_state, dynamic_state)

        # Get action and value from policy
        with torch.no_grad():
            action_dict = self.policy.get_action(obs.unsqueeze(0))

        action_id = action_dict["action_id"][0].item()

        # Track HP for reward computation
        player_hp = dynamic_state["player"]["Health"]
        boss_hp = dynamic_state["boss"]["Health"]

        angle_to_player = dynamic_state["boss"]["AngleToPlayer"]

        # Check if the chosen attack/dash is on cooldown (game will ignore it)
        on_cooldown = False
        if action_id in (3, 4, 5, 6):
            attack_index = action_id - 3  # Melee1→0, Melee2→1, Magic1→2, Magic2→3
            on_cooldown = dynamic_state["boss"]["AttackCooldownTimers"][attack_index] > 0
        elif action_id == DASH:
            on_cooldown = dynamic_state["boss"]["DashCooldownTimer"] > 0

        if self.last_player_hp is not None:
            # Compute damage deltas since last step
            # player_damage = damage dealt TO player (positive if player hp decreased)
            player_damage = self.last_player_hp - player_hp
            # boss_damage = damage dealt TO boss (positive if boss hp decreased)
            boss_damage = self.last_boss_hp - boss_hp

            step_reward = self.reward_tracker.step_reward(
                player_damage, boss_damage, action_id, angle_to_player, on_cooldown
            )
        else:
            # First step: no damage yet, no attack fires
            step_reward = self.reward_tracker.step_reward(
                0.0, 0.0, action_id, 0.0, False
            )
        
        # Store transition in trajectory
        self.trajectory.append({
            "obs": obs,
            "movement": action_dict["movement"][0],
            "action_id": action_dict["action_id"][0],
            "log_prob": action_dict["log_prob"][0],
            "value": action_dict["value"][0],
            "reward": step_reward,
            "done": False,
        })
        
        # Update HP tracking
        self.last_player_hp = player_hp
        self.last_boss_hp = boss_hp

        # Return action to send to game, and cache it in case the boss locks up
        # (attacks/dashes) before it's free to make another real decision.
        movement = action_dict["movement"][0].cpu().numpy()
        self.last_action = (float(movement[0]), float(movement[1]), int(action_id))
        return self.last_action

    def end_episode(self, boss_won: bool, final_player_hp: float, final_boss_hp: float) -> dict | None:
        """
        End the episode and compute terminal reward.
        
        Adds episode-end reward (bonus for close win, penalty for loss),
        then converts trajectory to training batch format.
        
        Also checks for timeout (if episode exceeded 10 minutes, penalizes heavily).
        
        Args:
            boss_won: True if boss won, False if player won
            final_player_hp: Player's final HP
            final_boss_hp: Boss's final HP
        
        Returns:
            Training batch ready for PPOTrainer.update(), or None if trajectory is empty
        """
        # Check for timeout: if episode > 10 minutes, force a loss
        if self.episode_start_time is not None:
            elapsed_time = time.time() - self.episode_start_time
            if elapsed_time > self.max_episode_duration:
                # Force timeout penalty (counted as loss + time penalty)
                terminal_reward = -15.0  # Worse than normal loss
                print(f"⚠️  Episode timeout: {elapsed_time:.1f}s > {self.max_episode_duration}s. "
                      f"Applying timeout penalty.")
                boss_won = False  # Treat as loss
            else:
                terminal_reward = self.reward_tracker.terminal_reward(
                    boss_won, final_boss_hp, final_player_hp
                )
        else:
            terminal_reward = self.reward_tracker.terminal_reward(
                boss_won, final_boss_hp, final_player_hp
            )
        
        if self.trajectory:
            # Add terminal reward to final step
            self.trajectory[-1]["reward"] += terminal_reward
            self.trajectory[-1]["done"] = True
        
        batch = self.get_batch()
        return batch

    def get_batch(self) -> dict | None:
        """
        Convert collected trajectory into batch format for PPOTrainer.update().
        
        Computes Generalized Advantage Estimation (GAE) which combines
        temporal difference errors and bias-variance tradeoff.
        
        Returns:
            Batch dict with tensors ready for training, or None if trajectory is empty
        """
        if not self.trajectory:
            return None
        
        # Get last value for GAE computation (bootstrapped from final value)
        last_value = self.trajectory[-1]["value"].item() if self.trajectory else 0.0
        
        rewards = [t["reward"] for t in self.trajectory]
        dones = [t["done"] for t in self.trajectory]
        values = [t["value"].item() for t in self.trajectory]
        
        # Compute advantages and returns using GAE
        trainer = PPOTrainer(obs_dim=1, num_actions=1)
        advantages, returns = trainer.compute_gae(rewards, values, dones, last_value)
        
        # Concatenate all transitions into batch tensors
        batch = {
            "obs": torch.stack([t["obs"] for t in self.trajectory]),
            "movement": torch.stack([t["movement"] for t in self.trajectory]),
            "action_id": torch.tensor([t["action_id"] for t in self.trajectory], device=DEVICE),
            "log_prob": torch.stack([t["log_prob"] for t in self.trajectory]),
            "value": torch.stack([t["value"] for t in self.trajectory]),
            "advantages": advantages,
            "returns": returns,
        }
        
        # Clear trajectory and reset trackers for the next episode
        self.trajectory = []
        self.reward_tracker.reset()
        self.last_player_hp = None
        self.last_boss_hp = None
        self.last_action = (0.0, 0.0, 0)
        self.episode_start_time = None

        return batch

    def reset(self):
        """Reset for new episode without computing batch (e.g., after disconnect)."""
        self.trajectory = []
        self.reward_tracker.reset()
        self.last_player_hp = None
        self.last_boss_hp = None
        self.last_action = (0.0, 0.0, 0)
        self.episode_start_time = None
