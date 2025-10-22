using Godot;

public interface IVisual
{
    void InitializeVisuals(AnimatedSprite2D sprite);
    void UpdateTimer(float delta);
    void PlayEffect(Node2D spriteContainer);
    void PlayEffect(AnimatedSprite2D sprite);
    void ClearEffect(Node2D spriteContainer);
    void ClearEffect(AnimatedSprite2D sprite);
    void ResetTimer();
    void EndTimer();
}