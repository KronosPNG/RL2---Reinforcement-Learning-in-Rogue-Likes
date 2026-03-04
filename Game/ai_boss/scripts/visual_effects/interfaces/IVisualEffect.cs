using Godot;

public interface IVisualEffect
{
    void InitializeVisuals(Sprite2D sprite);
    void UpdateTimer(float delta);
    void PlayEffect(Node2D spriteContainer);
    void PlayEffect(Sprite2D sprite);
    void ClearEffect(Node2D spriteContainer);
    void ClearEffect(Sprite2D sprite);
    void ResetTimer();
    void EndTimer();
}