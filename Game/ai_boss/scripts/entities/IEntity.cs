using Godot;

public interface IEntity
{
    // Health and status properties
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }
    bool IsInvulnerable { get; }

    // Status effects and damage types
    void ApplyStatusEffect(StatusEffectType effectType, float duration, float intensity = 1.0f);
    void RemoveStatusEffect(StatusEffectType effectType);
    bool HasStatusEffect(StatusEffectType effectType);
    
    // Visual and audio feedback
    void ShowDamageNumber(float damage);
    void PlayHitEffect(Vector2 hitPosition);
    void PlayDeathEffect();
}
