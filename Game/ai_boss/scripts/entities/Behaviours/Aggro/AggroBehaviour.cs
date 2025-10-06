using Godot;

[GlobalClass]
public abstract partial class AggroBehaviour : Resource, IAggroBehaviour
{
	[Export] public float DetectionRange { get; set; }
	[Export(PropertyHint.Range, "0, 2, 0.1")] public float ChaseSpeedModifier { get; set; } = 1f;
	[Export] public float AggroDecayTime { get; private set; } = 2f;
	public string animationName = "walk";

	public abstract void OnEnterNotice(Entity entity);
	public abstract void OnExitNotice(Entity entity);
	public virtual bool CanSeeTarget(Entity entity)
	{
		if (entity.Target == null || !IsInstanceValid(entity.Target))
			return false;
		return entity.GlobalPosition.DistanceTo(entity.Target.GlobalPosition) <= DetectionRange;
	}
	public abstract bool ShouldLoseTarget(Entity entity);
	public abstract Vector2 GetChaseVelocity(Entity entity, float delta);
	public abstract void PerformAggroBehaviour(Entity entity);
}