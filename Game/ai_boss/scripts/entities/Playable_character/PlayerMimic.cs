using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

public partial class PlayerMimic : PlayableCharacter, IDamageable, IHasHealth, IStateful<EntityState>, IAnimatable<EntityState>, INavigable
{
    // --- Behaviours ---
    [Export] private PlayerAttackBehaviour attackBehaviour;
    [Export] private DodgeBehaviour dodgeBehaviour;
    [Export] private ConsumableBehaviour consumableBehaviour;
    [Export] private WanderBehaviour wanderBehaviour;
    [Export] private AggroBehaviour aggroBehaviour;
    
    // --- Utility --- 
    private List<(float priority, int originalIndex, Action action)> BehaviourPriorityList;
    public List<Projectile> DetectedProjectiles = new List<Projectile>();
    
    // --- Node References ---
    public new BossRL Target{ get; set; }
    private Area2D ProjectileDetectionArea;
    public NavigationAgent2D NavAgent { get; private set; }

    // --- Constants ---
    private const float MIN_PRIORITY_THRESHOLD = 0.25f; // Minimum priority to consider executing a behaviour
    
    public override void _Ready()
    {
        base._Ready();
        
        // Find the boss in the scene tree
        Target = GetTree().Root.GetNodeOrNull<BossRL>("*/" + TargetType);
        ProjectileDetectionArea = GetNode<Area2D>("ProjectileDetectionArea");
        NavAgent = GetNode<NavigationAgent2D>("NavAgent");

        ProjectileDetectionArea.AreaEntered += OnProjectileAreaEntered; 
        ProjectileDetectionArea.AreaExited += OnProjectileAreaExited;
        ProjectileDetectionArea.BodyEntered += OnProjectileEntered;
        ProjectileDetectionArea.BodyExited += OnProjectileExited;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        _movementDirection = GetDesiredMovementDirection();
        EventBus.RaisePlayerDirectionChanged(_movementDirection);

        UpdateAI((float)delta);

    }

    protected override void UpdateAI(float delta)
    {
        var dodgeThreat = dodgeBehaviour.EvaluateOpportunity(this);
        var consumableUsefulness = consumableBehaviour.EvaluateOpportunity(this);
        var attackPriority = attackBehaviour.EvaluateOpportunity(this);

        // Thresholding: if behaviour under minimum threshold, ignore it
        if (dodgeThreat < MIN_PRIORITY_THRESHOLD) dodgeThreat = 0f;
        else dodgeThreat += dodgeBehaviour.Priority; // Add base priority if above threshold

        if (consumableUsefulness < MIN_PRIORITY_THRESHOLD) consumableUsefulness = 0f;
        else consumableUsefulness += consumableBehaviour.Priority;

        if (attackPriority < MIN_PRIORITY_THRESHOLD) attackPriority = 0f;
        else attackPriority += attackBehaviour.Priority;

        // Order priorities based on evaluations
        // Include index to break ties in favor of original order: Dodge > Consumable > Attack > Aggro > Wander
        BehaviourPriorityList = new List<(float, int, Action)>()
        {
            (dodgeThreat, 0, HandleDodgeControl),
            (consumableUsefulness, 1, HandleConsumableControl),
            (attackPriority, 2, HandleCombatControl),
        };
        
        // Sort by priority descending, then by original order ascending for ties
        BehaviourPriorityList = BehaviourPriorityList
            .OrderByDescending(a => a.priority)
            .ThenBy(a => a.originalIndex)
            .ToList();
    }

    private Vector2 GetDesiredMovementDirection()
    {
        switch (CurrentState)
        {
            case EntityState.Walking:
                return wanderBehaviour.GetWanderDirection(this, (float)GetProcessDeltaTime());
            
            case EntityState.Aggro:
                return aggroBehaviour.GetChaseDirection(this, (float)GetProcessDeltaTime());

            case EntityState.AttackCharging:
            case EntityState.Attacking:
                return attackBehaviour.GetMovementDirection(this);

            case EntityState.ConsumableCharging:
            case EntityState.ConsumableUse:
                return consumableBehaviour.GetMovementDirection(this);
        }

        return Vector2.Zero;
    }

    public override void HandleStateTransitions()
    {
        switch (CurrentState)
        {
            case EntityState.Idle:
                TransitionToState(EntityState.Walking);
                break;

            case EntityState.Walking:
                if (wanderBehaviour.ShouldStopWandering(this))
                {
                    // Get first behaviour in priority list that has a valid action (priority > 0)
                    // else aggro behaviour
                    if(!ExecuteValidBehaviour())
                    {
                        HandleAggroControl();
                    }
                }
                break;

            case EntityState.Aggro:
                ExecuteValidBehaviour();
                break;

            case EntityState.Hit:
                StateTimer += (float)GetProcessDeltaTime();
                if (StateTimer >= HitStunDuration)
                {
                    StateTimer = 0f;
                    if(!ExecuteValidBehaviour())
                    {
                        HandleAggroControl();
                    }
                }
                break;
        }
    }

    // --- Control ---

