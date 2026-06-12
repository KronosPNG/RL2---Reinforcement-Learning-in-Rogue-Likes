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
		public PlayerAction CurrentAction { get; set; }
		
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
	
	// ============= PUBLIC STATE INSTANCES =============
	
	public EquipmentData Equipment { get; set; } = new EquipmentData();
	public RoomBounds RoomData { get; set; } = new RoomBounds();
	
	public PlayerState Player { get; set; } = new PlayerState();
	public BossState Boss { get; set; } = new BossState();
	public ProjectileState Projectiles { get; set; } = new ProjectileState();
	
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

		EventBus.OnPlayerHealthChanged += (health) => { Player.Health = health; };
		EventBus.OnPlayerDamaged += (amount) => { Player.DamageTaken = amount; };

		EventBus.OnBossDamaged += (amount) => { Boss.DamageTaken = amount; };

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

		Equipment.WeaponDamageLight = weapon.LightAttackConfig.Damage;
		Equipment.WeaponDamageHeavy = weapon.HeavyAttackConfig.Damage;

		Equipment.WeaponRangeLight = weapon.LightAttackConfig.Range;
		Equipment.WeaponRangeHeavy = weapon.HeavyAttackConfig.Range;

		Equipment.WeaponChargeTimeLight = GetChargeTimeByAttack(weapon.LightAttackConfig);
		Equipment.WeaponChargeTimeHeavy = GetChargeTimeByAttack(weapon.HeavyAttackConfig);
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

		Equipment.ArmorDamageModifier = armor.DamageModifier;
		Equipment.ArmorKnockbackModifier = armor.KnockbackModifier;
		Equipment.ArmorSpeedModifier = armor.SpeedModifier;
	}

	// Consumable helper methods
	public void GetConsumableData(Consumable consumable)
	{
		if (consumable == null)
		{
			Equipment.HasConsumable = false;
			Equipment.ConsumableEffectDuration = 0f;
			Equipment.ConsumableHealAmount = 0f;
			Equipment.ConsumableChargeAmount = 0f;
			return;
		}

		Equipment.HasConsumable = true;
		Equipment.ConsumableEffectDuration = consumable.EffectConfig.Duration;
		Equipment.ConsumableHealAmount = consumable.EffectConfig.EffectValue;
		Equipment.ConsumableChargeAmount = 0f;

		if (consumable.EffectConfig is IChargeableConsumable chargeable){
			Equipment.ConsumableChargeAmount = chargeable.GetMaxChargeTime();
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

		RoomData.MinX = minX;
		RoomData.MaxX = maxX;

		RoomData.MinY = minY;
		RoomData.MaxY = maxY;
	}

	// --- Player Data ---

	public void AddPlayer(PlayableCharacter player){
		PlayerReference = player;
		Player.MaxHealth = player.MaxHealth;
	}

	public void UpdatePlayerState(PlayableCharacter player)
	{
		if (player == null) return;

		Player.Position = player.GlobalPosition;
		Player.MovementDirection = player.Velocity.Normalized();
		
		switch(player.CurrentState){
			case EntityState.Idle:
				Player.CurrentAction = PlayerAction.Idle;
				break;

			case EntityState.Walking:
				Player.CurrentAction = PlayerAction.Moving;
				break;

			case EntityState.AttackPrepare:
			case EntityState.AttackCharging:
				if(player.EquippedWeapon.IsCurrentAttackHeavy)
					Player.CurrentAction = PlayerAction.ChargingHeavyAttack;
				else
					Player.CurrentAction = PlayerAction.ChargingLightAttack;
				break;
			
			case EntityState.Attacking:
				if(player.EquippedWeapon.IsCurrentAttackHeavy)
					Player.CurrentAction = PlayerAction.PerformingHeavyAttack;
				else
					Player.CurrentAction = PlayerAction.PerformingLightAttack;
				break;

			case EntityState.ConsumableCharging:
				Player.CurrentAction = PlayerAction.ChargingConsumable;
				break;

			case EntityState.ConsumableUse:
				Player.CurrentAction = PlayerAction.UsingConsumable;
				break;

			case EntityState.DodgePrep:
			case EntityState.Dodging:
				Player.CurrentAction = PlayerAction.Dodging;
				break;

			default:
				Player.CurrentAction = PlayerAction.Idle; // Default to idle for other states like Hit    
				break;
		}


		Player.Health = player.CurrentHealth;

		Player.CurrentLightAttackCooldown = player.EquippedWeapon.LightCooldownTimer;
		Player.CurrentHeavyAttackCooldown = player.EquippedWeapon.HeavyCooldownTimer;

		if (player.EquippedWeapon.LightAttackConfig is IChargeable lightChargeable)
		{
			Player.CurrentLightAttackChargeTime = lightChargeable.getCurrentChargeTime();
		}
		
		if (player.EquippedWeapon.HeavyAttackConfig is IChargeable heavyChargeable)
		{
			Player.CurrentLightAttackChargeTime = heavyChargeable.getCurrentChargeTime();
		}

		Player.InvulnerabilityTimeRemaining = player.InvulnerabilityTimer;

		Player.DamageTaken = 0f;

		if (player.EquippedConsumable == null) return;

		if (player.EquippedConsumable.EffectConfig is IChargeableConsumable chargeableCons)
		{
			Player.CurrentConsumableChargeTime = chargeableCons.GetCurrentChargeTime();
		}

		
	}

	public void AddBoss(BossRL bossRef){
		BossReference = bossRef;
		Boss.MaxHealth = bossRef.MaxHealth;
		
		Boss.AttackCooldowns[0] = bossRef.AttackManager.MeleeAttack1.Cooldown;
		Boss.AttackCooldowns[1] = bossRef.AttackManager.MeleeAttack2.Cooldown;
		Boss.AttackCooldowns[2] = bossRef.AttackManager.MagicAttack1.Cooldown;
		Boss.AttackCooldowns[3] = bossRef.AttackManager.MagicAttack2.Cooldown;
	}

	public void UpdateBossState(BossRL bossRef){
		if (bossRef == null) return;

		Boss.Position = bossRef.Position;
		Boss.Health = bossRef.CurrentHealth;

		Boss.AttackCooldownTimers[0] = (float) bossRef.AttackManager.MeleeAttack1CooldownTimer.TimeLeft;
		Boss.AttackCooldownTimers[1] = (float) bossRef.AttackManager.MeleeAttack2CooldownTimer.TimeLeft;
		Boss.AttackCooldownTimers[2] = (float) bossRef.AttackManager.MagicAttack1CooldownTimer.TimeLeft;
		Boss.AttackCooldownTimers[3] = (float) bossRef.AttackManager.MagicAttack2CooldownTimer.TimeLeft;

		Boss.CurrentAttackId = (short)bossRef.AttackManager.CurrentAttack;
		Boss.InvulnerabilityTimeRemaining = (float) bossRef.InvulnerabilityTimer.TimeLeft;

		Boss.DistanceToPlayer = Boss.Position.DistanceTo(Player.Position);
		Boss.AngleToPlayer = Boss.Position.AngleTo(Player.Position);
	
		Boss.DamageTaken = 0f;
	}

	public void UpdateProjectiles(){
		var playerProjectileInstances = GetTree().GetNodesInGroup("PlayerProjectile");

		Projectiles.ActiveProjectileCount = playerProjectileInstances.Count;

		if (Projectiles.ActiveProjectileCount <= 0) return;

		float minDistance = float.PositiveInfinity;
		Node2D closestProjectile = null;

		foreach(Node2D proj in playerProjectileInstances.Cast<Node2D>())
		{
			var newDistance = Boss.Position.DistanceTo(proj.GlobalPosition);
			
			if(newDistance < minDistance)
			{
				minDistance = newDistance;
				closestProjectile = proj;
			}
		}

		if(closestProjectile is not Projectile projectile) return;

		Projectiles.NearestProjectileDistance = minDistance;
		Projectiles.NearestProjectileAngle = projectile.GetAngleTo(Boss.Position);
		Projectiles.NearestProjectileVelocity = projectile.Velocity;
		Projectiles.NearestProjectileDamage = projectile.Damage;
	}

	public void ResetState()
	{
		PlayerReference = null;
		BossReference = null;

		Equipment = new EquipmentData();
		RoomData = new RoomBounds();

		Player = new PlayerState();
		Boss = new BossState();
		Projectiles = new ProjectileState();
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
