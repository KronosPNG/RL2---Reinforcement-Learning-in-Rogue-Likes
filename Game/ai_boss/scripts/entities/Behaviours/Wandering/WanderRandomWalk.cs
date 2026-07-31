using Godot;

[GlobalClass]
public partial class WanderRandomWalk : WanderBehaviour
{
	private Vector2 _currentWanderDirection = Vector2.Zero;

	public override Vector2 GetWanderDirection(IEntity entity, float delta)
	{
		// Don't multiply by delta - MoveAndSlide() handles that internally
		return _currentWanderDirection;
	}

	public override void OnEnterWander(IEntity entity)
	{
		_currentWanderDirection = GenerateRandomWanderDirection();
	}

	public override void OnExitWander(IEntity entity)
	{
		return;
	}

	public override bool ShouldStopWandering(IEntity entity)
	{
		if (entity.StateTimer > WanderMaxDuration)
		{
			return true;
		}

		return false;
		
	}
}
