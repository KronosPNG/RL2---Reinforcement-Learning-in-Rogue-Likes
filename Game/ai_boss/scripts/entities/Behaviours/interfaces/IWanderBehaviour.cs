using Godot;

public interface IWanderBehaviour
{
	void OnEnterWander(IEntity entity);
	void OnExitWander(IEntity entity);
	Vector2 GetWanderDirection(IEntity entity, float delta);
	bool ShouldStopWandering(IEntity entity);
}
