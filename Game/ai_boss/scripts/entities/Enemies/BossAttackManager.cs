using System.Threading.Tasks;
using Godot;

public partial class BossAttackManager : WeaponBase
{
	public new BossRL OwnerCharacter { get; protected set; }

	public BossRL.BossAttackType CurrentAttack { get; set; }

	// ---- Attack Configuration ----
	[ExportGroup("Attack Configurations")]
	[Export] public AttackBase MeleeAttack1 { get; set; }
	[Export] public AttackBase MeleeAttack2 { get; set; }
	[Export] public AttackBase MagicAttack1 { get; set; }
	[Export] public AttackBase MagicAttack2 { get; set; }

	// ---- Attack Timers ----
	public Timer MeleeAttack1CooldownTimer { get; private set; }
	public Timer MeleeAttack2CooldownTimer { get; private set; }
	public Timer MagicAttack1CooldownTimer { get; private set; }
	public Timer MagicAttack2CooldownTimer { get; private set; } 

	
	private bool _playerAlreadyHit = false;

	public override void _Ready()
	{
		OwnerCharacter = GetParent<BossRL>();
		base.OwnerCharacter = OwnerCharacter;

		// Override base class node initialization to match Entity weapon structure
		// In entity weapons, the HitArea is called "AttackArea" and uses CollisionPolygon2D
		HitArea = GetNodeOrNull<Area2D>("AttackArea");
		HitAreaShape = GetNodeOrNull<CollisionPolygon2D>("AttackArea/CollisionPolygon2D");
		Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

		if (HitArea == null)
			GD.PrintErr($"[BossAttackManager] AttackArea node not found in weapon");

		if (HitAreaShape == null)
			GD.PrintErr($"[BossAttackManager] CollisionPolygon2D not found in weapon/AttackArea");

		if (Sprite == null)
			GD.PrintErr($"[BossAttackManager] Sprite node not found in weapon");

		// Connect hit area signals
		if (HitArea != null)
		{
			HitArea.Monitoring = false;
			HitArea.BodyEntered += OnBodyEntered;
			HitArea.AreaEntered += OnAreaEntered;
		}

		// Initialize cooldown timers
		MeleeAttack1CooldownTimer = GetNodeOrNull<Timer>("Timers/MeleeAttack1CooldownTimer");
		MeleeAttack1CooldownTimer.WaitTime = MeleeAttack1.Cooldown;

		MeleeAttack2CooldownTimer = GetNodeOrNull<Timer>("Timers/MeleeAttack2CooldownTimer");
		MeleeAttack2CooldownTimer.WaitTime = MeleeAttack2.Cooldown;

		MagicAttack1CooldownTimer = GetNodeOrNull<Timer>("Timers/MagicAttack1CooldownTimer");
		MagicAttack1CooldownTimer.WaitTime = MagicAttack1.Cooldown;

		MagicAttack2CooldownTimer = GetNodeOrNull<Timer>("Timers/MagicAttack2CooldownTimer");
		MagicAttack2CooldownTimer.WaitTime = MagicAttack2.Cooldown;
	}

	public override bool CanStartAttack()
	{
		// Check if any attack is currently active
		if (State != WeaponState.Ready)
			return false;

		// Check cooldowns based on the current attack type
		return CurrentAttack switch
		{
			BossRL.BossAttackType.Melee1 => MeleeAttack1CooldownTimer.IsStopped(),
			BossRL.BossAttackType.Melee2 => MeleeAttack2CooldownTimer.IsStopped(),
			BossRL.BossAttackType.Magic1 => MagicAttack1CooldownTimer.IsStopped(),
			BossRL.BossAttackType.Magic2 => MagicAttack2CooldownTimer.IsStopped(),
			_ => false,
		};
	}

	public void Attack()
	{
		_ = StartAttackSequence();
	}

	protected override async Task StartAttackSequence()
	{
		ResetCurrentAttackCooldown();

		State = WeaponState.Windup;

		float windup = CurrentAttack switch
		{
			BossRL.BossAttackType.Melee1 => MeleeAttack1.Windup,
			BossRL.BossAttackType.Melee2 => MeleeAttack2.Windup,
			BossRL.BossAttackType.Magic1 => MagicAttack1.Windup,
			BossRL.BossAttackType.Magic2 => MagicAttack2.Windup,
			_ => 0f,
		};

		await ToSignal(GetTree().CreateTimer(windup), "timeout");

		if (State == WeaponState.Windup)
		{
			OpenHitWindow();
		}
	}

