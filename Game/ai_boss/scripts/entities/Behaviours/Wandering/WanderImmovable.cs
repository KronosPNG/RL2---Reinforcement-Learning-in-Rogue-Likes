using Godot;

[GlobalClass]
public partial class WanderImmovable : WanderBehaviour
{
    public override void OnEnterWander(EnemyEntity entity)
    {
        // Immovable entities do not wander, so no action is needed
    }

    public override void OnExitWander(EnemyEntity entity)
    {
        // Immovable entities do not wander, so no action is needed
    }
    
    public override Vector2 GetWanderVelocity(EnemyEntity entity, float delta)
    {
        return Vector2.Zero; // No movement for immovable entities
    }

    public override bool ShouldStopWandering(EnemyEntity entity)
    {
        return true; // Immovable entities should stop wandering immediately
    }
}