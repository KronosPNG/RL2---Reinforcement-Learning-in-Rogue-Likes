using Godot;

public partial class Door : Entity<DoorState>, IStateful<DoorState>
{
    [Signal] public delegate void StateChangedEventHandler(string newState);

    public override void HandleStateTransitions()
    {
        throw new System.NotImplementedException();
    }

    public override void OnEnterState(DoorState state)
    {
        throw new System.NotImplementedException();
    }

    public override void OnExitState(DoorState state)
    {
        throw new System.NotImplementedException();
    }

    protected override void ApplyMovementByState(float delta)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnAnimationFinished()
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateAI(float delta)
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateAnimationIfNeeded()
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateFacing()
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateTimers(float delta)
    {
        throw new System.NotImplementedException();
    }
}