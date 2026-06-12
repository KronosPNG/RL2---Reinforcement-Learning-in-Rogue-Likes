using Godot;

public partial class CooldownContainer : HBoxContainer
{
	private CooldownIndicator _lightAttackIndicator;
	private CooldownIndicator _heavyAttackIndicator;
	private SimpleIndicator _consumableIndicator;

	public override void _Ready()
	{
		_lightAttackIndicator = GetNode<CooldownIndicator>("LightAttackCooldown");
		_heavyAttackIndicator = GetNode<CooldownIndicator>("HeavyAttackCooldown");
		_consumableIndicator = GetNode<SimpleIndicator>("ConsumableIndicator");
		_consumableIndicator.Visible = false;
		
		// Subscribe to weapon attack events to update cooldown indicators
		EventBus.OnWeaponAttackStarted += OnWeaponAttackStarted;
		EventBus.OnWeaponEquipped += OnWeaponEquipped;
		EventBus.OnConsumableEquipped += OnConsumableEquipped;
		EventBus.OnConsumableUsed += OnConsumableUsed;
	}

	private void OnWeaponAttackStarted(string attackName)
	{
		GD.Print($"[CooldownContainer] Received attack started event for '{attackName}' attack.");

		if (attackName == "light")
		{
			_lightAttackIndicator.StartCooldown();
		}
		else if (attackName == "heavy")
		{
			_heavyAttackIndicator.StartCooldown();
		}
	}

	private void OnWeaponEquipped(Weapon weapon)
	{
		GD.Print($"[CooldownContainer] Weapon equipped with light attack cooldown: {weapon.LightAttackConfig.Cooldown}s, heavy attack cooldown: {weapon.HeavyAttackConfig.Cooldown}s");
		_lightAttackIndicator.SetCooldownDuration(weapon.LightAttackConfig.Cooldown);
		_heavyAttackIndicator.SetCooldownDuration(weapon.HeavyAttackConfig.Cooldown);
	}

	private void OnConsumableEquipped(Consumable cons = null)
	{
		_consumableIndicator.Visible = true;
	}

	private void OnConsumableUsed()
	{
		_consumableIndicator.Visible = false;
	}
}
