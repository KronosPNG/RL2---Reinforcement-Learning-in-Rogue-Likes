"""
Training manager for centralized PPO policy training.

Manages a single shared policy across all game connections and coordinates
batch collection and periodic policy updates. All game clients train the same
agent through shared policy weights.
"""

from __future__ import annotations

import asyncio
from collections import deque
from datetime import datetime
import json
import os

import torch

from ai.policy import HybridPPOPolicy
from training.trainer import PPOTrainer, PPOConfig
from utils.device import DEVICE


class TrainingManager:
    """
    Orchestrates training of a shared boss AI policy.
    
    Responsibilities:
    - Maintains a single shared policy used by all game connections
    - Queues experience batches from completed episodes
    - Periodically performs PPO training updates
    - Tracks training statistics
    """
    
    def __init__(self, obs_dim: int = 56, num_actions: int = 7,
                 cfg: PPOConfig | None = None, batch_queue_size: int = 32,
                 checkpoint_dir: str = "checkpoints", checkpoint_interval: int = 10,
                 metrics_dir: str = "metrics"):
        """
        Initialize the training manager.
        
        Args:
            obs_dim: Observation vector dimension from collector.flatten_observation()
            num_actions: Number of discrete actions (weapon attacks, consumables, etc.)
            cfg: PPOConfig for training hyperparameters
            batch_queue_size: Max batches to queue before forcing training
            checkpoint_dir: Directory to save policy checkpoints
            checkpoint_interval: Save checkpoint every N training steps
            metrics_dir: Directory to save training metrics JSON
        """
        self.obs_dim = obs_dim
        self.num_actions = num_actions
        self.cfg = cfg or PPOConfig()
        
        # Checkpointing
        self.checkpoint_dir = checkpoint_dir
        self.checkpoint_interval = checkpoint_interval
        os.makedirs(checkpoint_dir, exist_ok=True)
        
        # Metrics logging
        self.metrics_dir = metrics_dir
        os.makedirs(metrics_dir, exist_ok=True)
        
        # Create shared policy - all connections use this
        self.policy = HybridPPOPolicy(obs_dim, num_actions)
        
        # Create trainer and bind it to the shared policy
        self.trainer = PPOTrainer(obs_dim, num_actions, cfg=self.cfg)
        self.trainer.model = self.policy  # Ensure trainer updates shared policy
        
        # Batch queue - holds experience from completed episodes
        self.batch_queue = deque(maxlen=batch_queue_size)
        
        # Training statistics
        self.episodes_processed = 0
        self.training_steps_completed = 0
        self.batches_pending = 0
        
        # Metrics history (for logging/analysis)
        self.metrics_history = []  # List of dicts with training metrics
    
    def get_policy(self) -> HybridPPOPolicy:
        """
        Get the shared policy for use by game connections.
        
        All PacketHandlers should call this in __init__ to share the same weights.
        
        Returns:
            The shared HybridPPOPolicy instance
        """
        return self.policy
    
    async def add_batch(self, batch: dict | None):
        """
        Queue a batch for training (called when an episode ends).
        
        Args:
            batch: Output from TrajectoryCollector.end_episode() containing:
                   - obs: observation tensor
                   - movement: action tensor
                   - action_id: discrete action indices
                   - log_prob: old policy log probabilities
                   - value: old value predictions
                   - advantages: computed advantages
                   - returns: computed returns
        """
        if batch is None:
            return
        
        self.batch_queue.append(batch)
        self.episodes_processed += 1
        self.batches_pending = len(self.batch_queue)
    
    async def training_step(self, force: bool = False) -> int:
        """
        Perform a single PPO training update on all queued batches.
        
        Combines all queued batches, then runs one epoch of PPO updates.
        This should be called periodically (e.g., every N episodes or on a timer).
        
        Args:
            force: If True, train even if queue is small. If False, skip if < 2 batches.
        
        Returns:
            Number of batches trained on (0 if skipped)
        """
        # Skip training if queue is too small and not forced
        if len(self.batch_queue) < 2 and not force:
            return 0
        
        if not self.batch_queue:
            return 0
        
        # Combine all batches into one
        combined_batch = self._combine_batches(list(self.batch_queue))
        
        # Run training update and get metrics
        metrics = self.trainer.update(combined_batch)
        
        # Add metadata to metrics
        metrics["timestamp"] = datetime.now().isoformat()
        metrics["training_step"] = self.training_steps_completed
        metrics["episodes_processed"] = self.episodes_processed
        metrics["batch_size"] = combined_batch["obs"].shape[0]
        
        # Store in history
        self.metrics_history.append(metrics)
        
        # Clear queue and update stats
        self.batch_queue.clear()
        self.training_steps_completed += 1
        self.batches_pending = 0
        
        # Save metrics to JSON
        self._save_metrics()
        
        # Checkpoint if interval reached
        if self.training_steps_completed % self.checkpoint_interval == 0:
            self._save_checkpoint()
        
        return self.episodes_processed
    
    def _combine_batches(self, batches: list[dict]) -> dict:
        """
        Combine multiple batches into a single batch for training.
        
        Args:
            batches: List of batch dicts from multiple episodes
        
        Returns:
            Combined batch with concatenated tensors
        """
        combined = {
            "obs": torch.cat([b["obs"] for b in batches]),
            "movement": torch.cat([b["movement"] for b in batches]),
            "action_id": torch.cat([b["action_id"] for b in batches]),
            "log_prob": torch.cat([b["log_prob"] for b in batches]),
            "value": torch.cat([b["value"] for b in batches]),
            "advantages": torch.cat([b["advantages"] for b in batches]),
            "returns": torch.cat([b["returns"] for b in batches]),
        }
        return combined
    
    def save_policy(self, path: str):
        """
        Save the shared policy to disk.
        
        Args:
            path: File path to save to (e.g., 'models/boss_policy.pt')
        """
        torch.save(self.policy.state_dict(), path)
    
    def _save_metrics(self):
        """
        Save all training metrics to JSON file (called after each training step).
        """
        metrics_path = os.path.join(self.metrics_dir, "training_metrics.json")
        
        # Convert metrics history to JSON-serializable format
        metrics_data = []
        for metric in self.metrics_history:
            # Convert tensor values to Python types
            clean_metric = {}
            for key, value in metric.items():
                if isinstance(value, torch.Tensor):
                    clean_metric[key] = value.item()
                else:
                    clean_metric[key] = value
            metrics_data.append(clean_metric)
        
        # Write to JSON file
        with open(metrics_path, "w") as f:
            json.dump(metrics_data, f, indent=2)
        print(f"Metrics saved: {metrics_path}")
    
    def _save_checkpoint(self):
        """
        Save a checkpoint with timestamp (called automatically every N training steps).
        """
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        checkpoint_path = os.path.join(
            self.checkpoint_dir,
            f"policy_step{self.training_steps_completed}_{timestamp}.pt"
        )
        torch.save(self.policy.state_dict(), checkpoint_path)
        print(f"Checkpoint saved: {checkpoint_path}")
    
    def load_policy(self, path: str):
        """
        Load a previously trained policy from disk.
        
        Args:
            path: File path to load from
        """
        self.policy.load_state_dict(torch.load(path, map_location=DEVICE))
        self.trainer.model = self.policy
    
    def get_stats(self) -> dict:
        """
        Get training statistics for monitoring.
        
        Returns:
            Dict with episode count, training steps, pending batches, and recent metrics
        """
        recent_metrics = None
        if self.metrics_history:
            recent_metrics = self.metrics_history[-1]
        
        return {
            "episodes_processed": self.episodes_processed,
            "training_steps_completed": self.training_steps_completed,
            "batches_pending": self.batches_pending,
            "recent_metrics": recent_metrics,
        }
    
    def check_plateau(self, window: int = 20, threshold: float = 0.005) -> bool:
        """
        Returns True when training has plateaued.

        Compares the average total_loss of the last `window` training steps
        against the `window` steps before that. Plateau is declared when the
        relative improvement drops below `threshold` (default 0.5%).
        Requires at least 2*window steps of history before it can fire.
        """
        min_steps = 2 * window
        if len(self.metrics_history) < min_steps:
            return False

        losses = [m.get("total_loss", 0.0) for m in self.metrics_history]
        recent_avg   = sum(losses[-window:])          / window
        previous_avg = sum(losses[-2*window:-window]) / window

        improvement = (previous_avg - recent_avg) / (abs(previous_avg) + 1e-8)
        return improvement < threshold

    def get_metrics_summary(self) -> dict:
        """
        Get summary statistics of recent training metrics.
        
        Returns:
            Dict with averaged metrics from last N training steps
        """
        if not self.metrics_history:
            return {}
        
        # Average last 10 metrics (or all if fewer)
        recent = self.metrics_history[-10:]
        
        summary = {}
        for key in ["policy_loss", "value_loss", "entropy_loss", "total_loss", 
                    "advantage_mean", "advantage_std"]:
            values = [m.get(key, 0.0) for m in recent if key in m]
            if values:
                summary[f"{key}_avg"] = sum(values) / len(values)
                summary[f"{key}_min"] = min(values)
                summary[f"{key}_max"] = max(values)
        
        return summary
