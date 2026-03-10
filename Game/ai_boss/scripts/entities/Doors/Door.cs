using Godot;
using System;

public partial class Door : Entity<DoorState>, IStateful<DoorState>, IAnimatable<DoorState>
{
	[Signal] public delegate void StateChangedEventHandler(string newState);
	[Signal] public delegate void DoorEnteredEventHandler(string doorId, string targetScenePath);
	// ---- Room Transition Properties ----
	[Export] public string TargetRoomPath; // The scene path to load when the door is entered (use string to avoid circular references)
	[Export] public DoorIDEnum DoorID; // Unique identifier for this door, used for saving/loading and room transitions

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
		if (body.IsInGroup(TargetType) && CurrentState == DoorState.Open)
		{
			if (string.IsNullOrEmpty(TargetRoomPath))
			{
				GD.PrintErr($"Door {Name} has no TargetRoomPath set!");
				return;
			}
			
			GD.Print($"Player entered door {Name} (ID: {DoorID}), transitioning to: {TargetRoomPath}");
			EmitSignal(SignalName.DoorEntered, DoorID.ToString().ToLower(), TargetRoomPath);
		}
	}
}
