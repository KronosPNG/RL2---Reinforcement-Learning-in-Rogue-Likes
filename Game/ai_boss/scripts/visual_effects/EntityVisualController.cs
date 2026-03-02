using Godot;
using System;
public partial class EntityVisualController<TState> : Node2D where TState : Enum
{  
    [Export] protected string FacingLibrary { get; private set; } = "right";

    protected AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public string GetAnimationNameForState(TState state)
    {
        return $"{FacingLibrary}/{state.ToString().ToLower()}";
    }
    
    public virtual void PlayState(TState state)
    {
        _sprite.Play(GetAnimationNameForState(state));
    }
}