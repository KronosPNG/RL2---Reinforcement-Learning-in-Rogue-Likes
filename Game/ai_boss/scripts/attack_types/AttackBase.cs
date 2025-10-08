using Godot;

[GlobalClass]
public abstract partial class AttackBase : Resource, IAttack
{
    [ExportGroup("Attack Properties")]
    [Export] public float Damage = 0f;
    [Export] public float Cooldown = 0f;
    [Export] public float Windup = 0f;
    [Export] public float Active = 0f;
    [Export] public float Knockback = 0f;

    public abstract void Execute(WeaponBase weapon, Vector2 target, bool facingLeft);
    public abstract void Interrupt(WeaponBase weapon);
}