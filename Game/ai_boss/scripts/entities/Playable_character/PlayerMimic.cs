using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Godot;

public partial class PlayerMimic : PlayableCharacter, IDamageable, IHasHealth, IStateful<EntityState>, IAnimatable<EntityState>
{
    // --- Behaviours ---
    [Export] private PlayerAttackBehaviour attackBehaviour;
    [Export] private DodgeBehaviour dodgeBehaviour;
    [Export] private ConsumableBehaviour consumableBehaviour;
    [Export] private WanderBehaviour wanderBehaviour;
    [Export] private AggroBehaviour aggroBehaviour;

    List<(float priority, int originalIndex, Action action)> BehaviourPriorityList;
    
    public BossRL BossRef;

    // --- Constants ---
    private const float WANDER_PRIORITY = 0.05f;
    private const float AGGRO_PRIORITY = 0.15f;
    
    public override void _Ready()
    {
        base._Ready();
        
        // Find the boss in the scene tree
        BossRef = GetTree().Root.GetNodeOrNull<BossRL>("*/" + TargetType);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        UpdateAI((float)delta);

    }

    protected override void UpdateAI(float delta)
    {
        var dodgeThreat = dodgeBehaviour.EvaluateThreat(this);
        var consumableUsefulness = consumableBehaviour.EvaluateUsefulness(this);
        var attackPriority = attackBehaviour.EvaluateOpportunity(this);

        // Order priorities based on evaluations
        // Include index to break ties in favor of original order: Dodge > Consumable > Attack > Aggro > Wander
        BehaviourPriorityList = new List<(float, int, Action)>()
        {
            (dodgeThreat, 0, HandleDodgeControl),
            (consumableUsefulness, 1, HandleConsumableControl),
            (attackPriority, 2, HandleCombatControl),
            (AGGRO_PRIORITY, 3, HandleAggroControl),
            (WANDER_PRIORITY, 4, HandleWanderControl)
        };
        
        // Sort by priority descending, then by original order ascending for ties
        BehaviourPriorityList = BehaviourPriorityList
            .OrderByDescending(a => a.priority)
            .ThenBy(a => a.originalIndex)
            .ToList();

        // Execute highest priority action
        BehaviourPriorityList[0].action.Invoke(); 
    }

    // --- Control ---

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
        
    }

    private void HandleConsumableControl()
    {
        
    }

    private void HandleAggroControl()
    {
        
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
        if (BossRef != null)
        {
            return BossRef.GlobalPosition;
        }

        return Vector2.Zero;
    }

    // --- "Input" Handling ---
    private void ExecuteAttackDecision(AttackDecision decision)
    {
        switch (decision.Type)
        {
            case AttackType.Light:
                if (CurrentState == EntityState.AttackCharging)
                    HandleChargingControl(decision);
                else
                    EquippedWeapon.AttackLight(decision.AimDirection);
                
                break;

            case AttackType.Heavy:
                if (CurrentState == EntityState.AttackCharging)
                    HandleChargingControl(decision);
                else
                    EquippedWeapon.AttackHeavy(decision.AimDirection);

                break;

            case AttackType.ChargedLight:
            case AttackType.ChargedHeavy:
                HandleChargingControl(decision);
                break;
        }
    }



}