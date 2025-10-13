using Godot;

public enum WeaponState { Ready, Windup, Active }

public abstract partial class WeaponBase : Node2D
{
	// ---- Node References ----
	public AnimatedSprite2D Sprite { get; protected set; } // Main sprite for the weapon;
	public Area2D HitArea { get; set; } // Hit area for the weapon
	public CollisionPolygon2D HitAreaShape { get; set; } // Hit area shape for the weapon


	// ---- Signals ----
	// Attack lifecycle signals
	[Signal] public delegate void AttackStartedEventHandler(string attackName); // Emitted when an attack starts
	[Signal] public delegate void AttackEndedEventHandler(string attackName); // Emitted when an attack ends
	[Signal] public delegate void EntityHitEventHandler(Node2D entity, float damage, float knockback); // Emitted when an entity is hit
	

	// ---- States ----
	public WeaponState State { get; protected set; } = WeaponState.Ready; // ✅ Private setter, public getter
	// facing direction of the mouse relative to the entity
	public bool FacingLeft { get; protected set; } = false;
	// Attack currently being charged, if any
	protected ChargedAttack _currentChargingAttack;
	public bool IsCharging { get; protected set; } = false;

	public override void _Ready()
	{
		// Initialize node references
		Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		HitArea = GetNodeOrNull<Area2D>("HitArea");
		HitAreaShape = GetNodeOrNull<CollisionPolygon2D>("HitArea/CollisionPolygon2D");

		if (Sprite == null)
		{
			GD.PrintErr($"[WeaponBase] Sprite node not found in weapon: {Name}");
			throw new System.Exception("Sprite node is required for WeaponBase");
		}
		
		if (HitArea == null)
		{
			GD.PrintErr($"[WeaponBase] HitArea node not found in weapon: {Name}");
			throw new System.Exception("HitArea node is required for WeaponBase");
		}

		if (HitAreaShape == null)
		{
			GD.PrintErr($"[WeaponBase] HitAreaShape node not found in weapon: {Name}");
			throw new System.Exception("HitAreaShape node is required for WeaponBase");
		}
		
		// Connect hit area signals
		if (HitArea != null)
		{
			HitArea.Monitoring = false;
			HitArea.BodyEntered += OnBodyEntered;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 direction = GetAimDirection();

		AdjustSpriteRotation(direction);
	}

	public abstract bool CanStartAttack();
	protected abstract System.Threading.Tasks.Task StartAttackSequence();
	public abstract void OpenHitWindow();
	public abstract void CloseHitWindow();
	public abstract void ResetWeaponState();
	protected abstract System.Threading.Tasks.Task AutoInterrupt(float secs);

	protected abstract Vector2 GetAimDirection();

	protected void AdjustSpriteRotation(Vector2 direction)
	{

		FacingLeft = direction.X < 0;

		Sprite.FlipH = FacingLeft;

		if (FacingLeft)
			// Adjust rotation to face left
			Sprite.Rotation = direction.Angle() + Mathf.Pi;
		else
			// Set rotation to face the mouse
			Sprite.Rotation = direction.Angle();
	}

	protected abstract void OnBodyEntered(Node body);
}
