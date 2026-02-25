using Godot;

[GlobalClass]
public abstract partial class AggroBehaviour : Resource, IAggroBehaviour
{
	[Export] public float DetectionRange { get; set; }
	[Export(PropertyHint.Range, "0, 2, 0.1")] public float ChaseSpeedModifier { get; set; } = 1f;
	[Export] public float AggroDecayTime { get; private set; } = 2f;
	public string animationName = "walk";

	public abstract void OnEnterNotice(IEntity entity);
	public abstract void OnExitNotice(IEntity entity);
	public virtual bool CanSeeTarget(IEntity entity)
	{
		if (entity.Target == null || !IsInstanceValid(entity.Target))
			return false;
		return entity.GlobalPosition.DistanceTo(entity.Target.GlobalPosition) <= DetectionRange;
	}
	public abstract bool ShouldLoseTarget(IEntity entity);
	public abstract Vector2 GetChaseVelocity(IEntity entity, float delta);
	public abstract void PerformAggroBehaviour(IEntity entity);
}