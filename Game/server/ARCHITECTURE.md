# Boss AI Training Server - Reorganized Architecture

This directory contains a centralized reinforcement learning training system for the Roguelike boss AI using PyTorch PPO (Proximal Policy Optimization).

## Directory Structure

```
server/
├── network/              # WebSocket protocol and connection handling
│   ├── __init__.py
│   ├── connection.py    # PacketHandler: routes binary messages, queries AI for actions, supports instance_id
│   ├── protocol.py      # Binary protocol: encode/decode game state and actions
│   └── state.py         # SessionState: per-connection session tracking
│
├── ai/                  # AI Agent and Policy
│   ├── __init__.py
│   ├── agent.py         # BossAI: wraps policy and trajectory collector
│   └── policy.py        # HybridPPOPolicy: neural network with continuous + discrete outputs
│
├── game/                # Game state, rewards, and player configuration
│   ├── __init__.py
│   ├── collector.py     # flatten_observation(): converts game state → feature vector
│   ├── rewards.py       # RewardTracker: step and terminal reward computation
│   └── player_config.py # PlayerConfig, PlayerFactory: player instance configuration (NEW)
│
├── training/            # RL training pipeline and orchestration
│   ├── __init__.py
│   ├── trainer.py       # PPOTrainer: implements PPO algorithm with GAE
│   ├── trajectory_collector.py  # TrajectoryCollector: collects experience, returns batches
│   ├── training_manager.py      # TrainingManager: centralized policy coordination
│   └── orchestrator.py          # TrainingOrchestrator: multi-instance management (NEW)
│
├── utils/               # Utilities
│   ├── __init__.py
│   └── normalization.py # normalize_01(), normalize_range(), angle_to_sin_cos()
│
├── main.py              # Entry point: WebSocket server + training loop (updated for orchestration)
├── requirements.txt     # Python dependencies
├── ARCHITECTURE.md      # This file
├── ORCHESTRATOR.md      # Multi-instance orchestration guide (NEW)
└── checkpoints/         # Auto-generated directory for policy checkpoints
```

## Module Responsibilities

### network/ - WebSocket Communication
- **protocol.py**: Binary message codec (62 byte STATIC_STATE, 142 byte DYNAMIC_STATE, etc.)
- **connection.py**: Per-client message router (PacketHandler)
- **state.py**: Per-session state container

### ai/ - Agent and Policy
- **policy.py**: HybridPPOPolicy network (continuous movement + discrete actions)
- **agent.py**: BossAI wrapper (maintains policy + trajectory collector)

### game/ - Game Mechanics
- **rewards.py**: RewardTracker (step shaping + terminal rewards)
- **collector.py**: Observation flattening (62-dim normalized feature vector)

### training/ - RL Training
- **trainer.py**: PPOTrainer (implements PPO with GAE advantage estimation)
- **trajectory_collector.py**: TrajectoryCollector (collects experience → training batch)
- **training_manager.py**: TrainingManager (centralized policy management + batch coordination)

### utils/ - Common Utilities
- **normalization.py**: Feature normalization helpers (normalize_01, normalize_range, angle_to_sin_cos)

## Control Flow

### Episode Startup
1. Godot game connects via WebSocket (→ connection_handler in main.py)
2. Creates new PacketHandler with reference to shared_policy from TrainingManager
3. PacketHandler creates BossAI with the shared policy

### Per-Frame (gameplay)
1. Game sends STATIC_STATE (once) and DYNAMIC_STATE (every frame)
2. PacketHandler routes to BossAI.choose_action()
3. BossAI calls TrajectoryCollector.collect_step():
   - Flattens observation via flatten_observation()
   - Queries policy.get_action() → samples movement + action
   - Computes step reward via reward_tracker.step_reward()
   - Stores in trajectory
   - Returns action to game
4. Game executes action and sends next DYNAMIC_STATE

### Episode End
1. Game sends OUTCOME message (won/lost)
2. PacketHandler calls BossAI.on_episode_end():
   - Adds terminal reward via reward_tracker.terminal_reward()
   - TrajectoryCollector.get_batch() computes GAE advantages
   - Returns training batch
3. PacketHandler queues batch via training_manager.add_batch()

