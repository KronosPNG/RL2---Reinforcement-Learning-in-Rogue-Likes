using Godot;

[GlobalClass]
public partial class DodgeDirectionLateral : Resource, IDodgeDirectionStrategy
{
    public Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        // Dodge laterally relative to the collider
        Vector2 direction = collider.GlobalPosition - entity.GlobalPosition;
        // Rotate either 90 degrees clockwise or counterclockwise randomly
        float angle = GD.Randf() < 0.5f ? Mathf.Pi / 2 : -Mathf.Pi / 2;
        return direction.Rotated(angle).Normalized();
    }
}