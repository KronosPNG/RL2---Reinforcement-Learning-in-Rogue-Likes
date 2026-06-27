using Godot;

[GlobalClass]
public partial class DodgeDirectionRandom : DodgeDirection, IDodgeDirectionStrategy
{
    public override Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        var direction = new Vector2(GD.Randf() * 2 - 1, GD.Randf() * 2 - 1);
        var snappedAngle = Mathf.Round(direction.Angle() / (Mathf.Pi / 4)) * (Mathf.Pi / 4);
        return Vector2.FromAngle(snappedAngle);
    }
}