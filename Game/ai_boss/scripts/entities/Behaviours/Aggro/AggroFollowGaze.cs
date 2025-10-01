using Godot;

[GlobalClass]
public partial class AggroFollowGaze : AggroBehaviour
{
    public override void OnEnterNotice(Entity entity)
    {
        // No special setup needed when entering notice state
    }

    public override void OnExitNotice(Entity entity)
    {
        // No special teardown needed when exiting notice state
    }
    public override bool CanSeeTarget(Entity entity)
    {
        if (entity.Target == null || !IsInstanceValid(entity.Target))
        {
            return false;
        }

        float distance = entity.GlobalPosition.DistanceTo(entity.Target.GlobalPosition);
        bool canSee = distance <= DetectionRange;
        return canSee;
    }

    public override bool ShouldLoseTarget(Entity entity)
    {
        if (entity.Target == null) return true;

        Vector2 toTarget = entity.Target.GlobalPosition - entity.GlobalPosition;
        float distanceToTarget = toTarget.Length();

        if (distanceToTarget > DetectionRange)
            return true;

        return false;
    }

    public override Vector2 GetChaseVelocity(Entity entity, float delta)
    {
        return Vector2.Zero; // This behavior does not move the entity
    }

    public override void PerformPlayerNoticeBehavior(Entity entity)
    {
        // Dummy does not chase but follows with eyes
		if (entity.Target != null && IsInstanceValid(entity.Target))
		{
			Vector2 direction = entity.GlobalPosition.DirectionTo(entity.Target.GlobalPosition);
			string animationName = "look_";

            // if player is to the left of dummy
            // considering flip
            if ((direction.X < 0 && !entity.FlipSpriteHorizontally) || (direction.X > 0 && entity.FlipSpriteHorizontally))
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
			entity.PlayAnimation(animationName);
		}
		return;
    }
}