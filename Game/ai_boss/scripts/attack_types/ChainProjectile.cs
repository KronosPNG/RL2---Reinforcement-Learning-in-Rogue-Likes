using Godot;

[GlobalClass]
public partial class ChainProjectile : ProjectileAttack
{
    [ExportGroup("Chain Properties")]
    [Export] public int ChainCount = 3; // Number of chained projectiles
    [Export] public float ChainDelay = 0.2f; // Delay between chained projectiles
    [Export] public float ProjectileDistance = 100f; // Distance between chained projectiles
    private int _currentChain = 0;
    private Vector2 _lastProjectilePosition;

    public override void SpawnProjectile(WeaponBase weapon, Vector2 spawnPosition, Vector2 baseDirection, int projectileIndex)
    {   
        if(_currentChain < ChainCount)
        {   
            base.SpawnProjectile(weapon, spawnPosition, baseDirection, projectileIndex);
            _currentChain++;
            _lastProjectilePosition = spawnPosition;
            // Schedule next projectile in the chain
            weapon.GetTree().CreateTimer(ChainDelay).Connect("timeout", Callable.From(() =>
            {
                Vector2 nextSpawnPosition = _lastProjectilePosition + (baseDirection * ProjectileDistance);
                SpawnProjectile(weapon, nextSpawnPosition, baseDirection, projectileIndex);
            }));


            return;
        }

        _currentChain = 0;
    }
}