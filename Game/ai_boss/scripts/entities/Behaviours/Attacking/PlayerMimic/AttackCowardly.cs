using Godot;

[GlobalClass]
public partial class AttackCowardly : PlayerAttackBehaviour
{
    public AttackCowardly(PlayerMimic player) : base(player)
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

        // COWARDLY: Only attack when boss is clearly not a threat (cooldown window)
        var bossState = player.BossRef.CurrentState;

        // Boss is actively attacking or charging - NEVER attack
        if (bossState == BossState.Attacking || 
            bossState == BossState.AttackCharging)
            return 0f;

        // Boss is in cooldown or idle - safe window to attack quickly and retreat
        return 0.9f;
    }

    public override AttackDecision GetAttackDecision(PlayerMimic player)
    {
        Vector2 aim = GetAimDirection(player);
        
        bool lightAvailable = weapon.CanStartAttack(false);
        bool heavyAvailable = weapon.CanStartAttack(true);
        bool lightIsCharged = weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = weapon.HeavyAttackConfig is ChargedAttack;
        
        float lightRange = weapon.LightAttackConfig.Range;
        float heavyRange = weapon.HeavyAttackConfig.Range;
        float lightCooldown = weapon.LightAttackConfig.Cooldown;
        float heavyCooldown = weapon.HeavyAttackConfig.Cooldown;
        
        // COWARDLY: Prioritize distance (range) first, then speed (cooldown)
        
        // Find the longest range among available attacks
        float maxRange = 0f;
        if (lightAvailable) maxRange = Mathf.Max(maxRange, lightRange);
        if (heavyAvailable) maxRange = Mathf.Max(maxRange, heavyRange);
        
        // Check which attacks have the max range
        bool lightHasMaxRange = lightAvailable && Mathf.IsEqualApprox(lightRange, maxRange);
        bool heavyHasMaxRange = heavyAvailable && Mathf.IsEqualApprox(heavyRange, maxRange);
        
        // If both have max range, pick the faster one
        if (lightHasMaxRange && heavyHasMaxRange)
        {
            bool pickLight = lightCooldown <= heavyCooldown;
            AttackType type = pickLight ? 
                (lightIsCharged ? AttackType.ChargedLight : AttackType.Light) :
                (heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy);
            return new AttackDecision { Type = type, AimDirection = aim };
        }
        
        // Only one has max range, pick it
        if (lightHasMaxRange)
        {
            return new AttackDecision 
            { 
                Type = lightIsCharged ? AttackType.ChargedLight : AttackType.Light, 
                AimDirection = aim 
            };
        }
        
        if (heavyHasMaxRange)
        {
            return new AttackDecision 
            { 
                Type = heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy, 
                AimDirection = aim 
            };
        }
        
        // Fallback: whatever is available
        if (lightAvailable)
        {
            return new AttackDecision 
            { 
                Type = lightIsCharged ? AttackType.ChargedLight : AttackType.Light, 
                AimDirection = aim 
            };
        }
        
        return new AttackDecision 
        { 
            Type = heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy, 
            AimDirection = aim 
        };
    }
}
