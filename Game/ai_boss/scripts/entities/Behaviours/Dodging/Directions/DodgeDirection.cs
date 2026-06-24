using Godot;

[GlobalClass]
public partial class DodgeDirection : Resource, IDodgeDirectionStrategy
{
    public virtual Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        return new Vector2(GD.Randf(), GD.Randf()).Normalized();
    }
}