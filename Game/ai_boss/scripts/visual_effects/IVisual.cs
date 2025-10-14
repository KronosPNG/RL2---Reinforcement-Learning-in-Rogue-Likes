using Godot;

public interface IVisual
{
    void InitializeVisuals(AnimatedSprite2D sprite);
    void UpdateTimer(float delta);
    void PlayEffect(AnimatedSprite2D sprite);
    void ClearEffect(AnimatedSprite2D sprite);
    void ResetTimer();
    void EndTimer();
}