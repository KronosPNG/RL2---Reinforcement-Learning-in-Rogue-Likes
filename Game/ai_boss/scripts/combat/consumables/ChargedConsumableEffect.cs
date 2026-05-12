using Godot;
using System;

[GlobalClass]
public partial class ChargedConsumableEffect : ConsumableEffectBase, IChargeableConsumable
{
	[ExportGroup("Charge Properties")]
	[Export] public ConsumableEffectBase BaseEffect; // The effect to execute when charged

	[ExportGroup("Visual Feedback")]
	[Export] public PackedScene ChargeEffectScene; // Visual effect during charging
	[Export] public string ChargeAnimation = "charge"; // Animation to play while charging
	[Export] public string FullyChargedAnimation = "fully_charged"; // Animation to play when fully charged

	private float _currentChargeTime = 0f;
	private Node _chargeEffect;
	private bool _isCharging = false;
	private bool _isFullyCharged = false;

	public void StartCharging(Consumable consumable, PlayableCharacter player)
	{
		_isCharging = true;
		_currentChargeTime = 0f;
		_isFullyCharged = false;

		if (consumable.ActiveSprite != null && !string.IsNullOrEmpty(ChargeAnimation))
		{
			consumable.ActiveSprite.Play(ChargeAnimation);
		}

		// Spawn charge effect
		if (ChargeEffectScene != null)
		{
			_chargeEffect = ChargeEffectScene.Instantiate();
			consumable.AddChild(_chargeEffect);
		}

		// GD.Print($"[ChargedConsumableEffect] Started charging consumable (charge time: {UsageTime}s)");
	}

	public void UpdateCharge(Consumable consumable, PlayableCharacter player, float delta)
	{
		if (!_isCharging) return;

		float previousChargeTime = _currentChargeTime;
		_currentChargeTime += delta;

		// Check if we've reached max charge time (UsageTime)
		if (_currentChargeTime >= UsageTime)
		{
			_currentChargeTime = UsageTime;

			// Play fully charged animation if we just reached max charge
			if (!_isFullyCharged && previousChargeTime < UsageTime)
			{
				_isFullyCharged = true;
				if (consumable.ActiveSprite != null && !string.IsNullOrEmpty(FullyChargedAnimation))
				{
					consumable.ActiveSprite.Play(FullyChargedAnimation);
				}
				// GD.Print($"[ChargedConsumableEffect] Fully charged!");
			}
		}

		// Update charge effect intensity based on charge level
		UpdateChargeEffect(consumable);
	}

	public bool CanReleaseCharge()
	{
		// Can only release when fully charged (reached UsageTime)
		return _isCharging && _currentChargeTime >= UsageTime;
	}

	public override void Execute(Consumable consumable, PlayableCharacter player)
	{
		if (!_isCharging)
		{
			GD.PrintErr("[ChargedConsumableEffect] Cannot execute charged effect: not currently charging.");
			return;
		}

		// Verify full charge was reached
		if (_currentChargeTime < UsageTime)
		{
			// GD.Print("[ChargedConsumableEffect] Charge time too short, cancelling");
			Interrupt(consumable, player);
			return;
		}

		// GD.Print($"[ChargedConsumableEffect] Executing charged effect after {_currentChargeTime:F2}s of charging");

		// Clean up charging effects
		StopCharging(consumable);

		// Execute the base effect (effect value is already set in the base effect config)
		if (BaseEffect != null)
		{
			BaseEffect.Execute(consumable, player);
		}
		else
		{
			GD.PrintErr("[ChargedConsumableEffect] No base effect configured!");
			consumable.CompleteEffect();
		}
	}

	public override void Interrupt(Consumable consumable, PlayableCharacter player)
	{
		StopCharging(consumable);
		consumable.ResetConsumableState();
	}

	private void StopCharging(Consumable consumable)
	{
		_isCharging = false;
		_currentChargeTime = 0f;
		_isFullyCharged = false;

		// Clean up charge effect
		if (_chargeEffect != null)
		{
			_chargeEffect.QueueFree();
			_chargeEffect = null;
		}
	}

	private void UpdateChargeEffect(Consumable consumable)
	{
		// Update visual/audio feedback based on charge level
		if (_chargeEffect == null) return;

		float chargeRatio = UsageTime > 0 ? _currentChargeTime / UsageTime : 0f;
		// Example: modulate effect based on charge level
		// if (_chargeEffect.HasMethod("SetChargeLevel"))
		// {
		// 	_chargeEffect.Call("SetChargeLevel", chargeRatio);
		// }
	}

	// ---- IChargeableConsumable Implementation ----
	void IChargeableConsumable.StartCharging(Consumable consumable, PlayableCharacter player)
	{
		StartCharging(consumable, player);
	}

	void IChargeableConsumable.UpdateCharge(Consumable consumable, PlayableCharacter player, float delta)
	{
		UpdateCharge(consumable, player, delta);
	}

	bool IChargeableConsumable.CanReleaseCharge()
	{
		return CanReleaseCharge();
	}

	float IChargeableConsumable.GetCurrentChargeTime() => _currentChargeTime;
	float IChargeableConsumable.GetMaxChargeTime() => UsageTime;
}
