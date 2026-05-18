using Godot;

[GlobalClass]

public abstract partial class DodgeBehaviour : Resource, IDodgeBehaviour
{
    public abstract float EvaluateThreat(IEntity entity);

    public abstract Vector2 GetDodgeDirection(IEntity entity);
    public abstract bool ShouldDodge(IEntity entity);
}