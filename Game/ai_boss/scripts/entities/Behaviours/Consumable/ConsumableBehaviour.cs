using Godot;

[GlobalClass]
public abstract partial class ConsumableBehaviour : Resource, IConsumableBehaviour
{
    public abstract float EvaluateUsefulness(PlayerMimic player);
    public abstract ConsumableUsageMode GetUsageMode(PlayerMimic player);
    public abstract bool ShouldUseConsumable(PlayerMimic player);
}