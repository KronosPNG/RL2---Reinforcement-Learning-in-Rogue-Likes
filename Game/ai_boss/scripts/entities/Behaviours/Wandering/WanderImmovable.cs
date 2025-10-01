using Godot;

[GlobalClass]
public partial class WanderImmovable : WanderBehaviour
{
    public override void OnEnterWander(Entity entity)
    {
        // Immovable entities do not wander, so no action is needed
    }

    public override void OnExitWander(Entity entity)
    {
        // Immovable entities do not wander, so no action is needed
    }
    
    public override Vector2 GetWanderVelocity(Entity entity, float delta)
    {
        return Vector2.Zero; // No movement for immovable entities
    }

    public override bool ShouldStopWandering(Entity entity)
    {
        return true; // Immovable entities should stop wandering immediately
    }
}