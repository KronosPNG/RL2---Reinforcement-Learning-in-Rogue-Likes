using Godot;

public interface IEntity
{
    public Vector2 GlobalPosition { get; }
    public Node2D Target { get; }  

    public float StateTimer { get; set; }
    public float BaseSpeed { get; set; }  
    public Vector2 FacingDirection { get; set; }
}