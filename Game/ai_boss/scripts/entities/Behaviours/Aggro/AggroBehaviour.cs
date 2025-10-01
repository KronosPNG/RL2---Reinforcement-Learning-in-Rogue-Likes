using Godot;

[GlobalClass]
public abstract partial class AggroBehaviour : Resource, IAggroBehaviour
{
	[Export] public float DetectionRange { get; set; }
	[Export] public float ChaseSpeed { get; set; }
	public abstract void OnEnterNotice(Entity entity);
	public abstract void OnExitNotice(Entity entity);
	public abstract bool CanSeeTarget(Entity entity);
	public abstract bool ShouldLoseTarget(Entity entity);
	public abstract Vector2 GetChaseVelocity(Entity entity, float delta);
	public abstract void PerformPlayerNoticeBehavior(Entity entity);
}