using Godot;
using Godot.Collections;
using System;

/// <summary>
/// Handler for training mode - spawns PlayerMimic with server-specified config
/// and manages episodes for RL training.
/// 
/// Command-line args from server:
/// --instance-id=0
/// --player-weapon=sword
/// --player-armor=light_armor
/// --player-consumable=medkit
/// --player-attack-behavior=AttackTactical
/// --player-dodge-behavior=DodgeRandom
/// --player-consumable-behavior=ConsumableTactical
/// --player-wander-behavior=WanderRandomWalk
/// --player-aggro-behavior=AggroFollowTarget
/// </summary>
public partial class TrainingHandler : Node
{
    [Export] int _frameInterval = 5;
    [Export] float _maxEpisodeDuration = 600.0f;  // 10 minutes

    int _instanceId = 0;
    Dictionary<string, string> _playerConfig = [];
    
    GlobalState _globalState;
    WebSocketPeer _socket = new WebSocketPeer();
    TrainingRoomManager _roomManager;
    PlayerMimic _playerMimic;
    BossRL _bossRef;
    
    float _episodeStartTime = 0f;
    bool _episodeActive = false;

    public override void _Ready()
    {
        _roomManager = GetNode<TrainingRoomManager>("RoomManager");

        EventBus.OnPlayerDied += HandlePlayerDied;
        EventBus.OnPlayerDied += () => EndEpisode(won: false);

        EventBus.OnBossKilled += EndEpisodeAfterBossKilled;
        EventBus.OnBossKilled += () => EndEpisode(won: true);

        _globalState = new GlobalState();
        AddChild(_globalState);

        // Parse command-line arguments
        ParseCommandLineArguments();

        // Start connection to training server
        StartConnection();
    }

    public override void _Process(double delta)
    {
        _socket.Poll();

        // Check for episode timeout
        if (_episodeActive && _episodeStartTime > 0)
        {
            float elapsedTime = Time.GetTicksMsec() / 1000.0f - _episodeStartTime;
            if (elapsedTime > _maxEpisodeDuration)
            {
                GD.Print($"[TRAINING] Episode timeout reached ({_maxEpisodeDuration}s)");
                EndEpisode(won: false);  // Treat timeout as loss
            }
        }

        switch (_socket.GetReadyState())
        {
            case WebSocketPeer.State.Open:
                ProcessIncomingMessages();

                if ((int)Engine.GetProcessFrames() % _frameInterval == 0 && _episodeActive)
                {
                    SendDynamicState();
                }
                break;

            case WebSocketPeer.State.Connecting:
                break;

            case WebSocketPeer.State.Closed:
                var code = _socket.GetCloseCode();
                var reason = _socket.GetCloseReason();
                GD.Print($"[TRAINING] WebSocket closed with code: {code}, reason: {reason}");
                break;

            case WebSocketPeer.State.Closing:
                break;
        }
    }

    /// <summary>
    /// Parse command-line arguments from server.
    /// Example: --player-weapon=sword --player-armor=light_armor ...
    /// </summary>
    private void ParseCommandLineArguments()
    {
        var args = OS.GetCmdlineUserArgs();

        foreach (string arg in args)
        {
            if (arg.StartsWith("--instance-id="))
            {
                int.TryParse(arg.Substring("--instance-id=".Length), out _instanceId);
                GD.Print($"[TRAINING] Instance ID: {_instanceId}");
            }
            else if (arg.StartsWith("--player-"))
            {
                var parts = arg.Substring("--player-".Length).Split("=");
                if (parts.Length == 2)
                {
                    string key = parts[0];
                    string value = parts[1];
                    _playerConfig[key] = value;
                    GD.Print($"[TRAINING] Player config: {key}={value}");
                }
            }
        }

        // Print final config
        GD.Print($"[TRAINING] Player configuration loaded: {_playerConfig.Count} settings");
    }

    /// <summary>
    /// Connect to training server on correct port (7000 + instance_id)
    /// and send handshake with instance_id.
    /// </summary>
    private async void StartConnection()
    {
        int port = 7000;
        string url = $"ws://localhost:{port}";

        GD.Print($"[TRAINING] Connecting to {url}");

        var code = _socket.ConnectToUrl(url);

        if (code == Error.Ok)
        {
            // Wait for connection to establish
            await ToSignal(GetTree().CreateTimer(2f), "timeout");
            _socket.Poll();

            if (_socket.GetReadyState() == WebSocketPeer.State.Open)
            {
                GD.Print($"[TRAINING] Connected to server");

                // Send handshake with instance_id
                SendHandshake();

                // Give server time to register connection
                await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

                // Start the training scene (go to boss room)
                StartTrainingScene();
            }
            else
            {
                GD.PrintErr("[TRAINING] Connection failed");
            }
        }
        else
        {
            GD.PrintErr($"[TRAINING] Failed to connect: {code}");
        }
    }

