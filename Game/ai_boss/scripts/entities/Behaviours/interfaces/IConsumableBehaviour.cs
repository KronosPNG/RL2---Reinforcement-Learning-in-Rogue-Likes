public interface IConsumableBehaviour
{
    float EvaluateUsefulness(PlayerMimic player);  // 0-1 usefulness
    bool ShouldUseConsumable(PlayerMimic player);
    ConsumableUsageMode GetUsageMode(PlayerMimic player);
}

public enum ConsumableUsageMode { Instant, Charged, None }