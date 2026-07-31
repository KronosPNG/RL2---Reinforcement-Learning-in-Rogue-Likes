using Godot;

[GlobalClass]
public partial class ConsumableNone : ConsumableBehaviour
{
    public ConsumableNone() : base()
    {
        Priority = 0f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        return 0f; // Never use
    }

    public override ConsumableAction GetConsumableAction(PlayerMimic player)
    {
        return ConsumableAction.Charge;
    }

    public override Vector2 GetMovementDirection(PlayerMimic player)
    {
        return Vector2.Zero; // No movement when using consumable
    }
}