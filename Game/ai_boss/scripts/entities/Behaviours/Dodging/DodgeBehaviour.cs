using Godot;

[GlobalClass]

public abstract partial class DodgeBehaviour : Resource, IDodgeBehaviour
{
    public float Priority {get; protected set;}
    public float TimeBetweenDodges {get; protected set;}
    public IDodgeDirectionStrategy DodgeDirection { get; set; } = new DodgeDirectionRandom();

    public DodgeBehaviour(){}

    public virtual Vector2 GetDodgeDirection(PlayerMimic player)
    {
        return DodgeDirection.GetDodgeDirection(player, null);
    }
    
    public abstract float EvaluateOpportunity(PlayerMimic player);
}