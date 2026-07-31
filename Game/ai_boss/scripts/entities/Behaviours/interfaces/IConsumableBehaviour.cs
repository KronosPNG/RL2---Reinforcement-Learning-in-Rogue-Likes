using Godot;

public interface IConsumableBehaviour
{
    float EvaluateOpportunity(PlayerMimic player);  // 0-1 opportunity
    ConsumableUsageMode GetUsageMode(PlayerMimic player);
    ConsumableAction GetConsumableAction(PlayerMimic player);
    Vector2 GetMovementDirection(PlayerMimic player);
}

public enum ConsumableUsageMode { Instant, Charged, None }
public enum ConsumableAction { Use, Charge }