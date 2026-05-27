using Godot;

[GlobalClass]
public partial class AggroFollowTarget : AggroBehaviour
{
    public override bool CanSeeTarget(IEntity entity)
    {
        return base.CanSeeTarget(entity);
    }

    public override Vector2 GetChaseDirection(IEntity entity, float delta)
    {
        if (entity.Target == null || !IsInstanceValid(entity.Target))
            return Vector2.Zero;

        Vector2 direction = entity.GlobalPosition.DirectionTo(entity.Target.GlobalPosition);

        if (entity is INavigable navigableEntity)
        {
            NavigationAgent2D navAgent = navigableEntity.NavAgent;

            if (navAgent != null && !navAgent.IsNavigationFinished())
            {   
                Vector2 nextPos = navAgent.GetNextPathPosition();
                    
                if (entity.GlobalPosition.DistanceSquaredTo(nextPos) > 1.0f)
                {
                    // GD.Print($"AggroFollowTarget: Using pathfinding direction towards next path position {nextPos}");
                    return entity.GlobalPosition.DirectionTo(nextPos);
                }

                else
                {
                    return Vector2.Zero; // Close enough to next path point, wait for next frame to update path
                }
            } 
            
            else
            {
                GD.PrintErr($"AggroFollowTarget: Entity is missing a NavigationAgent2D for pathfinding!");
                return direction; // Fallback to direct movement if no nav agent
            }
        }

        else
        {
            GD.PrintErr($"AggroFollowTarget: Entity is not navigable but is using AggroFollowTarget behaviour!");
            return direction; // Fallback to direct movement if no nav agent
        }

    }

    public override void OnEnterNotice(IEntity entity)
    {
        return;
    }

    public override void OnExitNotice(IEntity entity)
    {
        return;
    }

    public override void PerformAggroBehaviour(IEntity entity)
    {
        if (entity.Target == null || !IsInstanceValid(entity.Target))
            return;
            
        if (entity is not INavigable navigableEntity)
            return;

        NavigationAgent2D navAgent = navigableEntity.NavAgent;

		if (navAgent != null)
        {
            navAgent.TargetPosition = entity.Target.GlobalPosition;
        }
    }

    public override bool ShouldLoseTarget(IEntity entity)
    {
        if (entity.Target == null || !IsInstanceValid(entity.Target))
            return true;

        // If we can still see the target, definitely don't lose them
        if (CanSeeTarget(entity))
            return false;

        // Lose target only if we can't see them anymore AND enough time has passed
        if (entity.StateTimer > AggroDecayTime)
            return true;

        return false;
    }
}