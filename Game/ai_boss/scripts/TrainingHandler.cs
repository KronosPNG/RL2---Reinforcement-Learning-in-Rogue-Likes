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
		Engine.MaxFps = 60;
		_roomManager = GetNode<TrainingRoomManager>("RoomManager");

		EventBus.OnPlayerDied += HandlePlayerDied;
		EventBus.OnPlayerDied += () => EndEpisode(bossWon: true);   // player dies = boss wins

		EventBus.OnBossKilled += EndEpisodeAfterBossKilled;
		EventBus.OnBossKilled += () => EndEpisode(bossWon: false);  // boss dies = player wins

		EventBus.OnBossSpawned += (boss) => _bossRef = boss;

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
				GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Episode timeout reached ({_maxEpisodeDuration}s)");
				EndEpisode(bossWon: false);  // Treat timeout as loss
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
				GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] WebSocket closed with code: {code}, reason: {reason}");
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
				GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Instance ID: {_instanceId}");
			}
			else if (arg.StartsWith("--player-"))
			{
				var parts = arg.Substring("--player-".Length).Split("=");
				if (parts.Length == 2)
				{
					string key = parts[0];
					string value = parts[1];
					_playerConfig[key] = value;
					GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Player config: {key}={value}");
				}
			}
		}

		// Print final config
		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Player configuration loaded: {_playerConfig.Count} settings");
	}

	/// <summary>
	/// Connect to training server on correct port (7000 + instance_id)
	/// and send handshake with instance_id.
	/// </summary>
	private async void StartConnection()
	{
		int port = 7000;
		string url = $"ws://localhost:{port}";

		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Connecting to {url}");

		var code = _socket.ConnectToUrl(url);

		if (code == Error.Ok)
		{
			// Wait for connection to establish
			await ToSignal(GetTree().CreateTimer(2f), "timeout");
			_socket.Poll();

			if (_socket.GetReadyState() == WebSocketPeer.State.Open)
			{
				GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Connected to server");

				// Send handshake with instance_id
				SendHandshake();

				// Give server time to register connection
				await ToSignal(GetTree().CreateTimer(0.5f), "timeout");

				// Start the training scene (go to boss room)
				StartTrainingScene();
			}
			else
			{
				GD.PrintErr($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Connection failed");
			}
		}
		else
		{
			GD.PrintErr($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Failed to connect: {code}");
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
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Handshake sent: {text}");
		}
	}

	/// <summary>
	/// Start the training scene - load boss room and spawn PlayerMimic.
	/// </summary>
	private void StartTrainingScene()
	{
		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Starting training scene");

		// Boss is already in the boss room scene; grab it directly
		_bossRef = GetTree().GetFirstNodeInGroup("Boss") as BossRL;
		if (_bossRef == null)
			GD.PrintErr($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Could not find BossRL in scene tree");
		else
			_globalState.BossReference = _bossRef;

		// Instantiate first (no AddChild yet — _Ready won't fire)
		_playerMimic = _roomManager.InstantiatePlayerMimic();
		if (_playerMimic != null)
		{
			// Configure behaviors and equipment scenes BEFORE _Ready runs
			ConfigurePlayerMimic(_playerMimic);
			// Now add to scene — _Ready fires here with everything already set
			_roomManager.AddPlayerMimicToRoom(_playerMimic);
			_globalState.PlayerReference = _playerMimic;
		}

		// Send initial static state to server
		SendStaticState();

		// Mark episode as active and record start time
		_episodeActive = true;
		_episodeStartTime = Time.GetTicksMsec() / 1000.0f;

		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Episode started");
	}

	/// <summary>
	/// Configure PlayerMimic with weapon, armor, consumable, and behaviors
	/// from command-line arguments.
	/// </summary>
	/// <summary>
	/// Configure PlayerMimic BEFORE it enters the scene tree (before _Ready).
	/// Sets exported scene fields so _Ready's EquipWeapon/Armor/Consumable calls
	/// use the correct assets. Behaviors are set as exports so _Ready's timer
	/// setup and event subscriptions work correctly.
	/// </summary>
	private void ConfigurePlayerMimic(PlayerMimic playerMimic)
	{
		// Set equipment PackedScenes — _Ready will call EquipWeapon/Armor/Consumable with these
		if (_playerConfig.TryGetValue("weapon", out string weapon))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting weapon scene: {weapon}");
			playerMimic.EquippedWeaponScene = ResourceLoader.Load<PackedScene>($"res://scenes/weapons/{weapon}.tscn");
		}

		if (_playerConfig.TryGetValue("armor", out string armor))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting armor scene: {armor}");
			playerMimic.EquippedArmorScene = ResourceLoader.Load<PackedScene>($"res://scenes/armors/{armor}.tscn");
		}

		if (_playerConfig.TryGetValue("consumable", out string consumable))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting consumable scene: {consumable}");
			if(consumable != "none")
			{
				playerMimic.EquippedConsumableScene = ResourceLoader.Load<PackedScene>($"res://scenes/consumables/{consumable}.tscn");
			}
			
		}

		// Set behavior exports — _Ready's timer setup and OnWeaponEquipped/OnConsumableEquipped
		// handlers will see the correct behaviors
		if (_playerConfig.TryGetValue("attack-behavior", out string attackBehavior))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting attack behavior: {attackBehavior}");
			playerMimic.attackBehaviour = ResourceLoader.Load<PlayerAttackBehaviour>($"res://assets/behaviours/attack/{attackBehavior}.tres");
		}

		if (_playerConfig.TryGetValue("dodge-behavior", out string dodgeBehavior))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting dodge behavior: {dodgeBehavior}");
			playerMimic.dodgeBehaviour = ResourceLoader.Load<DodgeBehaviour>($"res://assets/behaviours/dodging/{dodgeBehavior}.tres");
		}

		if (_playerConfig.TryGetValue("consumable-behavior", out string consumableBehaviour))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting consumable behavior: {consumableBehaviour}");
			playerMimic.consumableBehaviour = ResourceLoader.Load<ConsumableBehaviour>($"res://assets/behaviours/consumable/{consumableBehaviour}.tres");
		}

		if (_playerConfig.TryGetValue("wander-behavior", out string wanderBehavior))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting wander behavior: {wanderBehavior}");
			playerMimic.wanderBehaviour = ResourceLoader.Load<WanderBehaviour>($"res://assets/behaviours/wandering/{wanderBehavior}.tres");
		}

		if (_playerConfig.TryGetValue("aggro-behavior", out string aggroBehavior))
		{
			GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Setting aggro behavior: {aggroBehavior}");
			playerMimic.aggroBehaviour = ResourceLoader.Load<AggroBehaviour>($"res://assets/behaviours/aggro/{aggroBehavior}.tres");
		}

		// Set target before _Ready so the mimic knows the boss from the start
		playerMimic.Target = _bossRef;
	}

	private void HandlePlayerDied()
	{
		CallDeferred(MethodName.ProcessPlayerDeath);
	}

	private void ProcessPlayerDeath()
	{
		var players = GetTree().GetNodesInGroup("Player");
		if (players.Count == 0) return;
		var player = players[0] as Node2D;
		if (player == null || !IsInstanceValid(player)) return;
		player.GetParent()?.RemoveChild(player);
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
					GD.PrintErr($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Received unexpected message type: {type}");
					break;
			}
		}
	}

	private void ApplyAiAction(AiAction action)
	{
		_bossRef.ApplyAction(action);
		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] AI action: X={action.X}, Y={action.Y}, ActionId={action.ActionId}");
	}

	private void HandleCommand(CommandType cmd)
	{
		switch (cmd)
		{
			case CommandType.Restart:
				GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Restart command received from server");
				RestartEpisode();
				break;

			case CommandType.CloseSession:
				GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Close session command received");
				GetTree().Quit();
				break;

			default:
				GD.PrintErr($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Unknown command: {cmd}");
				break;
		}
	}

	private void RestartEpisode()
	{
		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Restarting episode");

		// Disconnect EventBus handlers synchronously BEFORE QueueFree — deferred equip callbacks
		// for the new episode must not see stale handlers from this episode's player.
		if (IsInstanceValid(_playerMimic))
		{
			_playerMimic.DisconnectFromEventBus();
			_playerMimic.QueueFree();
		}
		_playerMimic = null;

		// ReloadRoom spawns the new boss, which fires OnBossSpawned → updates _bossRef
		_roomManager.ReloadRoom();

		_playerMimic = _roomManager.InstantiatePlayerMimic();
		if (_playerMimic != null)
		{
			ConfigurePlayerMimic(_playerMimic);
			_roomManager.AddPlayerMimicToRoom(_playerMimic);
			_globalState.PlayerReference = _playerMimic;
		}

		// Resend static state so the server has fresh context for the new episode
		SendStaticState();

		_episodeActive = true;
		_episodeStartTime = Time.GetTicksMsec() / 1000.0f;

		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Episode restarted");
	}

	public void EndEpisode(bool bossWon)
	{
		if (!_episodeActive)
			return;

		_episodeActive = false;

		float duration = Time.GetTicksMsec() / 1000.0f - _episodeStartTime;
		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Episode ended - BossWon: {bossWon}, Duration: {duration}s");
		_bossRef.SetProcess(false);
		_bossRef.SetPhysicsProcess(false);
		_playerMimic.SetProcess(false);
		_playerMimic.SetPhysicsProcess(false);

		SendOutcome(bossWon);

		// Wait for server to send restart command
		GD.Print($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Waiting for server to start next episode...");
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
			GD.PrintErr($"[TRAINING: {Time.GetDatetimeStringFromSystem()}] Cannot send {type}: WebSocket not open");
			return;
		}

		_socket.Send(NetworkProtocol.BuildMessage(type, payload));
	}
}
