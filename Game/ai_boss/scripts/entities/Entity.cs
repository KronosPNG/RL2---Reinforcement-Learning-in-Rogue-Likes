using Godot;

public partial class Entity : CharacterBody2D, IEntity
{
	// ---- Node references ----
	protected AnimatedSprite2D _sprite;
	protected CollisionShape2D _collisionShape;
	protected Area2D _hitArea;
	protected NavigationAgent2D _navAgent;

	// ---- Health properties ----
	[ExportGroup("Health Properties")]
	public float CurrentHealth { get; private set; }
	[Export] public float MaxHealth { get; private set; }
	[Export] public bool IsInvulnerable { get; private set; }
	public bool IsAlive => CurrentHealth > 0;

	// ---- Movement properties ----
	[ExportGroup("Movement Properties")]
	[Export] public float BaseSpeed { get; private set; } = 200f;
	
	private float _knockbackResistance = 0f;
	[Export(PropertyHint.Range, "0.0,1.0,0.01")] public float KnockbackResistance 
	{ 
		get => _knockbackResistance;
		private set => _knockbackResistance = Mathf.Clamp(value, 0f, 1f);
	}
	
	public Vector2 FacingDirection { get; set; } = Vector2.Right;
	protected sbyte _lastHorizontalFacing = 1;

