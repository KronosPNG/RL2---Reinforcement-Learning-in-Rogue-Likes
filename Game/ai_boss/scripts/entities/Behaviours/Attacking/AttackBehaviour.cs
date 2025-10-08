using Godot;

[GlobalClass]
public abstract partial class AttackBehaviour : Resource, IAttackBehaviour
{
	// ---- Attack Properties ----
	[Export] public float AttackRange { get; set; }
	public abstract void OnEnterAttack(Entity entity);
	public abstract void OnExitAttack(Entity entity);
	public abstract void PerformAttack(Entity entity);
	public virtual bool IsInAttackRange(Entity entity)
    {	
		if (entity.Target == null)
			return false;
			
        Vector2 targetPosition = entity.Target.GlobalPosition;
        return entity.GlobalPosition.DistanceTo(targetPosition) <= AttackRange;
    }
	
	public abstract bool CanAttack(Entity entity);
	public abstract Vector2 GetAttackVelocity(Entity entity, float delta);

}