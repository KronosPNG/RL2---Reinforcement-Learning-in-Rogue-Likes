using Godot;

public partial class EnemyEntity : Entity<EntityState>, IDamageable, IHasHealth, INavigable, IStateful<EntityState>, IAnimatable<EntityState>
{
	// ---- Node references ----
	public NavigationAgent2D NavAgent{ get; protected set; }
	public EntityAttackManager AttackManager { get; set; }

	// ---- Health properties ----
	[ExportGroup("Health Properties")]
	public float CurrentHealth { get; private set; }
	[Export] public float MaxHealth { get; private set; }
	[Export] public bool IsInvulnerable { get; private set; }
	public bool IsAlive => CurrentHealth > 0;

	// ---- Movement properties ----
	[ExportGroup("Movement Properties")]
	[Export] public float MaxIdleTime { get; private set; } = 3f; // Max time to stay idle before wandering
	private float _knockbackResistance = 0f;

	[Export(PropertyHint.Range, "0.0,1.0,0.01")]
	public float KnockbackResistance
	{
		get => _knockbackResistance;
		private set => _knockbackResistance = Mathf.Clamp(value, 0f, 1f);
	}

	protected sbyte _lastHorizontalFacing = 1;

	// ---- Behaviour Strategies ----
	[ExportGroup("AI Behaviours")]
	[Export] public WanderBehaviour WanderingBehavior;
	[Export] public AggroBehaviour AggroBehavior;
	[Export] public AttackBehaviour AttackBehavior;

	// ---- Visual properties ----
	[ExportGroup("Visual Effects")]
	[Export] public bool IsUnflippable = false;

	// ---- Animation properties ----
	[Export] EntityVisualController<EntityState> VisualController;

	// ---- Timers ----
	protected float _hitStunDuration = .5f;
	protected float _idleToWanderTimer = 0f;
	
	// ---- Knockback Smoothing ----
	private Vector2 _knockbackVelocity = Vector2.Zero;
	private Tween _knockbackTween;

	// ---- Signals ----
	[Signal] public delegate void StateChangedEventHandler(string newState);
	[Signal] public delegate void EntityHealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void EntityDiedEventHandler();

	public override void _Ready()
	{
		base._Ready();
		InitializeBehaviours();

		VisualController = GetNodeOrNull<EntityVisualController<EntityState>>("VisualController");
		
		if (VisualController == null)
			GD.PrintErr("EnemyEntity: Missing EntityVisualController node.");

		if (!IsUnflippable)
		{
			VisualController.UpdateFlip(true);
		}

		// Prevent player from pushing this entity
		MotionMode = MotionModeEnum.Floating;

		CurrentHealth = MaxHealth;
		TransitionToState(EntityState.Idle);

		VisualController.AnimationFinished += OnAnimationFinished;

		TargetType = "Player"; // Enemies target the player by default
	}

	protected override void InitializeNodes()
	{
		base.InitializeNodes();
		NavAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		AttackManager = GetNodeOrNull<EntityAttackManager>("AttackManager");
	}

	protected virtual void InitializeBehaviours()
	{
		// Set default behaviours only if not already set in inspector
		// This prevents overwriting inspector-configured behaviours
		if (WanderingBehavior == null)
			WanderingBehavior = new WanderImmovable();
		if (AggroBehavior == null)
			AggroBehavior = new AggroFollowTarget();
		if (AttackBehavior == null)
			AttackBehavior = new AttackInoffensive();
	}

	// ---- Public methods to change behaviours at runtime ----
	public void SetWanderBehaviour(WanderBehaviour behaviour)
	{
		WanderingBehavior = behaviour;
	}

	public void SetNoticeBehaviour(AggroBehaviour behaviour)
	{
		AggroBehavior = behaviour;
	}

	public void SetAttackBehaviour(AttackBehaviour behaviour)
	{
		AttackBehavior = behaviour;
	}

	// ---- Public methods to modify properties with validation ----
	public void SetKnockbackResistance(float value)
	{
		KnockbackResistance = value; // This will automatically clamp to 0-1 range
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		UpdateAnimationIfNeeded();
	}

	protected override void UpdateTimers(float delta)
	{
		StateTimer += delta;
		_idleToWanderTimer -= delta;
	}

	protected override void UpdateAI(float delta)
	{
		// Find target (usually player). If current target was freed, reacquire.
		if (_target == null || !IsInstanceValid(_target))
		{
			_target = FindTarget();
		}
	}
		

	protected override void UpdateFacing()
	{
		// Don't update facing direction while being knocked back
		if (CurrentState == EntityState.Hit)
			return;

		// Update facing direction based on movement
		if (Velocity.LengthSquared() > 0.1f)
		{
			FacingDirection = Velocity.Normalized();
			if (!Mathf.IsEqualApprox(FacingDirection.X, 0))
				_lastHorizontalFacing = (sbyte)Mathf.Sign(FacingDirection.X);
		}
	}

