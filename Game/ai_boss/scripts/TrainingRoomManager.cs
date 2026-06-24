using Godot;

/// <summary>
/// Simplified room manager for training mode.
/// 
/// Only handles:
/// - Loading the boss room
/// - Spawning PlayerMimic at designated spawn point
/// - Basic room state tracking
/// 
/// Does NOT handle:
/// - Door logic
/// - Room transitions/progression
/// - Pedestal spawning
/// - Other game flow features
/// </summary>
public partial class TrainingRoomManager : Node
{
	private const string BossRoomScene = "res://scenes/rooms/boss_room.tscn";
	private Node2D _currentRoom;
	private Vector2 _playerSpawnPoint = Vector2.Zero;

	public override void _Ready()
	{
		LoadBossRoom();
	}

	/// <summary>
	/// Load the boss room scene and set it as current.
	/// </summary>
	private void LoadBossRoom()
	{
		var scene = GD.Load<PackedScene>(BossRoomScene);
		_currentRoom = scene.Instantiate<Node2D>();
		AddChild(_currentRoom);
		GD.Print($"[TRAINING] Boss room loaded");
	}

	/// <summary>
	/// Spawn PlayerMimic at the designated spawn point.
	/// </summary>
	public Node2D SpawnPlayerMimic()
	{
		if (_currentRoom == null)
		{
			GD.PrintErr("[TRAINING] Cannot spawn PlayerMimic: no room loaded");
			return null;
		}

		try
		{
			const string PlayerMimicScene = "res://scenes/entities/player_mimic.tscn";
			var scene = GD.Load<PackedScene>(PlayerMimicScene);
			var playerMimic = scene.Instantiate<Node2D>();

			_currentRoom.AddChild(playerMimic);
			playerMimic.GlobalPosition = _playerSpawnPoint;

			GD.Print($"[TRAINING] PlayerMimic spawned at {playerMimic.GlobalPosition}");
			return playerMimic;
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"[TRAINING] Failed to spawn PlayerMimic: {e.Message}");
			return null;
		}
	}

	/// <summary>
	/// Get current room.
	/// </summary>
	public Node2D GetCurrentRoom()
	{
		return _currentRoom;
	}

	/// <summary>
	/// Reload the room (useful for episode restart).
	/// </summary>
	public void ReloadRoom()
	{
		if (_currentRoom != null)
		{
			_currentRoom.QueueFree();
			_currentRoom = null;
		}
		LoadBossRoom();
		GD.Print("[TRAINING] Room reloaded");
	}
}
