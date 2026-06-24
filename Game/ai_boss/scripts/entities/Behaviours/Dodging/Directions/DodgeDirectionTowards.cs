using Godot;

[GlobalClass]
public partial class DodgeDirectionTowards : DodgeDirection, IDodgeDirectionStrategy
{
    public override Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        // Dodge directly towards collider
        if(collider == null)
        {
            (entity.Target.GlobalPosition - entity.GlobalPosition).Normalized();
        }

        return (collider.GlobalPosition - entity.GlobalPosition).Normalized();
    }
}