	public override void HandleStateTransitions()
	{
		switch (CurrentState)
		{
			case EntityState.Idle:
				HandleIdleTransitions();
				break;
			case EntityState.Wandering:
				HandleWanderingTransitions();
				break;
			case EntityState.Aggro:
				HandleAggroTransitions();
				break;
			case EntityState.Attacking:
				HandleAttackingTransitions();
				break;
			case EntityState.Hit:
				HandleHitTransitions();
				break;
			case EntityState.Dying:
				HandleDyingTransitions();
				break;
			case EntityState.Dead:
			default:
				break;
		}
	}

	protected virtual void HandleIdleTransitions()
	{
		if (AggroBehavior.CanSeeTarget(this))
		{
			TransitionToState(EntityState.Aggro);
			return;
		}

		// Random wandering
		if (_idleToWanderTimer <= 0 && GD.Randf() < MaxIdleTime)
		{
			TransitionToState(EntityState.Wandering);
		}
	}

	protected virtual void HandleWanderingTransitions()
	{
		if (AggroBehavior.CanSeeTarget(this))
		{
			TransitionToState(EntityState.Aggro);
			return;
		}

		// Stop wandering after some time
		if (WanderingBehavior.ShouldStopWandering(this))
		{
			TransitionToState(EntityState.Idle);
		}
	}

	protected virtual void HandleAggroTransitions()
	{
		// Don't lose target immediately after being hit - give grace period
		if (AggroBehavior.ShouldLoseTarget(this))
		{
			TransitionToState(EntityState.Idle);
			return;
		}

		// Close enough to attack
		if (AttackBehavior.CanAttack(this) && AttackManager.CanStartAttack())
		{
			// GD.Print("Entity: HandleAggroTransitions() - CanAttack is true");
			TransitionToState(EntityState.AttackPrepare);
		}
	}

	protected virtual void HandleAttackingTransitions()
	{
		// Attack animation will handle transition back via OnAnimationFinished
	}

	protected virtual void HandleHitTransitions()
	{
		if (StateTimer >= _hitStunDuration)
		{
			// If we have a valid target (set when hit), transition to Aggro even if outside detection range
			if (_target != null && IsInstanceValid(_target))
				TransitionToState(EntityState.Aggro);
			else if (AggroBehavior.CanSeeTarget(this))
				TransitionToState(EntityState.Aggro);
			else
				TransitionToState(EntityState.Idle);
		}
	}

	protected virtual void HandleDyingTransitions(){}

	protected override void ApplyMovementByState(float delta)
	{
		Vector2 direction;

		switch (CurrentState)
		{
			case EntityState.Idle:
				Velocity = Vector2.Zero + _knockbackVelocity;
				break;

			case EntityState.Wandering:
				direction = WanderingBehavior.GetWanderDirection(this, delta);
				Velocity = (direction * BaseSpeed * WanderingBehavior.WanderSpeedMultiplier) + _knockbackVelocity;
				break;

			case EntityState.Aggro:
				AggroBehavior.PerformAggroBehaviour(this);
				direction = AggroBehavior.GetChaseDirection(this, delta);
				Velocity = (direction * BaseSpeed * AggroBehavior.ChaseSpeedModifier) + _knockbackVelocity;
				break;

			case EntityState.Attacking:
			case EntityState.AttackPrepare:
			case EntityState.AttackCharging:
				Velocity = Vector2.Zero + _knockbackVelocity;
				break;

			case EntityState.Hit:
			case EntityState.Dying:	
			case EntityState.Dead:		
				Velocity = _knockbackVelocity;
				break;

			
			default:
				Velocity = Vector2.Zero;
				break;
		}

		MoveAndSlide();
	}

	public override void OnEnterState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Idle:
				VisualController.PlayState(state);
				_idleToWanderTimer = WanderingBehavior.WanderCooldown;
				break;

			case EntityState.Wandering:
				VisualController.PlayState(state);
				WanderingBehavior.OnEnterWander(this);
				_idleToWanderTimer = WanderingBehavior.WanderCooldown;
				break;

			case EntityState.Aggro:
				VisualController.PlayState(state);
				AggroBehavior.OnEnterNotice(this);
				AggroBehavior.PerformAggroBehaviour(this);
				break;

			case EntityState.AttackPrepare:
				VisualController.PlayState(state);
				break;

			case EntityState.AttackCharging:
				VisualController.PlayState(state);
				break;

			case EntityState.Attacking:
				// GD.Print("Entity: Entered Attacking state");
				AttackBehavior.OnEnterAttack(this);
				AttackBehavior.PerformAttack(this);
				VisualController.PlayState(state);
				break;

			case EntityState.Hit:
				VisualController.PlayState(state);
				break;

			case EntityState.Dying:
				_hitArea.SetDeferred("monitorable", false);
				_physicalCollision.SetDeferred("disabled", true);
				VisualController.PlayState(state);
				NavAgent.SetDeferred("enabled", false);
				break;

