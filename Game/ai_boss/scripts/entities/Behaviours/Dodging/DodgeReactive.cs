using System.Linq;
using Godot;

[GlobalClass]
public partial class DodgeReactive : DodgeBehaviour
{
    Node2D closestProjectile;

    public DodgeReactive() { 
        Priority = 0.8f;
        TimeBetweenDodges = 1f;
    }

    public override Vector2 GetDodgeDirection(PlayerMimic player)
    {
        var boss = player.Target as BossRL;
        var bossDistance = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);
        var projDistance = player.GlobalPosition.DistanceTo(closestProjectile.GlobalPosition);

        var collider = projDistance < bossDistance ? closestProjectile : boss;

        return DodgeDirection.GetDodgeDirection(player, collider);
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        var boss = player.Target as BossRL;
        var bossState = boss.CurrentState;
        var bossDistance = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);

        if (bossState == BossState.Attacking)
        {
            if (bossDistance < 200f) // If boss is close and attacking, high priority to dodge
                return 0.9f;
        }

        if (player.DetectedProjectiles.Count > 0)
        {
            // If there are incoming projectiles, prioritize dodging based on proximity
            closestProjectile = player.DetectedProjectiles
                .MinBy(proj => player.GlobalPosition.DistanceTo(proj.GlobalPosition));

            float closestProjectileDistance = closestProjectile != null
                ? player.GlobalPosition.DistanceTo(closestProjectile.GlobalPosition)
                : float.MaxValue;

            if (closestProjectileDistance < 16f) // If a projectile is very close, high priority to dodge
                return 1f;
            else if (closestProjectileDistance < 32f) // Projectile is approaching, moderate priority
                return 0.9f;
            else // Projectiles are detected but far away, low priority
                return 0.75f;
        }

        return 0f; // No immediate threat detected, no need to dodge
    }
}