using System;
using Godot;

public static class EventBus
{
    // Pause/Resume Events
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;

    public static void RaiseGamePaused()
    {
        OnGamePaused?.Invoke();
    }

    public static void RaiseGameResumed()
    {
        OnGameResumed?.Invoke();
    }

    // Player Events
    public static event Action OnPlayerDied;
    public static event Action<float, float> OnPlayerDamaged; // intensity, duration
    public static event Action<float> OnPlayerHealthChanged; // current health

    public static void RaisePlayerDied()
    {
        OnPlayerDied?.Invoke();
    }

    public static void RaisePlayerDamaged(float intensity, float duration)
    {
        OnPlayerDamaged?.Invoke(intensity, duration);
    }
    
    public static void RaisePlayerHealthChanged(float currentHealth)
    {
        OnPlayerHealthChanged?.Invoke(currentHealth);
    }

    // Weapon Events
    public static event Action<string> OnWeaponAttackStarted; // attackName

    public static event Action<float, float> OnWeaponEquipped; // lightAttackCooldown, heavyAttackCooldown
    
    public static void RaiseWeaponAttackStarted(string attackName)
    {
        GD.Print($"[EventBus] Raising OnWeaponAttackStarted event for '{attackName}' attack.");
        OnWeaponAttackStarted?.Invoke(attackName);
    }

    public static void RaiseWeaponEquipped(float lightAttackCooldown, float heavyAttackCooldown)
    {
        GD.Print($"[EventBus] Raising OnWeaponEquipped event with light attack cooldown: {lightAttackCooldown}s, heavy attack cooldown: {heavyAttackCooldown}s");
        OnWeaponEquipped?.Invoke(lightAttackCooldown, heavyAttackCooldown);
    }

    // Consumable Events
    public static event Action OnConsumableEquipped;
    public static event Action OnConsumableUsed;

    public static void RaiseConsumableEquipped()
    {
        OnConsumableEquipped?.Invoke();
    }
    
    public static void RaiseConsumableUsed()
    {
        OnConsumableUsed?.Invoke();
    }

    // Scene Transition Event
    public static event Action<string, string> OnSceneTransition; // spawnPointName, target
    
    public static void RaiseSceneTransition(string spawnPointName, string targetScenePath)
    {
        OnSceneTransition?.Invoke(spawnPointName, targetScenePath);
    }
}