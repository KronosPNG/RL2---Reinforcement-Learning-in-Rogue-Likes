using Godot;

public interface IVisualEffect
{
    void InitializeVisuals(CanvasItem element);
    void PlayEffect(CanvasItem element);
    void ClearEffect(CanvasItem element);
}