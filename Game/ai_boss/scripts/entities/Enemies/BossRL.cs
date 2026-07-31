using System;
using Godot;

public partial class BossRL : Entity<BossState>, IDamageable, IHasHealth, INavigable, IStateful<BossState>, IAnimatable<BossState>
{
	public enum BossAttackType
	{
		Melee1,
		Melee2,
		Magic1,
		Magic2
	}

	// --- Health properties ---
	[ExportGroup("Health")]
	public float CurrentHealth { get; private set; }
	[Export] public float MaxHealth {get; set; } = 2000f;
	public bool IsAlive => CurrentHealth > 0;
	public bool IsInvulnerable { get; private set; }
	[Export(PropertyHint.Range, "0,5,0.1")] public float DamageModifier { get; set; } = 4f;

	// --- Movement properties ---
	[ExportGroup("Movement")]
	private Vector2 _movementDirection = Vector2.Zero;
	[Export(PropertyHint.Range, "0,3,0.1")] private float dashModifier = 2f;
	
	// --- Attack Properties ---
	public Vector2 AttackDirection { get; private set; }
	[Export(PropertyHint.Range, "0,1,0.1")] float KnockbackModifier { get; set; } = 0.1f;

	// --- Knockback ---
	protected Area2D _knockbackArea;
	[Export ]protected float _bodyKnockbackStrength = 32f;
	protected Vector2 _knockbackVelocity = Vector2.Zero;
	protected Tween _knockbackTween;

	// --- Node References ---
	public NavigationAgent2D NavAgent { get; private set; }
	public BossAttackManager AttackManager { get; set; }
	public BossVisualController VisualController { get; private set; }

	// --- Target tracking (for deterministic attack aim) ---
	private PlayableCharacter _playerRef;
	private Action<PlayableCharacter> _onPlayerSpawned;

	// --- Timers ---
	public Timer DashTimer { get; set; }
	public Timer DashCooldownTimer { get; set; }
	public Timer CooldownTimer { get; set; }
	public Timer InvulnerabilityTimer { get; set;}

	// ---
	private uint _baseCollisionMask;
	private uint _baseCollisionLayer;

	// ---- Signals ----
	[Signal] public delegate void StateChangedEventHandler(string newState);

	public override void _Ready()
	{
		base._Ready();

		_knockbackArea = GetNode<Area2D>("KnockbackArea");
		VisualController = GetNodeOrNull<BossVisualController>("VisualController");
		AttackManager = GetNodeOrNull<BossAttackManager>("AttackManager");
		DashTimer = GetNodeOrNull<Timer>("Timers/DashTimer");
		DashCooldownTimer = GetNodeOrNull<Timer>("Timers/DashCooldownTimer");
		CooldownTimer = GetNodeOrNull<Timer>("Timers/CooldownTimer");
		InvulnerabilityTimer = GetNodeOrNull<Timer>("Timers/InvulnerabilityTimer");

		_knockbackArea.BodyEntered += PushEnemies;

		VisualController.AnimationFinished += OnAnimationFinished;

		InvulnerabilityTimer.Timeout += InvulnerabilityEnd;
		DashTimer.Timeout += () => TransitionToState(BossState.Cooldown);

		DashTimer.Timeout += () => DashCooldownTimer.Start();
		CooldownTimer.Timeout += ()  => TransitionToState(BossState.Idle);
		
		_baseCollisionMask = CollisionMask;
		_baseCollisionLayer = CollisionLayer;

		CurrentHealth = MaxHealth;

		_onPlayerSpawned = (player) => _playerRef = player;
		EventBus.OnPlayerSpawned += _onPlayerSpawned;

		EventBus.RaiseBossSpawnedEvent(this);
	}

	public override void _ExitTree()
	{
		EventBus.OnPlayerSpawned -= _onPlayerSpawned;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		UpdateFacing();

		ApplyMovementByState((float)delta);

		UpdateAnimationIfNeeded();

		UpdateTimers((float)delta);
	}

