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

	public override void _Ready()
	{
		base._Ready();
		OwnerCharacter = GetParent<Entity>();
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);

		if (_attackCooldownTimer > 0)
			_attackCooldownTimer = Math.Max(0, _attackCooldownTimer - (float)delta);
	}

	public void Attack()
	{
		if (!CanStartAttack())
			return;

		_ = StartAttackSequence();
	}

	protected override async System.Threading.Tasks.Task StartAttackSequence()
	{
		State = WeaponState.Windup;  
		EmitSignal(nameof(AttackStarted), "EntityAttack");

		float windup = AttackConfig.Windup;
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
		bool facingLeft = FacingLeft;

		// AttackConfig.Execute(this, OwnerCharacter.Target.GlobalPosition, facingLeft);
		GD.Print($"[WeaponEntity] Executing attack towards target at {OwnerCharacter.Target.GlobalPosition}, facingLeft={facingLeft}");

		float activeTime = AttackConfig.Active;
		_ = AutoInterrupt(activeTime);
	}

	public override void CloseHitWindow()
	{
		ResetWeaponState();
	}

	public override void ResetWeaponState()
	{
		State = WeaponState.Ready;
		EmitSignal(nameof(AttackEnded), "EntityAttack");

	}

	protected override async System.Threading.Tasks.Task AutoInterrupt(float secs)
	{
		await ToSignal(GetTree().CreateTimer(secs), "timeout");
		// AttackConfig.Interrupt(this);

		GD.Print($"[WeaponEntity] Interrupting attack");
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
