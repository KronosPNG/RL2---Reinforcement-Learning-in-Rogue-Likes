using Godot;

[GlobalClass]
public partial class VisualFading : VisualEffect
{
	[Export] public Color DeathModulate { get; set; } = new Color(0.5f, 0.5f, 0.5f); // Grey color for death decolouring
	private Color _startDeathColor; // Color at the moment death started

	public override void PlayEffect(CanvasItem element)
	{
		// Use a tween so the effect runs reliably without needing per-frame updates from Entity.
		_startDeathColor = element.Modulate;

		// Create a tween on the sprite to transition its modulate to the death color.
		// This decouples the effect from _PhysicsProcess and state early-returns.
		Tween tween = element.CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		tween.TweenProperty(element, "modulate", DeathModulate, EffectDuration);
	}
}
