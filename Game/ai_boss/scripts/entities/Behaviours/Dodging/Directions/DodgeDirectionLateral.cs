using Godot;

[GlobalClass]
public partial class DodgeDirectionLateral : DodgeDirection, IDodgeDirectionStrategy
{
    public override Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        Vector2 direction;

        if(collider == null)
        {
            direction = entity.Target.GlobalPosition - entity.GlobalPosition;
        } else
        {
            direction = collider.GlobalPosition - entity.GlobalPosition;
        }

        // Dodge laterally relative to the collider
        
        // Rotate either 90 degrees clockwise or counterclockwise randomly
        float angle = GD.Randf() < 0.5f ? Mathf.Pi / 2 : -Mathf.Pi / 2;
        return direction.Rotated(angle).Normalized();
    }
}