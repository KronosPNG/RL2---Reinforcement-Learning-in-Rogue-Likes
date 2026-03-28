using Godot;

[GlobalClass]
public abstract partial class VisualEffect : Resource
{
    protected Color _originalModulate;
    [Export] public float EffectDuration { get; set; } = 2.0f; // Total duration of the blinking effect in seconds

    public virtual void InitializeVisuals(CanvasItem element)
    {
        _originalModulate = element.Modulate;
    }

    public abstract void PlayEffect(CanvasItem element);

    public virtual void ClearEffect(CanvasItem element)
    {
        element.Modulate = _originalModulate;
    }



}