	public override void OpenHitWindow()
	{
		if (State == WeaponState.Active)
			return;

		State = WeaponState.Active;
		_playerAlreadyHit = false;

		// Attack Execute() methods expect a world-space position as target.
		// Melee direction is already snapped to cardinal; project it to a world position.
		// Magic attacks pass the direction directly (works when boss is near origin).
		Vector2 meleeTarget = OwnerCharacter.GlobalPosition + GetAimDirection() * 1000f;

		switch (CurrentAttack)
		{
			case BossRL.BossAttackType.Melee1:
				MeleeAttack1.Execute(this, meleeTarget, FacingLeft);
				break;

			case BossRL.BossAttackType.Melee2:
				MeleeAttack2.Execute(this, meleeTarget, FacingLeft);
				break;

			case BossRL.BossAttackType.Magic1:
				MagicAttack1.Execute(this, GetAimDirection(), FacingLeft);
				break;

			case BossRL.BossAttackType.Magic2:
				MagicAttack2.Execute(this, GetAimDirection(), FacingLeft);
				break;
		}

		float activeDuration = CurrentAttack switch
		{
			BossRL.BossAttackType.Melee1 => MeleeAttack1.Active,
			BossRL.BossAttackType.Melee2 => MeleeAttack2.Active,
			BossRL.BossAttackType.Magic1 => MagicAttack1.Active,
			BossRL.BossAttackType.Magic2 => MagicAttack2.Active,
			_ => 0f,
		};

		_ = AutoInterrupt(activeDuration);
	}

	public override void CloseHitWindow()
	{
		ResetWeaponState();
	}

	public override void ResetWeaponState()
	{
		State = WeaponState.Ready;
		_playerAlreadyHit = false;
	}

	protected async override Task AutoInterrupt(float secs)
	{
		await ToSignal(GetTree().CreateTimer(secs), "timeout");

		if (State == WeaponState.Active)
		{
			var attack = GetAttackFromType(CurrentAttack);
			attack.Interrupt(this);
		}
	}

	protected override Vector2 GetAimDirection()
	{
		if (OwnerCharacter == null)
		{
			GD.PrintErr("BossAttackManager: GetAimDirection() - OwnerCharacter is null!");
			return Vector2.Right; // Default direction
		}

		Vector2 direction = OwnerCharacter.AttackDirection;
		direction = direction.Normalized();

		switch(CurrentAttack)
		{
			case BossRL.BossAttackType.Melee1:
			case BossRL.BossAttackType.Melee2:
				// Snap to cardinal directions
				if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
					return new Vector2(Mathf.Sign(direction.X), 0);
				else
					return new Vector2(0, Mathf.Sign(direction.Y));

			case BossRL.BossAttackType.Magic1:
			case BossRL.BossAttackType.Magic2:
				return direction;
			default:
				return Vector2.Right;
		}
	}

	protected override void OnBodyEntered(Node body)
	{
		if (body == null) return;
		if(_playerAlreadyHit) return;

		_playerAlreadyHit = true;

		var attack = GetAttackFromType(CurrentAttack);

		float damage = attack.Damage;
		float knockback = attack.Knockback;

		if (body is IDamageable damageable)
		{
			damageable.ApplyDamage(damage, OwnerCharacter, knockback);
		}
	}

	// --- Helper Methods ---

	private void ResetCurrentAttackCooldown()
	{
		switch (CurrentAttack)
		{
			case BossRL.BossAttackType.Melee1:
				MeleeAttack1CooldownTimer.Start();
				break;
			case BossRL.BossAttackType.Melee2:
				MeleeAttack2CooldownTimer.Start();
				break;
			case BossRL.BossAttackType.Magic1:
				MagicAttack1CooldownTimer.Start();
				break;
			case BossRL.BossAttackType.Magic2:
				MagicAttack2CooldownTimer.Start();
				break;
		}
	}

	private AttackBase GetAttackFromType(BossRL.BossAttackType type)
	{
		return type switch
		{
			BossRL.BossAttackType.Melee1 => MeleeAttack1,
			BossRL.BossAttackType.Melee2 => MeleeAttack2,
			BossRL.BossAttackType.Magic1 => MagicAttack1,
			BossRL.BossAttackType.Magic2 => MagicAttack2,
			_ => null,
		};
	}
}
