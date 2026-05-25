using Godot;

[GlobalClass]
public partial class DodgeNever : DodgeBehaviour
{
    public DodgeNever(IDodgeDirectionStrategy dodgeDirection) : base(dodgeDirection)
    {
        Priority = 0f;
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        return 0f; // Never dodge
    }
}