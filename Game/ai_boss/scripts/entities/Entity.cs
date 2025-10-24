using Godot;

public partial class Entity : CharacterBody2D, IEntity, IDamageable
{
	// ---- Node references ----
	protected AnimatedSprite2D _sprite;
	protected CollisionShape2D _collisionShape;
	protected Area2D _hitArea;
	protected NavigationAgent2D _navAgent;
	public WeaponEntity Weapon { get; set; }

	// ---- Health properties ----
	[ExportGroup("Health Properties")]
	public float CurrentHealth { get; private set; }
	[Export] public float MaxHealth { get; private set; }
	[Export] public bool IsInvulnerable { get; private set; }
	public bool IsAlive => CurrentHealth > 0;

	// ---- Movement properties ----
	[ExportGroup("Movement Properties")]
	[Export] public float BaseSpeed { get; private set; } = 1000f;
	[Export] public float MaxIdleTime { get; private set; } = 3f; // Max time to stay idle before wandering
	private float _knockbackResistance = 0f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")]
	public float KnockbackResistance
	{
		get => _knockbackResistance;
		private set => _knockbackResistance = Mathf.Clamp(value, 0f, 1f);
	}

	public Vector2 FacingDirection { get; set; } = Vector2.Right;
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

	// ---- State Machine ----
	public enum EntityState
	{
		Idle,
		Wandering,
		Aggro,
		AttackPrepare,
		AttackCharge,
		Attacking,
		Hit,
		Dying,
		Dead
	}

	protected EntityState _currentState = EntityState.Idle;
	protected EntityState _previousState = EntityState.Idle;

	// ---- Timers ----
	protected float _stateTimer = 0f;
	protected float _hitStunDuration = 0.25f;
	protected float _idleToWanderTimer = 0f;

	// ---- AI Properties ----
	protected Node2D _target;
	protected Vector2 _lastKnownTargetPosition = Vector2.Zero;

	// ---- Signals ----
	[Signal] public delegate void EntityHealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void EntityDiedEventHandler();
	[Signal] public delegate void EntityStateChangedEventHandler(string newState);

	public override void _Ready()
	{
		InitializeNodes();
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

	protected virtual void InitializeNodes()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
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

		UpdateTimers((float)delta);
		UpdateAI((float)delta);
		HandleStateTransitions((float)delta);
		ApplyMovementByState((float)delta);
		UpdateAnimationIfNeeded();
	}

	protected virtual void UpdateTimers(float delta)
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

	protected virtual void UpdateAI(float delta)
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

	protected virtual Node2D FindTarget()
	{
		// Find player node - override in derived classes for different targeting logic
		return GetTree().GetFirstNodeInGroup("Player") as Node2D;
	}

	protected virtual void HandleStateTransitions(float delta)
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
			GD.Print("Entity: HandleAggroTransitions() - CanAttack is true");
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

	protected virtual void ApplyMovementByState(float delta)
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
				Velocity = Vector2.Zero;
				break;
		}

		MoveAndSlide();
	}

	protected virtual void TransitionToState(EntityState newState)
	{
		if (_currentState == newState) return;

		OnExitState(_currentState);
		_previousState = _currentState;
		_currentState = newState;
		_stateTimer = 0f;
		OnEnterState(newState);

		EmitSignal(SignalName.EntityStateChanged, newState.ToString());
	}

	protected virtual void OnEnterState(EntityState state)
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
				GD.Print("Entity: Entered Attacking state");
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
				_collisionShape.SetDeferred("disabled", true);
				break;

			case EntityState.Dead:
				// Darken the sprite 
				_deathEffect.PlayEffect(_sprite);
				break;
		}
	}

	protected virtual void OnExitState(EntityState state)
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

	protected virtual void UpdateAnimationIfNeeded()
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

	protected virtual void OnAnimationFinished()
	{
		string animName = _sprite.Animation;
		GD.Print($"Entity: OnAnimationFinished() - Animation: {animName}, State: {_currentState}");

		switch (_currentState)
		{
			case EntityState.AttackPrepare:
				if (animName == "attack_prepare")
				{
					GD.Print("Entity: Transitioning AttackPrepare -> AttackCharge");
					TransitionToState(EntityState.AttackCharge);
				}
				break;

			case EntityState.AttackCharge:
				if (animName == "attack_charge")
				{
					GD.Print("Entity: Transitioning AttackCharge -> Attacking");
					TransitionToState(EntityState.Attacking);
				}
				break;

			case EntityState.Attacking:
				if (animName == "attack")
				{
					GD.Print("Entity: Attack animation finished, transitioning back...");
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
		GD.Print($"Entity {Name} has died.");
		TransitionToState(EntityState.Dying);
		CurrentHealth = 0;
		_sprite.Play("die");
	}

	// ---- IEntity Implementation ----

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

	public void PlayAnimation(string animationName)
	{
		if (_sprite == null) return;

		if (_sprite.SpriteFrames.HasAnimation(animationName))
		{
			_sprite.Play(animationName);
		}
		else
		{
			GD.PrintErr($"Entity {Name} does not have animation '{animationName}'");
		}
	}

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

		var collisionPos = _collisionShape.Position;
		var collisionScale = _collisionShape.Scale;
		
		collisionPos.X = flip ? -Mathf.Abs(collisionPos.X) : Mathf.Abs(collisionPos.X);
		collisionScale.X = flip ? -Mathf.Abs(collisionScale.X) : Mathf.Abs(collisionScale.X);
		
		_collisionShape.Position = collisionPos;
		_collisionShape.Scale = collisionScale;
		
	}

	// ---- Public Getters for AI customization ----
	public EntityState CurrentState => _currentState;
	public Node2D Target => _target;
	public float StateTimer => _stateTimer;
	public NavigationAgent2D NavAgent => _navAgent;
}
