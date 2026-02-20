using Godot;
using System.Collections.Generic;

public partial class Projectile : Entity
{
	[Signal] public delegate void ProjectileHitEventHandler(Node2D target, float damage);
	[Signal] public delegate void ProjectileDestroyedEventHandler();

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

	// Projectile behaviour
	[ExportCategory("Projectile Properties")]
	[Export] public AggroBehaviour Behaviour;

	public override void _Ready()
	{
		// Get node references
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_wallCollision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_baseSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
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

		_baseSprite.Play("default");

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
			TransitionToState(EntityState.Hit);
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
			TransitionToState(EntityState.Hit);
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
			TransitionToState(EntityState.Dying);
		}

		// Handle lifetime
		_lifetime -= (float)delta;
		if (_lifetime <= 0f)
		{
			TransitionToState(EntityState.Dead);
		}

		_previousPosition = this.GlobalPosition;
	}

	protected override void UpdateAI(float delta)
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

		switch (_currentState)
		{
			case EntityState.Aggro:
				if (Behaviour == null) return;
				
				Velocity = Behaviour.GetChaseVelocity(this, (float)delta);
				Behaviour.PerformAggroBehaviour(this);
				break;

			default:
				break;
		}
	}

	protected override void UpdateAnimationIfNeeded()
	{
		return;
	}

	protected override void OnEnterState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Hit:
				SetPhysicsProcess(false);
				if(!PlayAnimation("hit"))
				{
					GD.PrintErr("Projectile: 'hit' animation not found, skipping to destroy.");
					TransitionToState(EntityState.Dead);
				}
				break;

			case EntityState.Dying:
				if(!PlayAnimation("dying"))
				{
					GD.PrintErr("Projectile: 'dying' animation not found, skipping to destroy.");
					TransitionToState(EntityState.Dead);
				}
				break;

			case EntityState.Dead:
				DestroyProjectile();
				break;

			default:
				break;
		}
	}

	protected override void OnExitState(EntityState state)
	{
		return;
	}

	protected override void HandleStateTransitions(float delta)
	{

		if (Behaviour == null) return;

		switch (_currentState)
		{
			case EntityState.Idle:
				if (Behaviour == null) return;

				if(Behaviour.CanSeeTarget(this))
				{
					TransitionToState(EntityState.Aggro);
				}
				break;

			case EntityState.Aggro:
				if (Behaviour.ShouldLoseTarget(this))
				{
					TransitionToState(EntityState.Idle);
				}
				break;
		}
	}

	protected override void OnAnimationFinished()
	{
		string animName = _baseSprite.Animation;

		switch (animName)
		{
			case "hit":
				TransitionToState(EntityState.Dead);
				break;

			case "dying":
				TransitionToState(EntityState.Dead);
				break;

			default:
				break;
		}
	}
}
