using System;
using Godot;

public partial class WeaponEntity : WeaponBase
{
	public Entity OwnerCharacter { get; protected set; }
	[Export]public bool AllowFriendlyFire { get; set; } = false;
	
	//---- Attack Configuration ----
	[Export] public AttackBase AttackConfig { get; set; }

	// ---- Timers ----
	protected float _attackCooldownTimer;
	protected float _activeTimer = 0f;
	protected bool _isAttackActive = false;

	public override void _Ready()
	{
		OwnerCharacter = GetParent<Entity>();
		
		// Override base class node initialization to match Entity weapon structure
		// In entity weapons, the HitArea is called "AttackArea" and uses CollisionPolygon2D
		HitArea = GetNodeOrNull<Area2D>("AttackArea");
		HitAreaShape = GetNodeOrNull<CollisionPolygon2D>("AttackArea/CollisionPolygon2D");
		Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		
		if (HitArea == null)
			GD.PrintErr($"[WeaponEntity] AttackArea node not found in weapon");
			
		if (HitAreaShape == null)
			GD.PrintErr($"[WeaponEntity] CollisionPolygon2D not found in weapon/AttackArea");

		if (Sprite == null)
			GD.PrintErr($"[WeaponEntity] Sprite node not found in weapon");
			
		// Connect hit area signals
		if (HitArea != null)
		{
			HitArea.Monitoring = false;
			HitArea.BodyEntered += OnBodyEntered;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (_attackCooldownTimer > 0)
			_attackCooldownTimer = Math.Max(0, _attackCooldownTimer - (float)delta);
		
		// Handle active attack window
		if (_isAttackActive)
		{
			_activeTimer -= (float)delta;
			if (_activeTimer <= 0)
			{
				// GD.Print("WeaponEntity: Active window expired, closing");
				_isAttackActive = false;
				AttackConfig.Interrupt(this);
			}
		}
	}

	public void Attack()
	{
		GD.Print($"WeaponEntity: Attack() called - CanStartAttack: {CanStartAttack()}, State: {State}, Cooldown: {_attackCooldownTimer}");
		if (!CanStartAttack())
		{
			GD.Print($"WeaponEntity: Attack() blocked - State: {State}, Cooldown: {_attackCooldownTimer}");
			return;
		}

		StartAttack();
	}

	protected override System.Threading.Tasks.Task StartAttackSequence()
	{
		// Not used - keeping for base class compatibility
		return System.Threading.Tasks.Task.CompletedTask;
	}

	private void StartAttack()
	{
		_attackCooldownTimer = AttackConfig.Cooldown;
		GD.Print("WeaponEntity: StartAttack() called");
		
		// Start the attack immediately (windup is handled by Entity animations)
		OpenHitWindow();
		
		// Set up timer for active window
		_activeTimer = AttackConfig.Active;
		_isAttackActive = true;
		// GD.Print($"WeaponEntity: Active window started, duration = {_activeTimer}");
	}

	public override void OpenHitWindow()
	{
		if (State == WeaponState.Active)
			return;

		GD.Print("WeaponEntity: OpenHitWindow() called");

		State = WeaponState.Active;
		bool facingLeft = FacingLeft;

		AttackConfig.Execute(this, OwnerCharacter.Target.GlobalPosition, facingLeft);
	}

	public override void CloseHitWindow()
	{
		GD.Print("WeaponEntity: CloseHitWindow() called");
		ResetWeaponState();
	}

	public override void ResetWeaponState()
	{
		GD.Print("WeaponEntity: ResetWeaponState() called");
		State = WeaponState.Ready;
		_isAttackActive = false;
		_activeTimer = 0f;
		EmitSignal(nameof(AttackEnded), "EntityAttack");
	}

	protected override System.Threading.Tasks.Task AutoInterrupt(float secs)
	{
		// Not used - timer handled in _PhysicsProcess
		return System.Threading.Tasks.Task.CompletedTask;
	}

	public override bool CanStartAttack()
	{
		if (State != WeaponState.Ready)
			return false;

		if (_attackCooldownTimer > 0)
			return false;

		return true;
	}

	protected override Vector2 GetAimDirection()
	{
		if (OwnerCharacter?.Target == null || !IsInstanceValid(OwnerCharacter.Target))
		{
			GD.PrintErr("WeaponEntity: GetAimDirection() - No valid target!");
			return Vector2.Right; // Default direction
		}
		
		Vector2 direction = OwnerCharacter.Target.GlobalPosition - GlobalPosition;
		direction = direction.Normalized();
		return direction;
	}

	protected override void OnBodyEntered(Node body)
	{
		if (body == null) return;
		
		if (body is PlayerController player)
		{
			if (body.HasMethod("ApplyDamage")) body.Call("ApplyDamage", AttackConfig.Damage, this.OwnerCharacter, AttackConfig.Knockback);
		}

		else if (body is Entity entity)
		{
			if (entity == OwnerCharacter) return; // Prevent self-hits
			if (!AllowFriendlyFire) return; // Prevent friendly fire

			if (body.HasMethod("ApplyDamage")) body.Call("ApplyDamage", AttackConfig.Damage, this.OwnerCharacter, AttackConfig.Knockback);
		}
	}
}
