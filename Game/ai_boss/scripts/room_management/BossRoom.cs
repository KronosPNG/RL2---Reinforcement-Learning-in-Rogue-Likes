using Godot;

public partial class BossRoom : Room
{
    private BossRL _boss;
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
    

    protected override void SpawnMobs()
    {
        GD.Print("Spawning boss");
        var bossScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/boss_rl.tscn");
        _boss = bossScene.Instantiate<BossRL>();

        var sortedElements = GetNodeOrNull<Node2D>("SortSceneElements");
		sortedElements.AddChild(_boss);
        _boss.Position = _spawningPoints[0].GlobalPosition;

    }
}