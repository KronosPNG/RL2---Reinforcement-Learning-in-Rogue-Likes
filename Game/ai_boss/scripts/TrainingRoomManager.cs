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
public partial class TrainingRoomManager : Node2D
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
		_currentRoom.AddToGroup("CurrentRoom");
		GD.Print($"[TRAINING] Boss room loaded");
	}

	/// <summary>
	/// Instantiate a PlayerMimic WITHOUT adding it to the scene tree (_Ready won't run yet).
	/// Configure behaviors and equipment scenes on the returned instance, then call
	/// AddPlayerMimicToRoom to trigger AddChild and fire _Ready with everything set.
	/// </summary>
	public PlayerMimic InstantiatePlayerMimic()
	{
		if (_currentRoom == null)
		{
			GD.PrintErr("[TRAINING] Cannot instantiate PlayerMimic: no room loaded");
			return null;
		}

		try
		{
			const string PlayerMimicScene = "res://scenes/entities/player_mimic.tscn";
			var scene = GD.Load<PackedScene>(PlayerMimicScene);
			return scene.Instantiate<PlayerMimic>();
		}
		catch (System.Exception e)
		{
			GD.PrintErr($"[TRAINING] Failed to instantiate PlayerMimic: {e.Message}");
			return null;
		}
	}

	/// <summary>
	/// Add a pre-configured PlayerMimic to the room. This triggers AddChild and fires _Ready.
	/// </summary>
	public void AddPlayerMimicToRoom(PlayerMimic playerMimic)
	{
		if (_currentRoom == null || playerMimic == null) return;
		_currentRoom.AddChild(playerMimic);
		playerMimic.GlobalPosition = _playerSpawnPoint;
		GD.Print($"[TRAINING] PlayerMimic added to room at {playerMimic.GlobalPosition}");
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
			_currentRoom.RemoveFromGroup("CurrentRoom");
			_currentRoom.QueueFree();
			_currentRoom = null;
		}
		LoadBossRoom();
		GD.Print("[TRAINING] Room reloaded");
	}
}
