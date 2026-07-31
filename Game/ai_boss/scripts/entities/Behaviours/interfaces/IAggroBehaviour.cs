using Godot;

public interface IAggroBehaviour
{
    void OnEnterNotice(IEntity entity);
    void OnExitNotice(IEntity entity);
    bool CanSeeTarget(IEntity entity);
    bool ShouldLoseTarget(IEntity entity);
    Vector2 GetChaseDirection(IEntity entity, float delta);
    void PerformAggroBehaviour(IEntity entity);

}