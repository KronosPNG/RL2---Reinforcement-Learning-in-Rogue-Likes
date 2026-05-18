using Godot;

[GlobalClass]
public abstract partial class PlayerAttackBehaviour : Resource, IPlayerAttackBehaviour
{
    protected Weapon weapon;

    public PlayerAttackBehaviour(PlayerMimic player)
    {
        weapon = player.EquippedWeapon;
    }

    public virtual bool CanAttack(PlayerMimic player)
    {
        if (player.CurrentState == EntityState.Dead || 
            player.CurrentState == EntityState.Hit ||
            player.CurrentState == EntityState.DodgePrep)
        {
            return false;  // Can't attack in these states
        }

        return weapon.CanStartAttack(false) || weapon.CanStartAttack(true);
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
        if (weapon.CanStartAttack(false))
        {
            bool isChargeable = weapon.LightAttackConfig is ChargedAttack;
            return new AttackDecision
            (
                isChargeable ? AttackType.ChargedLight : AttackType.Light,
                aim
            );
        }
        else if (weapon.CanStartAttack(true))
        {
            bool isChargeable = weapon.HeavyAttackConfig is ChargedAttack;
            return new AttackDecision
            (
                isChargeable ? AttackType.ChargedHeavy : AttackType.Heavy,
                aim
            );
        }
        
        return new AttackDecision (AttackType.Light, aim);
    }

}