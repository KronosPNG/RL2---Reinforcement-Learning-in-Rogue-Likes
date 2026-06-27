using System.Linq;
using Godot;

/// <summary>
/// Global state manager for DRL boss AI. Autoload singleton.
/// Aggregates all observations (player, boss, projectiles, environment) for AI decision-making.
/// Automatically registered as an autoload in project.godot
/// </summary>
public partial class GlobalState : Node
{
	// ============= STATIC/PSEUDO-STATIC DATA =============
	// These remain constant during the boss fight
	
	public class EquipmentData
	{
		// Weapon
		public float WeaponDamageLight { get; set; }
		public float WeaponDamageHeavy { get; set; }

		public float WeaponRangeLight { get; set; }
		public float WeaponRangeHeavy { get; set; }

		public float WeaponChargeTimeLight { get; set; }
		public float WeaponChargeTimeHeavy { get; set; }

		public float WeaponCooldownLight { get; set; }
		public float WeaponCooldownHeavy { get; set; }

		// Armor
		public float ArmorDamageModifier { get; set; }
		public float ArmorKnockbackModifier { get; set; }
		public float ArmorSpeedModifier { get; set; }

		// Consumable
		public bool HasConsumable { get; set; }
		public float ConsumableEffectDuration { get; set; }
		public float ConsumableHealAmount { get; set; }
		public float ConsumableChargeAmount { get; set;}

	}
	
	public class RoomBounds
	{
		public float MinX { get; set; }
		public float MaxX { get; set; }
		public float MinY { get; set; }
		public float MaxY { get; set; }
		public float Width => MaxX - MinX;
		public float Height => MaxY - MinY;
	}
	
	// ============= DYNAMIC DATA =============
	
	public class PlayerState
	{
		public Vector2 Position { get; set; }
		public Vector2 MovementDirection { get; set; } // Normalized [-1, 1]
		public int CurrentAction { get; set; }
		
		public float Health { get; set; }
		public float MaxHealth { get; set; }
		
		public float CurrentLightAttackCooldown { get; set; }
		public float CurrentHeavyAttackCooldown { get; set; }

		public float CurrentLightAttackChargeTime { get; set; }
		public float CurrentHeavyAttackChargeTime { get; set; }

		public float InvulnerabilityTimeRemaining { get; set; }
		public float DamageTaken { get; set; } // Last frame damage

		public float CurrentConsumableChargeTime { get; set; }
	}
	
	public class BossState
	{
		public Vector2 Position { get; set; }
		public float Health { get; set; }
		public float MaxHealth { get; set; }
		
		// Cooldowns for the 4 attacks (indexed 0-3)
		public float[] AttackCooldowns { get; set; } = new float[4];
		public float[] AttackCooldownTimers { get; set; } = new float[4];
		public short CurrentAttackId { get; set; } = -1; // -1 if idle
		
		public float InvulnerabilityTimeRemaining { get; set; }
		public float DamageTaken { get; set; } // Last frame damage
		
		// Derived values for convenience
		public float DistanceToPlayer { get; set; }
		public float AngleToPlayer { get; set; } // Radians, relative to boss
	}
	
	public class ProjectileState
	{
		public int ActiveProjectileCount { get; set; }
		public float NearestProjectileDistance { get; set; }
		public float NearestProjectileAngle { get; set; } // Relative to boss
		public Vector2 NearestProjectileVelocity { get; set; }
		public float NearestProjectileDamage { get; set; }
	}

	public class StaticState
	{
		public EquipmentData Equipment = new EquipmentData();
		public RoomBounds RoomData = new RoomBounds();
	}

	public class DynamicsState
	{
		public PlayerState Player = new PlayerState();
		public BossState Boss = new BossState();
		public ProjectileState Projectiles = new ProjectileState();
	}

	// ============= PUBLIC STATE INSTANCES =============
	public StaticState statState = new StaticState();
	public DynamicsState dynState = new DynamicsState();
	
	public PlayableCharacter PlayerReference { get; set; } // Direct reference for AI to query additional data if needed
	public BossRL BossReference { get; set; } // Direct reference for AI to query additional data if needed

	// ============= LIFECYCLE =============
	
	public override void _Ready()
	{
		EventBus.OnWeaponEquipped += GetWeaponData;
		EventBus.OnArmorEquipped += GetArmorData;
		EventBus.OnConsumableEquipped += GetConsumableData;

		EventBus.OnRoomEnteredDimensions += GetRoomBounds;
		EventBus.OnPlayerSpawned += AddPlayer;
		EventBus.OnBossSpawned += AddBoss;

		EventBus.OnPlayerHealthChanged += (health) => { dynState.Player.Health = health; };
		EventBus.OnPlayerDamaged += (amount) => { dynState.Player.DamageTaken = amount; };

		EventBus.OnBossDamaged += (amount) => { dynState.Boss.DamageTaken = amount; };

		EventBus.OnGameRestarted += ResetState;
	}
	
	public override void _Process(double delta)
	{
		UpdatePlayerState(PlayerReference);
		UpdateBossState(BossReference);
		UpdateProjectiles();
	}

