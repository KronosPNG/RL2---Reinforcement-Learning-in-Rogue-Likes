using System;
using Godot;

[GlobalClass]
public partial class VisualFlash : Resource, IVisual
{
    [Export] public Color FlashModulate { get; set; } = new Color(1, 0.75f, 0.75f); // Light red color for damage flash
    [Export] public float FlashDuration { get; private set; } = 0.2f; // Duration of the damage flash effect in seconds

    private Color _originalModulate;
    public float Timer { get; private set; } = 0f;
    
    public void InitializeVisuals(AnimatedSprite2D sprite)
    {
        _originalModulate = sprite.Modulate;
    }

    public void UpdateTimer(float delta)
    {
        Timer -= delta;
    }

    public void PlayEffect(AnimatedSprite2D sprite)
    {
        sprite.Modulate = FlashModulate;
    }

    public void PlayEffect(Node2D spriteContainer)
    {
        foreach (var child in spriteContainer.GetChildren())
        {
            if (child is AnimatedSprite2D sprite)
            {
                PlayEffect(sprite);
            }
        }
    }

    public void ClearEffect(AnimatedSprite2D sprite)
    {
        sprite.Modulate = _originalModulate;
    }

    public void ClearEffect(Node2D spriteContainer)
    {
        foreach (var child in spriteContainer.GetChildren())
        {
            if (child is AnimatedSprite2D sprite)
            {
                ClearEffect(sprite);
            }
        }
    }

    public void ResetTimer()
    {
        Timer = FlashDuration;
    }

    public void EndTimer()
    {
        Timer = 0f;
    }

}