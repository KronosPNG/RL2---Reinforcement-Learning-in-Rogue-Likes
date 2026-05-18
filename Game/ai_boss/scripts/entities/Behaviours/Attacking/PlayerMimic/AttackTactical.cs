using Godot;

[GlobalClass]
public partial class AttackTactical : PlayerAttackBehaviour
{
    public AttackTactical(PlayerMimic player) : base(player)
    {
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (!CanAttack(player)) return 0f;

        float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        float maxRange = Mathf.Max(
            weapon.LightAttackConfig.Range,
            weapon.HeavyAttackConfig.Range
        );

        if (distanceToTarget > maxRange)
            return 0.05f;  // Out of range, close distance
        
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
            bool chargeReady = weapon.CanReleaseCharge();
            bool isHeavyCharge = weapon.IsCurrentAttackHeavy;
            
            if (chargeReady)
            {
                return new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.Heavy : AttackType.Light,
                    AimDirection = aim
                };
            }
            else
            {
                return new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.ChargedHeavy : AttackType.ChargedLight,
                    AimDirection = aim
                };
            }
        }

        // CASE 2: Analyze situation and pick the smartest attack
        var bossState = player.BossRef.CurrentState;
        float distanceToTarget = player.GlobalPosition.DistanceTo(player.BossRef.GlobalPosition);
        
        bool lightAvailable = weapon.CanStartAttack(false);
        bool heavyAvailable = weapon.CanStartAttack(true);
        bool lightIsCharged = weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = weapon.HeavyAttackConfig is ChargedAttack;
        
        bool lightInRange = distanceToTarget <= weapon.LightAttackConfig.Range;
        bool heavyInRange = distanceToTarget <= weapon.HeavyAttackConfig.Range;

        // Calculate DPS for instant attacks only
        float lightDPS = 0f;
        if (lightAvailable && !lightIsCharged && lightInRange)
        {
            lightDPS = weapon.LightAttackConfig.Damage / weapon.LightAttackConfig.Cooldown;
        }
        
        float heavyDPS = 0f;
        if (heavyAvailable && !heavyIsCharged && heavyInRange)
        {
            heavyDPS = weapon.HeavyAttackConfig.Damage / weapon.HeavyAttackConfig.Cooldown;
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
            return new AttackDecision { Type = GetAttackType(pickHeavy), AimDirection = aim };
        }

        // No instant attacks available - in safe windows, we CAN commit to charged attacks
        if (bossState == BossState.Idle || bossState == BossState.Cooldown)
        {
            if (heavyAvailable && heavyInRange)
                return new AttackDecision { Type = GetAttackType(true), AimDirection = aim };
            if (lightAvailable && lightInRange)
                return new AttackDecision { Type = GetAttackType(false), AimDirection = aim };
        }

        // Fallback: pick any available attack in range
        if (heavyAvailable && heavyInRange)
            return new AttackDecision { Type = GetAttackType(true), AimDirection = aim };
        
        if (lightAvailable && lightInRange)
            return new AttackDecision { Type = GetAttackType(false), AimDirection = aim };

        // Last resort: whatever is available
        if (heavyAvailable)
            return new AttackDecision { Type = GetAttackType(true), AimDirection = aim };

        return new AttackDecision { Type = GetAttackType(false), AimDirection = aim };
    }
}