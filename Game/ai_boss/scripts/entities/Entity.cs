using Godot;

public abstract partial class Entity : CharacterBody2D
{
    // ---- Signals ----
    [Signal] public delegate void EntityStateChangedEventHandler(string newState);

    // ---- State Machine ----
	protected EntityState _currentState = EntityState.Idle;
	protected EntityState _previousState = EntityState.Idle;

    // ---- Movement properties ----
	[ExportGroup("Movement Properties")]
	[Export] public float BaseSpeed { get; protected set; } = 1000f;
    public Vector2 FacingDirection { get; set; } = Vector2.Right;

    // ---- Timers ----
    protected float _stateTimer = 0f;

    // ---- Node references ----
    protected CollisionShape2D _wallCollision;
	protected Area2D _hitArea;
    protected AnimatedSprite2D _baseSprite;

    // ---- AI Properties ----
	protected Node2D _target;
    public string TargetType { get; set;}
	protected Vector2 _lastKnownTargetPosition = Vector2.Zero;

    // ---- Lifecycle methods ----

    public override void _Ready()
    {
        InitializeNodes();

        if (_baseSprite != null)
			_baseSprite.AnimationFinished += OnAnimationFinished;
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateTimers((float)delta);
		UpdateAI((float)delta);
		HandleStateTransitions((float)delta);
		ApplyMovementByState((float)delta);
		UpdateAnimationIfNeeded();
    }

    protected virtual void InitializeNodes()
    {
        _wallCollision = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
        _baseSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
    }

    protected abstract void UpdateTimers(float delta);
    
    protected abstract void UpdateAI(float delta);
    
    protected abstract void ApplyMovementByState(float delta);

    protected abstract void UpdateAnimationIfNeeded();

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
    protected abstract void OnEnterState(EntityState state);

    protected abstract void OnExitState(EntityState state);

    protected virtual void TransitionToState(EntityState newState)
	{
		if (_currentState == newState) return;

		OnExitState(_currentState);
		_previousState = _currentState;
		_currentState = newState;
		_stateTimer = 0f;
		OnEnterState(newState);

		EmitSignal(SignalName.EntityStateChanged, newState.ToString());
	}

    protected abstract void HandleStateTransitions(float delta);

    // ---- Animation methods ----
    protected abstract void OnAnimationFinished();

	public bool PlayAnimation(string animationName)
	{
		if (_baseSprite == null) return false;

		if (_baseSprite.SpriteFrames.HasAnimation(animationName))
		{
			_baseSprite.Play(animationName);
			return true;
		}

		return false;
	}

    // ---- Public Getters for AI customization ----
    public float StateTimer => _stateTimer;
    public EntityState CurrentState => _currentState;
	public Node2D Target => _target;

}