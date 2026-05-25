using System.Linq;
using Godot;

[GlobalClass]
public partial class DodgeReactive : DodgeBehaviour
{
    public DodgeReactive(IDodgeDirectionStrategy dodgeDirection) : base(dodgeDirection)
    {
        Priority = 0.8f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        var bossState = player.BossRef.CurrentState;
        var bossDistance = player.GlobalPosition.DistanceTo(player.BossRef.GlobalPosition);

        if (bossState == BossState.Attacking)
        {
            if (bossDistance < 200f) // If boss is close and attacking, high priority to dodge
                return 0.9f;
            else // Boss is attacking but far away, less urgent to dodge
                return 0.3f;
        }

        if (player.DetectedProjectiles.Count > 0)
        {
            // If there are incoming projectiles, prioritize dodging based on proximity
            float closestProjectileDistance = player.DetectedProjectiles
                .Select(proj => player.GlobalPosition.DistanceTo(proj.GlobalPosition))
                .DefaultIfEmpty(float.MaxValue)
                .Min();

            if (closestProjectileDistance < 100f) // If a projectile is very close, high priority to dodge
                return 1f;
            else if (closestProjectileDistance < 300f) // Projectile is approaching, moderate priority
                return 0.5f;
            else // Projectiles are detected but far away, low priority
                return 0.25f;
        }

        return 0f; // No immediate threat detected, no need to dodge
    }
}