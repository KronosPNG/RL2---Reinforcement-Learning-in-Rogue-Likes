using Godot;

public interface IHasHealth
{
    // Health properties
    float CurrentHealth { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }
    bool IsInvulnerable { get; }
    
    // Visual and audio feedback
    void ShowDamageNumber(float damage);
    void PlayHitEffect(Vector2 hitPosition);
    void PlayDeathEffect();
}
