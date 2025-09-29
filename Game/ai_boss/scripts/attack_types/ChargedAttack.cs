using System;
using Godot;

[GlobalClass]
public partial class ChargedAttack : AttackBase, IAttack, IChargeable
{
    [ExportGroup("Charge Properties")]
    [Export] public float MinChargeTime = 0.5f;        // Minimum time to charge
    [Export] public float MaxChargeTime = 2.0f;        // Maximum charge time
    [Export] public float MinChargeDamagePercentage = 0.1f;       // Damage at minimum charge
    [Export] public float MaxChargeDamagePercentage = 1f;       // Damage at maximum charge
    [Export] public bool RequiresMinCharge = true;     // If true, must charge for MinChargeTime

    [ExportGroup("Visual Feedback")]
    [Export] public PackedScene ChargeEffectScene;     // Visual effect during charging
    [Export] public string ChargeAnimation = "charge"; // Animation to play while charging
    [Export] public string FullyChargedAnimation = "fully_charged"; // Animation to play when fully charged
    [Export] public string IdleAnimation = "idle";     // Animation to play while idle
    public float _currentChargeTime = 0f;
    private Node _chargeEffect;
    private bool _isCharging = false;
    private bool _isFullyCharged = false; // Track if we've reached max charge
    private AnimatedSprite2D _anim;
    protected float chargeRatio = 0f;

    // Calculate damage based on charge time
    public float GetChargedDamage(float chargeTime)
    {
        if (chargeTime < MinChargeTime && RequiresMinCharge)
            return 0f; // No damage if under minimum charge
        // Clamp charge time to valid range
        float clampedChargeTime = Mathf.Clamp(chargeTime, MinChargeTime, MaxChargeTime);
        chargeRatio = Mathf.Clamp(clampedChargeTime / MaxChargeTime, 0f, 1f);

        chargeRatio = Math.Clamp(chargeRatio, 0f, 1f);

        float minChargeDamage = MinChargeDamagePercentage * Damage;
        float maxChargeDamage = MaxChargeDamagePercentage * Damage;

        return Mathf.Lerp(minChargeDamage, maxChargeDamage, chargeRatio);
    }

    public void StartCharging(Weapon weapon)
    {
        _isCharging = true;
        _currentChargeTime = 0f;
        _isFullyCharged = false; // Reset fully charged state


        if (weapon._anim != null && !string.IsNullOrEmpty(ChargeAnimation))
        {
            weapon._anim.Play(ChargeAnimation);
        }

        // Spawn charge effect
        if (ChargeEffectScene != null)
        {
            _chargeEffect = ChargeEffectScene.Instantiate();
            weapon.AddChild(_chargeEffect);
        }
    }

    public void UpdateCharge(Weapon weapon, float delta)
    {
        if (!_isCharging) return;

        float previousChargeTime = _currentChargeTime;
        _currentChargeTime += delta;

        // Check if we've reached max charge time
        if (_currentChargeTime >= MaxChargeTime)
        {
            _currentChargeTime = MaxChargeTime;
            
            // Play fully charged animation if we just reached max charge
            if (!_isFullyCharged && previousChargeTime < MaxChargeTime)
            {
                _isFullyCharged = true;
                if (weapon._anim != null && !string.IsNullOrEmpty(FullyChargedAnimation))
                {
                    weapon._anim.Play(FullyChargedAnimation);
                }
            }
        }

        // Update charge effect intensity based on charge level
        UpdateChargeEffect(weapon);
    }

    public bool CanReleaseCharge()
    {
        return _isCharging && (!RequiresMinCharge || _currentChargeTime >= MinChargeTime);
    }

    public override void Execute(Weapon weapon, Vector2 target, bool facingLeft)
    {   
        // GD.Print("[ChargedAttack] Execute called");
        
        if (!_isCharging)
        {
            // GD.PrintErr("[ChargedAttack]Cannot execute charged attack: not currently charging.");
            return;
        }

        // GD.Print($"[ChargedAttack] Charged damage calculated: {chargedDamage}");
        if (_currentChargeTime < MinChargeTime)
        {
            Interrupt(weapon);
            return;
        }

        // GD.Print($"[ChargedAttack] Current charge time: {_currentChargeTime} seconds");
        // Calculate final damage based on charge time
        float chargedDamage = GetChargedDamage(_currentChargeTime);

        // GD.Print($"[ChargedAttack] Executing charged attack with {chargedDamage} damage after {_currentChargeTime} seconds of charging.");
        // Clean up charging effects
        StopCharging(weapon);

        // Execute the actual attack with charged damage
        ExecuteChargedAttack(weapon, target, facingLeft, chargedDamage);

    }

    protected virtual void ExecuteChargedAttack(Weapon weapon, Vector2 target, bool facingLeft, float damage)
    {
        return;
    }

    public override void Interrupt(Weapon weapon)
    {
        // Return to idle animation
        if (weapon._anim != null)
        {
            weapon._anim.Play(IdleAnimation);
        }

        StopCharging(weapon);
        weapon.ResetWeaponState(false);
    }

    private void StopCharging(Weapon weapon)
    {
        _isCharging = false;
        _currentChargeTime = 0f;
        _isFullyCharged = false; // Reset fully charged state

        // Clean up charge effect
        if (_chargeEffect != null)
        {
            _chargeEffect.QueueFree();
            _chargeEffect = null;
        }
    }

    private void UpdateChargeEffect(Weapon weapon)
    {
        // Update visual/audio feedback based on charge level
        // This could modulate particle effects, change colors, play sounds, etc.
        if (_chargeEffect == null) return;

        float chargeRatio = _currentChargeTime / MaxChargeTime;
        // Example: modulate effect based on charge level
        if (_chargeEffect.HasMethod("SetChargeLevel"))
        {
            _chargeEffect.Call("SetChargeLevel", chargeRatio);
        }
    }

    // ---- Getters ----
    public float getCurrentChargeTime() => _currentChargeTime;
    public float getMaxChargeTime() => MaxChargeTime;
}