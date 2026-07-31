using Godot;

[GlobalClass]
public partial class DashMeleeAttack : CrescentMeleeAttack
{
    [Export] public float DashDistance = 50f; // Distance to dash forward during the attack
    [Export] public float DashDuration = 0.5f; // Duration of the dash movement

    public override void Execute(WeaponBase weapon, Vector2 target, bool facingLeft)
    {
        // Then execute the melee attack as usual
        base.Execute(weapon, target, facingLeft);

        // Perform the dash movement
        Vector2 dashDirection = (target - weapon.GlobalPosition).Normalized();
        
        // Move Owner forward by the dash distance
        var character = weapon.OwnerCharacter as IEntity;
        character.ApplyImpulse(dashDirection, DashDistance / DashDuration, DashDuration);
    }
}