	// Weapon helper methods
	public void GetWeaponData(Weapon weapon)
	{
		if (weapon == null) return;

		statState.Equipment.WeaponDamageLight = weapon.LightAttackConfig.Damage;
		statState.Equipment.WeaponDamageHeavy = weapon.HeavyAttackConfig.Damage;

		statState.Equipment.WeaponRangeLight = weapon.LightAttackConfig.Range;
		statState.Equipment.WeaponRangeHeavy = weapon.HeavyAttackConfig.Range;

		statState.Equipment.WeaponChargeTimeLight = GetChargeTimeByAttack(weapon.LightAttackConfig);
		statState.Equipment.WeaponChargeTimeHeavy = GetChargeTimeByAttack(weapon.HeavyAttackConfig);
	}

	public float GetChargeTimeByAttack(AttackBase attack)
	{
		if (attack is ChargedAttack chargedAttack)
		{
			return chargedAttack.MinChargeTime;
		}

		return 0f; // Non-charged attacks have 0 charge time
	}

	// Armor helper methods
	public void GetArmorData(Armor armor)
	{
		if (armor == null) return;

		statState.Equipment.ArmorDamageModifier = armor.DamageModifier;
		statState.Equipment.ArmorKnockbackModifier = armor.KnockbackModifier;
		statState.Equipment.ArmorSpeedModifier = armor.SpeedModifier;
	}

	// Consumable helper methods
	public void GetConsumableData(Consumable consumable)
	{
		if (consumable == null)
		{
			statState.Equipment.HasConsumable = false;
			statState.Equipment.ConsumableEffectDuration = 0f;
			statState.Equipment.ConsumableHealAmount = 0f;
			statState.Equipment.ConsumableChargeAmount = 0f;
			return;
		}

		statState.Equipment.HasConsumable = true;
		statState.Equipment.ConsumableEffectDuration = consumable.EffectConfig.Duration;
		statState.Equipment.ConsumableHealAmount = consumable.EffectConfig.EffectValue;
		statState.Equipment.ConsumableChargeAmount = 0f;

		if (consumable.EffectConfig is IChargeableConsumable chargeable){
			statState.Equipment.ConsumableChargeAmount = chargeable.GetMaxChargeTime();
		}
	}

	// Get room bounds from NavigationRegion2D
	public void GetRoomBounds(NavigationRegion2D navRegion)
	{
		var polygon = navRegion.GetNavigationPolygon();

		var vertices = polygon.GetVertices();

		// Calculate bounds from vertices - this accounts for the actual navigable shape
		float minX = float.MaxValue, minY = float.MaxValue;
		float maxX = float.MinValue, maxY = float.MinValue;

		foreach (Vector2 vertex in vertices)
		{
			minX = Mathf.Min(minX, vertex.X);
			minY = Mathf.Min(minY, vertex.Y);
			maxX = Mathf.Max(maxX, vertex.X);
			maxY = Mathf.Max(maxY, vertex.Y);
		}

		statState.RoomData.MinX = minX;
		statState.RoomData.MaxX = maxX;

		statState.RoomData.MinY = minY;
		statState.RoomData.MaxY = maxY;
	}

	// --- Player Data ---

	public void AddPlayer(PlayableCharacter player){
		PlayerReference = player;
		dynState.Player.MaxHealth = player.MaxHealth;
	}

	public void UpdatePlayerState(PlayableCharacter player)
	{
		if (player == null) return;

		dynState.Player.Position = player.GlobalPosition;
		dynState.Player.MovementDirection = player.Velocity.Normalized();
		
		switch(player.CurrentState){
			case EntityState.Idle:
				dynState.Player.CurrentAction = (int)PlayerAction.Idle;
				break;

			case EntityState.Walking:
				dynState.Player.CurrentAction = (int)PlayerAction.Moving;
				break;

			case EntityState.AttackPrepare:
			case EntityState.AttackCharging:
				if(player.EquippedWeapon.IsCurrentAttackHeavy)
					dynState.Player.CurrentAction = (int)PlayerAction.ChargingHeavyAttack;
				else
					dynState.Player.CurrentAction = (int)PlayerAction.ChargingLightAttack;
				break;
			
			case EntityState.Attacking:
				if(player.EquippedWeapon.IsCurrentAttackHeavy)
					dynState.Player.CurrentAction = (int)PlayerAction.PerformingHeavyAttack;
				else
					dynState.Player.CurrentAction = (int)PlayerAction.PerformingLightAttack;
				break;

			case EntityState.ConsumableCharging:
				dynState.Player.CurrentAction = (int)PlayerAction.ChargingConsumable;
				break;

			case EntityState.ConsumableUse:
				dynState.Player.CurrentAction = (int)PlayerAction.UsingConsumable;
				break;

			case EntityState.DodgePrep:
			case EntityState.Dodging:
				dynState.Player.CurrentAction = (int)PlayerAction.Dodging;
				break;

			default:
				dynState.Player.CurrentAction = (int)PlayerAction.Idle; // Default to idle for other states like Hit    
				break;
		}


		dynState.Player.Health = player.CurrentHealth;

		dynState.Player.CurrentLightAttackCooldown = player.EquippedWeapon.LightCooldownTimer;
		dynState.Player.CurrentHeavyAttackCooldown = player.EquippedWeapon.HeavyCooldownTimer;

		if (player.EquippedWeapon.LightAttackConfig is IChargeable lightChargeable)
		{
			dynState.Player.CurrentLightAttackChargeTime = lightChargeable.getCurrentChargeTime();
		}
		
		if (player.EquippedWeapon.HeavyAttackConfig is IChargeable heavyChargeable)
		{
			dynState.Player.CurrentLightAttackChargeTime = heavyChargeable.getCurrentChargeTime();
		}

		dynState.Player.InvulnerabilityTimeRemaining = player.InvulnerabilityTimer;

		dynState.Player.DamageTaken = 0f;

		if (player.EquippedConsumable == null) return;

		if (player.EquippedConsumable.EffectConfig is IChargeableConsumable chargeableCons)
		{
			dynState.Player.CurrentConsumableChargeTime = chargeableCons.GetCurrentChargeTime();
		}

		
	}

