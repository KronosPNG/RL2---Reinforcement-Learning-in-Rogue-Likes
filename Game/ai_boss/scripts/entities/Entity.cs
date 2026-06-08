using Godot;
using System;

public abstract partial class Entity<TState> : CharacterBody2D, IEntity, IStateful<TState> where TState : Enum
{
	// ---- Signals ----
	[Signal] public delegate void EntityStateChangedEventHandler(string newState);

	// ---- State Machine ----
	public StateMachine<TState> StateMachine{ get; protected set; }

	// ---- Movement properties ----
	[ExportGroup("Movement Properties")]
	[Export] public float BaseSpeed { get; set; } = 0f;
	public Vector2 FacingDirection { get; set; } = Vector2.Right;

	// ---- Node references ----
	protected CollisionShape2D _physicalCollision;
	protected Area2D _hitArea;

	// ---- AI Properties ----
	[ExportGroup("AI Properties")]
	protected Node2D _target;
	[Export]public string TargetType { get; set;}
	protected Vector2 _lastKnownTargetPosition = Vector2.Zero;

	// ---- Lifecycle methods ----

	public override void _Ready()
	{
		InitializeNodes();
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateTimers((float)delta);
		UpdateAI((float)delta);
		UpdateFacing();
		HandleStateTransitions();
		ApplyMovementByState((float)delta);
	}

	protected virtual void InitializeNodes()
	{
		_physicalCollision = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		
		StateMachine = new StateMachine<TState>(this, default);
	}

	protected abstract void UpdateTimers(float delta);
	
	protected abstract void UpdateAI(float delta);

	protected abstract void UpdateFacing();
	
	protected abstract void ApplyMovementByState(float delta);

	protected virtual Node2D FindTarget()
	{
		var targets = GetTree().GetNodesInGroup(TargetType);
		Node2D closestTarget = null;
		float closestDistance = float.MaxValue;
		
		foreach (Node node in targets)
		{
			if (node is Node2D enemy && enemy != this)
			{
				float distance = GlobalPosition.DistanceTo(enemy.GlobalPosition);
				if (distance < closestDistance)
				{
					closestDistance = distance;
					closestTarget = enemy;
				}
			}
		}
		
		return closestTarget;
	}

	// ---- State Transition Logic ----
	public abstract void OnEnterState(TState state);

	public abstract void OnExitState(TState state);

	public virtual void TransitionToState(TState newState)
	{
		StateMachine.TransitionToState(newState, OnExitState, OnEnterState);
	}

	public abstract void HandleStateTransitions();

	// ---- Public Getters for AI customization ----
	public float StateTimer
	{
		get => StateMachine.StateTimer;
		set => StateMachine.StateTimer = value;
	}
	public TState CurrentState => StateMachine.CurrentState;
	public TState PreviousState
	{
		get => StateMachine.PreviousState;
		set => StateMachine.PreviousState = value;
	}

	public Node2D Target => _target;

}
