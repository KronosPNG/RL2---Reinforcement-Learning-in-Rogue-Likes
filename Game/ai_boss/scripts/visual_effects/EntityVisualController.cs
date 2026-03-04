using Godot;
using System;

public partial class EntityVisualController<TState> : Node2D, IVisualController<TState> where TState : Enum
{
    [Signal] public delegate void AnimationFinishedEventHandler(); 
    protected AnimationPlayer _animationPlayer;
    protected Sprite2D _baseSprite;

    protected bool _isFlipped = false;
    public bool IsMoving {get; set;} = false;
    protected TState _currentState;
    protected TState _previousState;

    public Vector2 FacingDirection { get; set; } = Vector2.Right;

    public override void _Ready()
    {
        _baseSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        if (_animationPlayer != null)
        {
            _animationPlayer.AnimationFinished += OnAnimationFinished;
        }
    }

    public virtual string GetLibraryForFacing()
    {
        return "horizontal";
    }

    public virtual string GetAnimationNameForState(TState state)
    {
        return state.ToString().ToLower();
    }

    public virtual void UpdateFlip(bool shouldFlip)
    {
        _baseSprite.FlipH = shouldFlip;
    }

    public virtual void PlayState(TState state)
    {
        _currentState = state;

        string libraryName = GetLibraryForFacing();
        string animationName = GetAnimationNameForState(state);

        UpdateFlip(FacingDirection.X < 0);
        _animationPlayer.Play($"{libraryName}/{animationName}");
    }

    public virtual void UpdateAnimationIfNeeded()
    {   
        string newLibrary = GetLibraryForFacing();
        string targetAnimation = GetAnimationNameForState(_currentState);
        string fullTargetAnimation = $"{newLibrary}/{targetAnimation}";
        
        if (!_currentState.Equals(_previousState) || GetCurrentAnimation() != fullTargetAnimation)
        {
            PlayState(_currentState);
        } 

        _previousState = _currentState;
    }

    public string GetCurrentAnimation()
    {
        return _animationPlayer.CurrentAnimation;
    }
    
    public void StopAnimation()
    {
        _animationPlayer.Stop();
    }

    public bool IsPlaying()
    {
        return _animationPlayer.IsPlaying();
    }

    protected virtual void OnAnimationFinished(StringName animName)
    {
        EmitSignal(SignalName.AnimationFinished);
    }

    public virtual void ClearEffects()
    {
        // Default implementation does nothing - override in derived classes if needed
    }
}