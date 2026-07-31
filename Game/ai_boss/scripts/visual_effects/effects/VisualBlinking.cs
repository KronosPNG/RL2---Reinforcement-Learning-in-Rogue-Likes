using Godot;

[GlobalClass]
public partial class VisualBlinking : VisualEffect
{
	[Export] public Color ModulateA { get; set; } = new Color(1, 1, 1); // First color to alternate
	[Export] public Color ModulateB { get; set; } = new Color(1, 0, 0); // Second color to alternate
	[Export] public float BlinkInterval { get; set; } = 0.2f; // Time between color switches in seconds

	public override void PlayEffect(CanvasItem element)
	{
		// Create a looping tween that alternates between the two colors
		Tween tween = element.CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.SetLoops(Mathf.CeilToInt(EffectDuration / (BlinkInterval * 2))); // Calculate number of full blink cycles
		
		// Alternate between the two colors
		tween.TweenProperty(element, "modulate", ModulateA, BlinkInterval);
		tween.TweenProperty(element, "modulate", ModulateB, BlinkInterval);
		
		// Restore original color when done
		tween.TweenCallback(Callable.From(() => ClearEffect(element)));
	}
}
