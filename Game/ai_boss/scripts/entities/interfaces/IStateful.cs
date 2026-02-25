using System;

public interface IStateful<TState> where TState : Enum
{
    
    public delegate void StateChangedHandler(string newState);

    public StateMachine<TState> StateMachine { get; }
    public TState CurrentState { get; }
    public TState PreviousState { get; }

    public abstract void OnEnterState(TState newState);
    public abstract void OnExitState(TState oldState);
    public abstract void HandleStateTransitions();
}