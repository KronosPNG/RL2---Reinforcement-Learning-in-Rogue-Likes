using Godot;

public interface IWanderBehaviour
{
	void OnEnterWander(EnemyEntity entity);
	void OnExitWander(EnemyEntity entity);
	Vector2 GetWanderVelocity(EnemyEntity entity, float delta);
	bool ShouldStopWandering(EnemyEntity entity);
}
