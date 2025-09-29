using Godot;
using System.Collections.Generic;

public partial class Projectile : RigidBody2D
{
	[Signal] public delegate void ProjectileHitEventHandler(Node2D target, float damage);
	[Signal] public delegate void ProjectileExpiredEventHandler();

	// Properties
	public float Damage { get; private set; }
	public Node2D ProjectileOwner { get; private set; }
	public float Knockback { get; private set; }
	
	// Internal state
	private float _lifetime;
	private float _speed;
	private Vector2 _direction;
	private HashSet<Node> _alreadyHit = new HashSet<Node>();
	public bool DestroyOnHit = true;
	public bool DestroyOnWallHit = true;
	
	// Node references
	private Area2D _hitArea;
	private CollisionShape2D _hitShape; // wall collision
	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		// Get node references
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_hitShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

		if (_hitArea != null)
		{
			GD.Print("Projectile: HitArea found");
			_hitArea.BodyEntered += OnBodyEntered;
			_hitArea.AreaEntered += OnAreaEntered;
		}

		// Set up physics
		GravityScale = 0f; // No gravity for projectiles
		LockRotation = true; // Prevent spinning
		ContactMonitor = true;
		MaxContactsReported = 10;
		
		// Connect collision signal for wall/obstacle hits
		BodyEntered += OnCollisionBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Handle lifetime
		_lifetime -= (float)delta;
		if (_lifetime <= 0f)
		{
			ExpireProjectile();
		}

		// Rotate sprite to face movement direction (optional)
		if (_direction != Vector2.Zero)
		{
			Rotation = _direction.Angle();
		}
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
		_direction = direction.Normalized();
		_speed = speed;
		Damage = damage;
		Knockback = knockback;
		_lifetime = lifetime;
		ProjectileOwner = owner;
		DestroyOnHit = destroyOnHit;
		DestroyOnWallHit = destroyOnWallHit;

		// Set initial rotation
		if (_direction != Vector2.Zero)
		{
			Rotation = _direction.Angle();
		}

		_sprite.Play("default");

		// Move the projectile
		LinearVelocity = _direction * _speed;
	}

	private void OnBodyEntered(Node body)
	{
		GD.Print($"Projectile hit detected on body: {body.Name}");
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
			GD.Print($"Applying damage to {body.Name}");
			body.Call("ApplyDamage", Damage, this, Knockback);
		}
		else if (body.HasMethod("TakeDamage"))
		{
			body.Call("TakeDamage", Damage, this);
		}

		GD.Print($"Projectile hit {body.Name} for {Damage} damage.");

		// Destroy projectile on hit if allowed
		if (DestroyOnHit)
		{
			GD.Print("Destroying projectile on hit.");
			DestroyProjectile();
		}
			
	}

	private void OnAreaEntered(Area2D area)
	{
		GD.Print($"Projectile hit detected on area: {area.Name}");
		Node body = area.GetParent();

		OnBodyEntered(body);
	}

	private void OnCollisionBodyEntered(Node body)
	{
		if (body == ProjectileOwner) return; // Don't hit the owner4

		GD.Print($"Projectile collision detected with body: {body.Name}");
		
		if (DestroyOnWallHit)
		{
			DestroyProjectile();
		}
		else
		{
			// stop the projectile on collision with walls/obstacles
			LinearVelocity = Vector2.Zero;
			_hitArea.Monitoring = false; // Disable further hit detection
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
}
