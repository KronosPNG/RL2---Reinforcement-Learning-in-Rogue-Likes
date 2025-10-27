using Godot;
using System;
using System.Collections.Generic;

public partial class Weapon : WeaponBase, IPedestalItem
{
	public PlayerController OwnerCharacter { get; protected set; }
	// Signals for equipping/unequipping
	[Signal] public delegate void EquippedEventHandler();
	[Signal] public delegate void UnequippedEventHandler();

	// Signals for charging
	[Signal] public delegate void ChargeStartedEventHandler(string attackName);
	[Signal] public delegate void ChargeUpdatedEventHandler(float chargeLevel);
	[Signal] public delegate void ChargeReleasedEventHandler(string attackName);
	[Signal] public delegate void ChargeCancelledEventHandler(string attackName);

	// ---- Non Mechanical Properties ----
	[Export] public string ItemName { get; set; } = "Unnamed Weapon";
	[Export] public string Description { get; set; } = "No Description";

	//---- Pedestal Display ----
	public AnimatedSprite2D PedestalDisplaySprite { get; set; } // Optional separate sprite for pedestal display
	[Export] public Vector2 PedestalDisplayScale { get; set; } = new Vector2(7f, 7f); // Scale to use for pedestal display

	//---- Attack Configuration ----
	[Export] public AttackBase LightAttackConfig { get; set; }
	[Export] public AttackBase HeavyAttackConfig { get; set; }
	
	//---- Autoswing Configuration ----
	[Export] public bool EnableLightAutoswing { get; set; } = false;
	[Export] public bool EnableHeavyAutoswing { get; set; } = false;

	// ----- States -----
	// Track which attack is currently in progress (used by OpenHitWindow and CloseHitWindow)
	public bool IsCurrentAttackHeavy { get; private set; } = false;
	// Stores aim when the attack button was pressed (keeps damage decoupled from mouse movement during animation)
	public Vector2 PendingHitTarget { get; private set; } = Vector2.Zero;
	// Track already hit bodies during the current Active window
	protected HashSet<Node> _alreadyHit = new HashSet<Node>();

	// ---- Timers ----
	protected float _lightCooldownTimer = 0f; // Cooldown timer for light attacks
	protected float _heavyCooldownTimer = 0f; // Cooldown timer for heavy attacks

	// ---- Damage Application Settings ----
	// If false, only the signal will be emitted and other systems should subscribe.
	[Export] public bool AutoApplyDamage = true;
	[Export] public string EnemyDamageMethodName = "ApplyDamage"; // name of method to call on enemies (if AutoApplyDamage)


	public override void _Ready()
	{
		base._Ready();

		PedestalDisplaySprite = GetNodeOrNull<AnimatedSprite2D>("PedestalDisplaySprite");

		if (PedestalDisplaySprite == null)
		{
			GD.PrintErr("Weapon: could not find PedestalDisplaySprite");
			return;
		}
		else
		{
			// Hide pedestal display sprite by default (weapons start equipped)
			PedestalDisplaySprite.Visible = false;
		}
		
		// Disable physics processing until equipped (prevents null reference when OwnerCharacter isn't set yet)
		SetPhysicsProcess(false);
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 direction = GetAimDirection();
		
		if(State != WeaponState.Active)
		{
			AdjustSpriteRotation(direction);
		}

		if (_lightCooldownTimer > 0)
			_lightCooldownTimer = Math.Max(0, _lightCooldownTimer - (float)delta);

		if (_heavyCooldownTimer > 0)
			_heavyCooldownTimer = Math.Max(0, _heavyCooldownTimer - (float)delta);

		// Update charge state
		if (IsCharging)
		{
			UpdateCharge((float)delta);
		}
	}

	public virtual void Equip(Node2D owner)
	{
		// Logic for equipping the weapon
		OwnerCharacter = owner as PlayerController;
		// Enable physics processing now that we have a valid owner
		SetPhysicsProcess(true);
		EmitSignal(nameof(Equipped));
	}

	public virtual void Unequip()
	{
		// Logic for unequipping the weapon
		// Disable physics processing when unequipped
		SetPhysicsProcess(false);
		OwnerCharacter = null;
		EmitSignal(nameof(Unequipped));
	}

	// -------------------------
	// Public attack API 
	// They set up aim, play animation and set state to windup.
	// -------------------------

