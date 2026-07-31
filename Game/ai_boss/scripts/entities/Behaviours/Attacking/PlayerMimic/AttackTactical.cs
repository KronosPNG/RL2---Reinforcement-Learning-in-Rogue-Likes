using Godot;

[GlobalClass]
public partial class AttackTactical : PlayerAttackBehaviour
{
    public AttackTactical() : base()
    {
        Priority = 0.5f;
        TimeBetweenAttacks = 2f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        // Already charging — must keep going regardless of CanAttack (weapon isn't Ready while charging)
        if (player.CurrentState == EntityState.AttackCharging)
            return 0.6f;

        if (!CanAttack(player)) return 0f;

        float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        float maxRange = Mathf.Max(
            _weapon.LightAttackConfig.Range,
            _weapon.HeavyAttackConfig.Range
        );

        if (distanceToTarget > maxRange)
            return 0f;  // Out of range, close distance

        if (player.Target is not BossRL boss) return 0f;

		BossState bossState = boss.CurrentState;

        // Boss is attacking - very unsafe window
        if (bossState == BossState.Attacking)
            return 0.1f;

        // Boss is in cooldown or idle - safe to attack
        return 0.65f;
    }

    public override AttackDecision GetAttackDecision(PlayerMimic player)
    {
        Vector2 aim = GetAimDirection(player);
        
        // CASE 1: Already charging - fire as soon as charge reaches the boss's distance
        if (player.CurrentState == EntityState.AttackCharging)
        {
            bool isHeavyCharge = _weapon.IsCurrentAttackHeavy;
            float distance = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
            bool enoughCharge = _weapon.IsChargedEnough(distance) || _weapon.IsFullyCharged();

            AttackType keepCharging = isHeavyCharge ? AttackType.ChargedHeavy : AttackType.ChargedLight;
            AttackType release     = isHeavyCharge ? AttackType.Heavy        : AttackType.Light;

            _lastAttackDecision = new AttackDecision
            {
                Type = enoughCharge ? release : keepCharging,
                AimDirection = aim
            };
            return _lastAttackDecision;
        }

        // CASE 2: Analyze situation and pick the smartest attack
        var boss = player.Target as BossRL;
		BossState bossState = boss.CurrentState;

        float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        
        bool lightAvailable = _weapon.CanStartAttack(false);
        bool heavyAvailable = _weapon.CanStartAttack(true);
        bool lightIsCharged = _weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = _weapon.HeavyAttackConfig is ChargedAttack;
        
        bool lightInRange = distanceToTarget <= _weapon.LightAttackConfig.Range;
        bool heavyInRange = distanceToTarget <= _weapon.HeavyAttackConfig.Range;

        // Calculate DPS for instant attacks only
        float lightDPS = 0f;
        if (lightAvailable && !lightIsCharged && lightInRange)
        {
            lightDPS = _weapon.LightAttackConfig.Damage / _weapon.LightAttackConfig.Cooldown;
        }
        
        float heavyDPS = 0f;
        if (heavyAvailable && !heavyIsCharged && heavyInRange)
        {
            heavyDPS = _weapon.HeavyAttackConfig.Damage / _weapon.HeavyAttackConfig.Cooldown;
        }

        // Helper to convert attack type considering if it's charged
        AttackType GetAttackType(bool isHeavy)
        {
            if (isHeavy)
                return heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy;
            else
                return lightIsCharged ? AttackType.ChargedLight : AttackType.Light;
        }
        
        // If we have instant attacks in range, pick the best DPS
        // (This applies regardless of safe window - we always want max efficiency)
        if (lightDPS > 0 || heavyDPS > 0)
        {
            bool pickHeavy = heavyDPS >= lightDPS;
            _lastAttackDecision = new AttackDecision { Type = GetAttackType(pickHeavy), AimDirection = aim };
            return _lastAttackDecision;
        }

        // No instant attacks available - in safe windows, we CAN commit to charged attacks
        if (bossState == BossState.Idle || bossState == BossState.Cooldown || bossState == BossState.Walking)
        {
            if (heavyAvailable && heavyInRange)
            {
                _lastAttackDecision = new AttackDecision { Type = GetAttackType(true), AimDirection = aim };
                return _lastAttackDecision;
            }

            if (lightAvailable && lightInRange)
            {
                _lastAttackDecision = new AttackDecision { Type = GetAttackType(false), AimDirection = aim };
                return _lastAttackDecision;
            }
        }

        // Fallback: pick any available attack in range
        if (heavyAvailable && heavyInRange)
        {
            _lastAttackDecision = new AttackDecision { Type = GetAttackType(true), AimDirection = aim };
            return _lastAttackDecision;
        }
        
        if (lightAvailable && lightInRange)
        {
            _lastAttackDecision = new AttackDecision { Type = GetAttackType(false), AimDirection = aim };
            return _lastAttackDecision;
        }

        // Last resort: whatever is available
        if (heavyAvailable)
        {
            _lastAttackDecision = new AttackDecision { Type = GetAttackType(true), AimDirection = aim };
            return _lastAttackDecision;
        }

        _lastAttackDecision = new AttackDecision { Type = GetAttackType(false), AimDirection = aim };
        return _lastAttackDecision; 
    }
}