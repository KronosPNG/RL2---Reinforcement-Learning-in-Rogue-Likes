using Godot;

[GlobalClass]
public partial class ChargedMeleeAttack : ChargedAttack, IAttack, IChargeable
{
    [ExportGroup("Melee Attack")]
    [Export] public AttackBase MeleeAttack { get; set; } // The melee attack to use

    protected override async void ExecuteChargedAttack(Weapon weapon, Vector2 target, bool facingLeft, float chargedDamage)
    {
        if (MeleeAttack == null)
        {
            GD.PrintErr("ChargedMeleeAttack: MeleeAttack is null!");
            weapon.ResetWeaponState(weapon._isCurrentAttackHeavy);
            return;
        }

        // Play attack animation first
        if (weapon._anim != null)
        {
            // GD.Print("[ChargedMeleeAttack] Playing attack animation");
            string animName = weapon._isCurrentAttackHeavy ? "heavy_attack" : "light_attack";
            weapon._anim.Play(animName);
        }

        // Execute the melee attack
        MeleeAttack.Execute(weapon, target, facingLeft);

        // Calculate total attack duration (windup + active time)
        float attackDuration = Windup + Active;
        
        // Wait for the attack to complete, then reset weapon state
        await weapon.ToSignal(weapon.GetTree().CreateTimer(attackDuration), "timeout");
        
        Interrupt(weapon);
    }

    public override void Interrupt(Weapon weapon)
    {
        base.Interrupt(weapon);
        MeleeAttack?.Interrupt(weapon);
        // Ensure weapon state is reset when interrupted
        weapon.ResetWeaponState(weapon._isCurrentAttackHeavy);
    }

}