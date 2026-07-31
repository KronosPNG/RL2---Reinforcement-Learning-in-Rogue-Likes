using Godot;

[GlobalClass]
public partial class AggroStandStill : AggroBehaviour
{
    public override Vector2 GetChaseDirection(IEntity entity, float delta)
    {
        return Vector2.Zero;
    }

    public override void Initialize(IEntity entity)
    {
        return;
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
        return;
    }

    public override bool ShouldLoseTarget(IEntity entity)
    {
        return false;
    }
}