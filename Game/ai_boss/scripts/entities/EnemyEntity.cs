using Godot;

public partial class EnemyEntity : Entity, IDamageable, IHasHealth, INavigable
{
	// ---- Node references ----
	public NavigationAgent2D NavAgent{ get; protected set; }
	public WeaponEntity Weapon { get; set; }

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
	protected Color OriginalModulate;
	[Export] public bool FlipSpriteHorizontally = false;
	[Export] public bool IsUnflippable = false;
	[Export] public VisualFlash _damageEffect = new();
	[Export] public VisualFading _deathEffect = new();

	// ---- Death color transition ----
	[Export] public float DeathColorTransitionDuration { get; set; } = 1.0f;
	protected float _deathColorTimer = 0f;
	protected bool _isTransitioningToDeath = false;

	// ---- Timers ----
	protected float _hitStunDuration = 0.25f;
	protected float _idleToWanderTimer = 0f;

	// ---- Signals ----
	[Signal] public delegate void EntityHealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void EntityDiedEventHandler();

	public override void _Ready()
	{
		base._Ready();
		InitializeBehaviours();
		InitializeEntity();
		InitializeVisuals();


		if (_sprite != null)
			_sprite.AnimationFinished += OnAnimationFinished;

		if (FlipSpriteHorizontally)
		{
			_sprite.FlipH = true;
		}

		// Prevent player from pushing this entity
		MotionMode = MotionModeEnum.Floating;

		CurrentHealth = MaxHealth;
		TransitionToState(EntityState.Idle);
	}

	protected override void InitializeNodes()
	{
		base.InitializeNodes();
		NavAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		Weapon = GetNodeOrNull<WeaponEntity>("Weapon");
	}

	protected virtual void InitializeBehaviours()
	{
		// Set default behaviours only if not already set in inspector
		// This prevents overwriting inspector-configured behaviours
		if (WanderingBehavior == null)
			WanderingBehavior = new WanderImmovable();
		if (AggroBehavior == null)
			AggroBehavior = new AggroFollowGaze();
		if (AttackBehavior == null)
			AttackBehavior = new AttackInoffensive();
	}

	protected virtual void InitializeEntity()
	{
		// Override in derived classes
		OriginalModulate = _sprite.Modulate;
	}

	protected virtual void InitializeVisuals()
	{
		if (_sprite != null)
		{
			_damageEffect.InitializeVisuals(_sprite);
			_deathEffect.InitializeVisuals(_sprite);
		}
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
		if (!IsAlive && _currentState != EntityState.Dead)
			return;

		base._PhysicsProcess(delta);
	}

	protected override void UpdateTimers(float delta)
	{
		_stateTimer += delta;
		_idleToWanderTimer -= delta;
		_damageEffect.UpdateTimer(delta);

		// Update death color transition
		if (_isTransitioningToDeath)
		{
			_deathEffect.UpdateTimer(delta);
		}
	}

	protected override void UpdateAI(float delta)
	{
		// Find target (usually player). If current target was freed, reacquire.
		if (_target == null || !IsInstanceValid(_target))
		{
			_target = FindTarget();
		}

		// Update facing direction based on movement
		if (Velocity.LengthSquared() > 0.1f)
		{
			FacingDirection = Velocity.Normalized();
			if (!Mathf.IsEqualApprox(FacingDirection.X, 0))
				_lastHorizontalFacing = (sbyte)Mathf.Sign(FacingDirection.X);
		}
	}

	protected override Node2D FindTarget()
	{
		// Find player node - override in derived classes for different targeting logic
		return GetTree().GetFirstNodeInGroup("Player") as Node2D;
	}

	protected override void HandleStateTransitions(float delta)
	{
		switch (_currentState)
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
		if (AttackBehavior.CanAttack(this) && Weapon.CanStartAttack())
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
		if (_stateTimer >= _hitStunDuration)
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
		switch (_currentState)
		{
			case EntityState.Idle:
				Velocity = Vector2.Zero;
				break;

			case EntityState.Wandering:
				Velocity = WanderingBehavior.GetWanderVelocity(this, delta);
				break;

			case EntityState.Aggro:
				Velocity = AggroBehavior.GetChaseVelocity(this, delta);
				AggroBehavior.PerformAggroBehaviour(this);
				break;

			case EntityState.Attacking:
			case EntityState.AttackPrepare:
			case EntityState.AttackCharge:
				Velocity = AttackBehavior.GetAttackVelocity(this, delta);
				break;

			case EntityState.Hit:
			case EntityState.Dying:
			case EntityState.Dead:
			default:
				Velocity = Vector2.Zero;
				break;
		}

		MoveAndSlide();
	}

	protected override void OnEnterState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Idle:
				_idleToWanderTimer = WanderingBehavior.WanderCooldown;
				break;

			case EntityState.Wandering:
				WanderingBehavior.OnEnterWander(this);
				_idleToWanderTimer = WanderingBehavior.WanderCooldown;
				break;

			case EntityState.Aggro:
				AggroBehavior.OnEnterNotice(this);
				AggroBehavior.PerformAggroBehaviour(this);
				break;

			case EntityState.Attacking:
				// GD.Print("Entity: Entered Attacking state");
				AttackBehavior.OnEnterAttack(this);
				AttackBehavior.PerformAttack(this);
				break;

			case EntityState.Hit:
				_sprite.Play(GetAnimationForState(state));
				// Apply damage flash effect
				_damageEffect.PlayEffect(_sprite);
				break;

