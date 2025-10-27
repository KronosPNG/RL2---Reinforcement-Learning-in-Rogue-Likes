using Godot;

[GlobalClass]
public abstract partial class ConsumableEffectBase : Resource, IConsumableEffect
{
	[ExportGroup("Effect Properties")]
	[Export] public float EffectValue = 0f; // Primary effect value (heal amount, etc.)
	[Export] public float Duration = 0f; // Duration of the effect (0 for instant)
	[Export] public float Cooldown = 0f; // Cooldown before the consumable can be used again
	[Export] public float UsageTime = 0f; // Time taken to consume (windup)

	// Execute the consumable effect on the player
	public abstract void Execute(Consumable consumable, PlayerController player);
	
	// Interrupt the effect (if applicable, e.g., channeled consumables)
	public abstract void Interrupt(Consumable consumable, PlayerController player);
	
	// Update for effects that need per-frame updates (e.g., regeneration)
	public virtual void Update(Consumable consumable, PlayerController player, float delta)
	{
		// Default: no update needed for instant effects
	}
}
