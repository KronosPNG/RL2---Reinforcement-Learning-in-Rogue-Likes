using Godot;

[GlobalClass]
public abstract partial class AttackBehaviour : Resource, IAttackBehaviour
{
	[Export] public float AttackRange { get; set; }
	[Export] public float AttackCooldown { get; set; }
	[Export] public AttackBase AttackType { get; set; }
	public abstract void OnEnterAttack(Entity entity);
	public abstract void OnExitAttack(Entity entity);
	public abstract void PerformAttack(Entity entity);
	public abstract bool IsInAttackRange(Entity entity);
	public abstract bool CanAttack(Entity entity);
	public abstract Vector2 GetAttackVelocity(Entity entity, float delta);

}