    private void SendHandshake()
    {
        var data = new Dictionary();
        data["instance_id"] = _instanceId;
        var text = Json.Stringify(data);
        
        if (_socket.GetReadyState() == WebSocketPeer.State.Open)
        {
            _socket.SendText(text);
            GD.Print($"[TRAINING] Handshake sent: {text}");
        }
    }

    /// <summary>
    /// Start the training scene - load boss room and spawn PlayerMimic.
    /// </summary>
    private void StartTrainingScene()
    {
        GD.Print("[TRAINING] Starting training scene");

        // Boss is already in the boss room scene; grab it directly
        _bossRef = GetTree().GetFirstNodeInGroup("Boss") as BossRL;
        if (_bossRef == null)
            GD.PrintErr("[TRAINING] Could not find BossRL in scene tree");
        else
            _globalState.BossReference = _bossRef;

        // Spawn PlayerMimic with parsed configuration
        _playerMimic = _roomManager.SpawnPlayerMimic() as PlayerMimic;
        if (_playerMimic != null)
        {
            ConfigurePlayerMimic(_playerMimic);
            _globalState.PlayerReference = _playerMimic;
        }

        // Send initial static state to server
        SendStaticState();

        // Mark episode as active and record start time
        _episodeActive = true;
        _episodeStartTime = Time.GetTicksMsec() / 1000.0f;

        GD.Print("[TRAINING] Episode started");
    }

    /// <summary>
    /// Configure PlayerMimic with weapon, armor, consumable, and behaviors
    /// from command-line arguments.
    /// </summary>
    private void ConfigurePlayerMimic(PlayerMimic playerMimic)
    {
        // Configure equipment
        if (_playerConfig.TryGetValue("weapon", out string weapon))
        {
            // Load weapon scene and equip
            GD.Print($"[TRAINING] Equipping weapon: {weapon}");
            PackedScene weaponScene = ResourceLoader.Load<PackedScene>($"res://scenes/weapons/{weapon}.tscn");
            playerMimic.EquipWeapon(weaponScene);
        }

        if (_playerConfig.TryGetValue("armor", out string armor))
        {
            // Load armor scene and equip
            GD.Print($"[TRAINING] Equipping armor: {armor}");
            PackedScene armorScene = ResourceLoader.Load<PackedScene>($"res://scenes/weapons/{armor}.tscn");
            playerMimic.EquipArmor(armorScene);
        }

        if (_playerConfig.TryGetValue("consumable", out string consumable))
        {
            GD.Print($"[TRAINING] Setting consumable: {consumable}");
            PackedScene consumableScene = ResourceLoader.Load<PackedScene>($"res://scenes/weapons/{consumable}.tscn");
            playerMimic.EquipConsumable(consumableScene);
        }

        // Configure behaviors
        if (_playerConfig.TryGetValue("attack-behavior", out string attackBehavior))
        {
            GD.Print($"[TRAINING] Setting attack behavior: {attackBehavior}");
            PlayerAttackBehaviour behaviour = ResourceLoader.Load<PlayerAttackBehaviour>($"res://assets/behaviours/attack/{attackBehavior}.tres");
            playerMimic.attackBehaviour = behaviour;
        }

        if (_playerConfig.TryGetValue("dodge-behavior", out string dodgeBehavior))
        {
            GD.Print($"[TRAINING] Setting dodge behavior: {dodgeBehavior}");
            DodgeBehaviour behaviour = ResourceLoader.Load<DodgeBehaviour>($"res://assets/behaviours/dodging/{dodgeBehavior}.tres");
            playerMimic.dodgeBehaviour = behaviour;
        }

        if (_playerConfig.TryGetValue("consumable-behavior", out string consumableBehaviour))
        {
            GD.Print($"[TRAINING] Setting consumable behavior: {consumableBehaviour}");
            ConsumableBehaviour behaviour = ResourceLoader.Load<ConsumableBehaviour>($"res://assets/behaviours/consumable/{consumableBehaviour}.tres");
            playerMimic.consumableBehaviour = behaviour;
        }

        if (_playerConfig.TryGetValue("wander-behavior", out string wanderBehavior))
        {
            GD.Print($"[TRAINING] Setting wander behavior: {wanderBehavior}");
            WanderBehaviour behaviour = ResourceLoader.Load<WanderBehaviour>($"res://assets/behaviours/wandering/{wanderBehavior}.tres");
            playerMimic.wanderBehaviour = behaviour;
        }

        if (_playerConfig.TryGetValue("aggro-behavior", out string aggroBehavior))
        {
            GD.Print($"[TRAINING] Setting aggro behavior: {aggroBehavior}");
            AggroBehaviour behaviour = ResourceLoader.Load<AggroBehaviour>($"res://assets/behaviours/aggro/{aggroBehavior}.tres");
            playerMimic.aggroBehaviour = behaviour;
        }
    }

