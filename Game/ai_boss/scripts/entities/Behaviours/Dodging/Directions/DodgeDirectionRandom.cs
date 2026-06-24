using Godot;

[GlobalClass]
public partial class DodgeDirectionRandom : DodgeDirection, IDodgeDirectionStrategy
{
    public override Vector2 GetDodgeDirection(IEntity entity, Node2D collider)
    {
        // Dodge in a random direction (snap to 8 directions for more intentional dodges)
        var direction = new Vector2(GD.Randf(), GD.Randf()).Normalized();
        return new Vector2(Mathf.Round(direction.X * 4) / 4, Mathf.Round(direction.Y * 4) / 4).Normalized();
    }
}