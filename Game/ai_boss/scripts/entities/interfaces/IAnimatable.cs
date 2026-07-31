using Godot;
using System;
public interface IAnimatable<TState> where TState : Enum
{
    void OnAnimationFinished();
    void UpdateAnimationIfNeeded();
}