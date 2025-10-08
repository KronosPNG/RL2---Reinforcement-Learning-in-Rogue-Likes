using Godot;

[GlobalClass]
public partial class AttackInRange : AttackBehaviour
{
	public override bool CanAttack(Entity entity)
	{
		return IsInAttackRange(entity);
	}

	public override Vector2 GetAttackVelocity(Entity entity, float delta)
	{
		return Vector2.Zero;
	}

	public override void OnEnterAttack(Entity entity)
	{
	   return;
	}

	public override void OnExitAttack(Entity entity)
	{
	   return;
	}

	public override void PerformAttack(Entity entity)
	{
		if (entity.Weapon.CanStartAttack())
		{
			GD.Print("AttackInRange: PerformAttack() called");
			entity.Weapon.Attack();
		}
	}
}
