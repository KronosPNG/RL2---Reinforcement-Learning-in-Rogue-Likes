using Godot;

[GlobalClass]
public abstract partial class ConsumableBehaviour : Resource, IConsumableBehaviour
{
    public float Priority {get; protected set;}
    public ConsumableUsageMode usageMode;

    public ConsumableBehaviour(PlayerMimic player)
    {
        usageMode = GetUsageMode(player);
    }
    
    public virtual ConsumableAction GetConsumableAction(PlayerMimic player)
    {
        return usageMode == ConsumableUsageMode.Charged ? ConsumableAction.Charge : ConsumableAction.Use;
    }

    public virtual ConsumableUsageMode GetUsageMode(PlayerMimic player)
    {
        if(player.EquippedConsumable.EffectConfig is IChargeableConsumable)
            return ConsumableUsageMode.Charged;
        else
            return ConsumableUsageMode.Instant;
    }
    
    public virtual Vector2 GetMovementDirection(PlayerMimic player)
    {
        var bossRef = player.BossRef;
        return (player.Position - bossRef.Position).Normalized();
    }

    public abstract float EvaluateOpportunity(PlayerMimic player);
    

}