    private void HandlePlayerDied()
    {
        CallDeferred(MethodName.ProcessPlayerDeath);
    }

    private void ProcessPlayerDeath()
    {
        var player = GetTree().GetNodesInGroup("Player")[0] as Node2D;
        player.GetParent().RemoveChild(player);
        AddChild(player);
        player.ProcessMode = ProcessModeEnum.Always;
    }

    private void EndEpisodeAfterBossKilled()
    {
        CallDeferred(MethodName.ProcessBossKilled);
    }

    private void ProcessBossKilled()
    {
        // Placeholder for any cleanup
    }

    // --- WebSocket implementation ---

    private void ProcessIncomingMessages()
    {
        while (_socket.GetAvailablePacketCount() > 0)
        {
            byte[] packet = _socket.GetPacket();
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

                case NetMsgType.Command:
                    var cmd = NetworkProtocol.DeserializeCommand(payload);
                    HandleCommand(cmd);
                    break;

                default:
                    GD.PrintErr($"[TRAINING] Received unexpected message type: {type}");
                    break;
            }
        }
    }

    private void ApplyAiAction(AiAction action)
    {
        _bossRef.ApplyAction(action);
        GD.Print($"[TRAINING] AI action: X={action.X}, Y={action.Y}, ActionId={action.ActionId}");
    }

    private void HandleCommand(CommandType cmd)
    {
        switch (cmd)
        {
            case CommandType.Restart:
                GD.Print("[TRAINING] Restart command received from server");
                RestartEpisode();
                break;

            case CommandType.CloseSession:
                GD.Print("[TRAINING] Close session command received");
                GetTree().Quit();
                break;

            default:
                GD.PrintErr($"[TRAINING] Unknown command: {cmd}");
                break;
        }
    }

    private void RestartEpisode()
    {
        GD.Print("[TRAINING] Restarting episode");
        _roomManager.ReloadRoom();

        _bossRef = GetTree().GetFirstNodeInGroup("Boss") as BossRL;
        if (_bossRef != null)
            _globalState.BossReference = _bossRef;

        _playerMimic = _roomManager.SpawnPlayerMimic() as PlayerMimic;
        if (_playerMimic != null)
        {
            ConfigurePlayerMimic(_playerMimic);
            _globalState.PlayerReference = _playerMimic;
        }

        _episodeActive = true;
        _episodeStartTime = Time.GetTicksMsec() / 1000.0f;

        GD.Print("[TRAINING] Episode restarted");
    }

    public void EndEpisode(bool won)
    {
        if (!_episodeActive)
            return;

        _episodeActive = false;

        float duration = Time.GetTicksMsec() / 1000.0f - _episodeStartTime;
        GD.Print($"[TRAINING] Episode ended - Won: {won}, Duration: {duration}s");

        SendOutcome(won);

        // Wait for server to send restart command
        GD.Print("[TRAINING] Waiting for server to start next episode...");
    }

    private void SendStaticState()
    {
        var payload = GameStateSerializer.SerializeStatic(_globalState.statState);
        SendMessage(NetMsgType.StaticState, payload);
    }

    private void SendDynamicState()
    {
        var payload = GameStateSerializer.SerializeDynamics(_globalState.dynState);
        SendMessage(NetMsgType.DynamicState, payload);
    }

    private void SendOutcome(bool won)
    {
        var payload = NetworkProtocol.SerializeOutcome(won);
        SendMessage(NetMsgType.Outcome, payload);
    }

    private void SendMessage(NetMsgType type, byte[] payload)
    {
        if (_socket.GetReadyState() != WebSocketPeer.State.Open)
        {
            GD.PrintErr($"[TRAINING] Cannot send {type}: WebSocket not open");
            return;
        }

        _socket.Send(NetworkProtocol.BuildMessage(type, payload));
    }
}