	public override void TransitionToState(BossState newState)
	{
		if (newState == BossState.Dying || newState == BossState.Dead)
		{
			GD.Print($"[BOSS] Current state: {newState}");
			base.TransitionToState(newState);
			return;
		}

		if (CurrentState == BossState.Dashing && DashTimer.TimeLeft > 0)
			return;

		if (CurrentState == BossState.Cooldown && CooldownTimer.TimeLeft > 0)
			return;

		GD.Print($"[BOSS] Current state: {newState}");
		base.TransitionToState(newState);
	}

	public override void OnEnterState(BossState state)
	{
		VisualController.PlayState(state);

		switch (state)
		{
			case BossState.Idle:
				VisualController.IsMoving = false;
				
				break;
			
			case BossState.Dashing:
				DashTimer.Start();
				VisualController.IsMoving = true;

				_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
				_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);
				IsInvulnerable = true;
				
				// disable collision with player
				CollisionLayer = _baseCollisionLayer & ~2u; // player on layer 2;
				CollisionMask = _baseCollisionMask & ~2u & ~4u; // ignore player (layer 2) and player attacks (layer 3)
				break;

			case BossState.Walking:
				VisualController.IsMoving = true;
				break;

			case BossState.Attacking:
				AttackManager.Attack();
				break;

			case BossState.Cooldown:
				IsInvulnerable = false;
				CooldownTimer.Start();
				break;

			case BossState.Hit:
				break;

			case BossState.Dead:
				_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
				_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

				SetPhysicsProcess(false);
				SetProcess(false);
				EventBus.RaiseBossKilledEvent();
				break;
		}
	}

	public override void OnExitState(BossState state)
	{
		if(state == BossState.Dashing)
		{
			// re-enable collision with player
			CollisionLayer = _baseCollisionLayer;
			CollisionMask = _baseCollisionMask;
		}
	}

	public override void HandleStateTransitions() {}

	public void UpdateAnimationIfNeeded()
	{
		VisualController.UpdateAnimationIfNeeded();
	}

	protected override void ApplyMovementByState(float delta)
	{
		switch (CurrentState)
		{
			case BossState.Idle:
				Velocity = _knockbackVelocity;
				MoveAndSlide();
				break;
				
			case BossState.Attacking:
				Velocity = _knockbackVelocity;
				MoveAndSlide();
				break;

			case BossState.Walking:
			case BossState.Cooldown:
			case BossState.Hit:
				Velocity = _movementDirection * BaseSpeed  + _knockbackVelocity;
				MoveAndSlide();
				break;

			case BossState.Dashing:
				Velocity = _movementDirection * BaseSpeed * dashModifier + _knockbackVelocity;
				MoveAndSlide();
				break;

			case BossState.AttackPrepare:
				Velocity = _knockbackVelocity;
				MoveAndSlide();
				break;
		}
	}

	protected override void UpdateAI(float delta)
	{
		return;
	}

	// Mirrors BossAttackManager.CanStartAttack(): a hard, mechanical readiness check
	// used both to gate ApplyAction() and to tell the AI server (via DYNAMIC_STATE)
	// whether a dash request would actually be honored.
	public bool CanDash()
	{
		return DashCooldownTimer.IsStopped();
	}

	public void ApplyAction(AiAction action)
	{
		switch (CurrentState)
		{
			case BossState.Attacking:
			case BossState.AttackPrepare:
			case BossState.Cooldown:
			case BossState.Dashing:
			case BossState.Dying:
			case BossState.Dead:
				return;
		}

		ActionType type = (ActionType)action.ActionId;
		Vector2 direction = new Vector2(action.X, action.Y);
		BossAttackType attack = BossAttackType.Melee1;

		switch (type)
		{
			case ActionType.Idle:
				_movementDirection = Vector2.Zero;
				TransitionToState(BossState.Idle);
				return;

			case ActionType.Dash:
				if (!CanDash())
				{
					TransitionToState(BossState.Idle);
					return;
				}

				_movementDirection = direction;
				TransitionToState(BossState.Dashing);
				return;

			case ActionType.Walk:
				_movementDirection = direction;
				TransitionToState(BossState.Walking);
				return;

			case ActionType.Melee1:
				attack = BossAttackType.Melee1;
				break;
			
			case ActionType.Melee2:
				attack = BossAttackType.Melee2;
				break;

			case ActionType.Magic1:
				attack = BossAttackType.Magic1;
				break;
			
			case ActionType.Magic2:
				attack = BossAttackType.Magic2;
				break;
		}

		// In case it's an attack

		// Check readiness for this candidate BEFORE committing it to CurrentAttack — a
		// rejected request (still on cooldown, or another attack still in flight) must
		// not overwrite the attack type that AutoInterrupt() will later look up to know
		// which hitbox to close.
		if (!AttackManager.CanStartAttack(attack))
		{
			TransitionToState(BossState.Idle);
			return;
		}

		AttackManager.CurrentAttack = attack;

		// Aim is computed from known game state (player position/velocity), not the
		// policy's movement output — the network doesn't need to learn geometry the
		// game already knows exactly, and this decouples aim from the movement head.
		Vector2 aimDirection = ComputeAimDirection(attack);

		VisualController.CurrentAttackType = attack;
		VisualController.FacingDirection = aimDirection;

		_movementDirection = Vector2.Zero;
		AttackDirection = aimDirection;
		UpdateFacing();

		TransitionToState(BossState.AttackPrepare);
		return;
	}

	// Determines where an attack should aim. Melee/instant attacks (Windup = 0, no
	// travel time) aim straight at the player's current position — there's no gap
	// for them to have moved between the decision and the hit landing. Magic2 is the
	// one attack with real projectile travel time (see ProjectileAttack.ProjectileSpeed),
	// so it leads the shot based on the player's current velocity.
	private Vector2 ComputeAimDirection(BossAttackType attack)
	{
		// OnPlayerSpawned fires once, when the player character is created — typically
		// well before the boss (spawned only once the boss room is entered) exists to
		// subscribe. Self-heal via the "Player" group (same pattern MainHandler/
		// TrainingHandler use) instead of relying solely on having caught that event.
		if (_playerRef == null)
		{
			var playerNodes = GetTree().GetNodesInGroup("Player");
			if (playerNodes.Count > 0)
				_playerRef = playerNodes[0] as PlayableCharacter;
		}

		if (_playerRef == null)
			return AttackDirection.LengthSquared() > 0.000001f ? AttackDirection : Vector2.Right;

		if (attack == BossAttackType.Magic2 && AttackManager.MagicAttack2 is ProjectileAttack projectileAttack)
		{
			return ComputeLeadDirection(GlobalPosition, _playerRef.GlobalPosition, _playerRef.Velocity, projectileAttack.ProjectileSpeed);
		}

		Vector2 toPlayer = _playerRef.GlobalPosition - GlobalPosition;
		return toPlayer.LengthSquared() > 0.000001f ? toPlayer.Normalized() : Vector2.Right;
	}

	// Closed-form intercept solve: find the smallest positive t such that a projectile
	// fired now at projectileSpeed reaches the target's straight-line-extrapolated
	// position at time t. Falls back to aiming at the target's current position if no
	// interception is possible (e.g. target outrunning the projectile).
	private static Vector2 ComputeLeadDirection(Vector2 shooterPos, Vector2 targetPos, Vector2 targetVelocity, float projectileSpeed)
	{
		Vector2 toTarget = targetPos - shooterPos;

		if (projectileSpeed <= 0.01f)
			return toTarget.LengthSquared() > 0.000001f ? toTarget.Normalized() : Vector2.Right;

		float a = targetVelocity.LengthSquared() - projectileSpeed * projectileSpeed;
		float b = 2f * toTarget.Dot(targetVelocity);
		float c = toTarget.LengthSquared();

		float interceptTime = 0f;

		if (Mathf.Abs(a) < 0.0001f)
		{
			// Target speed ≈ projectile speed: quadratic degenerates to linear.
			if (Mathf.Abs(b) > 0.0001f)
				interceptTime = -c / b;
		}
		else
		{
			float discriminant = b * b - 4f * a * c;
			if (discriminant >= 0f)
			{
				float sqrtDisc = Mathf.Sqrt(discriminant);
				float t1 = (-b + sqrtDisc) / (2f * a);
				float t2 = (-b - sqrtDisc) / (2f * a);

				float best = float.MaxValue;
				if (t1 > 0f) best = t1;
				if (t2 > 0f && t2 < best) best = t2;

				interceptTime = best == float.MaxValue ? 0f : best;
			}
			// discriminant < 0: no interception solution — fall back to current position (interceptTime stays 0)
		}

		Vector2 predictedPosition = targetPos + targetVelocity * Mathf.Max(interceptTime, 0f);
		Vector2 aimVector = predictedPosition - shooterPos;

		return aimVector.LengthSquared() > 0.000001f ? aimVector.Normalized() : Vector2.Right;
	}

	protected override void UpdateFacing()
	{
		Vector2 facingSource = (CurrentState == BossState.AttackPrepare || CurrentState == BossState.Attacking)
			? AttackDirection
			: _movementDirection;

		if (!Mathf.IsEqualApprox(facingSource.X, 0) || !Mathf.IsEqualApprox(facingSource.Y, 0))
			VisualController.FacingDirection = facingSource;
	}

	protected override void UpdateTimers(float delta)
	{
		if(InvulnerabilityTimer.TimeLeft <= 0)
		{
			_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, true);
			_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, true);
		}
	}



	public void ApplyDamage(float damage, Node2D attacker, float knockbackStrength)
	{
		if (IsInvulnerable) return;

		IsInvulnerable = true;
		InvulnerabilityTimer.Start();
		_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

		var realDamage = damage * DamageModifier;

		CurrentHealth -= realDamage;
		EventBus.RaiseBossDamaged(realDamage);

		GD.Print($"[BOSS DAMAGED] Health: {CurrentHealth}/{MaxHealth}");
		
		// if (CurrentHealth > 0)
		// {
		// 	TransitionToState(BossState.Hit);
		// }

		if (attacker != null)
		{
			ApplyKnockback(attacker.GlobalPosition, knockbackStrength);
		}

		if (CurrentHealth <= 0)
		{
			CurrentHealth = 0;
			Die();
		}
	}

	public void InvulnerabilityEnd()
	{
		_hitArea.SetDeferred(Area2D.PropertyName.Monitoring, true);
		_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, true);
		IsInvulnerable = false;
	}

	public void Die()
	{
		if (CurrentState == BossState.Dead) return;
		CurrentHealth = 0;
		TransitionToState(BossState.Dead);
	}

	public void Heal(float amount)
	{
		throw new System.NotImplementedException();
	}


	public void OnAnimationFinished()
	{
		switch (CurrentState)
		{
			case BossState.AttackPrepare:
				TransitionToState(BossState.Attacking);
				break;
			
			case BossState.Attacking:
				TransitionToState(BossState.Idle);
				break;
		}
	}

	public void PlayDeathEffect()
	{
		return;
	}

	public void PlayHitEffect(Vector2 hitPosition)
	{
		return;
	}

	public void ShowDamageNumber(float damage)
	{
		return;
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
		Vector2 knockbackForce = knockbackDir * KnockbackModifier * strength*5;
		_knockbackVelocity = knockbackForce;
		
		// Tween knockback velocity back to zero over 0.2 seconds
		_knockbackTween = CreateTween();
		_knockbackTween.SetTrans(Tween.TransitionType.Linear);
		_knockbackTween.TweenProperty(this, "_knockbackVelocity", Vector2.Zero, 0.2f);
	}

	public override void ApplyImpulse(Vector2 direction, float speed, float duration)
	{
		if (_knockbackTween != null && _knockbackTween.IsValid())
			_knockbackTween.Kill();
		_knockbackVelocity = direction.Normalized() * speed;
		_knockbackTween = CreateTween();
		_knockbackTween.SetTrans(Tween.TransitionType.Linear);
		_knockbackTween.TweenProperty(this, "_knockbackVelocity", Vector2.Zero, duration);
	}

	public void PushEnemies(Node Body)
	{
		var player = Body as PlayableCharacter;

		player.ApplyKnockback(GlobalPosition, _bodyKnockbackStrength);
	}
}
