"""
Boss AI Training System - PPO-based reinforcement learning.

Architecture Overview:
======================

1. EXPERIENCE COLLECTION (TrajectoryCollector)
   - Each game connection calls TrajectoryCollector.collect_step() for every frame
   - Converts game state → observation tensor via flatten_observation()
   - Queries policy via get_action() to sample movement + discrete action
   - Computes reward using RewardTracker (damage dealt/taken + anti-spam penalty)
   - Stores (obs, action, reward, value) tuples in trajectory
   - When episode ends, calls end_episode() to:
     a) Add terminal reward (win/loss + HP gap bonus)
     b) Compute Generalized Advantage Estimation (GAE)
     c) Return training batch

2. CENTRALIZED TRAINING (TrainingManager + PPOTrainer)
   - TrainingManager maintains single shared policy used by ALL connections
   - Connection.handle() queues episode batches to TrainingManager
   - TrainingManager.training_step() combines batches and calls PPOTrainer.update()
   - PPOTrainer.update() performs PPO optimization:
     a) Normalize advantages
     b) Shuffle and split into minibatches
     c) Multiple epochs of policy gradient + value function + entropy updates
     d) Gradient clipping for stability

3. KEY DESIGN PRINCIPLES
   - Single shared policy: all connections train the same boss
   - Asynchronous collection: game connections don't block on training
   - Periodic training: batches collected, then training done every N seconds
   - Reward shaping: damages incentivize boss to win, anti-spam penalty

Usage:
======

The training system runs automatically in main.py:
  1. Create TrainingManager (one global instance)
  2. For each connection, create PacketHandler(shared_policy, training_manager)
  3. TrainingManager.training_step() runs periodically in background

To manually control training:
  from training_manager import TrainingManager
  from policy import HybridPPOPolicy
  
  tm = TrainingManager()
  # ... collect batches ...
  await tm.training_step(force=True)  # train now
  tm.save_policy("models/boss.pt")
"""
