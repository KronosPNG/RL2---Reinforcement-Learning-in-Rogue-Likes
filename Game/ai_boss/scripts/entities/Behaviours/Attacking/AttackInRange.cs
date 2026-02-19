using Godot;

[GlobalClass]
public partial class AttackInRange : AttackBehaviour
{
	public override bool CanAttack(EnemyEntity entity)
	{
		return IsInAttackRange(entity);
	}

	public override Vector2 GetAttackVelocity(EnemyEntity entity, float delta)
	{
		return Vector2.Zero;
	}

	public override void OnEnterAttack(EnemyEntity entity)
	{
	   return;
	}

	public override void OnExitAttack(EnemyEntity entity)
	{
	   return;
	}

	public override void PerformAttack(EnemyEntity entity)
	{
		if (entity.Weapon.CanStartAttack())
		{
			// GD.Print("AttackInRange: PerformAttack() called");
			entity.Weapon.Attack();
		}
	}
}
