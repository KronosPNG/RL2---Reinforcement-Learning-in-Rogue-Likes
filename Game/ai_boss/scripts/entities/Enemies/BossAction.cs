using Godot;

public struct BossAction
{   
    public Vector2 MovementDirection;
    public ActionType Action;
}

public enum ActionType{
        Idle,
        Dash,
        Walk,
        Melee1,
        Melee2,
        Magic1,
        Magic2
    }