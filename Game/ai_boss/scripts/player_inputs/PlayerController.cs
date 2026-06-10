using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerController : PlayableCharacter, IDamageable, IHasHealth, IStateful<EntityState>, IAnimatable<EntityState>
{

	public override void _PhysicsProcess(double delta)
	{
		// Check for other commands
		HandleCommands();

		// Read input direction
		_movementDirection = ReadDirection();

		// Handle combat input (only when not dodging)
		HandleCombatInput();

		// Handle consumable input
		HandleConsumableInput();

		base._PhysicsProcess(delta);
	}

	protected override void UpdateAI(float delta)
	{
		// No AI for player - all behavior is input-driven
		return;
	}

	private static Vector2 ReadDirection()
	{
		return Input.GetVector("move_left", "move_right", "move_up", "move_down"); // get already normalized direction vector
	}

	protected void HandleCommands()
	{
		if (Input.IsActionJustPressed("pause"))
		{
			EventBus.RaiseGamePaused();
		}
	}

	public override Vector2 GetAimDirection()
	{
		Vector2 mousePos = GetGlobalMousePosition();
		Vector2 direction = (mousePos - GlobalPosition).Normalized();
		if (direction.LengthSquared() <= 0.000001f) direction = Vector2.Right;

		return direction;
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
	public override void HandleStateTransitions()
	{
		switch (CurrentState)
		{
			case EntityState.Idle:
			case EntityState.Walking:
				HandleIdleTransitions();
				break;

			case EntityState.DodgePrep:
				HandleDodgePrepTransitions();
				break;

			case EntityState.Hit:
				HandleHitTransitions();
				break;

			case EntityState.AttackCharging:
				HandleChargingTransitions();
				break;

			case EntityState.ConsumableCharging:
				HandleConsumableChargingTransitions();
				break;

			default:
				// Other states (Dodge, Attacking, Dead) have no transitions here
				break;
		}
	}

	private void HandleIdleTransitions()
	{
		// Dodge starts on just-pressed regardless of whether there is movement
		if (Input.IsActionJustPressed("dodge"))
		{
			_dodgeInputTimer = DodgeInputLeniency;
			TransitionToState(EntityState.DodgePrep);

		}
		else
		{
			// Normal movement -> walking vs idle
			if (_movementDirection.Length() > 0)
				TransitionToState(EntityState.Walking);
			else
				TransitionToState(EntityState.Idle);
		}
	}

	private void HandleDodgePrepTransitions()
	{
		_dodgeInputTimer -= GetProcessDeltaTime();

		// If player pressed any movement key *this frame*, accept the (combined) held input immediately
		if (MovementJustPressed() != Vector2.Zero)
		{
			_dodgeDirection = ReadDirection();
			EventBus.RaisePlayerDodged(_dodgeDirection);
			TransitionToState(EntityState.Dodging);
			return;
		}

		// otherwise, wait for leniency timer to expire and then fall back to held direction or facing
		if (_dodgeInputTimer <= 0)
		{
			// Sample input now so simultaneous presses are respected
			Vector2 dodgeInput = ReadDirection();

			if (dodgeInput == Vector2.Zero)
			{
				TransitionToState(EntityState.Idle);
				return;
			}

			_dodgeDirection = dodgeInput;
			EventBus.RaisePlayerDodged(_dodgeDirection);
			TransitionToState(EntityState.Dodging);
		}
	}

	private void HandleChargingTransitions()
	{
		// Allow dodging while charging - this cancels the charge
		if (Input.IsActionJustPressed("dodge"))
		{
			// GD.Print("Dodge input while charging - cancelling charge");
			CancelChargingAttack();
			TransitionToState(EntityState.DodgePrep);
		}
	}

	private void HandleConsumableChargingTransitions()
	{
		// Allow dodging while charging consumable - this cancels the charge
		if (Input.IsActionJustPressed("dodge"))
		{
			CancelChargingConsumable();
			TransitionToState(EntityState.DodgePrep);
		}
	}

	private void HandleHitTransitions()
	{
		StateTimer += (float)GetProcessDeltaTime();
		if (StateTimer >= HitStunDuration)
		{
			// decide whether to be walking or idle after hit stun
			Vector2 currentInput = ReadDirection();
			if (currentInput.Length() > 0)
				TransitionToState(EntityState.Walking);
			else
				TransitionToState(EntityState.Idle);
		}
	}

	// --- Combat Input Handling -----------------
	private void HandleCombatInput()
	{

		switch (CurrentState)
		{
			// Don't allow attacks while dodging, in dodge preparation, or already attacking
			case EntityState.DodgePrep:
			case EntityState.Dodging:
			case EntityState.Attacking:
			case EntityState.Hit:
			case EntityState.Dead:
				return;

			case EntityState.AttackCharging:
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
			TransitionToState(EntityState.Attacking);
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
				TransitionToState(EntityState.Attacking);
				ExecuteChargedAttack(_isHeavyCharge);
			}
			else
			{
				// Charge was too short, cancel and return to appropriate state
				EquippedWeapon.CancelCharge();
				Vector2 currentInput = ReadDirection();
				TransitionToState(currentInput.Length() > 0 ? EntityState.Walking : EntityState.Idle);
			}
		}
	}

	private void StartChargingAttack(bool isHeavy)
	{
		_isChargingAttack = true;
		_isHeavyCharge = isHeavy;
		TransitionToState(EntityState.AttackCharging);
		EquippedWeapon.StartCharge(GetGlobalMousePosition(), isHeavy);
	}

	private void ExecuteChargedAttack(bool isHeavy)
	{
		if (isHeavy)
			EquippedWeapon.ExecuteChargedHeavy(GetGlobalMousePosition());
		else
			EquippedWeapon.ExecuteChargedLight(GetGlobalMousePosition());
	}

	// --- Consumable Input Handling -------------
	private void HandleConsumableInput()
	{
		switch (CurrentState)
		{
			// Don't allow consumable use while dodging, in dodge prep, attacking, or dead
			case EntityState.DodgePrep:
			case EntityState.Dodging:
			case EntityState.Attacking:
			case EntityState.AttackCharging:
			case EntityState.Hit:
			case EntityState.Dead:
			case EntityState.ConsumableUse:
				return;

			case EntityState.ConsumableCharging:
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
				TransitionToState(EntityState.ConsumableUse);
				EquippedConsumable.ExecuteCharged();
				EventBus.RaiseConsumableUsed();
			}
			else
			{
				// Charge was too short, cancel animation and return to appropriate state
				EquippedConsumable.CancelCharge();
				_isChargingConsumable = false;
				Vector2 currentInput = ReadDirection();
				TransitionToState(currentInput.Length() > 0 ? EntityState.Walking : EntityState.Idle);
			}
		}
	}
}
