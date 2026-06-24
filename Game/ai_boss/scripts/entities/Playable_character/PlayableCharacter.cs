using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayableCharacter : Entity<EntityState>, IDamageable, IHasHealth, IStateful<EntityState>, IAnimatable<EntityState>
{
	//---- Node References ----
	public Weapon EquippedWeapon;
	public Armor EquippedArmor;
	public Consumable EquippedConsumable;
	
	[ExportGroup("Equipment Scenes")]
	[Export] public PackedScene EquippedWeaponScene; // Store the PackedScene for weapon swapping
	[Export] public PackedScene EquippedArmorScene; // Store the PackedScene for armor swapping
	[Export] public PackedScene EquippedConsumableScene; // Store the PackedScene for consumable swapping
	protected Node2D _handNode;

	// ---- Signals ----
	[Signal] public delegate void StateChangedEventHandler(string newState);
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, byte maxHealth);

	//---- Health Data ----
	[ExportGroup("Health")]
	[Export] public float MaxHealth { get; set; } = 20;
	public float CurrentHealth { get; protected set; }
	public bool IsAlive => CurrentHealth > 0;
	public bool IsInvulnerable => InvulnerabilityTimer > 0 || CurrentState == EntityState.Dodging;
	
	//---- Movement Data ----
	[ExportGroup("Movement")]
	[Export] public float DodgeSpeed { get; set; } = 150f; // speed during dodge
	[Export] public float ChargeMoveModifier { get; set; } = .5f; // speed modifier during charge
	protected Vector2 _dodgeDirection = Vector2.Zero;

	// Collision management for dodge
	protected uint _normalCollisionMask = 0; // Store original mask
	protected uint _normalCollisionLayer = 0; // Store original layer

	// Direction leniency
	[Export] public float DodgeInputLeniency { get; set; } = 0.05f; // seconds to wait for input
	protected double _dodgeInputTimer = 0; // timer for input leniency for dodging

	// Facing direction
	protected sbyte _lastHorizontalFacing = 1; // 1 = right, -1 = left
	protected sbyte _lastVerticalFacing = 0; // 1 = down, -1 = up, 0 = no vertical facing

	//---- Player State ----
	protected bool _isChargingAttack = false;
	protected bool _isHeavyCharge = false;
	protected bool _isChargingConsumable = false;

	// ---- Animation ----
	public PlayerVisualController VisualController { get; protected set; }

	// ---- Timers ----
	[Export] public float HitStunDuration = 0.5f; // time for hit stun duration
	public float InvulnerabilityTimer { get; protected set; } = 0f; // timer for invulnerability after hit
	[Export] public float InvulnerabilityDuration = 1f; // time for invulnerability after hit
	
	// ---- Knockback Smoothing ----
	protected Vector2 _knockbackVelocity = Vector2.Zero;
	protected Tween _knockbackTween;

	// ---- Active Consumable Effects ----
	protected List<ConsumableEffectBase> _activeEffects = new List<ConsumableEffectBase>();

	// --- Input ----
	protected Vector2 _movementDirection = Vector2.Zero;

	public override void _Ready()
	{
		StateMachine = new StateMachine<EntityState>(this, EntityState.Idle);
		CurrentHealth = MaxHealth;

		// Initialize node references
		InitializeNodes();

		if (_handNode == null)
		{
			GD.PrintErr("Bean: could not find Hand node");
			return;
		}

		if (_physicalCollision == null)
		{
			GD.PrintErr("Bean: could not find PhysicalCollision node");
			return;
		}
		
		if (_hitArea == null)
		{
			GD.PrintErr("Bean: could not find HitArea node");
			return;
		}

		// Only connect the base sprite's animation finished signal to avoid multiple triggers
		VisualController.AnimationFinished += OnAnimationFinished;
		
		// Store the original collision properties
		_normalCollisionMask = CollisionMask;
		_normalCollisionLayer = CollisionLayer;

		EventBus.RaisePlayerSpawned(this);

		TargetType = "Enemy"; // Player targets enemies by default
	}

	protected override void InitializeNodes()
	{
		// Initialize node references
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_physicalCollision = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_handNode = GetNodeOrNull<Node2D>("VisualController/Hand");

		VisualController = GetNodeOrNull<PlayerVisualController>("VisualController");
		VisualController.HandNode = _handNode; // Pass reference to hand node for visual controller to manage visibility during attacks/dodges
	}

	public override void _PhysicsProcess(double delta)
	{

		// Update facing direction
		UpdateFacing();

		// State transitions 
		HandleStateTransitions();

		// Apply physics based on state
		ApplyMovementByState((float)delta);

		// Update animation if needed (state or facing changed)
		UpdateAnimationIfNeeded();

		// Update any active effects (e.g., regeneration)
		UpdateEffects((float)delta);

		// Update timers (hit stun, invulnerability)
		UpdateTimers((float)delta);
	}

	protected override void UpdateAI(float delta)
	{
		// No AI for player - all behavior is input-driven
		return;
	}

	// Update the player's facing direction based on input
	protected override void UpdateFacing()
	{
		if (!Mathf.IsEqualApprox(_movementDirection.X, 0) || !Mathf.IsEqualApprox(_movementDirection.Y, 0))
		{
			_lastHorizontalFacing = (sbyte)Mathf.Sign(_movementDirection.X);
			_lastVerticalFacing = (sbyte)Mathf.Sign(_movementDirection.Y);
			VisualController.FacingDirection = _movementDirection;
		}		
	}

	// Update Timers
	protected override void UpdateTimers(float delta)
	{
		// Hit invulnerability timer update
		if (InvulnerabilityTimer > 0)
		{
			InvulnerabilityTimer -= (float)delta;
			if (InvulnerabilityTimer <= 0)
			{
				InvulnerabilityTimer = 0;
				_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, true);
				_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, true);
				// End of invulnerability - can add visual effect here if desired	
			}
		}	
	}


	// --- State machine --------------------------
	public override void HandleStateTransitions() {}

	protected void CancelChargingAttack()
	{
		if (_isChargingAttack)
		{
			EquippedWeapon.CancelCharge();
			_isChargingAttack = false;
		}
	}

	protected void CancelChargingConsumable()
	{
		if (EquippedConsumable == null) return;

		// Cancel charging state
		if (_isChargingConsumable)
		{
			EquippedConsumable.CancelCharge();
			_isChargingConsumable = false;
		}

		// Also interrupt any active consumable use (windup or active effect)
		if (EquippedConsumable.State == ConsumableState.Windup || 
			EquippedConsumable.State == ConsumableState.InUse)
		{
			EquippedConsumable.InterruptUse();
		}
	}

	public override void OnEnterState(EntityState s)
	{
		switch (s)
		{
			case EntityState.Idle:
				VisualController.IsMoving = false;
				VisualController.PlayState(EntityState.Idle);
				break;

			case EntityState.Aggro:
			case EntityState.Walking:
				VisualController.IsMoving = true;
				VisualController.PlayState(EntityState.Walking);
				break;

			// play dodge animation; movement will be handled in ApplyMovementByState
			case EntityState.Dodging:
				OnEnterStateDodge();
				break;
			case EntityState.Hit:
				CancelChargingAttack();
				CancelChargingConsumable();

				VisualController.PlayState(EntityState.Hit);
				// _damageFlash.PlayEffect(_spriteContainer);
				break;

			case EntityState.Dead:
				// Stop physics processing
				
				CancelChargingAttack();
				CancelChargingConsumable();
				_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
				_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

				EquippedWeapon.SetPhysicsProcess(false);
				SetPhysicsProcess(false);
				
				VisualController.PlayState(EntityState.Dead);

				break;

			case EntityState.ConsumableUse:
			case EntityState.ConsumableCharging:
				VisualController.IsMoving = IsMoving();
				VisualController.PlayState(s);
				break;

			case EntityState.Attacking:
			case EntityState.AttackCharging:
				VisualController.IsMoving = IsMoving();
				VisualController.PlayState(s);
				break;
				
			default:
				break;
		}
	}
	
	protected void OnEnterStateDodge()
	{
		// Cancel any active charging states
		CancelChargingAttack();
		CancelChargingConsumable();
		
		// Set the sprite's flip based on direction
		VisualController.UpdateFlip(_dodgeDirection.X < 0 || _lastHorizontalFacing < 0);
		VisualController.IsMoving = true;
		VisualController.PlayState(EntityState.Dodging);
		// _dodgeFlash.PlayEffect(_spriteContainer);

		// Make player invulnerable during dodge (can't be detected by enemy weapons)
		_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);
		
		// Disable collision with enemies during dodge - remove Layer 2 from mask
		CollisionMask = _normalCollisionMask & ~2u; // Remove Layer 2 (enemies)
		
		// Make player "ghost" - enemies can't collide with us either
		// Remove Layer 3 so enemies (who check Layer 3) don't detect us
		CollisionLayer = _normalCollisionLayer & ~4u; // Remove Layer 3 (4 = 2^2)
	}

	public override void OnExitState(EntityState s)
	{
		switch (s)
		{
			case EntityState.Dodging:
				// Restore vulnerability after dodge
				_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, true);
				_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, true);
				
				// Restore normal collision properties after dodge
				CollisionMask = _normalCollisionMask;
				CollisionLayer = _normalCollisionLayer;
				break;

			default:
				break;
		}

		// Reset state timer on any state exit
		StateTimer = 0f;
	}

	// --- Physics & movement ---------------------
	protected override void ApplyMovementByState(float delta)
	{
		float armorSpeedModifier = EquippedArmor != null ? EquippedArmor.SpeedModifier : 1f;

		switch (CurrentState)
		{
			case EntityState.Dodging:
				// move using dodge vector & speed, plus any active knockback
				Velocity = _dodgeDirection * DodgeSpeed * armorSpeedModifier + _knockbackVelocity;
				MoveAndSlide();
				break;
			
			case EntityState.Hit:
			case EntityState.ConsumableUse:
			case EntityState.Attacking:
			case EntityState.Aggro:
			// allow movement while attacking
			case EntityState.Walking:
				// move using input vector, speed and modifier, plus any active knockback
				// Velocity = input * BaseSpeed * EquippedArmor.SpeedModifier;
				Velocity = _movementDirection * BaseSpeed * armorSpeedModifier + _knockbackVelocity;
				MoveAndSlide();
				break;

			case EntityState.Dead:
			// no movement when dead
			case EntityState.DodgePrep:
			// stop movement while preparing to dodge
			case EntityState.Idle:
				// stop movement
				Velocity = Vector2.Zero;
				MoveAndSlide();
				break;

			case EntityState.AttackCharging:
			case EntityState.ConsumableCharging: // Allow movement while charging consumable
												 // allow movement while charging (at reduced speed or full speed), plus any active knockback
												 // Velocity = input * BaseSpeed * ChargeMoveModifier * EquippedArmor.SpeedModifier;
				Velocity = _movementDirection * BaseSpeed * ChargeMoveModifier * armorSpeedModifier + _knockbackVelocity;
				MoveAndSlide();
				break;
		}
	}

	protected bool IsMoving()
	{
		return !Mathf.IsEqualApprox(Velocity.Length(), 0);
	}

	// --- Animation ------------------------------


	public void UpdateAnimationIfNeeded()
	{
		// Always keep IsMoving synced for states that allow movement
		switch (CurrentState)
		{
			case EntityState.Attacking:
			case EntityState.AttackCharging:
			case EntityState.ConsumableUse:
			case EntityState.ConsumableCharging:
				VisualController.IsMoving = IsMoving();
				break;
		}

		// Don't update animation during dodge - let it finish naturally
		if (CurrentState != EntityState.Dodging)
		{
			VisualController.UpdateAnimationIfNeeded();
		}

		

		PreviousState = CurrentState;
	}

	// Called by AnimatedSprite2D when any animation completes
	public void OnAnimationFinished()
	{
		if (CurrentState == EntityState.Dodging)
		{
			// decide whether to be walking or idle after dodge
			if (Velocity.Length() > 0)
				TransitionToState(EntityState.Walking);
			else
				TransitionToState(EntityState.Idle);
		}
	}

	// ---- Weapon Logic ----
	public void EquipWeapon(PackedScene weaponScene)
	{
		// If we reach this point, we have a new weapon to equip
		if (EquippedWeapon != null)
		{
			// Disconnect signals
			EquippedWeapon.AttackStarted -= OnWeaponAttackStarted;
			EquippedWeapon.AttackEnded -= OnWeaponAttackEnded;

			EquippedWeapon.Unequip();
			EquippedWeapon.QueueFree();
			EquippedWeapon = null;
		}

		// If we were attacking or charging when switching weapons, reset to appropriate state
		if (CurrentState == EntityState.Attacking || CurrentState == EntityState.AttackCharging)
		{
			TransitionToState(Velocity.Length() > 0 ? EntityState.Walking : EntityState.Idle);
		}

		if (weaponScene == null)
		{
			EquippedWeaponScene = null;
			return;
		}

		// Store the PackedScene reference
		EquippedWeaponScene = weaponScene;

		var weaponInstance = weaponScene.Instantiate() as Weapon;
		if (weaponInstance == null) return;

		_handNode.AddChild(weaponInstance);

		CallDeferred(nameof(CallEquipWeaponDeferred), weaponInstance);
	}

	// Called when equipping a new weapon
	protected void CallEquipWeaponDeferred(Weapon weapon)
	{
		if(weapon is null)
		{
			GD.PrintErr("FRA TI PREGO");
		}

		weapon.Equip(this);
		EquippedWeapon = weapon;

		// Connect to weapon signals
		weapon.AttackStarted += OnWeaponAttackStarted;
		weapon.AttackEnded += OnWeaponAttackEnded;

		GD.Print($"[PlayerController] Weapon equipped: {weapon.ItemName}");
		EventBus.RaiseWeaponEquipped(weapon);
	}

	protected void OnLightAttack()
	{
		EquippedWeapon.AttackLight(GetGlobalMousePosition());
	}

	protected void OnHeavyAttack()
	{
		EquippedWeapon.AttackHeavy(GetGlobalMousePosition());
	}

	// --- Weapon Signal Handlers ---------------
	protected void OnWeaponAttackStarted(string attackName)
	{
		// Weapon attack has started, ensure we're in attacking state
		if (CurrentState != EntityState.Attacking)
		{
			TransitionToState(EntityState.Attacking);
		}
	}

	protected void OnWeaponAttackEnded(string attackName)
	{
		// Weapon attack has ended, check for autoswing before transitioning state
		if (CurrentState == EntityState.Attacking)
		{
			// Check if we should autoswing
			bool isLightAttack = attackName == "light";
			bool isHeavyAttack = attackName == "heavy";
			
			bool shouldAutoswing = false;
			
			if (isLightAttack && EquippedWeapon.EnableLightAutoswing && Input.IsActionPressed("light_attack"))
			{
				shouldAutoswing = true;
			}
			else if (isHeavyAttack && EquippedWeapon.EnableHeavyAutoswing && Input.IsActionPressed("heavy_attack"))
			{
				shouldAutoswing = true;
			}
			
			if (shouldAutoswing)
			{
				// Queue the next attack to start after a tiny delay to ensure animation and cooldown are fully cleared
				// This prevents rapid-fire hit area creation from overlapping attacks
				_ = ProcessAutoswing(isLightAttack, isHeavyAttack);
				// Stay in Attacking state
				return;
			}
			
			// No autoswing, return to appropriate state
			if (Velocity.Length() > 0)
			{
				TransitionToState(EntityState.Walking);
			}
			else
			{
				TransitionToState(EntityState.Idle);
			}
		}
	}

	protected async System.Threading.Tasks.Task ProcessAutoswing(bool isLightAttack, bool isHeavyAttack)
	{
		// Wait for the remaining cooldown to fully elapse
		float remainingCooldown = EquippedWeapon.GetRemainingCooldown(isHeavyAttack);
		if (remainingCooldown > 0f)
		{
			await ToSignal(GetTree().CreateTimer(remainingCooldown), "timeout");
		}
		
		// Only trigger autoswing if weapon can actually start a new attack
		if (EquippedWeapon.CanStartAttack(isHeavyAttack))
		{
			if (isLightAttack)
				OnLightAttack();
			else if (isHeavyAttack)
				OnHeavyAttack();
		}
	}

	public virtual Vector2 GetAimDirection()
	{
		return Vector2.Zero; // This can be overridden by specific attack behaviors to provide directional attacks
	}

	// ---- Armor ----

	public void EquipArmor(PackedScene armorScene)
	{
		// GD.Print("Equipping new armor");

		// If we reach this point, we have a new armor to equip
		if (EquippedArmor != null)
		{
			// Disconnect signals
			EquippedArmor.Equipped -= OnArmorEquipped;
			EquippedArmor.Unequipped -= OnArmorUnequipped;

			EquippedArmor.Unequip();
			EquippedArmor.QueueFree();
			EquippedArmor = null;
		}

		if (armorScene == null)
		{
			EquippedArmorScene = null;
			return;
		}

		// Store the PackedScene reference
		EquippedArmorScene = armorScene;

		var armorInstance = armorScene.Instantiate() as Armor;
		if (armorInstance == null) return;

		CallDeferred(nameof(CallEquipArmorDeferred), armorInstance);
	}

	public void CallEquipArmorDeferred(Armor armor)
	{
		AddChild(armor);
		armor.Equip(this);
		EquippedArmor = armor;

		EventBus.RaiseArmorEquipped(armor);

		// Connect to armor signals
		armor.Equipped += OnArmorEquipped;
		armor.Unequipped += OnArmorUnequipped;
	}

	// --- Armor Signal Handlers ----------------
	protected void OnArmorEquipped()
	{
		// GD.Print($"[PlayerController] Armor equipped: {EquippedArmor.ItemName}");
		// Can add visual/audio feedback here
	}

	protected void OnArmorUnequipped()
	{
		// GD.Print($"[PlayerController] Armor unequipped");
		// Can add visual/audio feedback here
	}

	// ---- Visuals ----
	public void UpdateArmorVisuals(Armor armor)
	{
		if (VisualController is PlayerVisualController pvc)
		{
			pvc.UpdateArmorVisuals(armor);
		}
	}

	// ---- Consumable ----
	protected void UseConsumable()
	{
		if(CurrentHealth == MaxHealth) return; // Don't use if at full health (can remove this check if we want to allow non-healing consumables)

		TransitionToState(EntityState.ConsumableUse);
		EquippedConsumable.Use();
		EventBus.RaiseConsumableUsed();
	}

	protected void StartChargingConsumable()
	{
		if(CurrentHealth == MaxHealth)
		{
			// Don't start charging if at full health (can remove this check if we want to allow non-healing consumables)
			CancelChargingConsumable();
			return;
		}

		_isChargingConsumable = true;
		TransitionToState(EntityState.ConsumableCharging);
		EquippedConsumable.StartCharging();
		EventBus.RaiseConsumableCharging();
	}	

	public void EquipConsumable(PackedScene consumableScene)
	{
		// GD.Print("Equipping new consumable");

		// If we have an existing consumable, unequip it
		if (EquippedConsumable != null)
		{
			// Disconnect signals
			EquippedConsumable.ConsumableUsed -= OnConsumableUsed;
			EquippedConsumable.ConsumableCompleted -= OnConsumableCompleted;

			EquippedConsumable.Unequip();
			EquippedConsumable.QueueFree();
			EquippedConsumable = null;
		}

		if (consumableScene == null)
		{
			EquippedConsumableScene = null;
			return;
		}

		// Store the PackedScene reference
		EquippedConsumableScene = consumableScene;

		var consumableInstance = consumableScene.Instantiate() as Consumable;
		if (consumableInstance == null) return;

		CallDeferred(nameof(CallEquipConsumableDeferred), consumableInstance);
	}

	protected void CallEquipConsumableDeferred(Consumable consumable)
	{
		VisualController.ConsumableContainer.AddChild(consumable);
		consumable.Equip(this);
		EquippedConsumable = consumable;

		// Connect to consumable signalsù
		EventBus.RaiseConsumableEquipped(consumable);
		consumable.ConsumableUsed += OnConsumableUsed;
		consumable.ConsumableCompleted += OnConsumableCompleted;
	}

	// --- Consumable Signal Handlers -----------
	protected void OnConsumableUsed(string consumableName)
	{
		// Consumable has been used
		// GD.Print($"[PlayerController] Consumable used: {consumableName}");
	}

	protected void OnConsumableCompleted(string consumableName)
	{
		// Consumable effect has completed, return to appropriate state
		if (CurrentState == EntityState.ConsumableUse || CurrentState == EntityState.ConsumableCharging)
		{
			_isChargingConsumable = false;

			if (Velocity.Length() > 0)
			{
				TransitionToState(EntityState.Walking);
			}
			else
			{
				TransitionToState(EntityState.Idle);
			}
		}
	}

	// ---- Damage & Health ----
	public void ApplyDamage(float amount, Node2D attacker, float knockbackStrength = 0f)
	{
		if (IsInvulnerable) return;

		InvulnerabilityTimer = InvulnerabilityDuration;
		_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

		amount *= EquippedArmor.DamageModifier; // apply damage reduction

		CurrentHealth -= amount;
		// GD.Print($"Player took {amount} damage from {attacker.Name}");

		// Check if damage is lethal
		bool isLethalDamage = CurrentHealth <= 0;

		// Only transition to Hit state if damage is not lethal
		if (!isLethalDamage)
		{
			TransitionToState(EntityState.Hit);
		}
		
		if (attacker != null)
		{
			ApplyKnockback(attacker.GlobalPosition, knockbackStrength);
		}
		
		// Emit health changed signal and damage taken signal
		EventBus.RaisePlayerHealthChanged(CurrentHealth);
		EventBus.RaisePlayerDamaged(amount);
		GD.Print($"Player health: {CurrentHealth}/{MaxHealth}");
		EventBus.RaisePlayerDamagedEffect(5, .25f);

		if (isLethalDamage)
		{
			CurrentHealth = 0;
			Die();
		}
	}

	protected void ApplyKnockback(Vector2 attackerPosition, float strength)
	{
		// Kill existing tween if one is active (allows new knockback to override)
		if (_knockbackTween != null && _knockbackTween.IsValid())
		{
			_knockbackTween.Kill();
		}
		
		// Calculate knockback direction and apply initial velocity
		Vector2 knockbackDir = (GlobalPosition - attackerPosition).Normalized();
		Vector2 knockbackForce = knockbackDir * EquippedArmor.KnockbackModifier * strength*5;
		_knockbackVelocity = knockbackForce;
		
		// Tween knockback velocity back to zero over 0.2 seconds
		_knockbackTween = CreateTween();
		_knockbackTween.SetTrans(Tween.TransitionType.Linear);
		_knockbackTween.TweenProperty(this, "_knockbackVelocity", Vector2.Zero, 0.2f);
	}
	
	public void Die()
	{
		if (CurrentState == EntityState.Dead) return;
		
		CurrentHealth = 0;
		TransitionToState(EntityState.Dead);
		EventBus.RaisePlayerDied();
	}

	public void Heal(float amount)
	{
		CurrentHealth += amount;
		if (CurrentHealth > MaxHealth)
			CurrentHealth = MaxHealth;

		// GD.Print($"Player healed {amount} health.");

		// Emit health changed signal
		EventBus.RaisePlayerHealthChanged(CurrentHealth);
		EventBus.RaisePlayerHealed(amount);
	}

	// ---- Effects Update ----
	protected void UpdateEffects(float delta)
	{
		// Process all active effects
		foreach (var effect in _activeEffects.ToList()) // ToList to avoid modification during iteration
		{
			effect.Update(null, this, (float)delta); // Pass null for consumable since effect is now on player
		}
	}

	// Add a consumable effect to the player's active effects
	public void AddActiveEffect(ConsumableEffectBase effect)
	{
		if (effect == null)
		{
			GD.PrintErr("[PlayerController] Cannot add null effect");
			return;
		}

		_activeEffects.Add(effect);
		// GD.Print($"[PlayerController] Added active effect: {effect.GetType().Name}");
	}

	// Remove a consumable effect from the player's active effects
	public void RemoveActiveEffect(ConsumableEffectBase effect)
	{
		if (effect == null) return;

		if (_activeEffects.Remove(effect))
		{
			// GD.Print($"[PlayerController] Removed active effect: {effect.GetType().Name}");
		}
	}

	public void ShowDamageNumber(float damage)
	{
		throw new NotImplementedException();
	}

	public void PlayHitEffect(Vector2 hitPosition)
	{
		throw new NotImplementedException();
	}

	public void PlayDeathEffect()
	{
		throw new NotImplementedException();
	}

}
