using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

public partial class PlayerMimic : PlayableCharacter, IDamageable, IHasHealth, IStateful<EntityState>, IAnimatable<EntityState>, INavigable
{
	// --- Behaviours ---
	[Export] public PlayerAttackBehaviour attackBehaviour;
	[Export] public DodgeBehaviour dodgeBehaviour;
	[Export] public ConsumableBehaviour consumableBehaviour;
	[Export] public WanderBehaviour wanderBehaviour;
	[Export] public AggroBehaviour aggroBehaviour;
	
	// --- Utility ---
	private List<(float priority, int originalIndex, Action action)> BehaviourPriorityList = new();
	public List<Projectile> DetectedProjectiles = new List<Projectile>();
	
	// --- Node References ---
	private Area2D ProjectileDetectionArea;
	public NavigationAgent2D NavAgent { get; private set; }

	// --- Timers ---
	private Timer _attackTimer;
	private Timer _dodgeTimer;

	// --- EventBus handlers (stored for clean unsubscription on exit) ---
	private Action<BossRL> _onBossSpawned;
	private Action<Weapon> _onWeaponEquipped;
	private Action<Consumable> _onConsumableEquipped;
	private Action<string> _onWeaponAttackStarted;

	// --- Constants ---
	private const float MIN_PRIORITY_THRESHOLD = 0.25f; // Minimum priority to consider executing a behaviour
	
	public override void _Ready()
	{
		base._Ready();
		
		// Find the boss in the scene tree
		ProjectileDetectionArea = GetNode<Area2D>("ProjectileDetectionArea");
		NavAgent = GetNode<NavigationAgent2D>("NavigationAgent2D");
		_attackTimer = GetNode<Timer>("Timers/AttackTimer");
		_dodgeTimer = GetNode<Timer>("Timers/DodgeTimer");

		_onBossSpawned = (boss) => Target = boss;
		EventBus.OnBossSpawned += _onBossSpawned;

		EquipWeapon(EquippedWeaponScene);
		EquipArmor(EquippedArmorScene);
		EquipConsumable(EquippedConsumableScene);

		_onConsumableEquipped = (consumable) => consumableBehaviour.Initialize(this);
		EventBus.OnConsumableEquipped += _onConsumableEquipped;

		_onWeaponEquipped = (weapon) =>
		{
			attackBehaviour.Initialize(this);
			aggroBehaviour.Initialize(this);

			if (attackBehaviour.TimeBetweenAttacks > 0)
				_attackTimer.WaitTime = attackBehaviour.TimeBetweenAttacks;
		};
		EventBus.OnWeaponEquipped += _onWeaponEquipped;

		if (attackBehaviour.TimeBetweenAttacks > 0)
		{
			_onWeaponAttackStarted = (s) => _attackTimer.Start();
			EventBus.OnWeaponAttackStarted += _onWeaponAttackStarted;
		}

		ProjectileDetectionArea.AreaEntered += OnProjectileAreaEntered;
		ProjectileDetectionArea.AreaExited += OnProjectileAreaExited;
		ProjectileDetectionArea.BodyEntered += OnProjectileEntered;
		ProjectileDetectionArea.BodyExited += OnProjectileExited;
		
		if(dodgeBehaviour.TimeBetweenDodges > 0)
			_dodgeTimer.WaitTime = dodgeBehaviour.TimeBetweenDodges;

		StateTimer = 0f;
	}

	public void DisconnectFromEventBus()
	{
		EventBus.OnBossSpawned -= _onBossSpawned;
		EventBus.OnWeaponEquipped -= _onWeaponEquipped;
		EventBus.OnConsumableEquipped -= _onConsumableEquipped;
		if (_onWeaponAttackStarted != null)
			EventBus.OnWeaponAttackStarted -= _onWeaponAttackStarted;
	}

	public override void _ExitTree() => DisconnectFromEventBus();

	public override void _PhysicsProcess(double delta)
	{
		// Compute direction and AI priorities BEFORE base processes state transitions and movement
		_movementDirection = GetDesiredMovementDirection();
		EventBus.RaisePlayerDirectionChanged(_movementDirection);
		UpdateAI((float)delta);

		base._PhysicsProcess(delta);
	}

