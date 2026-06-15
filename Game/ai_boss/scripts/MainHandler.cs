using Godot;

public partial class MainHandler : Node
{
    [Export] int _frameInterval = 5;
    bool _isPaused = false;

    GlobalState globalState;
    AnimationPlayer _cutscenePlayer;
    WebSocketPeer socket = new WebSocketPeer();

    public override void _Ready()
    {
        _cutscenePlayer = GetNode<AnimationPlayer>("CutscenePlayer");

        // Subscribe to EventBus events
        EventBus.OnGamePaused += HandleGamePaused;
        EventBus.OnGameResumed += HandleGameResumed;
        EventBus.OnGameExit += HandleGameExit;
        EventBus.OnGameRestarted += HandleGameRestarted;

        EventBus.OnPlayerDied += HandlePlayerDied;
        EventBus.OnPlayerDied += () => SendOutcome(won: false);

        EventBus.OnBossRoomEntered += BossCutscene;
        EventBus.OnBossRoomEntered += StartConnection;
        EventBus.OnBossKilled += EndGameSequence;
        EventBus.OnBossKilled += () => SendOutcome(won: true);

        globalState = new GlobalState();
    }

    public override void _Process(double delta)
    {
        socket.Poll();

        switch (socket.GetReadyState())
        {
            case WebSocketPeer.State.Open:
                ProcessIncomingMessages();

                if ((int)Engine.GetProcessFrames() % _frameInterval == 0)
                {
                    SendDynamicState();
                }
                break;

            case WebSocketPeer.State.Connecting:
                // Still handshaking, nothing to do yet.
                break;

            case WebSocketPeer.State.Closed:
                var code = socket.GetCloseCode();
                var reason = socket.GetCloseReason();
                GD.Print($"WebSocket closed with code: {code}, reason: {reason}");
                break;

            case WebSocketPeer.State.Closing:
                // Waiting for close handshake to complete.
                break;
        }
    }

    private void HandleGamePaused()
    {
        GetTree().Paused = true;
        _isPaused = true;
    }

    private void HandleGameResumed()
    {
        GetTree().Paused = false;
        _isPaused = false;
    }

    private void HandleGameRestarted()
    {
        SendCommand(CommandType.Restart);

        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
        _isPaused = false;
    }

    private async void HandleGameExit()
    {
        SendCommand(CommandType.CloseSession);

        // Give the WebSocket a moment to actually flush the outgoing
        // message before the tree (and the socket with it) is torn down.
        socket.Poll();
        await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

        GetTree().Quit();
    }

    private void HandlePlayerDied()
    {
        // Defer scene tree modifications to avoid modifying state during physics query flush
        CallDeferred(MethodName.ProcessPlayerDeath);
    }

    private void ProcessPlayerDeath()
    {
        // Set player as directly under MainHandler to prevent pause and desaturation
        var player = GetTree().GetNodesInGroup("Player")[0] as Node2D;
        player.GetParent().RemoveChild(player);
        AddChild(player);
        player.ProcessMode = ProcessModeEnum.Always; // Ensure player continues processing to show death animation
    }

    private void BossCutscene()
    {
        // Placeholder for boss cutscene logic
    }

    private void EndGameSequence()
    {
        // Placeholder for end game sequence logic
    }

    // --- WebSocket implementation ---

    private async void StartConnection()
    {
        var code = socket.ConnectToUrl("ws://localhost:7000");

        if (code == Error.Ok)
        {
            await ToSignal(GetTree().CreateTimer(2f), "timeout");

            socket.Poll();

            if (socket.GetReadyState() == WebSocketPeer.State.Open)
            {
                SendStaticState();
            }
        }
        else
        {
            GD.PrintErr("Boss AI server is not available.");
        }
    }

    private void ProcessIncomingMessages()
    {
        while (socket.GetAvailablePacketCount() > 0)
        {
            byte[] packet = socket.GetPacket();
            if (packet.Length < 1)
            {
                continue;
            }

            var (type, payload) = NetworkProtocol.ParseMessage(packet);

            switch (type)
            {
                case NetMsgType.Action:
                    var action = NetworkProtocol.DeserializeAction(payload);
                    ApplyAiAction(action);
                    break;

                default:
                    GD.PrintErr($"Received unexpected message type from server: {type}");
                    break;
            }
        }
    }

    private void ApplyAiAction(AiAction action)
    {
        // TODO:
        // bossController.ApplyAction(action);
        GD.Print($"AI action received: X={action.X}, Y={action.Y}, ActionId={action.ActionId}");
    }

    private void SendStaticState()
    {
        var payload = GameStateSerializer.SerializeStatic(globalState.statState);
        SendMessage(NetMsgType.StaticState, payload);
    }

    private void SendDynamicState()
    {
        var payload = GameStateSerializer.SerializeDynamics(globalState.dynState);
        SendMessage(NetMsgType.DynamicState, payload);
    }

    public void SendOutcome(bool won)
    {
        var payload = NetworkProtocol.SerializeOutcome(won);
        SendMessage(NetMsgType.Outcome, payload);
    }

    public void SendCommand(CommandType cmd)
    {
        var payload = NetworkProtocol.SerializeCommand(cmd);
        SendMessage(NetMsgType.Command, payload);
    }

    private void SendMessage(NetMsgType type, byte[] payload)
    {
        if (socket.GetReadyState() != WebSocketPeer.State.Open)
        {
            GD.PrintErr($"Cannot send {type}: WebSocket is not open.");
            return;
        }

        socket.Send(NetworkProtocol.BuildMessage(type, payload));
    }
}
