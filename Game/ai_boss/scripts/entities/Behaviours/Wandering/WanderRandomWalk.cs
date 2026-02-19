using Godot;

[GlobalClass]
public partial class WanderRandomWalk : WanderBehaviour
{
    private Vector2 _currentWanderDirection = Vector2.Zero;

    public override Vector2 GetWanderVelocity(EnemyEntity entity, float delta)
    {
        return _currentWanderDirection * entity.BaseSpeed * WanderSpeedMultiplier * delta;
    }

    public override void OnEnterWander(EnemyEntity entity)
    {
        _currentWanderDirection = GenerateRandomWanderDirection();
    }

    public override void OnExitWander(EnemyEntity entity)
    {
        return;
    }

    public override bool ShouldStopWandering(EnemyEntity entity)
    {
        if (entity.StateTimer > WanderMaxDuration)
		{
			return true;
        }

        return false;
		
    }
}