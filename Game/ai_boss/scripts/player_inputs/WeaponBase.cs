using Godot;

public enum WeaponState { Ready, Windup, Active }

public abstract partial class WeaponBase : Node2D
{
	// ---- Node References ----
	public AnimatedSprite2D Sprite { get; protected set; } // Main sprite for the weapon;
	public Area2D HitArea { get; set; } // Hit area for the weapon
	public CollisionPolygon2D HitAreaShape { get; set; } // Hit area shape for the weapon
	public IEntity OwnerCharacter { get; protected set; } // The entity that owns this weapon (e.g., player or enemy)

	// ---- Signals ----
	// Attack lifecycle signals
	[Signal] public delegate void AttackStartedEventHandler(string attackName); // Emitted when an attack starts
	[Signal] public delegate void AttackEndedEventHandler(string attackName); // Emitted when an attack ends
	[Signal] public delegate void EntityHitEventHandler(Node2D entity, float damage, float knockback); // Emitted when an entity is hit
	

	// ---- States ----
	public WeaponState State { get; protected set; } = WeaponState.Ready;
	// facing direction of the mouse relative to the entity
	public bool FacingLeft { get; protected set; } = false;
	// Attack currently being charged, if any
	public ChargedAttack CurrentChargingAttack;
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
			HitArea.AreaEntered += OnAreaEntered;
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
	protected void OnAreaEntered(Area2D area)
	{
		if (area == null) return;

		// Try to get the entity that owns this Area2D
		// Owner gives us the scene root (e.g., the Entity that owns this hurtbox)
		Node2D targetEntity = null;
		
		if (area.Owner is Node2D ownerNode)
		{
			targetEntity = ownerNode;
		}
		// Fallback to parent if Owner is null (can happen with runtime-instantiated areas)
		else if (area.GetParent() is Node2D parentNode)
		{
			targetEntity = parentNode;
		}

		if (targetEntity == null)
		{
			// GD.Print($"Weapon: Area {area.Name} has no valid owner or parent to damage");
			return;
		}

		// Reuse the same damage logic as BodyEntered
		OnBodyEntered(targetEntity);
	}
}
