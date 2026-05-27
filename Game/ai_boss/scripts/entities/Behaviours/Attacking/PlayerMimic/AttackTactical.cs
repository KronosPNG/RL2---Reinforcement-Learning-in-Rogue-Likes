using Godot;

[GlobalClass]
public partial class AttackTactical : PlayerAttackBehaviour
{
    public AttackTactical(PlayerMimic player) : base(player)
    {
        Priority = 0.5f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (!CanAttack(player)) return 0f;

        float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        float maxRange = Mathf.Max(
            _weapon.LightAttackConfig.Range,
            _weapon.HeavyAttackConfig.Range
        );

        if (distanceToTarget > maxRange)
            return 0f;  // Out of range, close distance
        
        var bossState = player.BossRef.CurrentState;

        // Already charging, good opportunity to finish the attack
        if (player.CurrentState == EntityState.AttackCharging)
            return 0.6f;

        // Boss is attacking - very unsafe window
        if (bossState == BossState.Attacking)
            return 0.1f;

        // Boss is in cooldown or idle - safe to attack
        return 0.65f;
    }

    public override AttackDecision GetAttackDecision(PlayerMimic player)
    {
        Vector2 aim = GetAimDirection(player);
        
        // CASE 1: Already charging - finish the charge
        if (player.CurrentState == EntityState.AttackCharging)
        {
            bool chargeReady = _weapon.CanReleaseCharge();
            bool isHeavyCharge = _weapon.IsCurrentAttackHeavy;
            
            if (chargeReady)
            {
                _lastAttackDecision = new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.Heavy : AttackType.Light,
                    AimDirection = aim
                };
                return _lastAttackDecision;
            }
            else
            {
                _lastAttackDecision = new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.ChargedHeavy : AttackType.ChargedLight,
                    AimDirection = aim
                };
                return _lastAttackDecision;
            }
        }

        // CASE 2: Analyze situation and pick the smartest attack
        var bossState = player.BossRef.CurrentState;
        float distanceToTarget = player.GlobalPosition.DistanceTo(player.BossRef.GlobalPosition);
        
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
        if (bossState == BossState.Idle || bossState == BossState.Cooldown)
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