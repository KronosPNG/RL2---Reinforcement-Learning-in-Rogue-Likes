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
	[Export] public float StrafeSpeed { get; set; } = 300f;
	[Export] public bool EnableStrafing { get; set; } = true;
	[Export] public float MinStrafeDuration { get; set; } = 1.0f;
	[Export] public float MaxStrafeDuration { get; set; } = 2.5f;
	
	private float _strafeDirection = 1f;
	private float _strafeTimer = 0f;
	private float _currentStrafeInterval = 1.5f;
	private float lastDistanceFromTarget = 0f;

	public override bool CanSeeTarget(Entity entity)
	{
		return base.CanSeeTarget(entity);
	}

	public override Vector2 GetChaseVelocity(Entity entity, float delta)
	{
		if (entity.Target == null || !IsInstanceValid(entity.Target))
			return Vector2.Zero;

		Vector2 toTarget = entity.Target.GlobalPosition - entity.GlobalPosition;
		float distance = toTarget.Length();
		Vector2 direction = toTarget.Normalized();
		
		Vector2 velocity;

		animationName = "walk";
		
		// Move closer or away to maintain preferred distance
		if (distance > PreferredDistance + DistanceTolerance)
		{
			// Too far - move closer
			velocity = direction * entity.BaseSpeed * ChaseSpeedModifier * delta;
		}
		else if (distance < PreferredDistance - DistanceTolerance)
		{
			// Too close - move away
			velocity = -direction * ChaseSpeedModifier * entity.BaseSpeed * delta;
		}
		else if (EnableStrafing)
		{
			// At good distance - strafe sideways
			Vector2 perpendicular = new Vector2(-direction.Y, direction.X);
			velocity = perpendicular * _strafeDirection * StrafeSpeed * delta;
		}
		else
		{
			// At preferred distance but not strafing - stay idle and face player
			velocity = Vector2.Zero;
			animationName = "idle";
			
			// Update facing direction to look at the player
			// The Entity class will use this to flip the sprite when velocity is zero
			entity.FacingDirection = direction;
		}

		lastDistanceFromTarget = distance;

		return velocity;
	}

	public override void OnEnterNotice(Entity entity)
	{
		_strafeDirection = GD.Randf() > 0.5f ? 1f : -1f;
		_strafeTimer = 0f;
		_currentStrafeInterval = (float)GD.RandRange(MinStrafeDuration, MaxStrafeDuration);
	}

	public override void OnExitNotice(Entity entity)
	{
		_strafeDirection = 1f;
		_strafeTimer = 0f;
	}

	public override void PerformAggroBehaviour(Entity entity)
	{
		float deltaTime = (float)entity.GetPhysicsProcessDeltaTime();
		
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

	public override bool ShouldLoseTarget(Entity entity)
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
