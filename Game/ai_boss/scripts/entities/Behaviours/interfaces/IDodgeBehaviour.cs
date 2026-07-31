using Godot;

public interface IDodgeBehaviour
{
    float EvaluateOpportunity(PlayerMimic player);  // 0-1 threat level
    Vector2 GetDodgeDirection(PlayerMimic player);
}