    private void ExecuteAttackDecision(AttackDecision decision)
    {
        if (CurrentState == EntityState.AttackCharging || decision.Type == AttackType.ChargedHeavy || decision.Type == AttackType.ChargedLight)
        {
            HandleChargingControl(decision);
            return;
        }

        if (decision.Type == AttackType.Light)
        {
            TransitionToState(EntityState.Attacking);
            EquippedWeapon.AttackLight(decision.AimDirection);
        }

        else if (decision.Type == AttackType.Heavy)
        {
            TransitionToState(EntityState.Attacking);
            EquippedWeapon.AttackHeavy(decision.AimDirection);
        }
    }

    private void HandleCombatControl()
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
            
            case EntityState.ConsumableCharging:
                CancelChargingConsumable();
                return;
		}

        var decision = attackBehaviour.GetAttackDecision(this);
        ExecuteAttackDecision(decision);
    }

    private void HandleChargingControl(AttackDecision decision)
    {
        bool isHeavy = decision.Type == AttackType.ChargedHeavy || decision.Type == AttackType.Heavy;
        bool isCharged = decision.Type == AttackType.ChargedHeavy || decision.Type == AttackType.ChargedLight;

        if (CurrentState == EntityState.AttackCharging)
        {
            if(isCharged)
            {
                EquippedWeapon.UpdateCharge((float)GetProcessDeltaTime());
            } 
                
            else
            {
                TransitionToState(EntityState.Attacking);

                if(isHeavy)
                    EquippedWeapon.ExecuteChargedHeavy(decision.AimDirection);
                else
                    EquippedWeapon.ExecuteChargedLight(decision.AimDirection);
            }
        }

        else {
            TransitionToState(EntityState.AttackCharging);
            EquippedWeapon.StartCharge(decision.AimDirection, isHeavy);
        }  
    }

    private void HandleDodgeControl()
    {
        switch (CurrentState)
        {
            // Don't allow dodging while already dodging, in dodge preparation, attacking, or dead
            case EntityState.DodgePrep:
            case EntityState.Dodging:
            case EntityState.Hit:
            case EntityState.Dead:
                return;

            case EntityState.AttackCharging:
                CancelChargingAttack();
                break;

            case EntityState.ConsumableCharging:
                CancelChargingConsumable();
                break;
        }

        TransitionToState(EntityState.DodgePrep);

        _dodgeDirection = dodgeBehaviour.GetDodgeDirection(this);

        if(_dodgeDirection == Vector2.Zero)
        {
            if (Velocity != Vector2.Zero)
                TransitionToState(EntityState.Walking);
            else
                TransitionToState(EntityState.Idle);
        }

        EventBus.RaisePlayerDodged(_dodgeDirection);
        TransitionToState(EntityState.Dodging);
    }

    private void HandleConsumableControl()
    {   
        switch (CurrentState)
        {
            // Don't allow consumable use while dodging, in dodge prep, attacking, or dead
            case EntityState.DodgePrep:
            case EntityState.Dodging:
            case EntityState.Attacking:
            case EntityState.Hit:
            case EntityState.Dead:
                return;

            case EntityState.AttackCharging:
                CancelChargingAttack();
                break;
        }

        
        var consumableAction = consumableBehaviour.GetConsumableAction(this);

        if (consumableAction == ConsumableAction.Use)
        {
            UseConsumable();
        }

        else if (consumableAction == ConsumableAction.Charge)
        {
            if(CurrentState == EntityState.ConsumableCharging)
            {
                if (EquippedConsumable.CanReleaseCharge())
                {
                    TransitionToState(EntityState.ConsumableUse);
                    EquippedConsumable.ExecuteCharged();
                    EventBus.RaiseConsumableUsed();
                }
            }

            else {
                StartChargingConsumable();
            }
        }
    }

    private void HandleAggroControl()
    {
        TransitionToState(EntityState.Aggro);
    }

    private void HandleWanderControl()
    {
        
    }


    public override Vector2 GetAimDirection()
    {
        return attackBehaviour.GetAimDirection(this);
    }

    public Vector2 GetTargetPosition()
    {
        // Get boss position if it exists, otherwise return zero
        if (Target != null)
        {
            return Target.GlobalPosition;
        }

        return Vector2.Zero;
    }    

    private bool ExecuteValidBehaviour()
    {
        var validBehaviour = BehaviourPriorityList.FirstOrDefault(b => b.priority > 0);
        if (validBehaviour != default)
        {
            validBehaviour.action.Invoke();
            return true;
        }

        return false;
    }

    // --- Event Handlers ---
    private void OnProjectileAreaEntered(Area2D area){
        Node collider = area.GetParent();
        OnProjectileEntered(collider);
    }

    private void OnProjectileAreaExited(Area2D area){
        Node collider = area.GetParent();
        OnProjectileExited(collider);
    }

    private void OnProjectileEntered(Node body)
    {
        Projectile proj = body as Projectile;
        if (proj != null && !DetectedProjectiles.Contains(proj))
        {
            DetectedProjectiles.Add(proj);
        }
    }

    private void OnProjectileExited(Node body)
    {
        Projectile proj = body as Projectile;
        if (proj != null)
        {
            DetectedProjectiles.Remove(proj);
        }
    }


}