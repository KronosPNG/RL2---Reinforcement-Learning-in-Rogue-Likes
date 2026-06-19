"""
Hybrid PPO policy network for boss AI.

Continuous movement output (via Normal distribution)
Discrete action selection (via Categorical distribution)
"""

from __future__ import annotations

import torch
import torch.nn as nn
from torch.distributions import Normal, Categorical


class HybridPPOPolicy(nn.Module):
    """
    Policy network with continuous movement and discrete action outputs.
    
    Architecture:
    - Shared trunk: 3-layer MLP (256→256→128)
    - Movement head: 2D continuous (tanh-bounded)
    - Action head: discrete categorical
    - Value head: 1D scalar
    """
    
    def __init__(self, obs_dim: int, num_actions: int):
        super().__init__()

        self.trunk = nn.Sequential(
            nn.Linear(obs_dim, 256),
            nn.ReLU(),
            nn.Linear(256, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
            nn.ReLU(),
        )

        self.move_mean = nn.Linear(128, 2)
        self.action_logits = nn.Linear(128, num_actions)
        self.value_head = nn.Linear(128, 1)

        # learned global std for movement
        self.log_std = nn.Parameter(torch.zeros(2))

    def forward(self, obs: torch.Tensor):
        """Compute policy outputs (deterministic)."""
        h = self.trunk(obs)
        move_mean = torch.tanh(self.move_mean(h))     # X, Y in [-1, 1]
        action_logits = self.action_logits(h)
        value = self.value_head(h).squeeze(-1)
        return move_mean, action_logits, value

    def get_action(self, obs: torch.Tensor):
        """Sample an action and return log probability."""
        move_mean, action_logits, value = self.forward(obs)

        move_std = torch.exp(self.log_std).expand_as(move_mean)
        move_dist = Normal(move_mean, move_std)
        action_dist = Categorical(logits=action_logits)

        movement = move_dist.sample()
        action_id = action_dist.sample()

        log_prob = move_dist.log_prob(movement).sum(-1) + action_dist.log_prob(action_id)

        return {
            "movement": movement,
            "action_id": action_id,
            "log_prob": log_prob,
            "value": value,
        }

    def evaluate_actions(self, obs: torch.Tensor, movement: torch.Tensor, action_id: torch.Tensor):
        """Compute log_prob and entropy for training."""
        move_mean, action_logits, value = self.forward(obs)

        move_std = torch.exp(self.log_std).expand_as(move_mean)
        move_dist = Normal(move_mean, move_std)
        action_dist = Categorical(logits=action_logits)

        log_prob = move_dist.log_prob(movement).sum(-1) + action_dist.log_prob(action_id)
        entropy = move_dist.entropy().sum(-1) + action_dist.entropy()

        return log_prob, entropy, value
