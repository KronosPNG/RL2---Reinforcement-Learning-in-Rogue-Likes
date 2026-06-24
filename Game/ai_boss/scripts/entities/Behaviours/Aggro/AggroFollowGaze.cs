using Godot;

[GlobalClass]
public partial class AggroFollowGaze : AggroBehaviour
{
    public override void OnEnterNotice(IEntity entity)
    {
        // No special setup needed when entering notice state
    }

    public override void OnExitNotice(IEntity entity)
    {
        // No special teardown needed when exiting notice state
    }
    public override bool CanSeeTarget(IEntity entity)
    {
        return base.CanSeeTarget(entity);
    }

    public override bool ShouldLoseTarget(IEntity entity)
    {
        if (entity.Target == null || !IsInstanceValid(entity.Target)) return true;

        // If we can still see the target, don't lose them
        if (CanSeeTarget(entity))
            return false;

        // Lose target only after being unable to see them for the decay time
        if (entity.StateTimer > AggroDecayTime)
            return true;

        return false;
    }

    public override Vector2 GetChaseDirection(IEntity entity, float delta)
    {
        return Vector2.Zero; // This behavior does not move the entity
    }

    public override void PerformAggroBehaviour(IEntity entity)
    {
        PerformAggroBehaviour((EnemyEntity)entity);
    }

    public void PerformAggroBehaviour(EnemyEntity entity)
    {
        // Dummy does not chase but follows with eyes
		if (entity.Target != null && IsInstanceValid(entity.Target))
		{
			Vector2 direction = entity.GlobalPosition.DirectionTo(entity.Target.GlobalPosition);
			string animationName = "look_";

            // if player is to the left of dummy
            // considering flip
            if (direction.X < 0)
            {
                entity.FacingDirection = Vector2.Left;
                animationName += "left";
            }
            else
            {
                entity.FacingDirection = Vector2.Right;
                animationName += "right";
            }

            if (direction.Y <= 0)
            {
                animationName += "_up";
			}
            else
            {
                animationName += "_down";
            }

			// Play the appropriate animation
			//entity.VisualController.PlayState();
		}
		return;
    }

    public override void Initialize(IEntity entity)
    {
        throw new System.NotImplementedException();
    }

}