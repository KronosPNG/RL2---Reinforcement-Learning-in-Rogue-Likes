using Godot;

public partial class BossRoom : Room
{
    public override void _Ready()
    {
        base._Ready();
    }

    public override void OnEnterState(RoomState newState)
    {
        switch (newState)
        {
            case RoomState.Active:
                EventBus.RaiseBossRoomEnteredEvent();
                break;
        }

        base.OnEnterState(newState);
    }

    public override void OnExitState(RoomState oldState)
    {
        switch (oldState)
        {
            case RoomState.Active:
                EventBus.RaiseBossKilledEvent();
                break;
        }

        base.OnExitState(oldState);
    }
}