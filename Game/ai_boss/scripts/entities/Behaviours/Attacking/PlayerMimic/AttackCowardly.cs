using Godot;

[GlobalClass]
public partial class AttackCowardly : PlayerAttackBehaviour
{
    public AttackCowardly(PlayerMimic player) : base(player)
    {
        Priority = 0.3f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (!CanAttack(player)) return 0f;

        float distanceToTarget = player.GlobalPosition.DistanceTo(player.BossRef.GlobalPosition);
        float maxRange = Mathf.Max(
            _weapon.LightAttackConfig.Range,
            _weapon.HeavyAttackConfig.Range
        );

        if (distanceToTarget > maxRange)
            return 0f;

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
        
        bool lightAvailable = _weapon.CanStartAttack(false);
        bool heavyAvailable = _weapon.CanStartAttack(true);
        bool lightIsCharged = _weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = _weapon.HeavyAttackConfig is ChargedAttack;
        
        float lightRange = _weapon.LightAttackConfig.Range;
        float heavyRange = _weapon.HeavyAttackConfig.Range;
        float lightCooldown = _weapon.LightAttackConfig.Cooldown;
        float heavyCooldown = _weapon.HeavyAttackConfig.Cooldown;
        
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

            _lastAttackDecision = new AttackDecision { Type = type, AimDirection = aim };

            return _lastAttackDecision;
        }
        
        // Only one has max range, pick it
        if (lightHasMaxRange)
        {
            _lastAttackDecision = new AttackDecision 
            { 
                Type = lightIsCharged ? AttackType.ChargedLight : AttackType.Light, 
                AimDirection = aim 
            };
            return _lastAttackDecision;
        }
        
        if (heavyHasMaxRange)
        {
            _lastAttackDecision = new AttackDecision 
            { 
                Type = heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy, 
                AimDirection = aim 
            };
            return _lastAttackDecision;
        }
        
        // Fallback: whatever is available
        if (lightAvailable)
        {
            _lastAttackDecision = new AttackDecision 
            { 
                Type = lightIsCharged ? AttackType.ChargedLight : AttackType.Light, 
                AimDirection = aim 
            };
            return _lastAttackDecision;
        }
        
        _lastAttackDecision = new AttackDecision 
        { 
            Type = heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy, 
            AimDirection = aim 
        };
        return _lastAttackDecision;
    }
}
