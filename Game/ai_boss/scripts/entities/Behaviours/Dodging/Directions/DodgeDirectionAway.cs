using Godot;

[GlobalClass]
public partial class DodgeDirectionAway : Resource, IDodgeDirectionStrategy
{
    public Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        // Dodge directly away from collider
        return (entity.GlobalPosition - collider.GlobalPosition).Normalized();
    }
}