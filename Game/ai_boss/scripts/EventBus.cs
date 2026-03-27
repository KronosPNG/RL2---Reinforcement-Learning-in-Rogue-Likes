using System;

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


    // Scene Transition Event
    public static event Action<string, string> OnSceneTransition; // spawnPointName, target
    
    public static void RaiseSceneTransition(string spawnPointName, string targetScenePath)
    {
        OnSceneTransition?.Invoke(spawnPointName, targetScenePath);
    }
}