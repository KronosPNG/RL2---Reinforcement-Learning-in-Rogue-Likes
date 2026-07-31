using Godot;

[GlobalClass]
public partial class SelfCenteredAttack : ProjectileAttack
{
    public override void Execute(WeaponBase weapon, Vector2 target, bool facingLeft)
    {
        Vector2 originGlobal = weapon.GlobalPosition;   

        base.Execute(weapon, originGlobal, facingLeft);
    }

}