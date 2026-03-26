using Godot;

public partial class MainHandler : Node
{
    private Node2D _roomManager;

    public override void _Ready()
    {
        _roomManager = GetNode<Node2D>("RoomManager");

        // Subscribe to EventBus events

        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;

        EventBus.OnPlayerDied += HandlePlayerDied;
    }

    private void HandleGamePaused()
    {
        DesaturateScene(_roomManager);
        _roomManager.GetTree().Paused = true;
    }

    private void HandleGameResumed()
    {
        ResetSaturation(_roomManager);
        _roomManager.GetTree().Paused = false;
    }

    private void HandlePlayerDied()
    {
        // Defer scene tree modifications to avoid modifying state during physics query flush
        CallDeferred(MethodName.ProcessPlayerDeath);
        
        DesaturateScene(_roomManager);
        _roomManager.GetTree().Paused = true;
    }

    private void ProcessPlayerDeath()
    {
        // Set player as directly under MainHandler to prevent pause and desaturation
        var player = GetTree().GetNodesInGroup("Player")[0] as Node2D;
        player.GetParent().RemoveChild(player);
        AddChild(player);
        player.ProcessMode = ProcessModeEnum.Always; // Ensure player continues processing to show death animation
    }

    private void DesaturateScene(Node2D node)
    {
        node.Modulate = new Color(0.5f, 0.5f, 0.5f); // Simple desaturation by reducing color intensity
    }

    private void ResetSaturation(Node2D node)
    {
        node.Modulate = new Color(1f, 1f, 1f); // Reset to original colors
    }
}