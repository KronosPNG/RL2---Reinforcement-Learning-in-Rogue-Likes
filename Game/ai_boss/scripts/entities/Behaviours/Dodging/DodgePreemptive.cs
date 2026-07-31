using Godot;

[GlobalClass]
public partial class DodgePreemptive : DodgeBehaviour
{
	public DodgePreemptive() { 
		Priority = 0.65f;
		TimeBetweenDodges = 2f;
	}

	public override float EvaluateOpportunity(PlayerMimic player)
	{
		var boss = player.Target as BossRL;
		var bossState = boss.CurrentState;
		var bossDistance = player.GlobalPosition.DistanceTo(player.Target.GlobalPosition);

		// If the boss is winding up an attack and is within a threatening range, prioritize dodging
		if (bossState == BossState.AttackPrepare && bossDistance < 32f)
		{
			return 0.8f;
		}

		if(player.DetectedProjectiles.Count > 0)
		{
			return .8f;
		}

		return 0f; // Boss is not currently charging an attack, no need to dodge preemptively
	}
}
