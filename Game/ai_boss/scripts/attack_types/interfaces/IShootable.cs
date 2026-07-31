using Godot;

public interface IShootable
{
    void SpawnProjectile(WeaponBase weapon, Vector2 spawnPosition, Vector2 baseDirection, int projectileIndex);
    
}
