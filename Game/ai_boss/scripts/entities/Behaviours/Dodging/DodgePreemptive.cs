using Godot;

[GlobalClass]
public partial class DodgePreemptive : DodgeBehaviour
{
    public DodgePreemptive(IDodgeDirectionStrategy dodgeDirection) : base(dodgeDirection)
    {
        Priority = 0.65f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        var bossState = player.Target.CurrentState;
        var bossDistance = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);

        // If the boss is winding up an attack and is within a threatening range, prioritize dodging
        if (bossState == BossState.AttackCharging)
        {
            if (bossDistance < 150f) // If boss is close and charging, good opportunity to dodge
                return 0.8f;
            else // Boss is charging but far away, less urgent to dodge
                return 0.3f;
        }

        return 0f; // Boss is not currently charging an attack, no need to dodge preemptively
    }
}