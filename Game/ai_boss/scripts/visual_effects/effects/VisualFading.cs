using Godot;

[GlobalClass]
public partial class VisualFading : VisualEffect
{
	[Export] public Color DeathModulate { get; set; } = new Color(0.5f, 0.5f, 0.5f); // Grey color for death decolouring
	private Color _startDeathColor; // Color at the moment death started

	public override void PlayEffect(Sprite2D sprite)
	{
		// Use a tween so the effect runs reliably without needing per-frame updates from Entity.
		_startDeathColor = sprite.Modulate;
		EffectTimer = 0f;

		// Create a tween on the sprite to transition its modulate to the death color.
		// This decouples the effect from _PhysicsProcess and state early-returns.
		Tween tween = sprite.CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.TweenProperty(sprite, "modulate", DeathModulate, EffectDuration);
	}

	public bool UpdateEffect(Sprite2D sprite)
	{
		GD.PrintErr("Updating death effect");
		// Smoothly transition to death color
		float progress = Mathf.Clamp(EffectTimer / EffectDuration, 0f, 1f);
		sprite.Modulate = _startDeathColor.Lerp(DeathModulate, progress);

		return progress >= 1f;
	}

	public bool UpdateEffect(Node2D spriteContainer)
	{
		bool allDone = true;
		foreach (var child in spriteContainer.GetChildren())
		{
			if (child is Sprite2D sprite)
			{
				allDone &= UpdateEffect(sprite);
			}
		}
		return allDone;
	}
}
