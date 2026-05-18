using Godot;

[GlobalClass]
public partial class AttackEdgelord : PlayerAttackBehaviour
{
    public AttackEdgelord(PlayerMimic player) : base(player)
    {
    }
    

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (!CanAttack(player)) return 0f;

        float distanceToTarget = player.GlobalPosition.DistanceTo(player.BossRef.GlobalPosition);
        float maxRange = Mathf.Max(
            weapon.LightAttackConfig.Range,
            weapon.HeavyAttackConfig.Range
        );

        if (distanceToTarget > maxRange)
            return 0.05f;

        // EDGELORD: Committed to the charge! Priority increases as charge builds
        if (player.CurrentState == EntityState.AttackCharging)
        {
            // Get how far along the charge is (0.0 to 1.0)
            float chargePercent = weapon.CurrentChargingAttack.getCurrentChargeTime() / weapon.CurrentChargingAttack.MaxChargeTime;
            
            // Baseline 0.5 + up to 0.5 more as charge builds
            // At 0% charge: 0.5
            // At 50% charge: 0.75
            // At 100% charge: 1.0
            float priority = 0.5f + (chargePercent * 0.5f);
            return priority;
        }

        // Not charging - baseline medium priority to initiate charge
        return 0.5f;
    }

    public override AttackDecision GetAttackDecision(PlayerMimic player)
    {
        Vector2 aim = GetAimDirection(player);
        
        bool lightIsCharged = weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = weapon.HeavyAttackConfig is ChargedAttack;

        // CASE 1: Already charging - commit to it until release
        if (player.CurrentState == EntityState.AttackCharging)
        {
            bool chargeReady = weapon.CanReleaseCharge();
            bool isHeavyCharge = weapon.IsCurrentAttackHeavy;
            
            if (chargeReady)
            {
                // Time to release!
                return new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.Heavy : AttackType.Light,
                    AimDirection = aim
                };
            }
            else
            {
                // Keep charging
                return new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.ChargedHeavy : AttackType.ChargedLight,
                    AimDirection = aim
                };
            }
        }

        // CASE 2: Not charging - prefer charged heavy attacks if available
        if (weapon.CanStartAttack(true))
        {
            // Start charging heavy if it's a charged attack, otherwise just heavy
            AttackType heavyType = heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy;
            return new AttackDecision 
            { 
                Type = heavyType, 
                AimDirection = aim 
            };
        }

        // Heavy not ready, try charged light
        if (weapon.CanStartAttack(false))
        {
            AttackType lightType = lightIsCharged ? AttackType.ChargedLight : AttackType.Light;
            return new AttackDecision 
            { 
                Type = lightType, 
                AimDirection = aim 
            };
        }

        // Fallback (shouldn't reach here if CanAttack works right)
        return new AttackDecision 
        { 
            Type = AttackType.Light, 
            AimDirection = aim 
        };
    }
}
