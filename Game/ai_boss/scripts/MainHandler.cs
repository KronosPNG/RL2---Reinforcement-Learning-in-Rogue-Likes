using Godot;

public partial class MainHandler : Node
{
    public override void _Ready()
    {
        // Subscribe to EventBus events

        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;
        EventBus.OnPlayerDied += HandlePlayerDied;
        EventBus.OnGameExit += () => GetTree().Quit();
    }

    private void HandleGamePaused()
    {
        GetTree().Paused = true;
    }

    private void HandleGameResumed()
    {
        GetTree().Paused = false;
    }

    private void HandlePlayerDied()
    {
        // Defer scene tree modifications to avoid modifying state during physics query flush
        CallDeferred(MethodName.ProcessPlayerDeath);
        
        
    }

    private void ProcessPlayerDeath()
    {
        // Set player as directly under MainHandler to prevent pause and desaturation
        var player = GetTree().GetNodesInGroup("Player")[0] as Node2D;
        player.GetParent().RemoveChild(player);
        AddChild(player);
        player.ProcessMode = ProcessModeEnum.Always; // Ensure player continues processing to show death animation
    }
}