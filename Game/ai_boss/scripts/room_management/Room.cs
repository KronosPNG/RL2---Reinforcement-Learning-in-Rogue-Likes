using System.Linq;
using Godot;

public partial class Room : Node2D, IStateful<RoomState>
{
	[Signal] public delegate void StateChangedEventHandler(string newState);
	[Signal] public delegate void TransitionToSceneEventHandler(string doorId, string targetScenePath);
	// ---- State Machine ----
	public StateMachine<RoomState> StateMachine { get; private set; }
	public RoomState CurrentState => StateMachine.CurrentState;
	public RoomState PreviousState => StateMachine.PreviousState;

	// ---- Spawning points for mobs and pedestals ----
	protected Node2D[] _spawningPoints;
	protected Node2D[] _pedestalPoints;
	protected Node2D[] _doorPoints;
	protected EnemyEntity[] _spawnedMobs;
	[Export] protected PackedScene[] MobScenes; // List of mob scenes to spawn in this room
	[Export] protected PackedScene[] PedestalScenes; // List of pedestal scenes to spawn in this room
	[Export] protected int MobsToSpawn = 3; // Number of mobs to spawn when the room is activated

	// ---- Door management ----
	protected Door[] _doors;

	// ---- Main Scene Node References ----
	protected Node2D sortedSceneElements;

	public override void _Ready()
	{
		sortedSceneElements = GetNodeOrNull<Node2D>("SortSceneElements");
		StateMachine = new StateMachine<RoomState>(this, RoomState.Inactive);
		_spawningPoints =  GetNodeOrNull<Node2D>("SpawningPoints")?.GetChildren().OfType<Node2D>().ToArray() ?? [];
		_pedestalPoints = GetNodeOrNull<Node2D>("PedestalPoints")?.GetChildren().OfType<Node2D>().ToArray() ?? [];
		_doorPoints = GetNodeOrNull<Node2D>("DoorSpawnPoints")?.GetChildren().OfType<Node2D>().ToArray() ?? [];
		_doors = sortedSceneElements?.GetNodeOrNull<Node2D>("Doors")?.GetChildren().OfType<Door>().ToArray() ?? [];
		_spawnedMobs = [];

		GD.Print($"Room {Name} initialized with {MobsToSpawn} mobs to spawn, {_spawningPoints.Length} spawning points, {_pedestalPoints.Length} pedestal points, and {_doors.Length} doors with {_doorPoints.Length} door spawn points.");
		
		foreach (var door in _doors)
		{
			door.DoorEntered += OnDoorEntered;
		}
		
		OnEnterState(CurrentState);
	}

	public void OnEnterState(RoomState newState)
	{
		GD.Print($"Room {Name} entered state: {newState}");

		switch (newState)
		{
			case RoomState.Inactive:
				if (_spawningPoints.Length > 0 && MobsToSpawn > 0)
				{
					TransitionToState(RoomState.Active);
				}
				else
				{
					TransitionToState(RoomState.Cleared);
				}
				break;

			case RoomState.Active:
				CloseDoors();
				SpawnMobs();
				break;

			case RoomState.Cleared:
				OpenDoors();
				SpawnPedestals();
				break;

			default:
				GD.PrintErr($"Unhandled state: {newState}");
				break;
		}
	}

	public void OnExitState(RoomState oldState)
	{
		return;
	}

	protected void TransitionToState(RoomState newState)
	{
		StateMachine.TransitionToState(newState, OnExitState, OnEnterState);
	}

	// Wrapper method for CallDeferred to transition states (CallDeferred requires Variant-compatible types)
	private void DeferredTransitionToState(int stateValue)
	{
		TransitionToState((RoomState)stateValue);
	}

	public void HandleStateTransitions()
	{
		throw new System.NotImplementedException();
	}

	protected void SpawnMobs()
	{
		if(MobScenes == null || MobScenes.Length <= 0){
			TransitionToState(RoomState.Cleared);
			return;
		}

		GD.Print($"Spawning mobs in room {Name}...");

		for (int i = 0; i < MobsToSpawn && i < _spawningPoints.Length; i++)
		{
			// Spawn random mob from the list of mob scenes
			var mobInstance = MobScenes.Length > 0 ? MobScenes[GD.Randi() % MobScenes.Length].Instantiate<EnemyEntity>() : null;

			if (mobInstance == null)
			{
				GD.PrintErr("No mob scenes available to spawn.");
				return;
			}

			// Set the mob's position to the corresponding spawning point and add it to the scene
			mobInstance.GlobalPosition = _spawningPoints[i].GlobalPosition;
			sortedSceneElements.AddChild(mobInstance);
			_spawnedMobs = _spawnedMobs.Append(mobInstance).ToArray();
			mobInstance.Connect(nameof(EnemyEntity.EntityDied), Callable.From(() => OnMobDied(mobInstance)));
		}
	}

	protected void OnMobDied(EnemyEntity mob)
	{
		// Remove the mob from the list of spawned mobs
		_spawnedMobs = _spawnedMobs.Where(m => m != mob).ToArray();
		if (_spawnedMobs.Length == 0)
		{
			// Defer state transition to avoid modifying physics state during query flushing
			CallDeferred(nameof(DeferredTransitionToState), (int)RoomState.Cleared);
		}
	}

	protected virtual void SpawnPedestals()
	{
		return; // By default, rooms do not spawn pedestals. Override this method in specific room types to enable pedestal spawning.
	}

	protected void SpawnPedestalsWithItems(PackedScene[] itemScenes)
	{
		if (PedestalScenes == null || PedestalScenes.Length < 0)
		{
			return;
		}

		for (int i = 0; i < _pedestalPoints.Length; i++)
		{
			var pedestalInstance = PedestalScenes[i % PedestalScenes.Length].Instantiate<Node2D>();

			if (pedestalInstance == null)
			{
				GD.PrintErr("No pedestal scenes available to spawn.");
				return;
			}

			// Set the pedestal's position to the corresponding pedestal point and add it to the scene
			pedestalInstance.GlobalPosition = _pedestalPoints[i].GlobalPosition;
			sortedSceneElements.AddChild(pedestalInstance);

			// Spawn a random item on the pedestal
			if (itemScenes != null && i < itemScenes.Length && pedestalInstance is IPedestal pedestal)
			{
			   pedestal.SetItem(itemScenes[i % itemScenes.Length]);
			}
		}
	}

	protected void OpenDoors()
	{
		GD.Print($"Opening {_doors.Length} doors in room {Name}...");
		foreach (var door in _doors)
		{
			GD.Print($"Opening door: {door.Name}");
			door.TransitionToState(DoorState.Open);
		}
	}

	protected void CloseDoors()
	{
		foreach (var door in _doors)
		{
			door.TransitionToState(DoorState.Closed);
		}
	}

	protected void OnDoorEntered(string doorId, string targetScenePath)
	{
		GD.Print($"Room.OnDoorEntered - Door ID: '{doorId}', TargetPath: '{targetScenePath}'");
		
		if (string.IsNullOrEmpty(targetScenePath))
		{
			GD.PrintErr($"Door {doorId} passed empty scene path!");
			return;
		}

		CallDeferred(MethodName.EmitSignal, SignalName.TransitionToScene, EntranceFromDoorID(doorId), targetScenePath);
	}

	public string EntranceFromDoorID(string doorId)
	{
		return doorId switch
		{
			"up" => "down",
			"down" => "up",
			"left" => "right",
			"right" => "left",
			_ => "center"
		};
	}

}