	public void AddBoss(BossRL bossRef){
		BossReference = bossRef;
		dynState.Boss.MaxHealth = bossRef.MaxHealth;
		
		dynState.Boss.AttackCooldowns[0] = bossRef.AttackManager.MeleeAttack1.Cooldown;
		dynState.Boss.AttackCooldowns[1] = bossRef.AttackManager.MeleeAttack2.Cooldown;
		dynState.Boss.AttackCooldowns[2] = bossRef.AttackManager.MagicAttack1.Cooldown;
		dynState.Boss.AttackCooldowns[3] = bossRef.AttackManager.MagicAttack2.Cooldown;
	}

	public void UpdateBossState(BossRL bossRef){
		if (bossRef == null) return;
		if (bossRef.AttackManager == null)
		{
			GD.PrintErr("[GlobalState] BossRL.AttackManager is null — BossAttackManager node missing from boss scene");
			return;
		}

		dynState.Boss.Position = bossRef.Position;
		dynState.Boss.Health = bossRef.CurrentHealth;

		dynState.Boss.AttackCooldownTimers[0] = (float) bossRef.AttackManager.MeleeAttack1CooldownTimer.TimeLeft;
		dynState.Boss.AttackCooldownTimers[1] = (float) bossRef.AttackManager.MeleeAttack2CooldownTimer.TimeLeft;
		dynState.Boss.AttackCooldownTimers[2] = (float) bossRef.AttackManager.MagicAttack1CooldownTimer.TimeLeft;
		dynState.Boss.AttackCooldownTimers[3] = (float) bossRef.AttackManager.MagicAttack2CooldownTimer.TimeLeft;

		dynState.Boss.CurrentAttackId = (short)bossRef.AttackManager.CurrentAttack;
		dynState.Boss.InvulnerabilityTimeRemaining = (float) bossRef.InvulnerabilityTimer.TimeLeft;

		dynState.Boss.DistanceToPlayer = dynState.Boss.Position.DistanceTo(dynState.Player.Position);
		dynState.Boss.AngleToPlayer = dynState.Boss.Position.AngleTo(dynState.Player.Position);
	
		dynState.Boss.DamageTaken = 0f;
	}

	public void UpdateProjectiles(){
		var playerProjectileInstances = GetTree().GetNodesInGroup("PlayerProjectile");

		dynState.Projectiles.ActiveProjectileCount = playerProjectileInstances.Count;

		if (dynState.Projectiles.ActiveProjectileCount <= 0) return;

		float minDistance = float.PositiveInfinity;
		Node2D closestProjectile = null;

		foreach(Node2D proj in playerProjectileInstances.Cast<Node2D>())
		{
			var newDistance = dynState.Boss.Position.DistanceTo(proj.GlobalPosition);
			
			if(newDistance < minDistance)
			{
				minDistance = newDistance;
				closestProjectile = proj;
			}
		}

		if(closestProjectile is not Projectile projectile) return;

		dynState.Projectiles.NearestProjectileDistance = minDistance;
		dynState.Projectiles.NearestProjectileAngle = projectile.GetAngleTo(dynState.Boss.Position);
		dynState.Projectiles.NearestProjectileVelocity = projectile.Velocity;
		dynState.Projectiles.NearestProjectileDamage = projectile.Damage;
	}

	public void ResetState()
	{
		PlayerReference = null;
		BossReference = null;
		statState = new StaticState();
		dynState = new DynamicsState();
	}
}

/// <summary>
/// Represents all possible actions the player can perform.
/// Used for AI observation of player behavior.
/// </summary>
public enum PlayerAction
{
	Idle = 0,
	Moving = 1,
	ChargingLightAttack = 2,
	PerformingLightAttack = 3,
	ChargingHeavyAttack = 4,
	PerformingHeavyAttack = 5,
	ChargingConsumable = 6,
	UsingConsumable = 7,
	Dodging = 8
}
