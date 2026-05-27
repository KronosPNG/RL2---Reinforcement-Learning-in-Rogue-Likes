using Godot;

[GlobalClass]
public partial class ConsumableTactical : ConsumableBehaviour
{
    private float minDistanceToEnemy = 200f; // Use consumable when enemy is within this distance
    private float maxHealthGain;

    public ConsumableTactical(PlayerMimic player) : base(player)
    {
        Priority = 0.8f; // Moderate priority for tactical use
        maxHealthGain = player.EquippedConsumable.EffectConfig.EffectValue;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        if (player.EquippedConsumable == null) return 0f;

        var boss = player.BossRef;

        if (player.CurrentHealth + maxHealthGain > player.MaxHealth * 0.8f)
            return .25f; // Low priority if consumable would overheal or provide minimal benefit

        // if player too close to boss lower priority to avoid using consumable in dangerous situations
        if(player.Position.DistanceTo(boss.Position) < minDistanceToEnemy)
        {
            if (boss.PreviousState == BossState.Attacking)
                return 0.75f; // High priority to use when boss is attacking and player is close
            else
                return 0f; // Moderate priority when close but boss is not attacking
        } 

        return 0.85f; // High priority to use when boss is far away, as it's safer to heal
    }
}