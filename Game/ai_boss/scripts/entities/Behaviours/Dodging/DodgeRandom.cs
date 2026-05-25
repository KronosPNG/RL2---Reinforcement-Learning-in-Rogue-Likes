using Godot;

[GlobalClass]
public partial class DodgeRandom : DodgeBehaviour
{
    public DodgeRandom(IDodgeDirectionStrategy dodgeDirection): base(dodgeDirection)
    {
        Priority = .5f;
    }

    public override float EvaluateOpportunity(IEntity entity)
    {
        // Random dodging
        return (float)GD.RandRange(0f, 1f);
    }
}