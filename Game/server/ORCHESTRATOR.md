# Training Orchestrator - Multi-Instance Management

## Overview

The Training Orchestrator enables distributed RL training by spawning and managing multiple Godot instances, each running a PlayerMimic with different equipment and behavior configurations. All instances train a shared PPO policy, creating diverse training data that improves boss robustness.

## Architecture

### Components

#### 1. PlayerConfig & PlayerFactory (`game/player_config.py`)

**PlayerConfig** - Defines a single player instance:
- **Equipment**: weapon, armor, consumable
- **Behaviors**: 5 decision-making components (attack, dodge, consumable, wander, aggro)
- **Metadata**: seed, name

**PlayerFactory** - Generates configurations:
- `create_random_config(seed)` - Seed-based random generation
- `create_config(...)` - Explicit selection with wildcards
- `create_batch(count)` - Generate N diverse configs

#### 2. TrainingOrchestrator (`training/orchestrator.py`)

**Responsibilities**:
- Spawn Godot instances with unique configs
- Track instance lifecycle and health
- Detect timeouts and connection losses
- Coordinate episode boundaries across instances
- Report metrics and status

**Key Methods**:
- `spawn_instances(configs)` - Start N Godot processes
- `mark_instance_connected(instance_id)` - Called on WebSocket connect
- `mark_episode_end(instance_id, boss_wins, transitions)` - Called on episode end
- `monitor_instances()` - Background health check task
- `get_all_status()` - Get orchestrator + instance metrics

#### 3. PacketHandler Updates (`network/connection.py`)

Added parameters:
- `instance_id: int` - Which Godot instance this connection is from
- `orchestrator: TrainingOrchestrator` - Reference to orchestrator

Updated logic:
- OUTCOME handler now calls `orchestrator.mark_episode_end()` with episode results

#### 4. Main Entry Point Updates (`main.py`)

**Command-line Arguments**:
```
--num-instances=N      Number of Godot instances to spawn (default: 1)
--godot-exec=PATH      Path to Godot executable (auto-detect if omitted)
--port=N               Server port (default: 7000)
```

**Flow**:
1. Parse arguments
2. Create TrainingManager (shared policy)
3. If `num_instances > 1`:
   - Initialize TrainingOrchestrator
   - Generate diverse PlayerConfigs
   - Spawn Godot processes
   - Start monitoring task
   - Wait for connections
4. Start WebSocket server
5. Run training loop

## Available Options

### Equipment

**Weapons** (4 variants):
- `sword`
- `dagger`
- `bow`
- `staff`

**Armors** (4 variants):
- `light_armor`
- `medium_armor`
- `heavy_armor`
- `shirt`

**Consumables** (3 options):
- `none` (no consumable)
- `medkit`
- `potion`

### Behaviors

**Attack Behaviors** (4 styles):
- `AttackCowardly` - Avoids combat when possible
- `AttackSpam` - Rapid attacks without strategy
- `AttackTactical` - Calculated approach
- `AttackEdgelord` - Aggressive/risky

**Dodge Behaviors** (4 styles):
- `DodgeNever` - Never dodges
- `DodgePreemptive` - Predicts incoming attacks
- `DodgeRandom` - Random evasion
- `DodgeReactive` - Reacts to damage

**Consumable Behaviors** (5 strategies):
- `ConsumableNone` - Never uses consumables
- `ConsumableRandom` - Uses randomly
- `ConsumableScaredyCat` - Uses when low HP
- `ConsumableTactical` - Strategic usage
- `ConsumableThreshold` - Uses at HP threshold

**Wander Behaviors** (3 patterns):
- `WanderHide` - Seeks cover
- `WanderImmovable` - Stays put
- `WanderRandomWalk` - Random movement

**Aggro Behaviors** (2 targets):
- `AggroFollowTarget` - Pursues player
- `AggroKeepDistance` - Maintains distance
- *(AggroFollowGaze excluded as per PlayerMimic spec)*

## Usage Examples

### Single Instance (Baseline)
```bash
python main.py
# Creates 1 instance with default config, no spawning
```

### 4 Instances (Default Multi)
```bash
python main.py --num-instances=4
# Spawns 4 Godot processes with random, diverse configs
```

### Custom Godot Path
```bash
python main.py --num-instances=4 --godot-exec="C:\Games\Godot\Godot.exe"
```

### Scripted Multi-Instance Launch
```python
from training.orchestrator import TrainingOrchestrator
from game.player_config import PlayerFactory

# Create orchestrator
orch = TrainingOrchestrator(num_instances=8)

# Generate custom configs
factory = PlayerFactory()
configs = [
    factory.create_config(
        weapon="sword",
        armor="heavy_armor",
        attack_behavior="AttackTactical"
    )
    for _ in range(8)
]

# Spawn with custom configs
await orch.spawn_instances(configs)
```

## Flow Diagram

```
main.py
  ├─ Parse args (num_instances, etc)
  ├─ Create TrainingManager (shared policy)
  │
  ├─ If num_instances > 1:
  │  ├─ Create TrainingOrchestrator
  │  ├─ Generate N PlayerConfigs
  │  │  └─ Each: random weapon/armor/consumable/behaviors
  │  ├─ Spawn N Godot processes
  │  │  └─ Pass configs via command-line args
  │  └─ Start monitor_instances() background task
  │
  ├─ Start WebSocket server (ws://localhost:7000)
  │
  ├─ connection_handler():
  │  ├─ Extract instance_id from connection handshake
  │  ├─ Create PacketHandler with instance_id + orchestrator
  │  └─ orchestrator.mark_instance_connected(instance_id)
  │
  ├─ PacketHandler.handle(OUTCOME):
  │  ├─ Get episode result (boss_wins)
  │  ├─ Queue batch to training_manager
  │  └─ orchestrator.mark_episode_end(instance_id, boss_wins, transitions)
  │
  └─ training_loop():
     ├─ Check if training triggered (4+ episodes OR 512+ transitions)
     ├─ training_manager.training_step()
     ├─ Log metrics + orchestrator status
     └─ repeat every 2 seconds
```

