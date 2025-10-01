using Godot;

public interface IAggroBehaviour
{
    void OnEnterNotice(Entity entity);
    void OnExitNotice(Entity entity);
    bool CanSeeTarget(Entity entity);
    bool ShouldLoseTarget(Entity entity);
    Vector2 GetChaseVelocity(Entity entity, float delta);
    void PerformPlayerNoticeBehavior(Entity entity);

}