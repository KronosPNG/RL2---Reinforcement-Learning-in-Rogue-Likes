using Godot;

[GlobalClass]
public partial class VisualFading : Resource, IVisual
{
    [Export] public Color DeathModulate { get; set; } = new Color(0.5f, 0.5f, 0.5f); // Grey color for death decolouring
    [Export] public float DeathEffectDuration { get; set; } = 1.0f;
    private float _deathEffectTimer = 0f;
    private Color _originalModulate;
    private Color _startDeathColor; // Color at the moment death started

    public void InitializeVisuals(AnimatedSprite2D sprite)
    {
        _originalModulate = sprite.Modulate;
    }

    public void PlayEffect(AnimatedSprite2D sprite)
    {
        // Use a tween so the effect runs reliably without needing per-frame updates from Entity.
        _startDeathColor = sprite.Modulate;
        _deathEffectTimer = 0f;

        // Create a tween on the sprite to transition its modulate to the death color.
        // This decouples the effect from _PhysicsProcess and state early-returns.
        Tween tween = sprite.CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(sprite, "modulate", DeathModulate, DeathEffectDuration);
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

    public void UpdateTimer(float delta)
    {
        _deathEffectTimer += delta;
    }

    public bool UpdateEffect(AnimatedSprite2D sprite)
    {
        GD.PrintErr("Updating death effect");
        // Smoothly transition to death color
        float progress = Mathf.Clamp(_deathEffectTimer / DeathEffectDuration, 0f, 1f);
        sprite.Modulate = _startDeathColor.Lerp(DeathModulate, progress);

        return progress >= 1f;
    }

    public bool UpdateEffect(Node2D spriteContainer)
    {
        bool allDone = true;
        foreach (var child in spriteContainer.GetChildren())
        {
            if (child is AnimatedSprite2D sprite)
            {
                allDone &= UpdateEffect(sprite);
            }
        }
        return allDone;
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
        _deathEffectTimer = DeathEffectDuration;
    }
    
    public void EndTimer()
    {
        _deathEffectTimer = 0f;
    }
}