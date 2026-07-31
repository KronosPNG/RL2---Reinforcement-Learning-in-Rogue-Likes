using Godot;

public interface IDamageable
{
    public void ApplyDamage(float damage, Node2D attacker, float knockbackStrength);
    public void Heal(float amount);
    public void Die();
}