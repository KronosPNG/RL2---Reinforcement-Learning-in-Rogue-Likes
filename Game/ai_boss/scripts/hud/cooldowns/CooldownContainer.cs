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

    private void OnWeaponEquipped(float lightAttackCooldown, float heavyAttackCooldown)
    {
        GD.Print($"[CooldownContainer] Weapon equipped with light attack cooldown: {lightAttackCooldown}s, heavy attack cooldown: {heavyAttackCooldown}s");
        _lightAttackIndicator.SetCooldownDuration(lightAttackCooldown);
        _heavyAttackIndicator.SetCooldownDuration(heavyAttackCooldown);
    }

    private void OnConsumableEquipped()
    {
        _consumableIndicator.Visible = true;
    }

    private void OnConsumableUsed()
    {
        _consumableIndicator.Visible = false;
    }
}