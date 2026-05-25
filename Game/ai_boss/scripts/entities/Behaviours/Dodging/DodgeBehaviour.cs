using Godot;

[GlobalClass]

public abstract partial class DodgeBehaviour : Resource, IDodgeBehaviour
{
    public float Priority {get; protected set;}
    protected IDodgeDirectionStrategy DodgeDirection;

    public DodgeBehaviour(IDodgeDirectionStrategy dodgeDirection)
    {
        DodgeDirection = dodgeDirection;
    }

    public virtual Vector2 GetDodgeDirection(PlayerMimic player)
    {
        return DodgeDirection.GetDodgeDirection(player, null);
    }
    
    public abstract float EvaluateOpportunity(PlayerMimic player);
}