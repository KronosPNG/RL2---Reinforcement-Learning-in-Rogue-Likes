using Godot;

[GlobalClass]
public partial class DodgeDirectionAway : DodgeDirection, IDodgeDirectionStrategy
{
	public override Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
	{
		// Dodge directly away from collider
		if(collider == null)
		{
			return (entity.Target.GlobalPosition - collider.GlobalPosition).Normalized();
		}

		return (entity.GlobalPosition - collider.GlobalPosition).Normalized();
	}
}
