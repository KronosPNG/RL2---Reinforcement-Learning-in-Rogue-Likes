using Godot;

[GlobalClass]
public partial class MeleeProjectileAttack : CrescentMeleeAttack
{
    [Export] public ProjectileAttack ProjectileAttack { get; set; }

    public override void Execute(WeaponBase weapon, Vector2 target, bool facingLeft)
    {
        base.Execute(weapon, target, facingLeft);
        ProjectileAttack?.Execute(weapon, target, facingLeft);
    }
}