	protected override void UpdateAI(float delta)
	{
		var dodgeThreat = dodgeBehaviour.EvaluateOpportunity(this);
		var consumableUsefulness = consumableBehaviour.EvaluateOpportunity(this);
		var attackPriority = attackBehaviour.EvaluateOpportunity(this);

		// Thresholding: if behaviour under minimum threshold, ignore it
		if (_dodgeTimer.TimeLeft > 0)
			dodgeThreat = 0f;
		else if (dodgeThreat < MIN_PRIORITY_THRESHOLD) 
			dodgeThreat = 0f;
		else 
			dodgeThreat += dodgeBehaviour.Priority; // Add base priority if above threshold

		if (consumableUsefulness < MIN_PRIORITY_THRESHOLD)
			consumableUsefulness = 0f;
		else 
			consumableUsefulness += consumableBehaviour.Priority;

		if (_attackTimer.TimeLeft > 0)
			attackPriority = 0f;
		else if (attackPriority < MIN_PRIORITY_THRESHOLD) 
			attackPriority = 0f;
		else 
			attackPriority += attackBehaviour.Priority;

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

	protected override void ApplyMovementByState(float delta)
	{		
		float armorSpeedModifier = EquippedArmor != null ? EquippedArmor.SpeedModifier : 1f;

		switch (CurrentState)
		{
			case EntityState.Dodging:
				// move using dodge vector & speed, plus any active knockback
				Velocity = Vector2.Zero;
				Velocity = _dodgeDirection * DodgeSpeed * armorSpeedModifier + _knockbackVelocity;
				MoveAndSlide();
				break;
			
			case EntityState.Hit:
			case EntityState.ConsumableUse:
			case EntityState.Aggro:
			// allow movement while attacking
			case EntityState.Walking:
				// move using input vector, speed and modifier, plus any active knockback
				// Velocity = input * BaseSpeed * EquippedArmor.SpeedModifier;
				Velocity = _movementDirection * BaseSpeed * armorSpeedModifier + _knockbackVelocity;
				MoveAndSlide();
				break;

			case EntityState.Attacking:
			case EntityState.AttackCharging:
			case EntityState.Dead:
			// no movement when dead
			case EntityState.DodgePrep:
			// stop movement while preparing to dodge
			case EntityState.Idle:
				// stop movement
				Velocity = Vector2.Zero;
				MoveAndSlide();
				break;

			case EntityState.ConsumableCharging: // Allow movement while charging consumable
												 // allow movement while charging (at reduced speed or full speed), plus any active knockback
												 // Velocity = input * BaseSpeed * ChargeMoveModifier * EquippedArmor.SpeedModifier;
				Velocity = _movementDirection * BaseSpeed * ChargeMoveModifier * armorSpeedModifier + _knockbackVelocity;
				MoveAndSlide();
				break;
		}
	}

	public override void OnEnterState(EntityState s)
	{
		StateTimer = 0f;
		base.OnEnterState(s);

		if(s == EntityState.Walking)
		{
			wanderBehaviour.OnEnterWander(this);
		}
	}

	public override void TransitionToState(EntityState newState)
	{
		GD.Print($"[PLAYER_MIMIC] Transitioning to state {newState}");
		base.TransitionToState(newState);
	}

	public override void HandleStateTransitions()
	{
		StateTimer += (float)GetProcessDeltaTime();
		
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
				aggroBehaviour.PerformAggroBehaviour(this);
				ExecuteValidBehaviour();
				break;

			case EntityState.AttackCharging:
				HandleCombatControl();
				break;

			case EntityState.Attacking:
				if (StateTimer > 2f)
					TransitionToState(EntityState.Walking);
				break;

			case EntityState.ConsumableUse:
				if (EquippedConsumable == null || EquippedConsumable.State != ConsumableState.Windup)
					HandleAggroControl();
				break;

			case EntityState.Hit:
				if (StateTimer >= HitStunDuration)
				{
					StateTimer = 0f;
					HandleAggroControl();
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

		var actualAim = decision.AimDirection;
		
		if (decision.Type == AttackType.Light)
		{
			GD.Print($"[PLAYER_MIMIC] Attacking: {AttackType.Light} attack");
			TransitionToState(EntityState.Attacking);
			EquippedWeapon.AttackLight(actualAim);
		}

		else if (decision.Type == AttackType.Heavy)
		{
			GD.Print($"[PLAYER_MIMIC] Attacking: {AttackType.Heavy} attack");
			TransitionToState(EntityState.Attacking);
			EquippedWeapon.AttackHeavy(actualAim);
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

		if (CurrentState == EntityState.AttackCharging)
		{
			if (!EquippedWeapon.IsCharging)
			{
				_isChargingAttack = false;
				HandleAggroControl();
				return;
			}

			EquippedWeapon.UpdateCharge((float)GetProcessDeltaTime());

			// Only fire when the behaviour explicitly says so (Light/Heavy = release, ChargedLight/ChargedHeavy = keep charging)
			bool shouldFire = decision.Type == AttackType.Light || decision.Type == AttackType.Heavy;

			if (shouldFire && EquippedWeapon.CanReleaseCharge())
			{
				GD.Print($"[PLAYER_MIMIC] Executing charged attack");
				bool wasHeavy = EquippedWeapon.IsCurrentAttackHeavy;
				Vector2 aim = attackBehaviour.GetAimDirection(this);

				TransitionToState(EntityState.Attacking);

				if (wasHeavy)
					EquippedWeapon.ExecuteChargedHeavy(aim);
				else
					EquippedWeapon.ExecuteChargedLight(aim);
			}
		}

		else
		{
			GD.Print($"[PLAYER_MIMIC] Start Charging Attack");
			TransitionToState(EntityState.AttackCharging);
			EquippedWeapon.StartCharge(decision.AimDirection, isHeavy);
			_isChargingAttack = true;
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

		GD.Print($"[PLAYER_MIMIC] Dodging");
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
		_dodgeTimer.Start();
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

		GD.Print($"[PLAYER_MIMIC] Consumable Action");
		var consumableAction = consumableBehaviour.GetConsumableAction(this);

		if (consumableAction == ConsumableAction.Use)
		{
			UseConsumable();
			GD.Print($"[PLAYER_MIMIC] Using Consumable");
		}

		else if (consumableAction == ConsumableAction.Charge)
		{
			if(CurrentState == EntityState.ConsumableCharging)
			{
				if (EquippedConsumable.CanReleaseCharge())
				{
					GD.Print($"[PLAYER_MIMIC] Continue Charging Consumable");
					TransitionToState(EntityState.ConsumableUse);
					EquippedConsumable.ExecuteCharged();
					EventBus.RaiseConsumableUsed();
				}
			}

			else {
				GD.Print($"[PLAYER_MIMIC] Start Charging Consumable");
				StartChargingConsumable();
			}
		}
	}

	private void HandleAggroControl()
	{
		GD.Print($"[PLAYER_MIMIC] Switching to Aggro");
		TransitionToState(EntityState.Aggro);
		aggroBehaviour.PerformAggroBehaviour(this);
	}

	private void HandleWanderControl()
	{
		GD.Print($"[PLAYER_MIMIC] Switching to Wandering");
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
