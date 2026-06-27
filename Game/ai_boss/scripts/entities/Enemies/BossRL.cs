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
	public bool IsAlive => CurrentHealth > 0 || CurrentState != BossState.Dead;
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

	// --- Timers ---
	public Timer DashTimer { get; set; }
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
		CooldownTimer = GetNodeOrNull<Timer>("Timers/CooldownTimer");
		InvulnerabilityTimer = GetNodeOrNull<Timer>("Timers/InvulnerabilityTimer");

		_knockbackArea.BodyEntered += PushEnemies;

		VisualController.AnimationFinished += OnAnimationFinished;

		InvulnerabilityTimer.Timeout += InvulnerabilityEnd;
		DashTimer.Timeout += () => TransitionToState(BossState.Cooldown);
		CooldownTimer.Timeout += ()  => TransitionToState(BossState.Idle);
		
		_baseCollisionMask = CollisionMask;
		_baseCollisionLayer = CollisionLayer;

		CurrentHealth = MaxHealth;

		EventBus.RaiseBossSpawnedEvent(this);
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

	public void ApplyAction(AiAction action)
	{
		switch (CurrentState)
		{
			case BossState.Attacking:
			case BossState.AttackPrepare:
			case BossState.Cooldown:
			case BossState.Dashing:
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

		AttackManager.CurrentAttack = attack;

		if (!AttackManager.CanStartAttack())
		{
			TransitionToState(BossState.Idle);
			return;
		}

		VisualController.CurrentAttackType = attack;
		VisualController.FacingDirection = direction;

		_movementDirection = Vector2.Zero;
		AttackDirection = direction;
		UpdateFacing();
		
		TransitionToState(BossState.AttackPrepare);
		return;
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
