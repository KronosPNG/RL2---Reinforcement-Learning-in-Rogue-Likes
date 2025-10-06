using Godot;

[GlobalClass]
public partial class AttackMelee : AttackBehaviour
{
	public override bool CanAttack(Entity entity)
	{
		GD.Print($"[AttackMelee] CanAttack check for entity {entity.Name}");
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
		entity.Weapon.Attack();
	}
}
