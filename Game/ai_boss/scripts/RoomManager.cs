using Godot;
using System.Collections.Generic;

public partial class RoomManager : Node2D
{
	private Dictionary<string, Room> _visitedRooms = [];

	[Export] private PackedScene _firstRoom;
	private PlayerController _player;
	private Room _currentRoom;
	private CameraController _camera;
	private bool _isTransitioning = false;

	public override void _Ready()
	{
		LoadInitialRoom(_firstRoom);

		EventBus.OnGamePaused += HandleGamePaused;
		EventBus.OnGameResumed += HandleGameResumed;
		EventBus.OnPlayerDied += HandleGamePaused;
	}

	private void LoadInitialRoom(PackedScene roomScene)
	{
		_currentRoom = roomScene.Instantiate<Room>();
		AddChild(_currentRoom);

		EventBus.OnSceneTransition += OnTransitionToScene;

		_camera = GetNode<CameraController>("Camera2D");

		// Load player and spawn in the center of the first room
		var _beanScene = ResourceLoader.Load<PackedScene>("res://scenes/bean.tscn");
		_player = _beanScene.Instantiate<PlayerController>();

		// Spawn into sorted elements to ensure correct layering with doors and mobs
		var sortedElements = _currentRoom.GetNodeOrNull<Node2D>("SortSceneElements");
		sortedElements.AddChild(_player);
		_player.Position = Vector2.Zero; // Center of the room

		// Wait for Bean to be ready, then spawn sword in hand and no armor
		PackedScene weaponScene = ResourceLoader.Load<PackedScene>("res://scenes/weapons/sword.tscn");
		PackedScene armorScene = ResourceLoader.Load<PackedScene>("res://scenes/armors/shirt.tscn");

		_player.CallDeferred("EquipWeapon", weaponScene);
		_player.CallDeferred("EquipArmor", armorScene);

		_camera.SetTarget(_player);
		EventBus.OnPlayerDamagedEffect += _camera.Shake;
	}

	private void OnTransitionToScene(string spawnPointName, string targetScenePath)
	{
		// Prevent double transitions
		if (_isTransitioning)
		{
			// GD.Print("Already transitioning, ignoring door trigger");
			return;
		}
		
		// GD.Print($"RoomManager.OnTransitionToScene - SpawnPoint: '{spawnPointName}', TargetPath: '{targetScenePath}'");
		
		if (string.IsNullOrEmpty(targetScenePath))
		{
			GD.PrintErr("RoomManager received empty target scene path!");
			return;
		}
		
		_isTransitioning = true;
		// Pass spawn point NAME, not position - we'll calculate position from NEW room
		CallDeferred(MethodName.TransitionToRoom, targetScenePath, spawnPointName);
	}

	private void TransitionToRoom(string targetScenePath, string spawnPointName)
	{
		// Remove player from current room
		_player.GetParent().RemoveChild(_player);

		// Deactivate current room
		string currentRoomPath = _currentRoom.SceneFilePath;
		DisableRoom(_currentRoom);
		_visitedRooms[currentRoomPath] = _currentRoom;

		if (_currentRoom.JustCleared)
		{
			_currentRoom.ClearMobs();
		}

		if (_visitedRooms.ContainsKey(targetScenePath))
		{
			_currentRoom = _visitedRooms[targetScenePath];
			
		}
		else
		{
			// Load new room and add to tree
			var targetRoom = ResourceLoader.Load<PackedScene>(targetScenePath);
			_currentRoom = targetRoom.Instantiate<Room>();
			
			AddChild(_currentRoom);
		}

		EnableRoom(_currentRoom);

		// Get spawn point from NEW room
		var spawnNode = FindRoomSpawnPoint(_currentRoom, spawnPointName);
		Vector2 spawnPoint = spawnNode?.GlobalPosition ?? Vector2.Zero;

		// Set position BEFORE adding to scene tree to avoid triggering door collisions
		_player.Position = spawnPoint;
		
		var sortedElements = _currentRoom.GetNodeOrNull<Node2D>("SortSceneElements");
		sortedElements.AddChild(_player);
		
		// Re-enable door transitions after a brief delay
		GetTree().CreateTimer(0.3).Timeout += () => _isTransitioning = false;
	}

	private Node2D FindRoomSpawnPoint(Room room, string spawnPointName)
	{
		var groupNodes = GetTree().GetNodesInGroup("DoorSpawnPoint");

		foreach (var node in groupNodes)
		{
			if (node is Node2D spawnNode && room.IsAncestorOf(spawnNode) && spawnNode.Name == spawnPointName)
			{
				return spawnNode;
			}
		}

		return null;
	}

	private void EnableRoom(Room room)
	{
		room.Visible = true;
		room.ProcessMode = ProcessModeEnum.Inherit;
		room.AddToGroup("CurrentRoom");

		Callable.From(() => EnableRoomNavigation(_currentRoom)).CallDeferred();
		EnableRoomCollision(room);

	}

	private void DisableRoom(Room room)
	{
		room.Visible = false;
		room.ProcessMode = ProcessModeEnum.Disabled;
		room.RemoveFromGroup("CurrentRoom");
		
		DisableRoomNavigation(room);
		DisableRoomCollision(room);
	}

	private void DisableRoomCollision(Room room)
	{
		// Disable tilemap physics so inactive rooms stop producing collisions.
		var tileMaps = room.FindChildren("*", "TileMapLayer", recursive: true, owned: false);
		foreach (var node in tileMaps)
		{
			if (node is TileMapLayer layer)
			{
				layer.CollisionEnabled = false;
			}
		}

		var colliders = room.FindChildren("*", "CollisionShape2D", recursive: true, owned: false);
		foreach (var node in colliders)
		{
			if (node is CollisionShape2D collider)
			{
				collider.Disabled = true;
			}
		}
	}

	private void EnableRoomCollision(Room room)
	{
		var tileMaps = room.FindChildren("*", "TileMapLayer", recursive: true, owned: false);
		foreach (var node in tileMaps)
		{
			if (node is TileMapLayer layer)
			{
				layer.CollisionEnabled = true;
			}
		}

		var colliders = room.FindChildren("*", "CollisionShape2D", recursive: true, owned: false);
		foreach (var node in colliders)
		{
			if (node is CollisionShape2D collider)
			{
				collider.Disabled = false;
			}
		}
	}

	private void DisableRoomNavigation(Room room)
	{
		var regions = room.FindChildren("*", "NavigationRegion2D", recursive: true, owned: false);
		foreach (var node in regions)
		{
			if (node is NavigationRegion2D region)
			{
				region.Enabled = false;
			}
		}
	}

	private void EnableRoomNavigation(Room room)
	{
		var regions = room.FindChildren("*", "NavigationRegion2D", recursive: true, owned: false);
		foreach (var node in regions)
		{
			if (node is NavigationRegion2D region)
			{
				region.Enabled = true;
			}
		}
	}

	public override void _ExitTree()
	{
		EventBus.OnSceneTransition -= OnTransitionToScene;
	}

	// Pause Logic

	private void HandleGamePaused()
	{
		Modulate = new Color(0.5f, 0.5f, 0.5f);
	}

	private void HandleGameResumed()
	{
		Modulate = new Color(1f, 1f, 1f);
	}
}
