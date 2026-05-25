using Godot;

[GlobalClass]
public partial class DodgeDirectionTowards : Resource, IDodgeDirectionStrategy
{
    public Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        // Dodge directly towards collider
        return (collider.GlobalPosition - entity.GlobalPosition).Normalized();
    }
}