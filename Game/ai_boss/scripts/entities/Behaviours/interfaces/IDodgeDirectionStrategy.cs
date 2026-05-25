using Godot;

public interface IDodgeDirectionStrategy
{
    public Vector2 GetDodgeDirection(IEntity entity, Node2D collider);
}