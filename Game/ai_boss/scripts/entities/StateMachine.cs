using Godot;
using System;

public class StateMachine <TState>(Node owner, TState initialState) where TState : Enum
{
    private TState _currentState = initialState;
    private TState _previousState = initialState;
    private float stateTimer = 0f;

    private readonly Node _owner = owner;

    public TState CurrentState => _currentState;
    public TState PreviousState
    {
        get => _previousState;
        set => _previousState = value;
    }
    
    public float StateTimer {
        get => stateTimer;
        set => stateTimer = value;
    }

    public void TransitionToState(TState newState, Action<TState> onExit, Action<TState> onEnter)
    {
        if (newState.Equals(_currentState)) return;

        onExit?.Invoke(_currentState);
        _previousState = _currentState;
        _currentState = newState;
        stateTimer = 0f;
        onEnter?.Invoke(newState);

        _owner?.EmitSignal("StateChanged", newState.ToString());
    }

}