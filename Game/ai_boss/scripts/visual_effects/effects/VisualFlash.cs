using System;
using Godot;

[GlobalClass]
public partial class VisualFlash : VisualEffect
{
	[Export] public Color FlashModulate { get; set; } = new Color(1, 0.75f, 0.75f); // Light red color for damage flash
	
	public override void InitializeVisuals(Sprite2D sprite)
	{
		_originalModulate = sprite.Modulate;
	}

	public override void UpdateTimer(float delta)
	{
		EffectTimer -= delta;
	}

	public override void PlayEffect(Sprite2D sprite)
	{
		sprite.Modulate = FlashModulate;
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

	public override void ClearEffect(Sprite2D sprite)
	{
		sprite.Modulate = _originalModulate;
	}

	public override void ClearEffect(Node2D spriteContainer)
	{
		foreach (var child in spriteContainer.GetChildren())
		{
			if (child is Sprite2D sprite)
			{
				ClearEffect(sprite);
			}
		}
	}

	public override void ResetTimer()
	{
		EffectTimer = EffectDuration;
	}

	public override void EndTimer()
	{
		EffectTimer = 0f;
	}

}
