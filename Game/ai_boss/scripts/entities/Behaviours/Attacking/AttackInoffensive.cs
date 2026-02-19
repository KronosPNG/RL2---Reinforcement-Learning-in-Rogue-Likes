using Godot;

[GlobalClass]
public partial class AttackInoffensive : AttackBehaviour
{

    public override void OnEnterAttack(EnemyEntity entity)
    {
        // No special setup needed for inoffensive attack
    }

    public override void OnExitAttack(EnemyEntity entity)
    {
        // No special teardown needed for inoffensive attack
    }
    public override bool CanAttack(EnemyEntity entity)
    {
        return false;
    }

    public override bool IsInAttackRange(EnemyEntity entity)
    {
        return false;
    }

    public override void PerformAttack(EnemyEntity entity)
    {
        // Inoffensive attack does nothing
    }

    public override Vector2 GetAttackVelocity(EnemyEntity entity, float delta)
    {
        return entity.Velocity; // Maintain current velocity
    }
}