using Godot;

public struct AttackDecision
{
    public AttackType Type { get; set; }
    public Vector2 AimDirection { get; set; }

    public AttackDecision(AttackType type, Vector2 aimDirection)
    {
        Type = type;
        AimDirection = aimDirection;
    }

}