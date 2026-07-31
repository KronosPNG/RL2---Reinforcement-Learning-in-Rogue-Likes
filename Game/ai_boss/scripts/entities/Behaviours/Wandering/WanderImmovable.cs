using Godot;

[GlobalClass]
public partial class WanderImmovable : WanderBehaviour
{
    public override void OnEnterWander(IEntity entity)
    {
        // Immovable entities do not wander, so no action is needed
    }

    public override void OnExitWander(IEntity entity)
    {
        // Immovable entities do not wander, so no action is needed
    }
    
    public override Vector2 GetWanderDirection(IEntity entity, float delta)
    {
        return Vector2.Zero; // No movement for immovable entities
    }
 
    public override bool ShouldStopWandering(IEntity entity)
    {
        return true; // Immovable entities should stop wandering immediately
    }
}