## Instance Lifecycle

### States
- `PENDING` → Initial state
- `STARTING` → Spawning process
- `RUNNING` → Process alive, waiting for WebSocket
- `CONNECTED` → WebSocket established
- `EPISODE_ACTIVE` → Game running
- `EPISODE_DONE` → Game ended, awaiting next episode
- `TIMEOUT` → Exceeded max lifetime
- `CRASHED` → Process died
- `STOPPED` → Intentionally terminated

### Timeouts
- **Max instance lifetime**: 3600s (1 hour)
- **Max episode duration**: 600s (10 minutes) - enforced by game
- **Monitor interval**: 10 seconds

## Metrics & Monitoring

### Per-Instance Metrics
```
InstanceMetrics:
  - episodes_completed: int
  - total_transitions: int
  - avg_episode_length: float
  - last_episode_duration: float
  - boss_wins: int
  - player_wins: int
```

### Query Status
```python
# Single instance
status = orchestrator.get_instance_status(instance_id=0)

# All instances
all_status = orchestrator.get_all_status()

# Aggregated
total_episodes = orchestrator.get_total_episodes()
total_transitions = orchestrator.get_total_transitions()
connected = orchestrator.get_connected_instances()
```

### Training Loop Logging
When multi-instance mode is active, training loop prints:
```
Orchestrator Status:
  Connected Instances: 4/4
  Total Episodes: 42
  Total Transitions: 5120
```

## Command-Line Arguments Passed to Godot

When spawning instances, each Godot process receives arguments:

```
godot.exe res://scenes/main.tscn \
  --instance-id=0 \
  --instance-port=7000 \
  --player-weapon=sword \
  --player-armor=light_armor \
  --player-consumable=medkit \
  --player-attack-behavior=AttackTactical \
  --player-dodge-behavior=DodgeRandom \
  --player-consumable-behavior=ConsumableTactical \
  --player-wander-behavior=WanderRandomWalk \
  --player-aggro-behavior=AggroFollowTarget \
  --headless
```

These should be parsed by Godot's argument handler and applied to PlayerMimic initialization.

## Integration with Godot

### Expected PlayerMimic Implementation

PlayerMimic should:
1. Parse command-line arguments for all --player-* flags
2. Set equipment (weapon, armor, consumable) from parsed args
3. Initialize behaviors with specified classes
4. Connect to `ws://localhost:{port}` where port = 7000 + instance_id
5. Send `instance_id` in initial handshake message (JSON: `{"instance_id": N}`)

### Example Godot Pseudocode
```gdscript
extends CharacterBody2D
class_name PlayerMimic

func _ready():
    # Parse command-line args
    var weapon_name = OS.get_cmdline_user_args()["player-weapon"] or "sword"
    var armor_name = OS.get_cmdline_user_args()["player-armor"] or "shirt"
    # ... parse all player-* args ...
    
    # Initialize behaviors
    attack_behavior = load(f"res://scripts/entities/Behaviours/Attacking/PlayerMimic/{weapon_name}.cs").new()
    # ... etc for all behaviors ...
    
    # Connect to server
    var instance_id = OS.get_cmdline_user_args()["instance-id"] or 0
    var port = 7000 + instance_id
    websocket_client.connect_to_url(f"ws://localhost:{port}")
    
    # Send handshake
    var handshake = {"instance_id": instance_id}
    websocket_client.send_text(JSON.stringify(handshake))
```

## Troubleshooting

### Instances Don't Connect
1. Check Godot executable path: `python main.py --num-instances=1 --godot-exec="PATH"`
2. Verify ports aren't blocked: 7000, 7001, 7002, etc.
3. Check Godot logs for argument parsing errors

### High Memory Usage
- Each Godot instance ~300-500MB
- 4 instances = ~1.2-2GB
- Use `--headless` to reduce overhead

### Training Doesn't Start
1. Check that episodes are being completed (training loop logs should show episode_count)
2. Verify batch_queue is getting populated
3. Check for WebSocket connection errors

### Instances Timeout
- Default max lifetime is 1 hour per instance
- Adjust in TrainingOrchestrator.__init__: `max_instance_lifetime`
- Default max episode is 10 minutes (enforced by game, configurable in trajectory_collector)

## Performance Tips

1. **Number of Instances**: 4-8 recommended (diminishing returns >8)
2. **Headless Mode**: Set `headless=True` in TrainingOrchestrator for faster training
3. **Training Trigger**: Current thresholds (4 episodes OR 512 transitions OR 30s) are conservative
4. **Batch Size**: PPO trains on batches of ~512-2048 transitions; tune `training_step()` if needed

## Architecture Decisions

### Why asyncio + WebSockets?
- Handles hundreds of I/O connections efficiently
- Single thread, no GIL contention for GPU training
- Scales to many instances without memory overhead

### Why Shared Policy?
- All instances train the same policy
- Prevents redundant storage/compute
- Encourages diversity in training data

### Why Command-Line Arguments?
- No additional API burden on Godot
- Environment reproducibility
- Easy to script instance spawning

## Future Enhancements

1. **Dynamic Instance Scaling** - Add/remove instances based on training progress
2. **Instance Restart** - Respawn crashed instances automatically
3. **Config Presets** - Save/load common configuration sets
4. **Distributed Training** - Multiple servers, shared policy via centralized registry
5. **Curriculum Learning** - Gradually increase difficulty based on boss performance
