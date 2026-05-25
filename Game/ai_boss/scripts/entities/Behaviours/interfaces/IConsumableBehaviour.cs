public interface IConsumableBehaviour
{
    float EvaluateOpportunity(PlayerMimic player);  // 0-1 opportunity
    bool ShouldUseConsumable(PlayerMimic player);
    ConsumableUsageMode GetUsageMode(PlayerMimic player);
}

public enum ConsumableUsageMode { Instant, Charged, None }