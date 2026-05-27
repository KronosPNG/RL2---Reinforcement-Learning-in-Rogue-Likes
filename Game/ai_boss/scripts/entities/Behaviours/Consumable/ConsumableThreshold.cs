using Godot;

[GlobalClass]
public partial class ConsumableThreshold : ConsumableBehaviour
{
    private const float HealthThreshold = 0.5f; // Use consumable when health is below 50%

    public ConsumableThreshold(PlayerMimic player) : base(player)
    {
        Priority = 0.8f; // High priority when health is low
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (player.EquippedConsumable == null) return 0f;

        if (player.CurrentHealth / player.MaxHealth < HealthThreshold)
            return Priority;

        return 0f;
    }
}