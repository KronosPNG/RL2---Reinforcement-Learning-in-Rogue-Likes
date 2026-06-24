using Godot;

[GlobalClass]
public abstract partial class WanderBehaviour : Resource, IWanderBehaviour
{
	[Export] public float WanderMaxDuration { get; set; } = 1f;
	[Export] public float WanderCooldown { get; set; } = .5f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float WanderSpeedMultiplier { get; set; } = 0.75f;
	public abstract void OnEnterWander(IEntity entity);
	public abstract void OnExitWander(IEntity entity);
	public abstract Vector2 GetWanderDirection(IEntity entity, float delta);
	public abstract bool ShouldStopWandering(IEntity entity);
	protected Vector2 GenerateRandomWanderDirection()
	{
		return new Vector2(
			GD.Randf() * 2 - 1,
			GD.Randf() * 2 - 1
		).Normalized();
	}
}