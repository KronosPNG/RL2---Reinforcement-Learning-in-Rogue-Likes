using Godot;

[GlobalClass]
public partial class ConsumableScaredyCat : ConsumableBehaviour
{
    public ConsumableScaredyCat() : base()
    {
        Priority = 0.9f; // High priority
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (player.EquippedConsumable == null) return 0f;

        if (player.CurrentState == EntityState.ConsumableCharging)
            return 0.9f;

        if (player.CurrentState == EntityState.Hit || player.CurrentHealth < player.MaxHealth)
            return 1f;

        return 0f;
    }
}