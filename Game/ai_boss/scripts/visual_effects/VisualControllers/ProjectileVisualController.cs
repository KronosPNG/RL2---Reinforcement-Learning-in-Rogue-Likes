using Godot;

public partial class ProjectileVisualController : EntityVisualController<ProjectileState>
{
    public override void _Ready()
    {
        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        _baseSprite = GetNodeOrNull<Sprite2D>("Sprite2D");

        if (_animationPlayer != null)
        {
            _animationPlayer.AnimationFinished += OnAnimationFinished;
        }
    }

    public override string GetAnimationNameForState(ProjectileState state)
    {
        return state switch
        {
            ProjectileState.Idle => "default",
            ProjectileState.Active => "active",
            ProjectileState.Hit => "hit",
            ProjectileState.Fading => "fading",
            ProjectileState.Destroyed => "default",
            _ => "default"
        };
    }
}