	public void AttackLight(Vector2 mouseGlobalPos)
	{
		Attack(mouseGlobalPos, false);
	}

	public void AttackHeavy(Vector2 mouseGlobalPos)
	{
		Attack(mouseGlobalPos, true);
	}

	// Internal attack method
	private void Attack(Vector2 mouseGlobalPos, bool isHeavy)
	{
		// GD.Print($"Attack called: isHeavy={isHeavy}, weapon state={_state}, lightCooldown={_lightCooldownTimer}, heavyCooldown={_heavyCooldownTimer}");

		if (!CanStartAttack(isHeavy))
		{
			// GD.Print($"Attack blocked by CanStartAttack: weapon state={_state}, lightCooldown={_lightCooldownTimer}, heavyCooldown={_heavyCooldownTimer}");
			return;
		}

		PendingHitTarget = mouseGlobalPos; // Store the target position for the attack
		IsCurrentAttackHeavy = isHeavy;
		_ = StartAttackSequence(isHeavy);
	}

	// Check if the attack can be started
	// By checking the current state and cooldown timers
	public override bool CanStartAttack()
	{
		return CanStartAttack(IsCurrentAttackHeavy);
	}
	public bool CanStartAttack(bool isHeavy)
	{
		// GD.Print($"CanStartAttack check: isHeavy={isHeavy}, state={_state}, lightCooldown={_lightCooldownTimer}, heavyCooldown={_heavyCooldownTimer}");

		if (State != WeaponState.Ready)
		{
			// GD.Print($"Attack blocked: weapon state is {_state}, not Ready");
			return false;
		}
		if (!isHeavy && _lightCooldownTimer > 0f)
		{
			// GD.Print($"Light attack blocked: cooldown timer {_lightCooldownTimer}");
			return false;
		}
		if (isHeavy && _heavyCooldownTimer > 0f)
		{
			// GD.Print($"Heavy attack blocked: cooldown timer {_heavyCooldownTimer}");
			return false;
		}

		// GD.Print("Attack allowed");
		return true;
	}

	// Master sequence control (windup -> rely on animation call -> idle)
	protected override System.Threading.Tasks.Task StartAttackSequence()
	{
		return StartAttackSequence(IsCurrentAttackHeavy);
	}
	protected async System.Threading.Tasks.Task StartAttackSequence(bool isHeavyAttack)
	{
		// set cooldown immediately so player can't spam
		if (!isHeavyAttack) _lightCooldownTimer = LightAttackConfig.Cooldown;
		else _heavyCooldownTimer = HeavyAttackConfig.Cooldown;

		State = WeaponState.Windup;
		EmitSignal(nameof(AttackStarted), isHeavyAttack ? "heavy" : "light");

		// Play corresponding animation on the weapon's AnimationPlayer (animations must exist)
		if (Sprite != null)
		{
			if (isHeavyAttack)
				Sprite.Play("heavy_attack");
			else if (!isHeavyAttack)
				Sprite.Play("light_attack");
		}

		// Fallback: if animation doesn't call OpenHitWindow, we open it after windup time.
		float windup = isHeavyAttack ? HeavyAttackConfig.Windup : LightAttackConfig.Windup;
		await ToSignal(GetTree().CreateTimer(windup), "timeout");

		// If animation already opened the window and changed state, don't forcibly open again.
		if (State == WeaponState.Windup)
		{
			OpenHitWindow(isHeavyAttack); // string needed for AnimationPlayer compatibility (call from code too)
		}
	}

	// ---- Charging Logic ----
	public bool HasChargeableAttack(bool isHeavy)
	{
		var attackType = isHeavy ? HeavyAttackConfig : LightAttackConfig;
		return attackType is IChargeable;
	}

	// Start charging an attack
	public void StartCharge(Vector2 mouseGlobalPos, bool isHeavy)
	{
		if (State != WeaponState.Ready) return;

		var config = isHeavy ? HeavyAttackConfig : LightAttackConfig;
		if (config is not ChargedAttack chargedAttack) return;

		PendingHitTarget = mouseGlobalPos;
		IsCurrentAttackHeavy = isHeavy;
		_currentChargingAttack = chargedAttack;
		IsCharging = true;
		State = WeaponState.Windup; // Use Windup state for charging

		// Start the charging process
		chargedAttack.StartCharging(this);
		EmitSignal(nameof(ChargeStarted), isHeavy ? "heavy" : "light");
	}

