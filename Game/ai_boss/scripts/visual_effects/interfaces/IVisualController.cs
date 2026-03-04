using Godot;
using System;

public interface IVisualController<TState> where TState : Enum
{
    delegate void AnimationFinishedEventHandler(); 

    string GetLibraryForFacing();
    string GetAnimationNameForState(TState state);
    void PlayState(TState state);
}