using Godot;

[GlobalClass]
public abstract partial class WanderBehaviour : Resource, IWanderBehaviour
{
	[Export] public float WanderMaxDuration { get; set; }
	[Export] public float WanderCooldown { get; set; }
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float WanderSpeedMultiplier { get; set; } = 0.75f;
	public abstract void OnEnterWander(EnemyEntity entity);
	public abstract void OnExitWander(EnemyEntity entity);
	public abstract Vector2 GetWanderVelocity(EnemyEntity entity, float delta);
	public abstract bool ShouldStopWandering(EnemyEntity entity);
	protected Vector2 GenerateRandomWanderDirection()
	{
		return new Vector2(
			GD.Randf() * 2 - 1,
			GD.Randf() * 2 - 1
		).Normalized();
	}
}