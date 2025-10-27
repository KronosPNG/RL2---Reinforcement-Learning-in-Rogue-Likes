using Godot;

[GlobalClass]
public partial class InstantHealEffect : ConsumableEffectBase
{
	public override void Execute(Consumable consumable, PlayerController player)
	{
		if (player == null)
		{
			GD.PrintErr("[InstantHealEffect] Player is null, cannot execute effect");
			return;
		}

		// Apply instant healing
		player.Heal(EffectValue);
		GD.Print($"[InstantHealEffect] Healed player for {EffectValue} HP");

		// Mark consumable as complete
		consumable.CompleteEffect();
	}

	public override void Interrupt(Consumable consumable, PlayerController player)
	{
		// Instant effects can't be interrupted
		// Just mark as complete if it was in progress
		if (consumable.State == ConsumableState.InUse)
		{
			consumable.CompleteEffect();
		}
	}
}
