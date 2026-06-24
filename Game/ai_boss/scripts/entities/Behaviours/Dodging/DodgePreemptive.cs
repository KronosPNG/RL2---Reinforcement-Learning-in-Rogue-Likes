using Godot;

[GlobalClass]
public partial class DodgePreemptive : DodgeBehaviour
{
    public DodgePreemptive() { 
        Priority = 0.65f;
        TimeBetweenDodges = 1f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        var boss = player.Target as BossRL;
        var bossState = boss.CurrentState;
        var bossDistance = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);

        // If the boss is winding up an attack and is within a threatening range, prioritize dodging
        if (bossState == BossState.AttackPrepare)
        {
            if (bossDistance < 150f) // If boss is close and charging, good opportunity to dodge
                return 0.8f;
            else // Boss is charging but far away, less urgent to dodge
                return 0.3f;
        }

        if(player.DetectedProjectiles.Count > 0)
        {
            return .8f;
        }

        return 0f; // Boss is not currently charging an attack, no need to dodge preemptively
    }
}