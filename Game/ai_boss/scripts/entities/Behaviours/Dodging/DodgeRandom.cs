using Godot;

[GlobalClass]
public partial class DodgeRandom : DodgeBehaviour
{
	public DodgeRandom(): base()
	{
		Priority = .5f;
		TimeBetweenDodges = 5f;
	}

	public override float EvaluateOpportunity(PlayerMimic player)
	{
		// Random dodging
		return (float)GD.RandRange(0f, 1f);
	}
}
