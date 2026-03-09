using Godot;

public partial class Door : Entity<DoorState>, IStateful<DoorState>, IAnimatable<DoorState>
{
	[Signal] public delegate void StateChangedEventHandler(string newState);

	// ---- Room Transition Properties ----
	[Export] public PackedScene TargetRoomScene; // The scene to load when the door is entered
	
	// ---- Animation properties ----
	protected DoorVisualController VisualController;

	public override void _Ready()
	{	
		base._Ready();

		VisualController = GetNodeOrNull<DoorVisualController>("VisualController");

		if(_hitArea != null)
		{
			_hitArea.BodyEntered += OnBodyEntered;
		}

		VisualController?.AnimationFinished += OnAnimationFinished;

		TargetType = "Player"; // Doors should only react to the player
	}

	public override void _PhysicsProcess(double delta)
	{
		return; // Doors do not have physics processing
	}

	public override void HandleStateTransitions()
	{
		return;
	}

	public override void OnEnterState(DoorState state)
	{
		bool enableInteraction = state == DoorState.Open;
		bool enableCollision = state == DoorState.Closed;

		switch (state)
		{
			case DoorState.Closed:
			case DoorState.Open:
				break;
			default:
				GD.PrintErr($"Unhandled state: {state}");
				return;
		}

		VisualController.PlayState(state);
		_physicalCollision.SetDeferred("disabled", !enableCollision);

		_hitArea.SetDeferred("monitoring", enableInteraction);
		_hitArea.SetDeferred("monitorable", enableInteraction);
	}

	public override void OnExitState(DoorState state)
	{
		return; // No special logic needed on state exit for doors
	}

	protected override void ApplyMovementByState(float delta)
	{
		return; // Doors do not move
	}

	protected override void UpdateAI(float delta)
	{
		return; // Doors do not have AI
	}

	protected override void UpdateFacing()
	{
		return; // Doors do not change facing
	}

	protected override void UpdateTimers(float delta)
	{
		return; // Doors do not have timers
	}

	public void UpdateAnimationIfNeeded()
	{
		return;
	}

	public void OnAnimationFinished()
	{
		return;
	}

	protected void OnBodyEntered(Node body)
	{
		if (body.IsInGroup(TargetType))
		{
			GD.Print($"Player entered door {Name}, transitioning to target room...");
			EmitSignal(nameof(StateChanged), "Entered");
			// Additional logic for room transition can be added here
		}
	}
}
