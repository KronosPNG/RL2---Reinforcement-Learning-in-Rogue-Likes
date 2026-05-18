using Godot;

[GlobalClass]
public partial class AttackSpam : PlayerAttackBehaviour
{
    public AttackSpam(PlayerMimic player) : base(player){
        
    }
    
    public override float EvaluateOpportunity(PlayerMimic player)
    {
        // Step 1: checking if we can attack at all
        if (!CanAttack(player)) return 0f;
        
        // Step 2: checking distance
        float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        float maxRange = Mathf.Max(
            weapon.LightAttackConfig.Range,
            weapon.HeavyAttackConfig.Range
        );

        if (distanceToTarget > maxRange)
            return 0.05f;  // Target is out of range, but we want to close the distance

        // Step 3: if we are already in the middle of charging an attack, we are committed to it
        if (player.CurrentState == EntityState.AttackCharging)
            return 0.7f; // Already charging, so we are committed to attacking


        // Step 4: attack is available and target is in range, so we should attack
        return 0.65f;
    }

    public override AttackDecision GetAttackDecision(PlayerMimic player)
    {
        Vector2 aim = GetAimDirection(player);
        float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        
        // CASE 1: Already charging - decide to continue or release
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
        
        // CASE 2: Not charging yet - pick best DPS instant attack IN RANGE
        bool lightAvailable = weapon.CanStartAttack(false);
        bool heavyAvailable = weapon.CanStartAttack(true);
        bool lightIsCharged = weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = weapon.HeavyAttackConfig is ChargedAttack;
        
        // Check if attacks are in range
        bool lightInRange = distanceToTarget <= weapon.LightAttackConfig.Range;
        bool heavyInRange = distanceToTarget <= weapon.HeavyAttackConfig.Range;
        
        // Calculate DPS ONLY for instant attacks that are available AND in range
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
        
        // Pick highest DPS instant attack that's in range
        if (lightDPS > 0 || heavyDPS > 0)
        {
            AttackType chosenType = lightDPS >= heavyDPS ? AttackType.Light : AttackType.Heavy;
            
            return new AttackDecision 
            { 
                Type = chosenType, 
                AimDirection = aim 
            };
        }
        
        // Fallback (no instant attacks in range and ready)
        return new AttackDecision 
        { 
            Type = AttackType.Light, 
            AimDirection = aim 
        };
    }
}