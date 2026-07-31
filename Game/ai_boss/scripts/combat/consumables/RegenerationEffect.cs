using Godot;

[GlobalClass]
public partial class RegenerationEffect : ConsumableEffectBase
{
	[ExportGroup("Regeneration Properties")]
	[Export] public float TickInterval = 1f; // Time between heal ticks

	private float _elapsedTime = 0f;
	private float _nextTickTime = 0f;
	private bool _isActive = false;
	private PlayableCharacter _targetPlayer = null;

	public override void Execute(Consumable consumable, PlayableCharacter player)
	{
		if (player == null)
		{
			GD.PrintErr("[RegenerationEffect] Player is null, cannot execute effect");
			return;
		}

		// Store reference to player
		_targetPlayer = player;

		// Start regeneration
		_isActive = true;
		_elapsedTime = 0f;
		_nextTickTime = TickInterval;

		// GD.Print($"[RegenerationEffect] Started regeneration: {HealPerTick} HP every {TickInterval}s for {Duration}s");

		// Apply first heal immediately
		player.Heal(EffectValue);

		// Add this effect to the player's active effects list
		player.AddActiveEffect(this);

		// Mark consumable as complete immediately since effect is now on player
		if (consumable != null)
		{
			consumable.CompleteEffect();
		}
	}

	public override void Update(Consumable consumable, PlayableCharacter player, float delta)
	{
		if (!_isActive || _targetPlayer == null) return;

		_elapsedTime += delta;
		_nextTickTime -= delta;

		// Check if it's time for next heal tick
		if (_nextTickTime <= 0f)
		{
			_targetPlayer.Heal(EffectValue);
			_nextTickTime = TickInterval;
			// GD.Print($"[RegenerationEffect] Heal tick: {HealPerTick} HP ({_elapsedTime:F1}s / {Duration}s)");
		}

		// Check if effect duration is complete
		if (_elapsedTime >= Duration)
		{
			// GD.Print($"[RegenerationEffect] Regeneration complete after {Duration}s");
			Interrupt(null, _targetPlayer);
		}
	}

	public override void Interrupt(Consumable consumable, PlayableCharacter player)
	{
		if (!_isActive) return;

		_isActive = false;
		_elapsedTime = 0f;
		_nextTickTime = 0f;

		// GD.Print("[RegenerationEffect] Regeneration interrupted or completed");

		// Remove this effect from the player's active effects list
		if (_targetPlayer != null)
		{
			_targetPlayer.RemoveActiveEffect(this);
			_targetPlayer = null;
		}
	}
}
