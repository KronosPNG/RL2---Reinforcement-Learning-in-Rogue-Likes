using Godot;

/// <summary>
/// Manages camera following the player with proper smoothing.
/// Decouples the camera from the player physics body to prevent trembling.
/// </summary>
public partial class CameraController : Camera2D
{
	[Export] public Node2D TargetNode { get; set; }
	[Export] public float SmoothSpeed = 16.0f;
	
	public override void _Ready()
	{
		if (TargetNode == null)
		{
			SetTarget(GetParent<Node2D>());
		}
		
		// Initial position
		GlobalPosition = TargetNode.GlobalPosition;
		MakeCurrent();
	}

	public override void _Process(double delta)
	{
		if (TargetNode == null) return;

		// Smoothly follow target position
		GlobalPosition = GlobalPosition.Lerp(TargetNode.GlobalPosition, SmoothSpeed * (float)delta);
	}

	public void SetTarget(Node2D newTarget)
	{
		if(TargetNode is PlayerController player)
		{
			player.PlayerDamaged -= Shake;
		}

		if (newTarget is PlayerController newPlayer)
		{
			newPlayer.PlayerDamaged += Shake;
		}
		
		TargetNode = newTarget;
	}

	public void ChangeTarget(Node2D newTarget)
	{
		// Smoothly transition to new target by first moving towards it, then switching
		var tween = CreateTween();
		tween.TweenProperty(this, "GlobalPosition", newTarget.GlobalPosition, 0.5f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		
		tween.Connect("finished", Callable.From(() =>
		{
			SetTarget(newTarget);
		}));
	}

	public void Shake(float intensity, float duration)
	{
		// GD.Print($"Camera shake triggered with intensity {intensity} and duration {duration}");

		var tween = CreateTween();
		var shakeCount = (int)(duration * 10); // Number of shake oscillations
		var shakeDuration = duration / shakeCount;
		
		// Create rapid back-and-forth oscillations
		for (int i = 0; i < shakeCount; i++)
		{
			var randomOffset = new Vector2(
				(float)GD.Randf() * intensity * (GD.Randf() > 0.5f ? 1 : -1),
				(float)GD.Randf() * intensity * (GD.Randf() > 0.5f ? 1 : -1)
			);
			
			tween.TweenProperty(this, "offset", randomOffset, shakeDuration)
				.SetTrans(Tween.TransitionType.Linear);
		}
		
		// Reset offset to zero at the end
		tween.TweenProperty(this, "offset", Vector2.Zero, 0);
	}
}
