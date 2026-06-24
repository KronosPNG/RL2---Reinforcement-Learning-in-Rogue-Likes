using Godot;

[GlobalClass]
public partial class WanderHide : WanderBehaviour
{
    private Vector2 _hidingSpot;

    public override void OnEnterWander(IEntity entity)
    {
        _hidingSpot = GenerateFurthestHidingSpot(entity);
    }

    public override void OnExitWander(IEntity entity)
    {
        return;
    }
    
    public override Vector2 GetWanderDirection(IEntity entity, float delta)
    {
        if (entity is not INavigable navigableEntity)
        {
            // Move directly towards hiding spot
            return entity.GlobalPosition.DirectionTo(_hidingSpot);
        }

        var navAgent = navigableEntity.NavAgent;
        navAgent.TargetPosition = _hidingSpot;

        if (!navAgent.IsNavigationFinished())
        {
            Vector2 nextPos = navAgent.GetNextPathPosition();
            
            if (entity.GlobalPosition.DistanceSquaredTo(nextPos) > 1.0f)
            {
                return entity.GlobalPosition.DirectionTo(nextPos);
            }

            else
            {
                return Vector2.Zero; // Close enough to next path point, wait for next frame to update path
            }
        }

        return Vector2.Zero; // Already at hiding spot
    }
 
    public override bool ShouldStopWandering(IEntity entity)
    {
        if (entity.StateTimer > WanderMaxDuration)
		{
			return true;
		}

		return false;
    }

    private Vector2 GenerateFurthestHidingSpot(IEntity entity)
    {
       // Find furthest point from boss within navigable area

        if (entity is not INavigable navigableEntity)
        {
            // Sample a single random point
            return entity.GlobalPosition + GenerateRandomWanderDirection() * 400f; // Arbitrary distance
        }

        // Sample multiple random points and choose the furthest one from the boss
        Vector2 bestSpot = entity.GlobalPosition;
        float maxDistance = 0f;

        for (int i = 0; i < 10; i++)
        {
            Vector2 randomDirection = GenerateRandomWanderDirection();
            Vector2 samplePoint = entity.GlobalPosition + randomDirection * 400f; // Arbitrary distance

            if (samplePoint.DistanceTo(entity.Target.GlobalPosition) > maxDistance)
            {
                maxDistance = samplePoint.DistanceTo(entity.Target.GlobalPosition);
                bestSpot = samplePoint;
            }
        }

        if (entity.GlobalPosition.DistanceSquaredTo(bestSpot) > 1.0f)
        {
            return bestSpot;
        }

        return entity.GlobalPosition; // If the best spot is too close, just stay put      
    }
}