	// ---- Visual properties ----
	[ExportGroup("Visual Effects")]
	protected Color OriginalModulate;
	[Export] public bool FlipSpriteHorizontally = false;
	[Export] public Color DamagedModulate { get; set; } = new Color(1, 0.75f, 0.75f);
	[Export] public Color DeadModulate { get; set; } = new Color(0.5f, 0.5f, 0.5f);
	[Export] public float DamageFlashDuration { get; set; } = 0.1f;
	protected float _damageFlashTimer = 0f;
	
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
		Attacking,
		Hit,
		Dying,
		Dead
	}

	protected EntityState _currentState = EntityState.Idle;
	protected EntityState _previousState = EntityState.Idle;

	// ---- Timers ----
	protected float _stateTimer = 0f;
	protected float _hitStunDuration = 0.5f;
	protected float _wanderTimer = 0f;
	
	[Export] public float PlayerNoticeDecay { get; private set; } = 2f;

	// ---- AI Properties ----
	protected Node2D _target;
	protected Vector2 _lastKnownTargetPosition = Vector2.Zero;

	// ---- Behaviour Strategies ----
	[ExportGroup("AI Behaviours")]
	[Export] public WanderBehaviour WanderingBehavior;
	[Export] public AggroBehaviour AggroBehavior;
	[Export] public AttackBehaviour AttackBehavior;

	// ---- Signals ----
	[Signal] public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void StateChangedEventHandler(string newState);

	public override void _Ready()
	{
		InitializeNodes();
		InitializeBehaviours();
		InitializeEntity();

		if (_sprite != null)
			_sprite.AnimationFinished += OnAnimationFinished;

		if (FlipSpriteHorizontally)
		{
			_sprite.FlipH = true;
		}

		CurrentHealth = MaxHealth;
		TransitionToState(EntityState.Idle);
	}

	protected virtual void InitializeNodes()
	{
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_collisionShape = GetNodeOrNull<CollisionShape2D>("PhysicalCollision");
		_hitArea = GetNodeOrNull<Area2D>("HitArea");
		_navAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
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
		_wanderTimer -= delta;
		_damageFlashTimer -= delta;
		
		// Update death color transition
		if (_isTransitioningToDeath)
		{
			_deathColorTimer += delta;
		}
	}

	protected virtual void UpdateAI(float delta)
	{
		// Find target (usually player)
		if (_target == null)
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
				HandlePlayerNoticedTransitions();
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
		if (AggroBehavior != null && AggroBehavior.CanSeeTarget(this))
		{
			TransitionToState(EntityState.Aggro);
			return;
		}

		// Random wandering
		if (_wanderTimer <= 0 && GD.Randf() < 0.3f)
		{
			TransitionToState(EntityState.Wandering);
		}
	}

	protected virtual void HandleWanderingTransitions()
	{
		if (AggroBehavior != null && AggroBehavior.CanSeeTarget(this))
		{
			TransitionToState(EntityState.Aggro);
			return;
		}

		// Stop wandering after some time
		if (WanderingBehavior != null && WanderingBehavior.ShouldStopWandering(this))
		{
			TransitionToState(EntityState.Idle);
		}
	}

	protected virtual void HandlePlayerNoticedTransitions()
	{
		if (AggroBehavior != null && AggroBehavior.ShouldLoseTarget(this))
		{
			TransitionToState(EntityState.Idle);
			return;
		}

		// Close enough to attack
		if (AttackBehavior != null && AttackBehavior.IsInAttackRange(this) && AttackBehavior.CanAttack(this))
		{
			TransitionToState(EntityState.Attacking);
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
			if (AggroBehavior != null && AggroBehavior.CanSeeTarget(this))
				TransitionToState(EntityState.Aggro);
			else
				TransitionToState(EntityState.Idle);
		}
	}

	protected virtual void HandleDyingTransitions()
	{
		// Death animation will handle transition to Dead via OnAnimationFinished
	}

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
				AggroBehavior.PerformPlayerNoticeBehavior(this);
				break;

			case EntityState.Attacking:
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

		EmitSignal(SignalName.StateChanged, newState.ToString());
	}

	protected virtual void OnEnterState(EntityState state)
	{
		switch (state)
		{
			case EntityState.Idle:
				_wanderTimer = WanderingBehavior.WanderCooldown;
				break;

			case EntityState.Wandering:
				WanderingBehavior.OnEnterWander(this);
				_wanderTimer = WanderingBehavior.WanderCooldown;
				break;

			case EntityState.Aggro:
				AggroBehavior.OnEnterNotice(this);
				AggroBehavior.PerformPlayerNoticeBehavior(this);
				break;

			case EntityState.Attacking:
				AttackBehavior.OnEnterAttack(this);
				AttackBehavior.PerformAttack(this);
				break;

			case EntityState.Hit:
				_sprite.Play(GetAnimationForState(state));
				// Apply damage flash effect
				ApplyDamageFlash();
				break;

			case EntityState.Dying:
				_hitArea.SetDeferred("monitoring", false);
				_collisionShape.SetDeferred("disabled", true);
				// Start death color transition
				StartDeathColorTransition();
				break;

			case EntityState.Dead:				
				// Darken the sprite 
				DarkenSprite();
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
		}
	}

	protected virtual void UpdateAnimationIfNeeded()
	{
		if (_sprite == null) return;
		
		// Handle visual effects
		UpdateVisualEffects();
		
		if (!IsAlive || _currentState == EntityState.Dead)
		{
			return;
		}

		bool stateChanged = _currentState != _previousState;
		
		// Update sprite facing - only update if we're in a state where movement affects facing
		// Don't update facing during Hit, Dying, or Attacking states to preserve direction
		if (_currentState != EntityState.Hit && _currentState != EntityState.Dying && _currentState != EntityState.Attacking)
		{
			if (!Mathf.IsEqualApprox(Velocity.X, 0))
			{
				_sprite.FlipH = Velocity.X < 0;
				_lastHorizontalFacing = (sbyte)(Velocity.X < 0 ? -1 : 1);
			}
			else
			{
				// For entities that don't move (like dummies), respect the FlipSpriteHorizontally setting
				// Otherwise use the last horizontal facing direction
				_sprite.FlipH = FlipSpriteHorizontally || _lastHorizontalFacing < 0;
			}
		}
		// During Hit, Dying, and Attacking states, preserve the current flip state

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
			EntityState.Aggro => "walk",
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

		switch (_currentState)
		{
			case EntityState.Attacking:
				if (animName == "attack")
				{
					if (AggroBehavior != null && AggroBehavior.CanSeeTarget(this))
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

	// ---- IEntity Implementation ----

	public void ApplyDamage(float amount, Node2D attacker, float knockbackStrength = 400f)
	{
		if (!IsAlive) return;

		if (!IsInvulnerable) CurrentHealth -= amount;

		// Check if damage is lethal
		bool isLethalDamage = CurrentHealth <= 0;
		
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
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

		if (isLethalDamage)
		{
			CurrentHealth = 0;
			Die();
		}
	}

	public void ApplyStatusEffect(StatusEffectType effectType, float duration, float intensity = 1)
	{
		throw new System.NotImplementedException();
	}

	public bool CanTakeDamageFrom(Node2D attacker)
	{
		throw new System.NotImplementedException();
	}

	public void Die()
	{
		GD.Print($"Entity {Name} has died.");
		TransitionToState(EntityState.Dying);
		CurrentHealth = 0;
		_sprite.Play("die");
	}

	public bool HasStatusEffect(StatusEffectType effectType)
	{
		throw new System.NotImplementedException();
	}

	public void Heal(float amount)
	{
		if (IsAlive)
		{
			CurrentHealth += amount;
			if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;
		}
	}

	public void PlayDeathEffect()
	{
		throw new System.NotImplementedException();
	}

	public void PlayHitEffect(Vector2 hitPosition)
	{
		throw new System.NotImplementedException();
	}

	public void RemoveStatusEffect(StatusEffectType effectType)
	{
		throw new System.NotImplementedException();
	}

	public void ShowDamageNumber(float damage)
	{
		throw new System.NotImplementedException();
	}

	protected void DarkenSprite()
	{
		// gradually darken the sprite over time
		_sprite.Modulate = OriginalModulate.Lerp(DeadModulate, 0.1f);
	}
	
	protected virtual void ApplyDamageFlash()
	{
		if (_sprite == null) return;
		
		_sprite.Modulate = DamagedModulate;
		_damageFlashTimer = DamageFlashDuration;
	}
	
	protected virtual void StartDeathColorTransition()
	{
		if (_sprite == null) return;
		
		_isTransitioningToDeath = true;
		_deathColorTimer = 0f;
		// Clear damage flash to prevent flickering
		_damageFlashTimer = 0f;
	}
	
	protected virtual void UpdateVisualEffects()
	{
		if (_sprite == null) return;
		
		// Death transition takes priority over damage flash
		if (_isTransitioningToDeath)
		{
			// Smoothly transition to death color
			float progress = Mathf.Clamp(_deathColorTimer / DeathColorTransitionDuration, 0f, 1f);
			_sprite.Modulate = OriginalModulate.Lerp(DeadModulate, progress);
			
			// Stop transition when complete
			if (progress >= 1f)
			{
				_isTransitioningToDeath = false;
			}
		}
		// Handle damage flash only if not dying
		else if (_damageFlashTimer > 0f && _currentState != EntityState.Dying && _currentState != EntityState.Dead)
		{
			// Flash is active, keep damaged color
			_sprite.Modulate = DamagedModulate;
		}
		else if (_currentState != EntityState.Hit && _currentState != EntityState.Dying && _currentState != EntityState.Dead)
		{
			// Return to original color when not damaged/dying
			_sprite.Modulate = OriginalModulate;
		}
	}
	
	// Immediately sets the sprite to the dead color without transition
	public virtual void SetDeadColorImmediate()
	{
		if (_sprite == null) return;
		
		_sprite.Modulate = DeadModulate;
		_isTransitioningToDeath = false;
	}
	
	// Resets the sprite color to original
	public virtual void ResetSpriteColor()
	{
		if (_sprite == null) return;
		
		_sprite.Modulate = OriginalModulate;
		_isTransitioningToDeath = false;
		_damageFlashTimer = 0f;
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
	
	// ---- Public Getters for AI customization ----
	public EntityState CurrentState => _currentState;
	public Node2D Target => _target;
	public float StateTimer => _stateTimer;
}
