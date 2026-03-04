using Godot;
using System.Collections.Generic;

public partial class Projectile : Entity<ProjectileState>, IStateful<ProjectileState>
{
	[Signal] public delegate void ProjectileHitEventHandler(Node2D target, float damage);
	[Signal] public delegate void ProjectileDestroyedEventHandler();
	[Signal] public delegate void StateChangedEventHandler(string newState);

	// Properties
	public float Damage { get; private set; }
	public Node2D ProjectileOwner { get; private set; }
	public float Knockback { get; private set; }
	
	// Internal state
	private float _range;
	private float _remainingRange;
	private float _lifetime;
	private Vector2 _startingPosition;
	private Vector2 _previousPosition; // For range calculation
	private HashSet<Node> _alreadyHit = new HashSet<Node>();
	public bool DestroyOnHit = true;
	public bool DestroyOnWallHit = true;

	// ---- Projectile behaviour ----
	[ExportCategory("Projectile Properties")]
	[Export] public AggroBehaviour Behaviour;

	// ---- Animation properties ----
	EntityVisualController<ProjectileState> VisualController;

	public override void _Ready()
	{
		StateMachine = new StateMachine<ProjectileState>(this, ProjectileState.Idle);
		VisualController = GetNodeOrNull<EntityVisualController<ProjectileState>>("EntityVisualController");
		
		// Get node references
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_physicalCollision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		TargetType = (ProjectileOwner as EnemyEntity)?.TargetType ?? "Enemy";

		if (_hitArea != null)
		{
			// GD.Print("Projectile: HitArea found");
			_hitArea.BodyEntered += OnBodyEntered;
			_hitArea.AreaEntered += OnAreaEntered;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
	}

	public void Initialize(
		Vector2 startPosition,
		Vector2 direction,
		float speed,
		float damage,
		float knockback,
		float range,
		Node2D owner,
		bool destroyOnHit = true,
		bool destroyOnWallHit = true)
	{
		GlobalPosition = startPosition;
		_startingPosition = startPosition;
		_previousPosition = startPosition;
		FacingDirection = direction.Normalized();
		BaseSpeed = speed;
		Damage = damage;
		Knockback = knockback;
		_range = range;
		_remainingRange = range;
		ProjectileOwner = owner;
		DestroyOnHit = destroyOnHit;
		DestroyOnWallHit = destroyOnWallHit;

		// Max lifetime of 30 seconds 
		_lifetime = 15f; // Default max lifetime

		// Set initial rotation
		if (FacingDirection != Vector2.Zero)
		{
			Rotation = FacingDirection.Angle();
		}

		VisualController.PlayState(ProjectileState.Idle);

		// Set the projectile velocity
		Velocity = FacingDirection * BaseSpeed;
	}

	private void OnBodyEntered(Node body)
	{
		// GD.Print($"Projectile hit detected on body: {body.Name}");

		// Handle hits with physics bodies (enemies, destructibles, etc.)
		if (body == ProjectileOwner) return; // Don't hit the owner
		if (_alreadyHit.Contains(body)) return;

		_alreadyHit.Add(body);

		// Emit hit signal
		if (body is Node2D node2d)
		{
			EmitSignal(nameof(ProjectileHit), node2d, Damage);
		}

		// Try to apply damage
		if (body is IDamageable damageable)
		{
			// GD.Print($"Applying damage to {body.Name}");
			damageable.ApplyDamage(Damage, ProjectileOwner, Knockback);
		}

		// GD.Print($"Projectile hit {body.Name} for {Damage} damage.");

		// Destroy projectile on hit if allowed
		if (DestroyOnHit)
		{
			// GD.Print("Destroying projectile on hit.");
			TransitionToState(ProjectileState.Hit);
		}
			
	}

	private void OnAreaEntered(Area2D area)
	{
		// GD.Print($"Projectile hit detected on area: {area.Name}");
		Node body = area.GetParent();

		OnBodyEntered(body);
	}

	private void OnCollisionBodyEntered(GodotObject collider)
	{
		if (collider == ProjectileOwner) return; // Don't hit the owner

		// GD.Print($"Projectile collision detected with body: {(collider as Node)?.Name}");
		
		if (DestroyOnWallHit)
		{
			TransitionToState(ProjectileState.Fading);
		}
		else
		{
			// stop the projectile on collision with walls/obstacles
			Velocity = Vector2.Zero;

			if (_hitArea != null)
			{
				_hitArea.Monitoring = false; // Disable further hit detection
			}
		}
		
	}

	private void DestroyProjectile()
	{
		EmitSignal(nameof(ProjectileDestroyed));
		QueueFree();
	}

	protected override void UpdateTimers(float delta)
	{
		// Handle range 
		_remainingRange -= _previousPosition.DistanceTo(this.GlobalPosition);

		if (_remainingRange <= 0f)
		{
			GD.Print("Projectile reached max range, destroying.");
			TransitionToState(ProjectileState.Fading);
		}

		// Handle lifetime
		_lifetime -= (float)delta;
		if (_lifetime <= 0f)
		{
			TransitionToState(ProjectileState.Destroyed);
		}

		_previousPosition = this.GlobalPosition;
	}

	protected override void UpdateAI(float delta)
	{
		return;
	}

	protected override void UpdateFacing()
	{
		return;
	}

	protected override void ApplyMovementByState(float delta)
	{
		// Move and check for collisions with walls/obstacles
		var collision = MoveAndCollide(Velocity * (float)delta);
		if (collision != null)
		{
			OnCollisionBodyEntered(collision.GetCollider());
		}

		// Rotate sprite to face movement direction (optional)
		if (FacingDirection != Vector2.Zero)
		{
			Rotation = FacingDirection.Angle();
		}

		switch (CurrentState)
		{
			case ProjectileState.Active:
				if (Behaviour == null) return;
				
				Velocity = Behaviour.GetChaseVelocity(this, (float)delta);
				Behaviour.PerformAggroBehaviour(this);
				break;

			default:
				break;
		}
	}

	public override void UpdateAnimationIfNeeded()
	{
		return;
	}

	public override void OnEnterState(ProjectileState state)
	{
		switch (state)
		{
			case ProjectileState.Hit:
				SetPhysicsProcess(false);
				VisualController.PlayState(ProjectileState.Hit);
				break;

			case ProjectileState.Fading:
				VisualController.PlayState(ProjectileState.Fading);
				break;

			case ProjectileState.Destroyed:
				DestroyProjectile();
				break;

			default:
				break;
		}
	}

	public override void OnExitState(ProjectileState state)
	{
		return;
	}

	public override void HandleStateTransitions()
	{

		if (Behaviour == null) return;

		switch (CurrentState)
		{
			case ProjectileState.Idle:
				if (Behaviour == null) return;

				if(Behaviour.CanSeeTarget(this))
				{
					TransitionToState(ProjectileState.Active);
				}
				break;

			case ProjectileState.Active:
				if (Behaviour.ShouldLoseTarget(this))
				{
					TransitionToState(ProjectileState.Idle);
				}
				break;
		}
	}

	public void OnAnimationFinished()
	{
		string animName = VisualController.GetCurrentAnimation();

		switch (animName)
		{
			case "hit":
				TransitionToState(ProjectileState.Destroyed);
				break;

			case "fading":
				TransitionToState(ProjectileState.Destroyed);
				break;

			default:
				break;
		}
	}
}