			case EntityState.Dying:
				_hitArea.SetDeferred("monitoring", false);
				_wallCollision.SetDeferred("disabled", true);
				break;

			case EntityState.Dead:
				// Darken the sprite 
				_deathEffect.PlayEffect(_sprite);
				break;
		}
	}

	protected override void OnExitState(EntityState state)
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
				// Clear damage flash effect
				_damageEffect.ClearEffect(_sprite);
				break;
		}
	}

	protected override void UpdateAnimationIfNeeded()
	{
		if (_sprite == null) return;

		if (!IsAlive || _currentState == EntityState.Dead)
		{
			return;
		}

		bool stateChanged = _currentState != _previousState;

		// Update sprite facing - only update if we're in a state where movement affects facing
		// Don't update facing during Hit, Dying, or Attacking states to preserve direction
		switch (_currentState)
		{
			case EntityState.Idle:
			case EntityState.Wandering:
			case EntityState.Aggro:
				// These states allow facing updates
				if (!Mathf.IsEqualApprox(Velocity.X, 0))
				{
					FlipEntity(Velocity.X < 0);
					_lastHorizontalFacing = (sbyte)(Velocity.X < 0 ? -1 : 1);
				}
				else
				{
					// When not moving, check if we have a facing direction set
					if (!Mathf.IsEqualApprox(FacingDirection.X, 0))
					{
						// Use FacingDirection to determine sprite flip
						FlipEntity(FacingDirection.X < 0);
						_lastHorizontalFacing = (sbyte)(FacingDirection.X < 0 ? -1 : 1);
					}
					else
					{
						// For entities that don't move (like dummies), respect the FlipSpriteHorizontally setting
						// Otherwise use the last horizontal facing direction
						FlipEntity(FlipSpriteHorizontally || _lastHorizontalFacing < 0);
					}
				}
				break;
			default:
				// Preserve current facing
				break;
		}

		// Set animation based on state
		string targetAnimation = GetAnimationForState(_currentState);

		if (stateChanged || _sprite.Animation != targetAnimation)
		{
			if (_sprite.SpriteFrames.HasAnimation(targetAnimation))
				_sprite.Play(targetAnimation);
		}
	}

	protected virtual string GetAnimationForState(EntityState state)
	{
		return state switch
		{
			EntityState.Idle => "idle",
			EntityState.Wandering => "walk",
			EntityState.Aggro => AggroBehavior.animationName,
			EntityState.AttackPrepare => "attack_prepare",
			EntityState.AttackCharge => "attack_charge",
			EntityState.Attacking => "attack",
			EntityState.Hit => "hit",
			EntityState.Dying => "die",
			EntityState.Dead => "die",
			_ => "idle"
		};
	}

	protected override void OnAnimationFinished()
	{
		string animName = _sprite.Animation;
		// GD.Print($"Entity: OnAnimationFinished() - Animation: {animName}, State: {_currentState}");

		switch (_currentState)
		{
			case EntityState.AttackPrepare:
				if (animName == "attack_prepare")
				{
					// GD.Print("Entity: Transitioning AttackPrepare -> AttackCharge");
					TransitionToState(EntityState.AttackCharge);
				}
				break;

			case EntityState.AttackCharge:
				if (animName == "attack_charge")
				{
					// GD.Print("Entity: Transitioning AttackCharge -> Attacking");
					TransitionToState(EntityState.Attacking);
				}
				break;

			case EntityState.Attacking:
				if (animName == "attack")
				{
					// GD.Print("Entity: Attack animation finished, transitioning back...");
					if (AggroBehavior.CanSeeTarget(this))
						TransitionToState(EntityState.Aggro);
					else
						TransitionToState(EntityState.Idle);
				}
				break;

			case EntityState.Dying:
				if (animName == "die")
				{
					TransitionToState(EntityState.Dead);
				}
				break;
		}
	}


	// ---- IDamageable Implementation ----
	public void ApplyDamage(float amount, Node2D attacker, float knockbackStrength = 400f)
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
			// Apply knockback or other effects based on the attacker
			Vector2 knockbackDir = (GlobalPosition - attacker.GlobalPosition).Normalized();
			Velocity += knockbackDir * (1 - KnockbackResistance) * knockbackStrength;
			MoveAndSlide();
		}

		// Emit health changed signal
		EmitSignal(SignalName.EntityHealthChanged, CurrentHealth, MaxHealth);

		if (isLethalDamage)
		{
			CurrentHealth = 0;
			Die();
		}
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
		_sprite.Play("die");
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

		_sprite.FlipH = flip;
		
		// Flip the hit area and collision shape from the entity's origin
		var hitAreaPos = _hitArea.Position;
		var hitAreaScale = _hitArea.Scale;
		
		hitAreaPos.X = flip ? -Mathf.Abs(hitAreaPos.X) : Mathf.Abs(hitAreaPos.X);
		hitAreaScale.X = flip ? -Mathf.Abs(hitAreaScale.X) : Mathf.Abs(hitAreaScale.X);
		
		_hitArea.Position = hitAreaPos;
		_hitArea.Scale = hitAreaScale;

		var collisionPos = _wallCollision.Position;
		var collisionScale = _wallCollision.Scale;
		
		collisionPos.X = flip ? -Mathf.Abs(collisionPos.X) : Mathf.Abs(collisionPos.X);
		collisionScale.X = flip ? -Mathf.Abs(collisionScale.X) : Mathf.Abs(collisionScale.X);
		
		_wallCollision.Position = collisionPos;
		_wallCollision.Scale = collisionScale;
		
	}
}
