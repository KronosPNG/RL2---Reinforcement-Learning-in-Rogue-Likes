using Godot;

[GlobalClass]
public abstract partial class PlayerAttackBehaviour : Resource, IPlayerAttackBehaviour
{
    protected Weapon _weapon;
    protected AttackDecision _lastAttackDecision;

    public float Priority {get; protected set;}

    public PlayerAttackBehaviour(PlayerMimic player)
    {
        _weapon = player.EquippedWeapon;
    }

    public virtual bool CanAttack(PlayerMimic player)
    {
        if (player.CurrentState == EntityState.Dead || 
            player.CurrentState == EntityState.Hit ||
            player.CurrentState == EntityState.DodgePrep)
        {
            return false;  // Can't attack in these states
        }

        return _weapon.CanStartAttack(false) || _weapon.CanStartAttack(true);
    }
    
    public virtual float EvaluateOpportunity(PlayerMimic player)  // 0-1 opportunity
    {
        return 1f;
    }

    public virtual Vector2 GetAimDirection(PlayerMimic player)
    {
        var targetPosition = player.GetTargetPosition();

        return (targetPosition - player.GlobalPosition).Normalized();  
    }

    public virtual AttackDecision GetAttackDecision(PlayerMimic player)
    {
        Vector2 aim = GetAimDirection(player);
        
        // Decide: Light, Heavy, or Charged variant?
        if (_weapon.CanStartAttack(false))
        {
            bool isChargeable = _weapon.LightAttackConfig is ChargedAttack;

            _lastAttackDecision = new AttackDecision
            (
                isChargeable ? AttackType.ChargedLight : AttackType.Light,
                aim
            );

            return _lastAttackDecision;
        }

        else if (_weapon.CanStartAttack(true))
        {
            bool isChargeable = _weapon.HeavyAttackConfig is ChargedAttack;

            _lastAttackDecision = new AttackDecision
            (
                isChargeable ? AttackType.ChargedHeavy : AttackType.Heavy,
                aim
            );

            return _lastAttackDecision;
        }
        
        _lastAttackDecision = new AttackDecision(AttackType.Light, aim); // Default fallback (shouldn't happen if CanAttack is used correctly)
        
        return _lastAttackDecision;
    }

    public virtual Vector2 GetMovementDirection(PlayerMimic player)
    {
        bool isAttackLight = _lastAttackDecision.Type switch
        {
            AttackType.Light => false,
            AttackType.ChargedLight => true,
            AttackType.Heavy => false,
            AttackType.ChargedHeavy => true,
            _ => false
        };
        var bossRef = player.BossRef;

        float distanceToBoss = player.GlobalPosition.DistanceTo(bossRef.GlobalPosition);
        float attackRange;

        if(isAttackLight)
        {
            attackRange = player.EquippedWeapon.LightAttackConfig.Range;
        }

        else
        {
            attackRange = player.EquippedWeapon.HeavyAttackConfig.Range;
        }

        // If we're outside of attack range, move towards the boss
        if (distanceToBoss > attackRange * 0.8f) // Add some buffer to prevent constant jittering at the edge of range
        {
            return player.GlobalPosition.DirectionTo(bossRef.GlobalPosition);
        }

        return Vector2.Zero; // Otherwise, stay still while attacking

    }
}