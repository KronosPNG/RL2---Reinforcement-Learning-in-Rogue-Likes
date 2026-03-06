using Godot;

[GlobalClass]
public partial class AggroFollowTarget : AggroBehaviour
{
    public override bool CanSeeTarget(IEntity entity)
    {
        return base.CanSeeTarget(entity);
    }

    public override Vector2 GetChaseVelocity(IEntity entity, float delta)
    {
        if (entity.Target == null || !IsInstanceValid(entity.Target))
            return Vector2.Zero;

        Vector2 direction = (entity.Target.GlobalPosition - entity.GlobalPosition).Normalized();

        if (entity is INavigable navigableEntity)
        {
            NavigationAgent2D navAgent = navigableEntity.NavAgent;

            // Use navigation path if available and ready, otherwise move directly toward target
            if (navAgent != null && !navAgent.IsNavigationFinished() && navAgent.IsTargetReachable())
            {
                Vector2 nextPos = navAgent.GetNextPathPosition();
                // Only use nav path if it's valid (not at current position)
                if (entity.GlobalPosition.DistanceSquaredTo(nextPos) > 1.0f)
                {
                    direction = entity.GlobalPosition.DirectionTo(nextPos);
                }
            }
        }

        // Don't multiply by delta - MoveAndSlide() handles that internally
        return direction * entity.BaseSpeed * ChaseSpeedModifier;
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
        // Setup navigation if available
        // Note: Velocity is already set by GetChaseVelocity, don't override it here
        if (entity.Target == null || !IsInstanceValid(entity.Target))
            return;
            
        if (entity is not INavigable navigableEntity)
            return;

        NavigationAgent2D navAgent = navigableEntity.NavAgent;

		if (navAgent != null)
        {
            // Update navigation target
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