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
    public float CurrentHealth { get; private set; }
    public float MaxHealth => 2000f;
    public bool IsAlive => CurrentHealth > 0 || CurrentState != BossState.Dead;
    public bool IsInvulnerable { get; private set; }

    // --- Movement properties ---
    [ExportGroup("Movement")]
    private Vector2 _movementDirection = Vector2.Zero;
    [Export(PropertyHint.Range, "0,3,0.1")] private float dashModifier = 2f;
    
    // ---- Knockback Smoothing ----
	protected Vector2 _knockbackVelocity = Vector2.Zero;
	protected Tween _knockbackTween;

    // --- Node References ---
    public NavigationAgent2D NavAgent { get; private set; }
    public BossAttackManager AttackManager { get; set; }
    public BossVisualController VisualController { get; private set; }

    // --- Timers ---
    public Timer CooldownTimer { get; set; }
    public Timer InvulnerabilityTimer { get; set;}

    // ---
    private uint _baseCollisionMask;
    private uint _baseCollisionLayer;

    private BossAction _currentAction;


    public override void _Ready()
    {
        base._Ready();

        AttackManager = GetNodeOrNull<BossAttackManager>("BossAttackManager");
        CooldownTimer = GetNodeOrNull<Timer>("Timers/CooldownTimer");
        InvulnerabilityTimer = GetNodeOrNull<Timer>("Timers/InvulnerabilityTimer");

        InvulnerabilityTimer.Timeout += InvulnerabilityEnd;
        
        _baseCollisionMask = CollisionMask;
        _baseCollisionLayer = CollisionLayer;

        CurrentHealth = MaxHealth;

        EventBus.RaiseBossSpawnedEvent(this);
    }

    public override void OnEnterState(BossState state)
    {
        switch (state)
        {
            case BossState.Idle:
                VisualController.IsMoving = false;
				
                break;
            
            case BossState.Dashing:
                VisualController.IsMoving = true;

                _hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
                _hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);
                
                // disable collision with player
                CollisionLayer = _baseCollisionLayer & ~2u; // player on layer 2;
                CollisionMask = _baseCollisionMask & ~2u; // other enemies detection on layer 2;
                CollisionMask = _baseCollisionMask & ~4u; // player on layer 2, player attacks on layer 3
                break;

            case BossState.Walking:
                VisualController.IsMoving = true;
                break;

            case BossState.Cooldown:
                CooldownTimer.Start();
                break;

            case BossState.Hit:
                break;

            case BossState.Dead:
                _hitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);
				_hitArea.SetDeferred(Area2D.PropertyName.Monitorable, false);

                SetPhysicsProcess(false);
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
        throw new System.NotImplementedException();
    }

    protected override void ApplyMovementByState(float delta)
    {
        switch (CurrentState)
        {
            case BossState.Idle:
            case BossState.Attacking:
                Velocity = Vector2.Zero;
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
        throw new System.NotImplementedException();
    }

    protected override void UpdateFacing()
    {
        if (!Mathf.IsEqualApprox(_movementDirection.X, 0) || !Mathf.IsEqualApprox(_movementDirection.Y, 0))
        {
            VisualController.FacingDirection = _movementDirection;
        }
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

        CurrentHealth -= damage;
        
        if (CurrentHealth > 0)
        {
            TransitionToState(BossState.Hit);
        }

        ApplyKnockback(attacker.GlobalPosition, knockbackStrength);

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
        return;
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
		Vector2 knockbackForce = knockbackDir * strength*5;
		_knockbackVelocity = knockbackForce;
		
		// Tween knockback velocity back to zero over 0.2 seconds
		_knockbackTween = CreateTween();
		_knockbackTween.SetTrans(Tween.TransitionType.Linear);
		_knockbackTween.TweenProperty(this, "_knockbackVelocity", Vector2.Zero, 0.2f);
	}
}