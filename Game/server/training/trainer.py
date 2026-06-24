"""
PPO (Proximal Policy Optimization) trainer for the boss AI policy.

Implements the PPO algorithm for on-policy reinforcement learning,
including GAE (Generalized Advantage Estimation) and policy clipping.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

import torch
import torch.nn.functional as F
from torch.optim import Adam

from ai.policy import HybridPPOPolicy


@dataclass
class PPOConfig:
    """Configuration for PPO training hyperparameters."""
    gamma: float = 0.99  # Discount factor for future rewards
    gae_lambda: float = 0.95  # GAE smoothing parameter (0.95 is standard)
    clip_ratio: float = 0.2  # PPO clip ratio for policy gradient clipping
    vf_coef: float = 0.5  # Weight for value function loss
    ent_coef: float = 0.01  # Weight for entropy bonus (exploration)
    lr: float = 3e-4  # Learning rate for Adam optimizer
    epochs: int = 4  # Number of passes over each batch
    minibatch_size: int = 64  # Size of minibatches for gradient updates
    max_grad_norm: float = 0.5  # Gradient clipping threshold


class PPOTrainer:
    """
    Trainer for PPO policy optimization.
    
    Responsibilities:
    - Compute advantages using Generalized Advantage Estimation (GAE)
    - Perform PPO policy updates with gradient clipping
    - Compute value function and entropy losses
    """
    
    def __init__(self, obs_dim: int, num_actions: int, cfg: PPOConfig | None = None):
        """
        Initialize the PPO trainer.
        
        Args:
            obs_dim: Observation vector dimension
            num_actions: Number of discrete actions
            cfg: PPOConfig for hyperparameters
        """
        self.cfg = cfg or PPOConfig()
        self.model = HybridPPOPolicy(obs_dim, num_actions)
        self.optim = Adam(self.model.parameters(), lr=self.cfg.lr)

    def compute_gae(self, rewards, values, dones, last_value):
        """
        Compute Generalized Advantage Estimation (GAE).
        
        GAE provides a bias-variance tradeoff between:
        - Low lambda (0): lower variance, higher bias (TD error)
        - High lambda (1): higher variance, lower bias (MC estimate)
        
        Standard: lambda=0.95 balances both.
        
        Args:
            rewards: List of step rewards from trajectory
            values: List of value predictions from policy
            dones: List of done flags (episode termination)
            last_value: Value prediction at trajectory end (for bootstrapping)
        
        Returns:
            Tuple of (advantages, returns) as tensors
        """
        advantages = []
        gae = 0.0
        values = values + [last_value]  # Add bootstrapped value at end

        # Backward pass through trajectory
        for t in reversed(range(len(rewards))):
            mask = 1.0 - float(dones[t])  # Zero out advantage at episode end
            # TD error: r_t + gamma * V(s_{t+1}) - V(s_t)
            delta = rewards[t] + self.cfg.gamma * values[t + 1] * mask - values[t]
            # Accumulate GAE with exponential smoothing
            gae = delta + self.cfg.gamma * self.cfg.gae_lambda * mask * gae
            advantages.insert(0, gae)

        # Returns = advantages + values (not bootstrapped)
        returns = [adv + val for adv, val in zip(advantages, values[:-1])]
        return torch.tensor(advantages, dtype=torch.float32), torch.tensor(returns, dtype=torch.float32)

    def update(self, batch):
        """
        Perform a PPO training update on a batch of experience.
        
        Algorithm:
        1. Normalize advantages for stability
        2. Shuffle batch and split into minibatches
        3. For each epoch:
            - Recompute log probs and values for current policy
            - Compute PPO surrogate losses (clipped)
            - Backward pass and optimization step
        
        Args:
            batch: Dict with keys:
                   - obs: observation tensor [batch_size, obs_dim]
                   - movement: continuous actions [batch_size, 2]
                   - action_id: discrete actions [batch_size]
                   - log_prob: old policy log probs [batch_size]
                   - advantages: computed advantages [batch_size]
                   - returns: computed returns [batch_size]
        
        Returns:
            Dict with training metrics for logging
        """
        obs = batch["obs"]
        movement = batch["movement"]
        action_id = batch["action_id"]
        old_log_prob = batch["log_prob"].detach()
        advantages = batch["advantages"].detach()
        returns = batch["returns"].detach()

        # Normalize advantages for better gradient stability
        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)

        n = obs.shape[0]
        idxs = torch.randperm(n)

        # Accumulate metrics across epochs
        metrics = {
            "policy_loss": 0.0,
            "value_loss": 0.0,
            "entropy_loss": 0.0,
            "total_loss": 0.0,
            "update_count": 0,
            "advantage_mean": float(advantages.mean().item()),
            "advantage_std": float(advantages.std().item()),
        }

        # Multiple passes over batch (PPO typically uses 4 epochs)
        for _ in range(self.cfg.epochs):
            # Iterate over minibatches
            for start in range(0, n, self.cfg.minibatch_size):
                mb = idxs[start:start + self.cfg.minibatch_size]

                # Evaluate policy on minibatch
                log_prob, entropy, value = self.model.evaluate_actions(
                    obs[mb], movement[mb], action_id[mb]
                )

                # PPO policy gradient with clipping
                ratio = torch.exp(log_prob - old_log_prob[mb])
                surr1 = ratio * advantages[mb]
                surr2 = torch.clamp(ratio, 1.0 - self.cfg.clip_ratio, 1.0 + self.cfg.clip_ratio) * advantages[mb]
                policy_loss = -torch.min(surr1, surr2).mean()

                # Value function loss (MSE between predicted and target returns)
                value_loss = F.mse_loss(value, returns[mb])
                
                # Entropy bonus (encourages exploration)
                entropy_loss = -entropy.mean()

                # Combined loss
                loss = policy_loss + self.cfg.vf_coef * value_loss + self.cfg.ent_coef * entropy_loss

                # Optimization step
                self.optim.zero_grad()
                loss.backward()
                torch.nn.utils.clip_grad_norm_(self.model.parameters(), self.cfg.max_grad_norm)
                self.optim.step()
                
                # Accumulate metrics
                metrics["policy_loss"] += policy_loss.item()
                metrics["value_loss"] += value_loss.item()
                metrics["entropy_loss"] += entropy_loss.item()
                metrics["total_loss"] += loss.item()
                metrics["update_count"] += 1
        
        # Average metrics
        if metrics["update_count"] > 0:
            for key in ["policy_loss", "value_loss", "entropy_loss", "total_loss"]:
                metrics[key] /= metrics["update_count"]
        
        return metrics