	// Update the charging process (called from player controller)
	public void UpdateCharge(float delta)
	{
		if (!IsCharging || _currentChargingAttack == null) return;

		_currentChargingAttack.UpdateCharge(this, delta);

		// Emit charge level updates for UI/feedback
		float chargeLevel = _currentChargingAttack.getCurrentChargeTime() / _currentChargingAttack.getMaxChargeTime();
		EmitSignal(nameof(ChargeUpdated), chargeLevel);
	}

	// Check if the charge can be released
	public bool CanReleaseCharge()
	{
		return IsCharging && _currentChargingAttack?.CanReleaseCharge() == true;
	}

	// Execute the charged attack
	public void ExecuteChargedLight(Vector2 mouseGlobalPos)
	{
		if (!IsCharging || IsCurrentAttackHeavy)
		{
			// GD.Print("Charge not released!");
			return;
		}

		ExecuteChargedAttack(mouseGlobalPos);
	}

	public void ExecuteChargedHeavy(Vector2 mouseGlobalPos)
	{
		if (!IsCharging || !IsCurrentAttackHeavy) return;
		ExecuteChargedAttack(mouseGlobalPos);
	}

	private void ExecuteChargedAttack(Vector2 mouseGlobalPos)
	{
		if (_currentChargingAttack == null)
		{
			// GD.PrintErr("No current charging attack to execute!");
			return;
		}

		PendingHitTarget = mouseGlobalPos; // Update target in case mouse moved

		// Set cooldown
		if (IsCurrentAttackHeavy)
			_heavyCooldownTimer = HeavyAttackConfig.Cooldown;
		else
			_lightCooldownTimer = LightAttackConfig.Cooldown;

		// Emit signals
		EmitSignal(nameof(AttackStarted), IsCurrentAttackHeavy ? "heavy" : "light");
		EmitSignal(nameof(ChargeReleased), IsCurrentAttackHeavy ? "heavy" : "light");

		// Execute the charged attack
		bool facingAtStart = FacingLeft;

		State = WeaponState.Active;
		// GD.Print($"[Weapon]Executing charged attack: isHeavy={_isCurrentAttackHeavy}, target={_pendingHitTarget}, facingLeft={facingAtStart}");
		_currentChargingAttack.Execute(this, PendingHitTarget, facingAtStart);

		// Clean up charging state
		IsCharging = false;
		_currentChargingAttack = null;

		// The attack execution will handle state transitions
	}

	// Cancel the current charge
	public void CancelCharge()
	{
		if (!IsCharging) return;

		string attackName = IsCurrentAttackHeavy ? "heavy" : "light";

		if (_currentChargingAttack != null)
		{
			_currentChargingAttack.Interrupt(this);
		}

		EmitSignal(nameof(ChargeCancelled), attackName);

		IsCharging = false;
		_currentChargingAttack = null;
		State = WeaponState.Ready;
	}

	// -------------------------
	// AnimationPlayer Call Method targets
	// - OpenHitWindow(true/false)  // call on the exact frame the weapon visually hits
	// - CloseHitWindow(true/false) // optional: call at the end of active frames to stop hits early
	// The code will also proceed with fallback timings if these are not called.
	// -------------------------

	// Start the hit window
	public override void OpenHitWindow()
	{
		OpenHitWindow(IsCurrentAttackHeavy);
	}
	public void OpenHitWindow(bool isHeavy)
	{
		if (State == WeaponState.Active)
			return; // already open

		State = WeaponState.Active;
		IsCurrentAttackHeavy = isHeavy;
		_alreadyHit.Clear(); // Clear the list of already hit targets if it wasn't already cleared

		bool facingAtStart = FacingLeft;

		if (isHeavy)
		{
			if (HeavyAttackConfig != null)
				HeavyAttackConfig.Execute(this, PendingHitTarget, facingAtStart);
		}
		else
		{
			if (LightAttackConfig != null)
				LightAttackConfig.Execute(this, PendingHitTarget, facingAtStart);
		}

		// schedule end of active window if animation didn't call CloseHitWindow
		float activeDuration = isHeavy ? HeavyAttackConfig.Active : LightAttackConfig.Active;
		_ = AutoInterrupt(activeDuration, isHeavy);
	}

