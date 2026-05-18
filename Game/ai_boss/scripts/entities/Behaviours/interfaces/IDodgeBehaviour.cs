using Godot;

public interface IDodgeBehaviour
{
    float EvaluateThreat(IEntity entity);  // 0-1 threat level
    bool ShouldDodge(IEntity entity);
    Vector2 GetDodgeDirection(IEntity entity);
}