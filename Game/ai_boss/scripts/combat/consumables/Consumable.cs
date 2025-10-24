using Godot;

public enum ConsumableState { Available, InUse }

public abstract partial class Consumable : Node2D, IPedestalItem
{
    protected AnimatedSprite2D ActiveSprite;

    [Export] public string ItemName { get; set; } = "Unnamed Consumable Item";
    [Export] public string Description { get; set; } = "No description available.";

    // ---- Pedestal Item Properties ----
    [Export] public AnimatedSprite2D PedestalDisplaySprite { get; set; }
    [Export] public Vector2 PedestalDisplayScale { get; set; } = new Vector2(1f, 1f);

    // ---- Consumable Properties ----
    [Export] public ConsumableState State { get; set; } = ConsumableState.Available;
    [Export] public float EffectDuration { get; set; } = 0f; // Duration of the consumable effect in seconds
    [Export] public float CooldownTime { get; set; } = 0f; // Cooldown time before it can be used again
    [Export] public float UsageTime { get; set; } = 0f; // Time taken to use the consumable
    [Export] public bool IsReusable { get; private set; } = false; // Indicates if the consumable can be reused

    public override void _Ready()
    {
        ActiveSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (ActiveSprite == null)
        {
            GD.PrintErr($"[Consumable] AnimatedSprite2D node not found in consumable: {Name}");
            throw new System.Exception("AnimatedSprite2D node is required for Consumable");
        }
    }

    public void UseConsumable(PlayerController player)
    {
        if (State == ConsumableState.Available)
        {
            ApplyConsumableEffect(player);
        }
        else
        {
            GD.Print($"{ItemName} is currently in use and cannot be used.");
        }
    }

    protected abstract void ApplyConsumableEffect(PlayerController player);


    public bool CanSwapWith(IPedestalItem otherItem)
    {
        return otherItem == null || otherItem is Consumable;
    }

    public Vector2 GetDisplayScale()
    {
        // If we have a pedestal display sprite, use its configured scale
        if (PedestalDisplaySprite != null)
        {
            return PedestalDisplayScale;
        }

        return new Vector2(7f, 7f);
    }

    public void OnPickedUp(PlayerController player)
    {
        return;
    }
}