using Godot;

[GlobalClass]
public partial class VisualBlinking : VisualEffect
{
	[Export] public Color ModulateA { get; set; } = new Color(1, 1, 1); // First color to alternate
	[Export] public Color ModulateB { get; set; } = new Color(1, 0, 0); // Second color to alternate
	[Export] public float BlinkInterval { get; set; } = 0.2f; // Time between color switches in seconds

	public override void PlayEffect(Sprite2D sprite)
	{
		// Use a tween to handle the blinking automatically
		EffectTimer = 0f;

		// Create a looping tween that alternates between the two colors
		Tween tween = sprite.CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.SetLoops(Mathf.CeilToInt(EffectDuration / (BlinkInterval * 2))); // Calculate number of full blink cycles
		
		// Alternate between the two colors
		tween.TweenProperty(sprite, "modulate", ModulateA, BlinkInterval);
		tween.TweenProperty(sprite, "modulate", ModulateB, BlinkInterval);
		
		// Restore original color when done
		tween.TweenCallback(Callable.From(() => sprite.Modulate = _originalModulate));
	}

	public override void PlayEffect(Node2D spriteContainer)
	{
		foreach (var child in spriteContainer.GetChildren())
		{
			if (child is Sprite2D sprite)
			{
				PlayEffect(sprite);
			}
		}
	}
}
