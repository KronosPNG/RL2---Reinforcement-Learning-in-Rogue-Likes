# Quick Reference - Multi-Instance Training

## Start Training

```bash
# Baseline (1 instance, no spawning)
python main.py

# Recommended (4 diverse instances)
python main.py --num-instances=4

# Maximum (8 instances for fastest training)
python main.py --num-instances=8

# Custom Godot path
python main.py --num-instances=4 --godot-exec="C:\Path\To\Godot.exe"

# Custom port (if 7000 is busy)
python main.py --port=8000
```

## What Gets Generated

Each instance spawns with a random player config:

```
Instance 0: sword / light_armor / medkit | Attack:AttackTactical Dodge:DodgeRandom ...
Instance 1: bow / heavy_armor / none | Attack:AttackSpam Dodge:DodgePreemptive ...
Instance 2: staff / medium_armor / potion | Attack:AttackCowardly Dodge:DodgeReactive ...
Instance 3: dagger / shirt / medkit | Attack:AttackEdgelord Dodge:DodgeNever ...
```

All instances train a **single shared policy**.

## Expected Output

```
============================================================
Boss AI Training Server
============================================================
Checkpoint directory: checkpoints
Checkpoint interval: every 10 training steps
Multi-instance mode: 4 instances
Godot executable: C:\Program Files\Godot\Godot.exe
Waiting for instances to connect...
Connected instances: 4/4
============================================================
[SERVER] Listening on ws://localhost:7000
[SERVER] Training loop starting (checks every 2 seconds)

... (game running) ...

============================================================
Training Step #42 [trigger: episode_count=4]
Episodes Processed: 168
Batch Size: 2048 transitions
Batches Pending: 0

Orchestrator Status:
  Connected Instances: 4/4
  Total Episodes: 168
  Total Transitions: 10752

Loss Metrics:
  Policy Loss:  0.234567
  Value Loss:   0.123456
  Entropy Loss: 0.045678
  Total Loss:   0.404701

Advantage Statistics:
  Mean: 0.123456
  Std:  0.456789

============================================================
```

## What Godot Should Do

For each PlayerMimic:

1. **Read command-line args** (these are auto-generated):
   ```
   --instance-id=0
   --player-weapon=sword
   --player-armor=light_armor
   --player-consumable=medkit
   --player-attack-behavior=AttackTactical
   --player-dodge-behavior=DodgeRandom
   --player-consumable-behavior=ConsumableTactical
   --player-wander-behavior=WanderRandomWalk
   --player-aggro-behavior=AggroFollowTarget
   ```

2. **Initialize with parsed values**

3. **Connect to WebSocket**:
   ```
   ws://localhost:(7000 + instance_id)
   ```

4. **Send handshake**:
   ```json
   {"instance_id": 0}
   ```

5. **Play game** (send/receive packets as normal)

## Available Options

### Equipment
- **Weapons**: sword, dagger, bow, staff
- **Armors**: light_armor, medium_armor, heavy_armor, shirt
- **Consumables**: none, medkit, potion

### Attack Behaviors
- AttackCowardly - Avoids combat
- AttackSpam - Rapid attacks
- AttackTactical - Calculated
- AttackEdgelord - Aggressive/risky

### Dodge Behaviors
- DodgeNever - Stationary target
- DodgePreemptive - Predicts attacks
- DodgeRandom - Chaotic evasion
- DodgeReactive - Reacts to damage

### Consumable Behaviors
- ConsumableNone - Never uses
- ConsumableRandom - Uses randomly
- ConsumableScaredyCat - Uses when low HP
- ConsumableTactical - Strategic usage
- ConsumableThreshold - Uses at HP threshold

### Wander Behaviors
- WanderHide - Seeks cover
- WanderImmovable - Stays put
- WanderRandomWalk - Random movement

### Aggro Behaviors
- AggroFollowTarget - Chases player
- AggroKeepDistance - Maintains distance

## Monitoring Training

### In Console
Watch training steps appear every 2-30 seconds as episodes accumulate:
- Episode count & transitions
- Loss metrics (Policy, Value, Entropy)
- Advantage statistics
- Instance status (connected count, total episodes/transitions)

### In Checkpoints
```
checkpoints/
├── policy_step_10.pt
├── policy_step_20.pt
├── policy_step_30.pt
└── ... (auto-saved every 10 training steps)
```

Load latest checkpoint:
```python
import torch
policy = torch.load("checkpoints/policy_step_30.pt")
```

## Troubleshooting

### No instances connect
- Check Godot path: `python main.py --num-instances=1 --godot-exec="PATH"`
- Verify ports 7000-7007 aren't firewalled
- Check Godot logs for argument parsing errors

### Training doesn't trigger
- Verify instances are actually connected (check "Connected instances: X/4")
- Check that episodes are completing (game should end after boss wins/player dies)
- Training requires: 4+ episodes OR 512+ transitions OR 30s timeout

### High memory usage
- 1 Godot instance ≈ 300-500MB
- 4 instances ≈ 1.2-2GB
- Use `--headless` for smaller footprint (edit main.py TrainingOrchestrator init)

### Instance crashes
- Check Godot error logs
- Verify command-line arguments are parsed correctly
- Ensure behaviors exist (spell check class names)

## Key Differences from Single-Instance

| Aspect | Single | Multi |
|--------|--------|-------|
| Instances | 1 | 4-8 |
| Configs | Fixed | Randomized |
| Training Data | Same player | Diverse players |
| Policy Convergence | Slower | Faster |
| Boss Robustness | Lower | Higher |
| Memory | ~500MB | ~2GB for 4 instances |
| Training Time | Baseline | 2-3x faster to convergence |

## Files to Read

- **ORCHESTRATOR.md** - Full multi-instance guide
- **ORCHESTRATOR_IMPLEMENTATION.md** - Implementation summary
- **ARCHITECTURE.md** - System overview
- **game/player_config.py** - Player configuration classes
- **training/orchestrator.py** - Orchestrator implementation

## Next Steps

1. Launch with `python main.py --num-instances=4`
2. Implement PlayerMimic argument parsing + handshake in Godot
3. Test connection (monitor console output)
4. Watch training progress
5. Tune config diversity if needed