			case EntityState.Dead:
				SetPhysicsProcess(false);
				break;
		}
	}

	public override void OnExitState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Wandering:
				WanderingBehavior.OnExitWander(this);
				break;

			case EntityState.Aggro:
				AggroBehavior.OnExitNotice(this);
				break;

			case EntityState.Attacking:
				AttackBehavior.OnExitAttack(this);
				break;

			case EntityState.Hit:
				break;
		}
	}

	public void UpdateAnimationIfNeeded()
	{
		if (VisualController == null) return;

		if (!IsAlive || CurrentState == EntityState.Dead)
		{
			return;
		}

		VisualController.FacingDirection = FacingDirection;
		VisualController.UpdateAnimationIfNeeded();
	}

	public void OnAnimationFinished()
	{
		
		switch (CurrentState)
		{
			case EntityState.AttackPrepare:
				TransitionToState(EntityState.AttackCharging);
				break;

			case EntityState.AttackCharging:
				TransitionToState(EntityState.Attacking);
				break;

			case EntityState.Attacking:
				if (AggroBehavior.CanSeeTarget(this))
						TransitionToState(EntityState.Aggro);
					else
						TransitionToState(EntityState.Idle);
				break;

			case EntityState.Dying:
				TransitionToState(EntityState.Dead);
				break;
		}
	}


	// ---- IDamageable Implementation ----
	public void ApplyDamage(float amount, Node2D attacker, float knockbackStrength = 0f)
	{
		if (!IsAlive) return;

		if (!IsInvulnerable) CurrentHealth -= amount;

		// Check if damage is lethal
		bool isLethalDamage = CurrentHealth <= 0;

		// Set the attacker as target when hit (aggro on hit regardless of detection range)
		if (attacker != null && !isLethalDamage)
		{
			_target = attacker;
			_lastKnownTargetPosition = attacker.GlobalPosition;
		}

		// Only transition to Hit state if damage is not lethal
		if (!isLethalDamage)
		{
			TransitionToState(EntityState.Hit);
		}

		if (attacker != null)
		{
			ApplyKnockback(attacker.GlobalPosition, knockbackStrength);
		}

		// Emit health changed signal
		EmitSignal(SignalName.EntityHealthChanged, CurrentHealth, MaxHealth);

		if (isLethalDamage)
		{
			CurrentHealth = 0;
			Die();
		}
	}

	protected void ApplyKnockback(Vector2 attackerPosition, float strength)
	{
		// Apply smooth knockback using tween
		// Kill existing tween if one is active
		if (_knockbackTween != null && _knockbackTween.IsValid())
		{
			_knockbackTween.Kill();
		}
		
		// Calculate knockback direction and apply initial velocity
		Vector2 knockbackDir = (GlobalPosition - attackerPosition).Normalized();
		Vector2 knockbackForce = knockbackDir * (1 - KnockbackResistance) * strength*5;
		_knockbackVelocity = knockbackForce;

		GD.Print($"EnemyEntity: Applying knockback with strength {strength}, resistance {KnockbackResistance}, resulting velocity {knockbackForce}");
		
		// Tween knockback velocity back to zero over 0.2 seconds
		_knockbackTween = CreateTween();
		_knockbackTween.SetTrans(Tween.TransitionType.Linear);
		_knockbackTween.TweenProperty(this, "_knockbackVelocity", Vector2.Zero, 0.2f);
	}

	public void Heal(float amount)
	{
		if (IsAlive)
		{
			CurrentHealth += amount;
			if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
		}
	}

	public void Die()
	{
		// GD.Print($"Entity {Name} has died.");
		TransitionToState(EntityState.Dying);
		CurrentHealth = 0;
		VisualController.PlayState(EntityState.Dying);
		EmitSignal(SignalName.EntityDied);
	}

	// ---- IHasHealth Implementation ----

	public void PlayDeathEffect()
	{
		throw new System.NotImplementedException();
	}

	public void PlayHitEffect(Vector2 hitPosition)
	{
		throw new System.NotImplementedException();
	}

	public void ShowDamageNumber(float damage)
	{
		throw new System.NotImplementedException();
	}

	// ---- Utility methods ----

	public void FlipEntity(bool flip)
	{
		if (IsUnflippable) return;

		VisualController.UpdateFlip(flip);
		
		// Flip the hit area and collision shape from the entity's origin
		var hitAreaPos = _hitArea.Position;
		var hitAreaScale = _hitArea.Scale;
		
		hitAreaPos.X = flip ? -Mathf.Abs(hitAreaPos.X) : Mathf.Abs(hitAreaPos.X);
		hitAreaScale.X = flip ? -Mathf.Abs(hitAreaScale.X) : Mathf.Abs(hitAreaScale.X);
		
		_hitArea.Position = hitAreaPos;
		_hitArea.Scale = hitAreaScale;

		var collisionPos = _physicalCollision.Position;
		var collisionScale = _physicalCollision.Scale;
		
		collisionPos.X = flip ? -Mathf.Abs(collisionPos.X) : Mathf.Abs(collisionPos.X);
		collisionScale.X = flip ? -Mathf.Abs(collisionScale.X) : Mathf.Abs(collisionScale.X);
		
		_physicalCollision.Position = collisionPos;
		_physicalCollision.Scale = collisionScale;
		
	}
}
