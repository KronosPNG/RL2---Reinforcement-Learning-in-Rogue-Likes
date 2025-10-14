using Godot;
using System;

public partial class PlayerController : CharacterBody2D, IDamageable
{
	//---- Node References ----
	private AnimatedSprite2D _sprite;
	public Weapon EquippedWeapon;
	public Armor EquippedArmor;
	public PackedScene EquippedWeaponScene; // Store the PackedScene for weapon swapping
	public PackedScene EquippedArmorScene; // Store the PackedScene for armor swapping
	private Node2D _handNode;
	private CollisionShape2D _hitbox;

	// ---- Signals ----
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, byte maxHealth);
	[Signal] public delegate void PlayerDiedEventHandler();

	//---- Character Data ----
	public float DamageReduction { get; set; } = 0f;
	[Export] public byte Health { get; set; } = 100;
	public float CurrentHealth { get; private set; } = 100f;
	private const byte MaxHealth = 100;

	//---- Movement Data ----
	[Export] public float BaseSpeed { get; set; } = 500f; // normal speed
	public float SpeedModifier { get; set; } = 1f; // speed modifier
	[Export] public float DodgeSpeed { get; set; } = 1500f; // speed during dodge
	[Export] public float ChargeMoveModifier { get; set; } = .5f; // speed modifier during charge
	private Vector2 _dodgeDirection = Vector2.Zero;

	// direction leniency
	[Export] public float DodgeInputLeniency { get; set; } = 0.05f; // seconds to wait for input
	private double _dodgeInputTimer = 0; // timer for input leniency for dodging

	// facing direction
	private Vector2 _facing = Vector2.Right; // default facing direction
	private sbyte _lastHorizontalFacing = 1; // 1 = right, -1 = left

	//---- Player State ----
	private enum PlayerState { Idle, Walking, DodgePrep, Dodge, Attacking, Charging, Hit, Dead }
	private PlayerState _state = PlayerState.Idle;
	private PlayerState _prevState = PlayerState.Idle; // for detecting state changes
	private bool _isChargingAttack = false;
	private bool _isHeavyCharge = false;
	private float _knockbackResistance = 0f;

	// ---- Visual Effects ----
	[Export] public VisualFlash _damageFlash;
	[Export] public VisualFlash _dodgeFlash;

	// ---- Timers ----
	private float _stateTimer = 0f; // timer for tracking time in current state
	private float _hitStunTimer = 0.5f; // timer for hit stun duration


	public override void _Ready()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("PlayerSprite");
		if (_sprite == null)
		{
			GD.PrintErr("Bean: could not find AnimatedSprite2D node 'PlayerSprite'");
			return;
		}

		_handNode = GetNodeOrNull<Node2D>("Hand");
		if (_handNode == null)
		{
			GD.PrintErr("Bean: could not find Hand node");
			return;
		}

		_hitbox = GetNodeOrNull<CollisionShape2D>("HitBox");
		if (_hitbox == null)
		{
			GD.PrintErr("Bean: could not find Hitbox node");
			return;
		}

		// Initialize visual effects
		if (_damageFlash == null)
			_damageFlash = new VisualFlash();
		if (_dodgeFlash == null)
			_dodgeFlash = new VisualFlash();
			
		_damageFlash.InitializeVisuals(_sprite);
		_dodgeFlash.InitializeVisuals(_sprite);

		_sprite.AnimationFinished += OnAnimationFinished; // Connect animation finished signal
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

		// Apply physics based on state (movement independent of animation)
		ApplyMovementByState(delta, inputDir);

		// Update animation if needed (state or facing changed)
		UpdateAnimationIfNeeded();
	}

	private Vector2 ReadDirection()
	{
		return Input.GetVector("move_left", "move_right", "move_up", "move_down"); // get already normalized direction vector
	}

	// Update the player's facing direction based on input
	void UpdateFacing(Vector2 inputDir)
	{
		_facing = inputDir;

		if (!Mathf.IsEqualApprox(_facing.X, 0))
			_lastHorizontalFacing = (sbyte)Mathf.Sign(_facing.X);
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
			EquippedWeapon.CancelCharge();
			_isChargingAttack = false;
			_dodgeInputTimer = DodgeInputLeniency;
			TransitionToState(PlayerState.DodgePrep);
		}
	}

	private void HandleHitTransitions()
	{
		_stateTimer += (float)GetProcessDeltaTime();
		if (_stateTimer >= _hitStunTimer)
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
				OnEnterStateDodge();
				break;
			case PlayerState.Hit:
				_sprite.Play(GetAnimationForState(s));
				_damageFlash.PlayEffect(_sprite);
				break;

			default:
				break;
		}
	}
	
	private void OnEnterStateDodge()
	{
		// Set the sprite's flip based on direction
		_sprite.FlipH = _dodgeDirection.X < 0 || _lastHorizontalFacing < 0;
		_sprite.Play(GetAnimationForState(PlayerState.Dodge));
		_dodgeFlash.PlayEffect(_sprite);
		_hitbox.Disabled = true;
		
	}

	private void OnExitState(PlayerState s)
	{
		switch (s)
		{
			case PlayerState.Hit:
				_damageFlash.ClearEffect(_sprite);
				break;

			case PlayerState.Dodge:
				_dodgeFlash.ClearEffect(_sprite);
				_hitbox.Disabled = false;
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
		switch (_state)
		{
			case PlayerState.Dodge:
				// move using dodge vector & speed
				Velocity = _dodgeDirection * DodgeSpeed;
				MoveAndSlide();
				break;

			case PlayerState.Attacking:
			// allow movement while attacking
			case PlayerState.Walking:
				// move using input vector, speed and modifier
				Velocity = input * BaseSpeed * SpeedModifier;
				MoveAndSlide();
				break;

			case PlayerState.DodgePrep:
			// stop movement while preparing to dodge
			case PlayerState.Idle:
				// stop movement
				Velocity = Vector2.Zero;
				MoveAndSlide();
				break;

			case PlayerState.Charging:
				// allow movement while charging (at reduced speed or full speed)
				Velocity = input * BaseSpeed * ChargeMoveModifier;
				MoveAndSlide();
				break;
		}
	}

	// --- Animation ------------------------------
	private void UpdateAnimationIfNeeded()
	{
		// Only update visuals when state changed OR we need to update facing while walking/idle
		bool stateChanged = _state != _prevState;

		// If we are in dodge or attacking, don't let other animations override
		if (_state == PlayerState.Dodge || _state == PlayerState.Attacking)
		{
			_prevState = _state; // just update prev state to avoid repeated checks
			return;
		}

		bool IsMoving = !Mathf.IsEqualApprox(Velocity.Length(), 0);

		// Update sprite facing
		_sprite.FlipH = _lastHorizontalFacing < 0;

		// Set animation based on state
		string targetAnimation;

		if (_state == PlayerState.Charging)
		{
			if (IsMoving)
			{
				targetAnimation = "charge_walking";
			}
			else
			{
				targetAnimation = "charge_idle";
			}
		}
		else
		{
			targetAnimation = GetAnimationForState(_state);
		}


		if (stateChanged || _sprite.Animation != targetAnimation)
		{
			if (_sprite.SpriteFrames.HasAnimation(targetAnimation))
				_sprite.Play(targetAnimation);
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
			PlayerState.Charging => "charge",
			PlayerState.Hit => "hit",
			PlayerState.Dead => "dead",
			_ => null
		};
	}

	// Called by AnimatedSprite2D when any animation completes
	private void OnAnimationFinished()
	{
		// Get the name of the finished animation
		var animName = _sprite.Animation;
		// If dodge finished, end dodge and transit to Idle/Walking based on current input
		if (animName == GetAnimationForState(PlayerState.Dodge) && _state == PlayerState.Dodge)
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
		// Weapon attack has ended, return to appropriate state
		if (_state == PlayerState.Attacking)
		{
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
		// If we reach this point, we have a new armor to equip
		if (EquippedArmor != null)
		{
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

		AddChild(armorInstance);

		CallDeferred(nameof(CallEquipArmorDeferred), armorInstance);
	}

	public void CallEquipArmorDeferred(Armor armor)
	{
		armor.Equip(this);
		EquippedArmor = armor;
	}

	// ---- Damage & Health ----

	public void ApplyDamage(float amount, Node2D attacker, float knockbackStrength = 400f)
	{
		GD.Print($"Player took {amount} damage from {attacker.Name}");
		CurrentHealth -= amount;

		// Check if damage is lethal
		bool isLethalDamage = CurrentHealth <= 0;

		// Only transition to Hit state if damage is not lethal
		if (!isLethalDamage)
		{
			TransitionToState(PlayerState.Hit);
		}

		// Apply knockback or other effects based on the attacker
		Vector2 knockbackDir = (GlobalPosition - attacker.GlobalPosition).Normalized();
		Velocity += knockbackDir * (1 - _knockbackResistance) * knockbackStrength;
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
		throw new NotImplementedException();
	}

}
