using Godot;

[GlobalClass]
public partial class AttackInoffensive : AttackBehaviour
{

    public override void OnEnterAttack(Entity entity)
    {
        // No special setup needed for inoffensive attack
    }

    public override void OnExitAttack(Entity entity)
    {
        // No special teardown needed for inoffensive attack
    }
    public override bool CanAttack(Entity entity)
    {
        return false;
    }

    public override bool IsInAttackRange(Entity entity)
    {
        return false;
    }

    public override void PerformAttack(Entity entity)
    {
        // Inoffensive attack does nothing
    }

    public override Vector2 GetAttackVelocity(Entity entity, float delta)
    {
        return entity.Velocity; // Maintain current velocity
    }
}