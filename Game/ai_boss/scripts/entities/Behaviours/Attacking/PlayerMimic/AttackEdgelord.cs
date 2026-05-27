using Godot;

[GlobalClass]
public partial class AttackEdgelord : PlayerAttackBehaviour
{

    public AttackEdgelord(PlayerMimic player) : base(player)
    {
        Priority = .8f;
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
            return 0f;

        // EDGELORD: Committed to the charge! Priority increases as charge builds
        if (player.CurrentState == EntityState.AttackCharging)
        {
            // Get how far along the charge is (0.0 to 1.0)
            float chargePercent = _weapon.CurrentChargingAttack.getCurrentChargeTime() / _weapon.CurrentChargingAttack.MaxChargeTime;
            
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
        
        bool lightIsCharged = _weapon.LightAttackConfig is ChargedAttack;
        bool heavyIsCharged = _weapon.HeavyAttackConfig is ChargedAttack;

        // CASE 1: Already charging - commit to it until release
        if (player.CurrentState == EntityState.AttackCharging)
        {
            bool chargeReady = _weapon.CanReleaseCharge();
            bool isHeavyCharge = _weapon.IsCurrentAttackHeavy;
            
            if (chargeReady)
            {
                // Time to release!
                _lastAttackDecision = new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.Heavy : AttackType.Light,
                    AimDirection = aim
                };
                return _lastAttackDecision;
            }
            else
            {
                // Keep charging
                _lastAttackDecision = new AttackDecision
                {
                    Type = isHeavyCharge ? AttackType.ChargedHeavy : AttackType.ChargedLight,
                    AimDirection = aim
                };
                return _lastAttackDecision;
            }
        }

        // CASE 2: Not charging - prefer charged heavy attacks if available
        if (_weapon.CanStartAttack(true))
        {
            // Start charging heavy if it's a charged attack, otherwise just heavy
            AttackType heavyType = heavyIsCharged ? AttackType.ChargedHeavy : AttackType.Heavy;
            _lastAttackDecision = new AttackDecision 
            { 
                Type = heavyType, 
                AimDirection = aim 
            };
            return _lastAttackDecision;
        }

        // Heavy not ready, try charged light
        if (_weapon.CanStartAttack(false))
        {
            AttackType lightType = lightIsCharged ? AttackType.ChargedLight : AttackType.Light;
            _lastAttackDecision = new AttackDecision 
            { 
                Type = lightType, 
                AimDirection = aim 
            };
            return _lastAttackDecision;
        }

        // Fallback (shouldn't reach here if CanAttack works right)
        _lastAttackDecision = new AttackDecision 
        { 
            Type = AttackType.Light, 
            AimDirection = aim 
        };
        return _lastAttackDecision;
    }
}