	// Auto-close the hit window after a delay
	protected override System.Threading.Tasks.Task AutoInterrupt(float secs)
	{
		return AutoInterrupt(secs, IsCurrentAttackHeavy);
	}
	protected async System.Threading.Tasks.Task AutoInterrupt(float secs, bool isHeavy)
	{
		await ToSignal(GetTree().CreateTimer(secs), "timeout");
		// Only close if still active for this attack kind
		if (State == WeaponState.Active && IsCurrentAttackHeavy == isHeavy)
		{
			if (isHeavy)
				HeavyAttackConfig.Interrupt(this);
			else
				LightAttackConfig.Interrupt(this);
		}
	}

	public override void CloseHitWindow()
	{
		CloseHitWindow(IsCurrentAttackHeavy);
	}

	public void CloseHitWindow(bool isHeavy)
	{
		// GD.Print($"WeaponBase.CloseHitWindow called: isHeavy={isHeavy}");
		ResetWeaponState(isHeavy);
	}

	// Reset the weapon state and hit detection
	public override void ResetWeaponState()
	{
		ResetWeaponState(IsCurrentAttackHeavy);
	}
	public void ResetWeaponState(bool isHeavy)
	{
		// GD.Print($"WeaponBase.ResetWeaponState called: isHeavy={isHeavy}, current state={_state}");
		State = WeaponState.Ready;
		PendingHitTarget = Vector2.Zero;

		// Reset list of already hit targets 
		_alreadyHit.Clear();

		// GD.Print($"About to emit AttackEnded signal for {(isHeavy ? "heavy" : "light")} attack");
		EmitSignal(nameof(AttackEnded), isHeavy ? "heavy" : "light");
		// GD.Print($"AttackEnded signal emitted, weapon state is now {_state}");
	}

	// -------------------------
	// Collision handling
	// -------------------------
	protected override void OnBodyEntered(Node body)
	{

		GD.Print($"Weapon hit detected on body: {body.Name}");
		// Called when a physics body enters the hit area while Monitoring=true.
		if (body == null) return;
		if (_alreadyHit.Contains(body)) return;

		_alreadyHit.Add(body);

		GD.Print($"Weapon registering hit on body: {body.Name}");

		float damage = IsCurrentAttackHeavy ? HeavyAttackConfig.Damage : LightAttackConfig.Damage;
		float knockback = IsCurrentAttackHeavy ? HeavyAttackConfig.Knockback : LightAttackConfig.Knockback;

		// Emit signal so other systems (ui, sfx, particles) can respond
		if (body is Node2D node2d)
			EmitSignal(nameof(EntityHit), node2d, damage, knockback);

		// Optionally auto-apply damage directly.
		if (AutoApplyDamage)
		{
			GD.Print($"Weapon auto-applying {damage} damage with {knockback} knockback to {body.Name}");

			// Attempt to call configured method
			if (body.HasMethod(EnemyDamageMethodName))
			{
				body.Call(EnemyDamageMethodName, damage, this.OwnerCharacter, knockback);
			}
			else
			{
				// fallback: try common names
				if (body.HasMethod("ApplyDamage")) body.Call("ApplyDamage", damage, this.OwnerCharacter, knockback);
				else if (body.HasMethod("TakeDamage")) body.Call("TakeDamage", damage, this.OwnerCharacter, knockback);
				// else GD.Print($"Weapon: hit {body.Name} for {damage}, but no damage method found.");
			}
		}
	}

	public Vector2 GetDisplayScale()
	{
		// If we have a pedestal display sprite, use its configured scale
		if (PedestalDisplaySprite != null)
		{
			return PedestalDisplayScale;
		}

		// Otherwise, use a smaller scale for the main sprite to account for animation sizing
		// Since weapons are typically scaled 7x for animations, we'll use 2x for pedestal display
		return new Vector2(7f, 7f);
	}

	public bool CanSwapWith(IPedestalItem otherItem)
	{
		return otherItem == null || otherItem is Weapon;
	}

	public void OnPickedUp(PlayerController player)
	{
		// Possibly play a sound or effect here
	}

	protected override Vector2 GetAimDirection()
	{
		// No null check needed - physics processing is disabled until OwnerCharacter is set in Equip()
		Vector2 mousePos = GetGlobalMousePosition();
		Vector2 direction = (mousePos - OwnerCharacter.GlobalPosition).Normalized();
		if (direction.LengthSquared() <= 0.000001f) direction = Vector2.Right;
		return direction;
	}
}