### Periodic Training
Training is triggered **adaptively** based on accumulated experience, not a fixed timer:
1. training_loop() checks for training conditions every 2 seconds
2. Triggers when ANY of these occur:
   - 4+ episodes queued (good PPO batch size)
   - 512+ transitions accumulated (enough for 4 gradient epochs)
   - 30+ seconds since last training (safety timeout)
3. TrainingManager combines queued batches and calls PPOTrainer.update()
4. Metrics logged to console with trigger reason, checkpoints saved every 10 steps

**Rationale**: Real-time boss fights (2000 HP ÷ 100 damage = 20+ attacks) are 5-10+ minutes long.
Fixed 5-second intervals would fire too often, starving episodes of time to accumulate.
Episode-based triggering adapts to fight length while ensuring steady progress.

## Key Design Decisions

### Shared Policy Across Connections
- All game clients train the **same** policy via shared HybridPPOPolicy instance
- TrainingManager.get_policy() returns this shared reference to every new PacketHandler
- All clients contribute their experience to the same training queue
- Centralized training ensures convergence on a unified agent

### Centralized Batch Coordination
- Episodes from multiple clients queue in TrainingManager.batch_queue
- Training step combines all queued batches into one larger batch
- Stable training: maintains consistent replay of recent experience

### Binary Protocol
- Fixed-size messages for network efficiency (73 + 142 bytes per frame)
- Little-endian encoding matches C# BinaryWriter
- Reduces bandwidth: ~215 bytes per game frame vs JSON (~1KB+)

### Reward Semantics
- player_damage = damage dealt TO player (by boss)
- boss_damage = damage dealt TO boss (by player) 
- Reward = +0.4*player_damage - 0.6*boss_damage (boss wants to hit player, avoid damage)
- Anti-spam penalty: -0.005*(consecutive_action_count)²
- **Episode Timeout**: If fight exceeds 10 minutes, episode is forced to end with -15.0 penalty (worse than loss) to prevent infinite stalls

## Running the Server

```bash
# Install dependencies
pip install -r requirements.txt

# Start server
python main.py
```

Server listens on `ws://localhost:7000` for game client connections.

## Imports and Dependencies

### Within server/
- Relative imports use dots: `from .module` (same level), `from ..module` (parent level)
- Example: `from network.connection import PacketHandler` works from main.py
- Example: `from ..ai.policy import HybridPPOPolicy` works from training/training_manager.py

### External Libraries
- `torch`: PyTorch for neural networks and tensor operations
- `websockets`: Async WebSocket server
- `asyncio`: Async I/O for concurrent connections

## Migration Notes

The server was reorganized from a flat structure to a modular hierarchy:

**Before:**
```
server/
├── ai.py
├── policy.py
├── collector.py
├── rewards.py
├── connection.py
├── protocol.py
├── state.py
├── trainer.py
├── trajectory_collector.py
├── training_manager.py
└── main.py
```

**After:**
```
server/
├── ai/ (agent.py, policy.py)
├── game/ (collector.py, rewards.py, player_config.py)
├── network/ (connection.py, protocol.py, state.py)
├── training/ (trainer.py, trajectory_collector.py, training_manager.py, orchestrator.py)
├── utils/ (normalization.py)
└── main.py
```

All imports were updated to reflect the new structure. Old files can be safely deleted once migration is complete.

## Multi-Instance Training (Orchestrator)

For distributed training with multiple diverse player opponents:

1. **PlayerConfig** (`game/player_config.py`): Defines player equipment and behaviors
2. **PlayerFactory**: Generates random or explicit configurations
3. **TrainingOrchestrator** (`training/orchestrator.py`): Spawns and manages Godot instances

**Usage:**
```bash
python main.py --num-instances=4  # Spawn 4 diverse players
```

See [ORCHESTRATOR.md](ORCHESTRATOR.md) for detailed multi-instance training guide.

### Key Integration Points
- **PacketHandler** now accepts `instance_id` and `orchestrator` parameters
- **OUTCOME handler** calls `orchestrator.mark_episode_end()` to track instance metrics
- **main.py** parses `--num-instances`, spawns instances with varied configs
- **Connection handshake** extracts `instance_id` to route messages correctly

