using System;
using Godot;

public static class EventBus
{
    // Pause/Resume Events
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;
    public static event Action OnGameExit;

    public static void RaiseGamePaused()
    {
        GD.Print("Game paused");
        OnGamePaused?.Invoke();
    }

    public static void RaiseGameResumed()
    {
        GD.Print("Game resumed");
        OnGameResumed?.Invoke();
    }

    public static void RaiseGameExit()
    {
        OnGameExit?.Invoke();
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
        OnWeaponAttackStarted?.Invoke(attackName);
    }

    public static void RaiseWeaponEquipped(float lightAttackCooldown, float heavyAttackCooldown)
    {
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

    // Boss Room Events
    public static event Action OnBossRoomEntered;
    public static event Action OnBossKilled;

    public static void RaiseBossRoomEnteredEvent()
    {
        OnBossRoomEntered?.Invoke();
    }

    public static void RaiseBossKilledEvent()
    {
        OnBossKilled?.Invoke();
    }
}