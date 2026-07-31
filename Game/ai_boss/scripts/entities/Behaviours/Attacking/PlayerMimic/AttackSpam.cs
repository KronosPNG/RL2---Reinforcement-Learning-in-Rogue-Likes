using Godot;

[GlobalClass]
public partial class AttackSpam : PlayerAttackBehaviour
{
	public AttackSpam() : base()
	{
		Priority = 0.65f;
		TimeBetweenAttacks = 0.1f;
	}
	
	public override float EvaluateOpportunity(PlayerMimic player)
	{
		// Already charging — must continue regardless of CanAttack (weapon isn't Ready while charging)
		if (player.CurrentState == EntityState.AttackCharging)
			return 0.7f;

		// Step 1: checking if we can attack at all
		if (!CanAttack(player)) return 0f;

		// Step 2: checking distance
		float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
		float maxRange = Mathf.Max(
			_weapon.LightAttackConfig.Range,
			_weapon.HeavyAttackConfig.Range
		);

		if (distanceToTarget > maxRange)
			return 0f;  // Target is out of range, but we want to close the distance


		// Step 4: attack is available and target is in range, so we should attack
		return 0.65f;
	}

	public override AttackDecision GetAttackDecision(PlayerMimic player)
	{
		Vector2 aim = GetAimDirection(player);
		float distanceToTarget = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
		
		// CASE 1: Already charging - decide to continue or release
		if (player.CurrentState == EntityState.AttackCharging)
		{
			bool chargeReady = _weapon.CanReleaseCharge();
			bool isHeavyCharge = _weapon.IsCurrentAttackHeavy;
			
			if (chargeReady)
			{
				_lastAttackDecision = new AttackDecision
				{
					Type = isHeavyCharge ? AttackType.Heavy : AttackType.Light,
					AimDirection = aim
				};
				return _lastAttackDecision;
			}
			else
			{
				_lastAttackDecision = new AttackDecision
				{
					Type = isHeavyCharge ? AttackType.ChargedHeavy : AttackType.ChargedLight,
					AimDirection = aim
				};
				return _lastAttackDecision;
			}
		}
		
		// CASE 2: Not charging yet - pick best DPS instant attack IN RANGE
		bool lightAvailable = _weapon.CanStartAttack(false);
		bool heavyAvailable = _weapon.CanStartAttack(true);
		bool lightIsCharged = _weapon.LightAttackConfig is ChargedAttack;
		bool heavyIsCharged = _weapon.HeavyAttackConfig is ChargedAttack;
		
		// Check if attacks are in range
		bool lightInRange = distanceToTarget <= _weapon.LightAttackConfig.Range;
		bool heavyInRange = distanceToTarget <= _weapon.HeavyAttackConfig.Range;
		
		// Calculate DPS ONLY for instant attacks that are available AND in range
		float lightDPS = 0f;
		if (lightAvailable && !lightIsCharged && lightInRange)
		{
			lightDPS = _weapon.LightAttackConfig.Damage / _weapon.LightAttackConfig.Cooldown;
		}
		
		float heavyDPS = 0f;
		if (heavyAvailable && !heavyIsCharged && heavyInRange)
		{
			heavyDPS = _weapon.HeavyAttackConfig.Damage / _weapon.HeavyAttackConfig.Cooldown;
		}
		
		// Pick highest DPS instant attack that's in range
		if (lightDPS > 0 || heavyDPS > 0)
		{
			AttackType chosenType = lightDPS >= heavyDPS ? AttackType.Light : AttackType.Heavy;
			
			_lastAttackDecision = new AttackDecision 
			{ 
				Type = chosenType, 
				AimDirection = aim 
			};
			return _lastAttackDecision;
		}
		
		// Fallback (no instant attacks in range and ready)
		_lastAttackDecision = new AttackDecision 
		{ 
			Type = AttackType.Light, 
			AimDirection = aim 
		};
		return _lastAttackDecision;
	}
}
