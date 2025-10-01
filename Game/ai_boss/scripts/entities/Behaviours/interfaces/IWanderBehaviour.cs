using Godot;

public interface IWanderBehaviour
{
	void OnEnterWander(Entity entity);
	void OnExitWander(Entity entity);
	Vector2 GetWanderVelocity(Entity entity, float delta);
	bool ShouldStopWandering(Entity entity);
}
