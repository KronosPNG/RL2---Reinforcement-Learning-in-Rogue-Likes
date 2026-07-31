using Godot;

[GlobalClass]
public abstract partial class AttackBehaviour : Resource, IAttackBehaviour
{
	// ---- Attack Properties ----
	[Export] public float AttackRange { get; set; }
	public abstract void OnEnterAttack(EnemyEntity entity);
	public abstract void OnExitAttack(EnemyEntity entity);
	public abstract void PerformAttack(EnemyEntity entity);
	public virtual bool IsInAttackRange(EnemyEntity entity)
    {	
		if (entity.Target == null)
			return false;
			
        Vector2 targetPosition = entity.Target.GlobalPosition;
        return entity.GlobalPosition.DistanceTo(targetPosition) <= AttackRange;
    }
	
	public abstract bool CanAttack(EnemyEntity entity);
}