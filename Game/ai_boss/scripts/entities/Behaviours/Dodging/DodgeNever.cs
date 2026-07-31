using Godot;

[GlobalClass]
public partial class DodgeNever : DodgeBehaviour
{
    public DodgeNever() { 
        Priority = 0f; 
        TimeBetweenDodges = 0f;    
    }

    public override float EvaluateOpportunity(PlayerMimic player)
    {
        return 0f; // Never dodge
    }
}