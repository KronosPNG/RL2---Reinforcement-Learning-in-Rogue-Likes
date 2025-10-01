using Godot;

[GlobalClass]
public abstract partial class WanderBehaviour : Resource, IWanderBehaviour
{
	[Export] public float WanderMaxDuration { get; set; }
	[Export] public float WanderCooldown { get; set; }
	public abstract void OnEnterWander(Entity entity);
	public abstract void OnExitWander(Entity entity);
	public abstract Vector2 GetWanderVelocity(Entity entity, float delta);
	public abstract bool ShouldStopWandering(Entity entity);
}