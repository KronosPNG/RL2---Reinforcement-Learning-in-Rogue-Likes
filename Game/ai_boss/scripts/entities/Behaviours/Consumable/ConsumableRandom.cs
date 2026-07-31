using Godot;

[GlobalClass]
public partial class ConsumableRandom : ConsumableBehaviour
{
    public ConsumableRandom() : base()
    {
        Priority = 0.5f; // Baseline priority for random use
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (player.EquippedConsumable == null) return 0f;
        if (player.CurrentHealth >= player.MaxHealth) return 0f;
        if (!player.EquippedConsumable.CanUse()) return 0f;

        // Randomly decide to use the consumable with a 50% chance each evaluation
        return GD.Randf() < 0.5f ? Priority : 0f;
    }
}