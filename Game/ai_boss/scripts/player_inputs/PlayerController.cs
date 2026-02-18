using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody2D, IDamageable
{
	//---- Node References ----
	private Node2D _spriteContainer;
	private AnimatedSprite2D _bodyBaseSprite;
	private AnimatedSprite2D _bodyArmorSprite;
	private AnimatedSprite2D _helmetArmorSprite;
	public Weapon EquippedWeapon;
	public Armor EquippedArmor;
	public Consumable EquippedConsumable;
	public PackedScene EquippedWeaponScene; // Store the PackedScene for weapon swapping
	public PackedScene EquippedArmorScene; // Store the PackedScene for armor swapping
	public PackedScene EquippedConsumableScene; // Store the PackedScene for consumable swapping
	private Node2D _handNode;
	private CollisionShape2D _physicalCollision;
	private Area2D _hitboxArea;

	// ---- Signals ----
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, byte maxHealth);
	[Signal] public delegate void PlayerDiedEventHandler();

	//---- Character Data ----
	[Export] public byte Health { get; set; } = 100;
	public float CurrentHealth { get; private set; } = 100f;
	private const byte MaxHealth = 100;

	//---- Movement Data ----
	[Export] public float BaseSpeed { get; set; } = 100f; // normal speed
	[Export] public float DodgeSpeed { get; set; } = 250f; // speed during dodge
	[Export] public float ChargeMoveModifier { get; set; } = .5f; // speed modifier during charge
	private Vector2 _dodgeDirection = Vector2.Zero;

	// Collision management for dodge
	private uint _normalCollisionMask = 0; // Store original mask
	private uint _normalCollisionLayer = 0; // Store original layer

	// direction leniency
	[Export] public float DodgeInputLeniency { get; set; } = 0.05f; // seconds to wait for input
	private double _dodgeInputTimer = 0; // timer for input leniency for dodging

	// facing direction
	private Vector2 _facing = Vector2.Right; // default facing direction-
	private sbyte _lastHorizontalFacing = 1; // 1 = right, -1 = left
	private sbyte _lastVerticalFacing = 0; // 1 = down, -1 = up, 0 = no vertical facing

	//---- Player State ----
	private enum PlayerState { Idle, Walking, DodgePrep, Dodge, Attacking, Charging, Hit, Dead, ConsumableUse, ConsumableCharging }
	private PlayerState _state = PlayerState.Idle;
	private PlayerState _prevState = PlayerState.Idle; // for detecting state changes
	private bool _isChargingAttack = false;
	private bool _isHeavyCharge = false;
	private bool _isChargingConsumable = false;

	// ---- Visual Effects ----
	[Export] public VisualFlash _damageFlash;
	[Export] public VisualFlash _dodgeFlash;

	// ---- Timers ----
	private float _stateTimer = 0f; // timer for tracking time in current state
	[Export] public float HitStunDuration = 0.25f; // time for hit stun duration
	private float _invulnerabilityTimer = 0f; // timer for invulnerability after hit
	[Export] public float InvulnerabilityDuration = 1f; // time for invulnerability after hit
	private float _effectTimer = 0f; // timer for tracking effect durations

	// ---- Active Consumable Effects ----
	private List<ConsumableEffectBase> _activeEffects = new List<ConsumableEffectBase>();


	public override void _Ready()
	{
		// Initialize node references
		_spriteContainer = GetNodeOrNull<Node2D>("BodyLayers");
		_bodyBaseSprite = GetNodeOrNull<AnimatedSprite2D>("BodyLayers/BodyBase");
		_bodyArmorSprite = GetNodeOrNull<AnimatedSprite2D>("BodyLayers/BodyArmor");
		_helmetArmorSprite = GetNodeOrNull<AnimatedSprite2D>("BodyLayers/Helmet");

		if (_spriteContainer == null || _bodyBaseSprite == null || _bodyArmorSprite == null || _helmetArmorSprite == null)
		{
			GD.PrintErr("Bean: could not find one or more body sprite nodes");
			return;
		}

		_handNode = GetNodeOrNull<Node2D>("Hand");
		if (_handNode == null)
		{
			GD.PrintErr("Bean: could not find Hand node");
			return;
		}

		_physicalCollision = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		if (_physicalCollision == null)
		{
			GD.PrintErr("Bean: could not find PhysicalCollision node");
			return;
		}

		_hitboxArea = GetNodeOrNull<Area2D>("HitArea");
		if (_hitboxArea == null)
		{
			GD.PrintErr("Bean: could not find HitArea node");
			return;
		}

		// Initialize visual effects
		if (_damageFlash == null)
			_damageFlash = new VisualFlash();
		if (_dodgeFlash == null)
			_dodgeFlash = new VisualFlash();
			
		_damageFlash.InitializeVisuals(_bodyBaseSprite);
		_dodgeFlash.InitializeVisuals(_bodyBaseSprite);

		// Only connect the base sprite's animation finished signal to avoid multiple triggers
		_bodyBaseSprite.AnimationFinished += OnAnimationFinished;
		_bodyBaseSprite.FrameChanged += OnBaseFrameChanged;
		
		// Store the original collision properties
		_normalCollisionMask = CollisionMask;
		_normalCollisionLayer = CollisionLayer;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Read input direction
		Vector2 inputDir = ReadDirection();

		// Update facing direction
		UpdateFacing(inputDir);

		// State transitions (input-driven)
		HandleStateTransitions(inputDir);

		// Handle combat input (only when not dodging)
		HandleCombatInput();

		// Handle consumable input
		HandleConsumableInput();

		// Apply physics based on state (movement independent of animation)
		ApplyMovementByState(delta, inputDir);

		// Update animation if needed (state or facing changed)
		UpdateAnimationIfNeeded();

		// Update any active effects (e.g., regeneration)
		UpdateEffects(delta);

		// Hit invulnerability timer update
		if (_invulnerabilityTimer > 0)
		{
			_invulnerabilityTimer -= (float)delta;
			if (_invulnerabilityTimer <= 0)
			{
				_invulnerabilityTimer = 0;
				_hitboxArea.SetDeferred(Area2D.PropertyName.Monitorable, true);
				// End of invulnerability - can add visual effect here if desired	
			}
		}
	}

	private Vector2 ReadDirection()
	{
		return Input.GetVector("move_left", "move_right", "move_up", "move_down"); // get already normalized direction vector
	}

	// Update the player's facing direction based on input
	void UpdateFacing(Vector2 inputDir)
	{
		_facing = inputDir;

		if (!Mathf.IsEqualApprox(_facing.X, 0) || !Mathf.IsEqualApprox(_facing.Y, 0))
        {
            _lastHorizontalFacing = (sbyte)Mathf.Sign(_facing.X);
			_lastVerticalFacing = (sbyte)Mathf.Sign(_facing.Y);
        }		
	}

	string AddVerticalFacingToAnim(string baseAnim)
	{
		if (_lastHorizontalFacing == 0)
        {
            if (_lastVerticalFacing > 0)
				return "down_" + baseAnim;
			else if (_lastVerticalFacing < 0)
				return "up_" + baseAnim;
			else
				return baseAnim; // no vertical facing, return base animation
        }

		else
		{
			if (_lastVerticalFacing > 0)
				return baseAnim;
			else if (_lastVerticalFacing < 0)
				return "up_" + baseAnim;
			else
				return baseAnim;
		}	
	}

	// Checks if any movement keys were just pressed
	private Vector2 MovementJustPressed()
	{
		if (Input.IsActionJustPressed("move_right")) return Vector2.Right;
		if (Input.IsActionJustPressed("move_left")) return Vector2.Left;
		if (Input.IsActionJustPressed("move_down")) return Vector2.Down;
		if (Input.IsActionJustPressed("move_up")) return Vector2.Up;
		return Vector2.Zero;

	}

	// --- State machine --------------------------
	private void HandleStateTransitions(Vector2 input)
	{
		switch (_state)
		{
			case PlayerState.Idle:
			case PlayerState.Walking:
				HandleIdleTransitions(input);
				break;

			case PlayerState.DodgePrep:
				HandleDodgePrepTransitions();
				break;

			case PlayerState.Hit:
				HandleHitTransitions();
				break;

			case PlayerState.Charging:
				HandleChargingTransitions();
				break;

			case PlayerState.ConsumableCharging:
				HandleConsumableChargingTransitions();
				break;

			default:
				// Other states (Dodge, Attacking, Dead) have no transitions here
				break;
		}
	}

	private void HandleIdleTransitions(Vector2 input)
	{
		// Dodge starts on just-pressed regardless of whether there is movement
		if (Input.IsActionJustPressed("dodge"))
		{
			_dodgeInputTimer = DodgeInputLeniency;
			TransitionToState(PlayerState.DodgePrep);

		}
		else
		{
			// Normal movement -> walking vs idle
			if (input.Length() > 0)
				TransitionToState(PlayerState.Walking);
			else
				TransitionToState(PlayerState.Idle);
		}
	}

	private void HandleDodgePrepTransitions()
	{
		_dodgeInputTimer -= GetProcessDeltaTime();

		// If player pressed any movement key *this frame*, accept the (combined) held input immediately
		if (MovementJustPressed() != Vector2.Zero)
		{
			_dodgeDirection = ReadDirection();
			TransitionToState(PlayerState.Dodge);
			return;
		}

		// otherwise, wait for leniency timer to expire and then fall back to held direction or facing
		if (_dodgeInputTimer <= 0)
		{
			// Sample input now so simultaneous presses are respected
			Vector2 dodgeInput = ReadDirection();

			if (dodgeInput == Vector2.Zero)
			{
				TransitionToState(PlayerState.Idle);
				return;
			}

			_dodgeDirection = dodgeInput;
			TransitionToState(PlayerState.Dodge);
		}
	}

	private void HandleChargingTransitions()
	{
		// Allow dodging while charging - this cancels the charge
		if (Input.IsActionJustPressed("dodge"))
		{
			// GD.Print("Dodge input while charging - cancelling charge");
			CancelChargingAttack();
			TransitionToState(PlayerState.DodgePrep);
		}
	}

	private void HandleConsumableChargingTransitions()
	{
		// Allow dodging while charging consumable - this cancels the charge
		if (Input.IsActionJustPressed("dodge"))
		{
			CancelChargingConsumable();
			TransitionToState(PlayerState.DodgePrep);
		}
	}

	private void HandleHitTransitions()
	{
		_stateTimer += (float)GetProcessDeltaTime();
		if (_stateTimer >= HitStunDuration)
		{
			// decide whether to be walking or idle after hit stun
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
				TransitionToState(PlayerState.Walking);
			else
				TransitionToState(PlayerState.Idle);
		}
	}

	// --- Combat Input Handling -----------------
	private void HandleCombatInput()
	{

		switch (_state)
		{
			// Don't allow attacks while dodging, in dodge preparation, or already attacking
			case PlayerState.DodgePrep:
			case PlayerState.Dodge:
			case PlayerState.Attacking:
			case PlayerState.Hit:
			case PlayerState.Dead:
				return;

			case PlayerState.Charging:
				HandleChargingInput();
				return;
		}

		// Handle light attack input
		if (Input.IsActionJustPressed("light_attack"))
		{
			HandleAttackInput(false);
		}
		// Handle heavy attack input  
		else if (Input.IsActionJustPressed("heavy_attack"))
		{
			HandleAttackInput(true);
		}
	}

	private void HandleAttackInput(bool isHeavy)
	{
		if (!EquippedWeapon.CanStartAttack(isHeavy)) return;

		if (EquippedWeapon.HasChargeableAttack(isHeavy))
		{
			StartChargingAttack(isHeavy);
		}
		else
		{
			TransitionToState(PlayerState.Attacking);
			if (isHeavy)
				OnHeavyAttack();
			else
				OnLightAttack();
		}
	}

	private void HandleChargingInput()
	{
		bool lightPressed = Input.IsActionPressed("light_attack");
		bool heavyPressed = Input.IsActionPressed("heavy_attack");

		// Check if the relevant button is still held
		bool shouldContinueCharging = _isHeavyCharge ? heavyPressed : lightPressed;

		if (shouldContinueCharging)
		{
			// Continue charging - weapon handles the charging logic
			EquippedWeapon.UpdateCharge((float)GetProcessDeltaTime());
		}
		else
		{
			// Button released - execute or cancel the charged attack
			if (EquippedWeapon.CanReleaseCharge())
			{
				TransitionToState(PlayerState.Attacking);
				ExecuteChargedAttack(_isHeavyCharge);
			}
			else
			{
				// Charge was too short, cancel and return to appropriate state
				EquippedWeapon.CancelCharge();
				Vector2 currentInput = ReadDirection();
				TransitionToState(currentInput.Length() > 0 ? PlayerState.Walking : PlayerState.Idle);
			}
		}
	}

	private void StartChargingAttack(bool isHeavy)
	{
		_isChargingAttack = true;
		_isHeavyCharge = isHeavy;
		TransitionToState(PlayerState.Charging);
		EquippedWeapon.StartCharge(GetGlobalMousePosition(), isHeavy);
	}

	private void ExecuteChargedAttack(bool isHeavy)
	{
		if (isHeavy)
			EquippedWeapon.ExecuteChargedHeavy(GetGlobalMousePosition());
		else
			EquippedWeapon.ExecuteChargedLight(GetGlobalMousePosition());
	}

	private void CancelChargingAttack()
	{
		if (_isChargingAttack)
		{
			EquippedWeapon.CancelCharge();
			_isChargingAttack = false;
		}
	}

	// --- Consumable Input Handling -------------
	private void HandleConsumableInput()
	{
		switch (_state)
		{
			// Don't allow consumable use while dodging, in dodge prep, attacking, or dead
			case PlayerState.DodgePrep:
			case PlayerState.Dodge:
			case PlayerState.Attacking:
			case PlayerState.Charging:
			case PlayerState.Hit:
			case PlayerState.Dead:
			case PlayerState.ConsumableUse:
				return;

			case PlayerState.ConsumableCharging:
				HandleConsumableChargingInput();
				return;
		}

		// Check if consumable is equipped
		if (EquippedConsumable == null) return;

		// Handle consumable use input
		if (Input.IsActionJustPressed("consumable_use"))
		{
			if (EquippedConsumable.HasChargeableEffect())
			{
				StartChargingConsumable();
			}
			else
			{
				UseConsumable();
			}
		}
	}
	private void HandleConsumableChargingInput()
	{
		bool consumablePressed = Input.IsActionPressed("consumable_use");

		if (!consumablePressed)
		{
			// Button released - execute or cancel the charged consumable
			if (EquippedConsumable.CanReleaseCharge())
			{
				TransitionToState(PlayerState.ConsumableUse);
				EquippedConsumable.ExecuteCharged();
			}
			else
			{
				// Charge was too short, cancel animation and return to appropriate state
				EquippedConsumable.CancelCharge();
				_isChargingConsumable = false;
				Vector2 currentInput = ReadDirection();
				TransitionToState(currentInput.Length() > 0 ? PlayerState.Walking : PlayerState.Idle);
			}
		}
	}

	private void StartChargingConsumable()
	{
		_isChargingConsumable = true;
		TransitionToState(PlayerState.ConsumableCharging);
		EquippedConsumable.StartCharging();
	}

	private void UseConsumable()
	{
		TransitionToState(PlayerState.ConsumableUse);
		EquippedConsumable.Use();
	}

	private void CancelChargingConsumable()
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

	private void TransitionToState(PlayerState next)
	{
		if (_state == next) return;

		OnExitState(_state);
		_prevState = _state;
		_state = next;
		OnEnterState(next);
	}

	private void OnEnterState(PlayerState s)
	{
		switch (s)
		{
			// play dodge animation; movement will be handled in ApplyMovementByState
			case PlayerState.Dodge:
			_handNode.Visible = false;
				OnEnterStateDodge();
				break;
			case PlayerState.Hit:
				CancelChargingAttack();
				CancelChargingConsumable();

				string animation = GetAnimationForState(s);
				animation = AddVerticalFacingToAnim(animation);	

				PlayAnimation(animation);
				_damageFlash.PlayEffect(_spriteContainer);
				break;

			case PlayerState.Dead:
				// Stop physics processing
				
				CancelChargingAttack();
				CancelChargingConsumable();
				_hitboxArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

				EquippedWeapon.SetPhysicsProcess(false);
				_handNode.Visible = false;
				SetPhysicsProcess(false);
				
				PlayAnimation(GetAnimationForState(s));

				break;

			case PlayerState.ConsumableUse:
			case PlayerState.ConsumableCharging:
				_handNode.Visible = false;
				break;
				
			default:
				break;
		}
	}
	
	private void OnEnterStateDodge()
	{
		// Cancel any active charging states
		CancelChargingAttack();
		CancelChargingConsumable();
		
		// Set the sprite's flip based on direction
		FlipSprites(_dodgeDirection.X < 0 || _lastHorizontalFacing < 0);
		string animation = GetAnimationForState(PlayerState.Dodge);
		animation = AddVerticalFacingToAnim(animation);

		PlayAnimation(animation);
		_dodgeFlash.PlayEffect(_spriteContainer);

		// Make player invulnerable during dodge (can't be detected by enemy weapons)
		_hitboxArea.SetDeferred(Area2D.PropertyName.Monitorable, false);
		
		// Disable collision with enemies during dodge - remove Layer 2 from mask
		CollisionMask = _normalCollisionMask & ~2u; // Remove Layer 2 (enemies)
		
		// Make player "ghost" - enemies can't collide with us either
		// Remove Layer 3 so enemies (who check Layer 3) don't detect us
		CollisionLayer = _normalCollisionLayer & ~4u; // Remove Layer 3 (4 = 2^2)
	}

	private void OnExitState(PlayerState s)
	{
		switch (s)
		{
			case PlayerState.Hit:
				_damageFlash.ClearEffect(_spriteContainer);
				break;

			case PlayerState.Dodge:
				_handNode.Visible = true;
				_dodgeFlash.ClearEffect(_spriteContainer);

				// Restore vulnerability after dodge
				_hitboxArea.SetDeferred(Area2D.PropertyName.Monitorable, true);
				
				// Restore normal collision properties after dodge
				CollisionMask = _normalCollisionMask;
				CollisionLayer = _normalCollisionLayer;
				break;
			
			case PlayerState.ConsumableUse:
			case PlayerState.ConsumableCharging:
				_handNode.Visible = true;
				break;

			default:
				break;
		}

		// Reset state timer on any state exit
		_stateTimer = 0f;
	}

	// --- Physics & movement ---------------------
	private void ApplyMovementByState(double delta, Vector2 input)
	{
		float armorSpeedModifier = EquippedArmor != null ? EquippedArmor.SpeedModifier : 1f;

		switch (_state)
		{
			case PlayerState.Dodge:
				// move using dodge vector & speed
				Velocity = _dodgeDirection * DodgeSpeed * armorSpeedModifier;
				MoveAndSlide();
				break;

			case PlayerState.ConsumableUse:
			case PlayerState.Attacking:
			// allow movement while attacking
			case PlayerState.Walking:
				// move using input vector, speed and modifier
				// Velocity = input * BaseSpeed * EquippedArmor.SpeedModifier;
				Velocity = input * BaseSpeed * armorSpeedModifier;
				MoveAndSlide();
				break;

			case PlayerState.Dead:
			// no movement when dead
			case PlayerState.Hit:
			// stop movement while in hit stun
			case PlayerState.DodgePrep:
			// stop movement while preparing to dodge
			case PlayerState.Idle:
				// stop movement
				Velocity = Vector2.Zero;
				MoveAndSlide();
				break;

			case PlayerState.Charging:
			case PlayerState.ConsumableCharging: // Allow movement while charging consumable
												 // allow movement while charging (at reduced speed or full speed)
												 // Velocity = input * BaseSpeed * ChargeMoveModifier * EquippedArmor.SpeedModifier;
				Velocity = input * BaseSpeed * ChargeMoveModifier * armorSpeedModifier;
				MoveAndSlide();
				break;
		}
	}

	private bool IsMoving()
	{
		return !Mathf.IsEqualApprox(Velocity.Length(), 0);
	}

	// --- Animation ------------------------------
	private void UpdateAnimationIfNeeded()
	{
		// Only update visuals when state changed OR we need to update facing while walking/idle
		bool stateChanged = _state != _prevState;

		// If we are in dodge, don't let other animations override
		if (_state == PlayerState.Dodge)
		{
			_prevState = _state; // just update prev state to avoid repeated checks
			return;
		}

		// Update sprite facing (even during attacks for autoswing)
		FlipSprites(_lastHorizontalFacing < 0);

		// Don't override weapon attack animations with player animations
		// The weapon controls the sprite during attacking state
		if (_state == PlayerState.Attacking)
		{
			_prevState = _state;
			return;
		}

		// Set animation based on state
		string targetAnimation = GetAnimationForState(_state);
		
		if(targetAnimation != "dead") // don't apply vertical facing to death animation to avoid weird stretching
		 	targetAnimation = AddVerticalFacingToAnim(targetAnimation);

		if (stateChanged || _bodyBaseSprite.Animation != targetAnimation)
		{
			PlayAnimation(targetAnimation);
		}

		if (_lastVerticalFacing < 0)
        {
			_handNode.ZIndex = -1; // behind body when facing up
        } else
		{
			_handNode.ZIndex = 0; // in front of body when facing down or horizontal
		}

		_prevState = _state;
	}

	private string GetAnimationForState(PlayerState state)
	{
		return state switch
		{
			PlayerState.Dodge => "dodge",
			PlayerState.Walking => "walking",
			PlayerState.Idle => "idle",
			PlayerState.Attacking => "attack",
			PlayerState.Charging => IsMoving() ? "charge_walking" : "charge_idle",
			PlayerState.ConsumableCharging => IsMoving() ? "charge_walking" : "charge_idle",
			PlayerState.ConsumableUse => IsMoving() ? "walking" : "idle",
			PlayerState.Hit => "hit",
			PlayerState.Dead => "dead",
			PlayerState.DodgePrep => "idle", 
			_ => "idle" // Default to idle for any unhandled state
		};
	}

	private void PlayAnimation(string animationName)
	{
		// Play on base sprite first
		_bodyBaseSprite.Play(animationName);
		_bodyArmorSprite.Play(animationName);
		_helmetArmorSprite.Play(animationName);
	}
	
	private void SyncArmorFrames()
	{
		string animation = _bodyBaseSprite.Animation;
		int frame = _bodyBaseSprite.Frame;	

		_bodyArmorSprite.Animation = animation;
		_bodyArmorSprite.Frame = frame;
		
		_helmetArmorSprite.Animation = animation;
		_helmetArmorSprite.Frame = frame;
	}

	private void FlipSprites(bool flip)
	{
		_spriteContainer.Scale = new Vector2(
			flip ? -1 : 1, 
			1
		);
	}

	// Sync armor sprites to base sprite every frame change
	private void OnBaseFrameChanged()
	{
		_bodyArmorSprite.Animation = _bodyBaseSprite.Animation;
		_bodyArmorSprite.Frame = _bodyBaseSprite.Frame;

		_helmetArmorSprite.Animation = _bodyBaseSprite.Animation;
		_helmetArmorSprite.Frame = _bodyBaseSprite.Frame;
	}

	// Called by AnimatedSprite2D when any animation completes
	private void OnAnimationFinished()
	{
		// Get the name of the finished animation
		var animName = _bodyBaseSprite.Animation;
		// If dodge finished, end dodge and transit to Idle/Walking based on current input
		var dodgeAnim = GetAnimationForState(PlayerState.Dodge);

		bool isDodgeAnim = animName ==  dodgeAnim || animName == "up_" + dodgeAnim || animName == "down_" + dodgeAnim;

		if (isDodgeAnim && _state == PlayerState.Dodge)
		{
			// decide whether to be walking or idle after dodge
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
				TransitionToState(PlayerState.Walking);
			else
				TransitionToState(PlayerState.Idle);
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
		if (_state == PlayerState.Attacking || _state == PlayerState.Charging)
		{
			Vector2 currentInput = ReadDirection();
			TransitionToState(currentInput.Length() > 0 ? PlayerState.Walking : PlayerState.Idle);
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
	private void CallEquipWeaponDeferred(Weapon weapon)
	{
		weapon.Equip(this);
		EquippedWeapon = weapon;

		// Connect to weapon signals
		weapon.AttackStarted += OnWeaponAttackStarted;
		weapon.AttackEnded += OnWeaponAttackEnded;
	}

	private void OnLightAttack()
	{
		EquippedWeapon.AttackLight(GetGlobalMousePosition());
	}

	private void OnHeavyAttack()
	{
		EquippedWeapon.AttackHeavy(GetGlobalMousePosition());
	}

	// --- Weapon Signal Handlers ---------------
	private void OnWeaponAttackStarted(string attackName)
	{
		// Weapon attack has started, ensure we're in attacking state
		if (_state != PlayerState.Attacking)
		{
			TransitionToState(PlayerState.Attacking);
		}
	}

	private void OnWeaponAttackEnded(string attackName)
	{
		// Weapon attack has ended, check for autoswing before transitioning state
		if (_state == PlayerState.Attacking)
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
				// Trigger another attack immediately
				if (isLightAttack)
					OnLightAttack();
				else if (isHeavyAttack)
					OnHeavyAttack();
				// Stay in Attacking state
				return;
			}
			
			// No autoswing, return to appropriate state
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
			{
				TransitionToState(PlayerState.Walking);
			}
			else
			{
				TransitionToState(PlayerState.Idle);
			}
		}
	}

	// ---- Armor ----

	public void EquipArmor(PackedScene armorScene)
	{
		GD.Print("Equipping new armor");
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
		SyncArmorFrames();

		// Connect to armor signals
		armor.Equipped += OnArmorEquipped;
		armor.Unequipped += OnArmorUnequipped;
	}

	// --- Armor Signal Handlers ----------------
	private void OnArmorEquipped()
	{
		GD.Print($"[PlayerController] Armor equipped: {EquippedArmor.ItemName}");
		// Can add visual/audio feedback here
	}

	private void OnArmorUnequipped()
	{
		GD.Print($"[PlayerController] Armor unequipped");
		// Can add visual/audio feedback here
	}

	// ---- Visuals ----
	public void UpdateArmorVisuals(Armor armor)
	{
		if (armor != null)
		{
			_bodyArmorSprite.SpriteFrames = armor.BodySpriteFrames;
			_helmetArmorSprite.SpriteFrames = armor.HelmetSpriteFrames;
		}
		else
		{
			_bodyArmorSprite.SpriteFrames = null;
			_helmetArmorSprite.SpriteFrames = null;
		}
	}

	// ---- Consumable ----
	public void EquipConsumable(PackedScene consumableScene)
	{
		GD.Print("Equipping new consumable");
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

	private void CallEquipConsumableDeferred(Consumable consumable)
	{
		AddChild(consumable);
		consumable.Equip(this);
		EquippedConsumable = consumable;

		// Connect to consumable signals
		consumable.ConsumableUsed += OnConsumableUsed;
		consumable.ConsumableCompleted += OnConsumableCompleted;
	}

	// --- Consumable Signal Handlers -----------
	private void OnConsumableUsed(string consumableName)
	{
		// Consumable has been used
		GD.Print($"[PlayerController] Consumable used: {consumableName}");
	}

	private void OnConsumableCompleted(string consumableName)
	{
		// Consumable effect has completed, return to appropriate state
		if (_state == PlayerState.ConsumableUse || _state == PlayerState.ConsumableCharging)
		{
			_isChargingConsumable = false;
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
			{
				TransitionToState(PlayerState.Walking);
			}
			else
			{
				TransitionToState(PlayerState.Idle);
			}
		}
	}

	// ---- Damage & Health ----
	public bool IsInvulnerable()
	{
		return _invulnerabilityTimer > 0 || _state == PlayerState.Dodge;
	}

	public void ApplyDamage(float amount, Node2D attacker, float knockbackStrength = 400f)
	{
		_invulnerabilityTimer = InvulnerabilityDuration;
		_hitboxArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

		amount *= EquippedArmor.DamageModifier; // apply damage reduction

		CurrentHealth -= amount;
		GD.Print($"Player took {amount} damage from {attacker.Name}");

		// Check if damage is lethal
		bool isLethalDamage = CurrentHealth <= 0;

		// Only transition to Hit state if damage is not lethal
		if (!isLethalDamage)
		{
			TransitionToState(PlayerState.Hit);
		}

		// Apply knockback or other effects based on the attacker
		Vector2 knockbackDir = (GlobalPosition - attacker.GlobalPosition).Normalized();
		Velocity += knockbackDir * EquippedArmor.KnockbackModifier * knockbackStrength;
		MoveAndSlide();

		// Emit health changed signal
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

		if (isLethalDamage)
		{
			CurrentHealth = 0;
			Die();
		}
	}
	
	public void Die()
	{
		if (_state == PlayerState.Dead) return;
		
		CurrentHealth = 0;
		TransitionToState(PlayerState.Dead);
		EmitSignal(SignalName.PlayerDied);
	}

	public void Heal(float amount)
	{
		CurrentHealth += amount;
		if (CurrentHealth > MaxHealth)
			CurrentHealth = MaxHealth;

		GD.Print($"Player healed {amount} health.");

		// Emit health changed signal
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
	}

	// ---- Effects Update ----
	private void UpdateEffects(double delta)
	{
		// Process all active effects
		for (int i = _activeEffects.Count - 1; i >= 0; i--)
		{
			var effect = _activeEffects[i];
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
		GD.Print($"[PlayerController] Added active effect: {effect.GetType().Name}");
	}

	// Remove a consumable effect from the player's active effects
	public void RemoveActiveEffect(ConsumableEffectBase effect)
	{
		if (effect == null) return;

		if (_activeEffects.Remove(effect))
		{
			GD.Print($"[PlayerController] Removed active effect: {effect.GetType().Name}");
		}
	}

}
