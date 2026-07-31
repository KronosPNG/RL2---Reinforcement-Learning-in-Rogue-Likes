using System;
using Godot;

[GlobalClass]
public partial class VisualFlash : VisualEffect
{
	[Export] public Color FlashModulate { get; set; } = new Color(1, 0.75f, 0.75f); // Light red color for damage flash
	
	public override void InitializeVisuals(CanvasItem element)
	{
		_originalModulate = element.Modulate;
	}


	public override void PlayEffect(CanvasItem element)
	{
		Tween tween = element.CreateTween();
		tween.SetPauseMode(Tween.TweenPauseMode.Process);
		
		tween.TweenProperty(element, "modulate", FlashModulate, 0);

		tween.TweenProperty(element, "modulate", _originalModulate, EffectDuration / 2)
			.SetDelay(EffectDuration / 2); // Start flash at the midpoint of the effect duration

		tween.TweenCallback(Callable.From(() => ClearEffect(element)));
	}

}
