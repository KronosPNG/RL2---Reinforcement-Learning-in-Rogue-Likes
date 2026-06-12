using System;
using Godot;

public static class EventBus
{
    // --- Pause/Resume Events ---
    public static event Action OnGamePaused;
    public static event Action OnGameResumed;
    public static event Action OnGameExit;
    public static event Action OnGameRestarted;

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

    public static void RaiseGameRestarted()
    {
        OnGameRestarted?.Invoke();
    }

    // --- Player Events ---
    public static event Action OnPlayerDied;
    public static event Action<float, float> OnPlayerDamagedEffect; // intensity, duration
    public static event Action<float> OnPlayerHealthChanged; // current health
    public static event Action<Vector2> OnPlayerDodged; // dodge direction
    public static event Action<Vector2> OnPlayerDirectionChanged; // movement direction
    public static event Action<float> OnPlayerHealed; // amount healed
    public static event Action<float> OnPlayerDamaged; // amlunt damaged
    public static event Action<PlayableCharacter> OnPlayerSpawned; // player reference

    public static void RaisePlayerDied()
    {
        OnPlayerDied?.Invoke();
    }

    public static void RaisePlayerHealthChanged(float currentHealth)
    {
        OnPlayerHealthChanged?.Invoke(currentHealth);
    }

    public static void RaisePlayerDodged(Vector2 dodgeDirection)
    {
        OnPlayerDodged?.Invoke(dodgeDirection);
    }

    public static void RaisePlayerDirectionChanged(Vector2 movementDirection)
    {
        OnPlayerDirectionChanged?.Invoke(movementDirection);
    }

    public static void RaisePlayerHealed(float amount)
    {
        OnPlayerHealed?.Invoke(amount);
    }

    public static void RaisePlayerDamaged(float amount)
    {
        OnPlayerDamaged?.Invoke(amount);
    }

    public static void RaisePlayerSpawned(PlayableCharacter player)
    {
        OnPlayerSpawned?.Invoke(player);
    }

    // For graphical effect
    public static void RaisePlayerDamagedEffect(float intensity, float duration)
    {
        OnPlayerDamagedEffect?.Invoke(intensity, duration);
    }

    // --- Weapon Events ---
    public static event Action<string> OnWeaponAttackStarted; // attackName
    public static event Action<Weapon> OnWeaponEquipped; // weapon
    public static event Action<string> OnWeaponCharging; // attackName
    public static event Action<string> OnWeaponChargingCancelled; // attackName

    public static void RaiseWeaponAttackStarted(string attackName)
    {
        OnWeaponAttackStarted?.Invoke(attackName);
    }

    public static void RaiseWeaponEquipped(Weapon weapon)
    {
        OnWeaponEquipped?.Invoke(weapon);
    }

    public static void RaiseWeaponCharging(string attackName)
    {
        OnWeaponCharging?.Invoke(attackName);
    }

    public static void RaiseWeaponChargingCancelled(string attackName)
    {
        OnWeaponChargingCancelled?.Invoke(attackName);
    }

    // --- Consumable Events ---
    public static event Action<Consumable> OnConsumableEquipped;
    public static event Action OnConsumableUsed;
    public static event Action OnConsumableCharging;
    public static event Action OnConsumableCancelled;

    public static void RaiseConsumableEquipped(Consumable consumable)
    {
        OnConsumableEquipped?.Invoke(consumable);
    }
    
    public static void RaiseConsumableUsed()
    {
        OnConsumableUsed?.Invoke();
    }

    public static void RaiseConsumableCharging()
    {
        OnConsumableCharging?.Invoke();
    }

    public static void RaiseConsumableCancelled()
    {
        OnConsumableCancelled?.Invoke();
    }

    // --- Armor Events ---
    public static event Action<Armor> OnArmorEquipped;
    
    public static void RaiseArmorEquipped(Armor armor)
    {
        OnArmorEquipped?.Invoke(armor);
    }

    // --- Scene Transition Event ---
    public static event Action<string, string> OnSceneTransition; // spawnPointName, target
    
    public static void RaiseSceneTransition(string spawnPointName, string targetScenePath)
    {
        OnSceneTransition?.Invoke(spawnPointName, targetScenePath);
    }

    // --- Room Events ---
    public static event Action<NavigationRegion2D> OnRoomEnteredDimensions; // room reference

    public static void RaiseRoomEnteredDimensions(NavigationRegion2D navRegion)
    {
        OnRoomEnteredDimensions?.Invoke(navRegion);
    }

    // --- Boss Room Events ---
    public static event Action OnBossRoomEntered;
    public static event Action OnBossKilled;
    public static event Action<BossRL> OnBossSpawned; // Boss reference for any final setup
    public static event Action<float> OnBossDamaged;

    public static void RaiseBossRoomEnteredEvent()
    {
        OnBossRoomEntered?.Invoke();
    }

    public static void RaiseBossKilledEvent()
    {
        OnBossKilled?.Invoke();
    }

    public static void RaiseBossSpawnedEvent(BossRL boss)
    {
        OnBossSpawned?.Invoke(boss);
    }

    public static void RaiseBossDamaged(float amount)
    {
        OnBossDamaged?.Invoke(amount);
    }
}