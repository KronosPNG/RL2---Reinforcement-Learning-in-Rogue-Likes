using Godot;

[GlobalClass]
public partial class AttackInRange : AttackBehaviour
{
	public override bool CanAttack(EnemyEntity entity)
	{
		return IsInAttackRange(entity);
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
		if (entity.AttackManager.CanStartAttack())
		{
			// GD.Print("AttackInRange: PerformAttack() called");
			entity.AttackManager.Attack();
		}
	}
}
