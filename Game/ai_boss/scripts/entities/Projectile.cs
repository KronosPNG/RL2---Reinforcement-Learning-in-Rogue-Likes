using Godot;
using System.Collections.Generic;

public partial class Projectile : Entity
{
	[Signal] public delegate void ProjectileHitEventHandler(Node2D target, float damage);
	[Signal] public delegate void ProjectileExpiredEventHandler();

	// Properties
	public float Damage { get; private set; }
	public Node2D ProjectileOwner { get; private set; }
	public float Knockback { get; private set; }
	
	// Internal state
	private float _lifetime;
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
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

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
		float lifetime,
		Node2D owner,
		bool destroyOnHit = true,
		bool destroyOnWallHit = true)
	{
		GlobalPosition = startPosition;
		FacingDirection = direction.Normalized();
		BaseSpeed = speed;
		Damage = damage;
		Knockback = knockback;
		_lifetime = lifetime;
		ProjectileOwner = owner;
		DestroyOnHit = destroyOnHit;
		DestroyOnWallHit = destroyOnWallHit;

		// Set initial rotation
		if (FacingDirection != Vector2.Zero)
		{
			Rotation = FacingDirection.Angle();
		}

		_sprite.Play("default");

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
	if (body.HasMethod("ApplyDamage"))
	{
		// GD.Print($"Applying damage to {body.Name}");
		body.Call("ApplyDamage", Damage, ProjectileOwner, Knockback);
	}

	// GD.Print($"Projectile hit {body.Name} for {Damage} damage.");

		// Destroy projectile on hit if allowed
	if (DestroyOnHit)
	{
		// GD.Print("Destroying projectile on hit.");
		DestroyProjectile();
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
			DestroyProjectile();
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

	private void ExpireProjectile()
	{
		EmitSignal(nameof(ProjectileExpired));
		DestroyProjectile();
	}

	private void DestroyProjectile()
	{
		// Could add destruction effects here (particles, sound, etc.)
		QueueFree();
	}

    protected override void UpdateTimers(float delta)
    {
        // Handle lifetime
		_lifetime -= (float)delta;
		if (_lifetime <= 0f)
		{
			ExpireProjectile();
		}
    }

	protected override void UpdateAI(float delta)
    {
        throw new System.NotImplementedException();
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
    }

    protected override void UpdateAnimationIfNeeded()
    {
        return;
    }

    protected override void OnEnterState(EntityState state)
    {
		switch (state)
		{
			case EntityState.Dying:
				/*
					FINISCI LGOGICA DI VITA PROIETTILI
					CAPISCI STRUTTURA PER HOMING, ECC

				*/
		}
    }

    protected override void OnExitState(EntityState state)
    {
        throw new System.NotImplementedException();
    }

    protected override void HandleStateTransitions(float delta)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnAnimationFinished()
    {
        throw new System.NotImplementedException();
    }
}
