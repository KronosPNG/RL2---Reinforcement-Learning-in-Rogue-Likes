using Godot;

[GlobalClass]
public abstract partial class VisualEffect : Resource, IVisualEffect
{
    protected Color _originalModulate;
    public float EffectTimer { get; protected set; } = 0f;
    [Export] public float EffectDuration { get; set; } = 2.0f; // Total duration of the blinking effect in seconds

    public virtual void InitializeVisuals(Sprite2D sprite)
    {
        _originalModulate = sprite.Modulate;
    }

    public virtual void PlayEffect(Node2D spriteContainer)
    {
        foreach (var child in spriteContainer.GetChildren())
		{
			if (child is Sprite2D sprite)
			{
				PlayEffect(sprite);
			}
		}
    }

    public abstract void PlayEffect(Sprite2D sprite);

    public virtual void ClearEffect(Node2D spriteContainer)
    {
        foreach (var child in spriteContainer.GetChildren())
		{
			if (child is Sprite2D sprite)
			{
				ClearEffect(sprite);
			}
		}
    }

    public virtual void ClearEffect(Sprite2D sprite)
    {
        sprite.Modulate = _originalModulate;
    }

    public virtual void UpdateTimer(float delta)
    {
        EffectTimer -= delta;
    }

    public virtual void ResetTimer()
    {
        EffectTimer = EffectDuration;
    }

    public virtual void EndTimer()
    {
        EffectTimer = 0f;
    }

}