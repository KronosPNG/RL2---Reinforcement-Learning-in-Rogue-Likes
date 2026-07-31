using System;
using Godot;

/// <summary>
/// Aggro behavior that maintains a specific distance from the player
/// Moves closer if too far, moves away if too close
/// </summary>
[GlobalClass]
public partial class AggroKeepDistance : AggroBehaviour
{
	[ExportGroup("Distance Keeping Properties")]
	[Export] public float PreferredDistance { get; set; } = 150f;
	[Export] public float DistanceTolerance { get; set; } = 20f;
	[Export] public float StrafeSpeed { get; set; } = 12f;
	[Export] public bool EnableStrafing { get; set; } = true;
	[Export] public float MinStrafeDuration { get; set; } = 1.0f;
	[Export] public float MaxStrafeDuration { get; set; } = 2.5f;
	
	private float _strafeDirection = 1f;
	private float _strafeTimer = 0f;
	private float _currentStrafeInterval = 1.5f;
	private float lastDistanceFromTarget = 0f;

	public override void Initialize(IEntity entity)
	{
		if(entity is PlayerMimic player)
		{
			var weapon = player.EquippedWeapon;
			var lightRange = weapon.LightAttackConfig.Range;
			var heavyRange = weapon.HeavyAttackConfig.Range;
			
			PreferredDistance = (lightRange <= heavyRange) ? lightRange : heavyRange;
			DistanceTolerance = (PreferredDistance / 100) * 15; // 15 % of pref distance
		}
	}

	public override bool CanSeeTarget(IEntity entity)
	{
		return base.CanSeeTarget(entity);
	}

	public override Vector2 GetChaseDirection(IEntity entity, float delta)
	{
		if (entity.Target == null || !IsInstanceValid(entity.Target))
			return Vector2.Zero;

		Vector2 toTarget = entity.Target.GlobalPosition - entity.GlobalPosition;
		float distance = toTarget.Length();
		Vector2 direction = toTarget.Normalized();
		
		Vector2 _finalDirection;

		// Move closer or away to maintain preferred distance
		if (distance > PreferredDistance + DistanceTolerance)
		{
			// Too far - move closer
			_finalDirection = direction;
		}
		else if (distance < PreferredDistance - DistanceTolerance)
		{
			// Too close - move away
			_finalDirection = -direction;
		}
		else if (EnableStrafing)
		{
			// At good distance - strafe sideways
			Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
			_finalDirection = perpendicular * _strafeDirection;
		}
		else
		{
			// At preferred distance but not strafing - stay idle and face player
			_finalDirection = Vector2.Zero;
			
			// Update facing direction to look at the player
			// The IEntity class will use this to flip the sprite when velocity is zero
			entity.FacingDirection = direction;
		}
		
		

		lastDistanceFromTarget = distance;

		return _finalDirection;
	}

	public override void OnEnterNotice(IEntity entity)
	{
		_strafeDirection = GD.Randf() > 0.5f ? 1f : -1f;
		_strafeTimer = 0f;
		_currentStrafeInterval = (float)GD.RandRange(MinStrafeDuration, MaxStrafeDuration);
	}

	public override void OnExitNotice(IEntity entity)
	{
		_strafeDirection = 1f;
		_strafeTimer = 0f;
	}

	public override void PerformAggroBehaviour(IEntity entity)
	{
		if (entity is not CharacterBody2D ent)
			return;


		float deltaTime = (float)ent.GetPhysicsProcessDeltaTime();
		
		if (!EnableStrafing)
			return;

		_strafeTimer += deltaTime;
		
		// Randomly change strafe direction
		if (_strafeTimer >= _currentStrafeInterval)
		{
			_strafeDirection *= -1f;
			_strafeTimer = 0f;
			_currentStrafeInterval = (float)GD.RandRange(MinStrafeDuration, MaxStrafeDuration);
		}
	}

	public override bool ShouldLoseTarget(IEntity entity)
	{
		if (entity.Target == null || !IsInstanceValid(entity.Target))
			return true;

		float distance = entity.GlobalPosition.DistanceTo(entity.Target.GlobalPosition);
		
		// Lose target if way too far
		if (distance > DetectionRange * 1.5f)
			return true;

		return false;
	}
}
