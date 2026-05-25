using Godot;

[GlobalClass]
public abstract partial class ConsumableBehaviour : Resource, IConsumableBehaviour
{
    public float Priority {get; protected set;}
    public abstract float EvaluateOpportunity(PlayerMimic player);
    public abstract ConsumableUsageMode GetUsageMode(PlayerMimic player);
    public abstract bool ShouldUseConsumable(PlayerMimic player);
}