using Godot;

public partial class MainHandler : Node
{
	[Export] int _frameInterval = 5;
	bool _isPaused = false;

	// _Process() checks socket state every frame independently of StartConnection()'s
	// own coroutine; without this gate it starts sending DYNAMIC_STATE as soon as the
	// socket reports Open (near-instant) instead of waiting for STATIC_STATE to have
	// actually been sent first (which StartConnection() delays behind a fixed timer).
	bool _hasSentStaticState = false;

	// True once we intend to be connected — set when StartConnection() is first called,
	// cleared on intentional shutdown. Distinguishes "connection dropped, keep retrying"
	// from "we're closing, stop trying."
	bool _shouldMaintainConnection = false;
	double _reconnectTimer = 0.0;
	const double ReconnectIntervalSeconds = 1.0;

	// PID of the bundled AI server process we launched (-1 if we didn't launch one —
	// e.g. running in the editor, where a dev-managed server is assumed instead).
	long _serverProcessId = -1;

	GlobalState globalState;
	AnimationPlayer _cutscenePlayer;
	WebSocketPeer socket = new WebSocketPeer();
	BossRL _bossRef;

	public override void _Ready()
	{
		Engine.MaxFps = 60;
		_cutscenePlayer = GetNode<AnimationPlayer>("CutscenePlayer");

		// We handle the window-close request ourselves (see _Notification) so the
		// bundled server process gets a chance to be killed before the game exits —
		// without this, Godot quits immediately on its own and our cleanup never runs.
		GetTree().AutoAcceptQuit = false;

		StartServerProcess();

		// Subscribe to EventBus events
		EventBus.OnGamePaused += HandleGamePaused;
		EventBus.OnGameResumed += HandleGameResumed;
		EventBus.OnGameExit += HandleGameExit;
		EventBus.OnGameRestarted += HandleGameRestarted;

		EventBus.OnPlayerDied += HandlePlayerDied;
		EventBus.OnPlayerDied += () => SendOutcome(won: false);

		EventBus.OnBossSpawned += (boss) => _bossRef = boss;
		EventBus.OnBossRoomEntered += BossCutscene;
		EventBus.OnBossRoomEntered += StartConnection;
		EventBus.OnBossKilled += EndGameSequence;
		EventBus.OnBossKilled += () => SendOutcome(won: true);

		globalState = new GlobalState();
		// Must be added to the tree — GlobalState._Ready()/_Process() (which wire up
		// the EventBus subscriptions and populate statState/dynState every frame) only
		// run for nodes actually in the scene tree. TrainingHandler.cs does this too.
		AddChild(globalState);
	}

	public override void _Notification(int what)
	{
		if (what == (int)NotificationWMCloseRequest)
		{
			EventBus.RaiseGameExit();
		}
	}

	public override void _Process(double delta)
	{
		socket.Poll();

		switch (socket.GetReadyState())
		{
			case WebSocketPeer.State.Open:
				ProcessIncomingMessages();

				// Send STATIC_STATE the moment the socket is actually usable, rather than
				// guessing a fixed delay and hoping the handshake finished by then — this
				// polls every frame until it succeeds, so it's correct regardless of how
				// long the connection actually takes to open.
				if (!_hasSentStaticState)
				{
					SendStaticState();
				}
				else if ((int)Engine.GetProcessFrames() % _frameInterval == 0)
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

				// Keep retrying while we still want to be connected — the bundled server
				// can take several seconds to load torch and the policy checkpoint, so
				// the first few connection attempts failing is the expected common case,
				// not an error condition.
				if (_shouldMaintainConnection)
				{
					_reconnectTimer += delta;
					if (_reconnectTimer >= ReconnectIntervalSeconds)
					{
						_reconnectTimer = 0.0;
						GD.Print("Retrying connection to AI server...");
						AttemptConnect();
					}
				}
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
		_shouldMaintainConnection = false;

		SendCommand(CommandType.CloseSession);

		// Give the WebSocket a moment to actually flush the outgoing
		// message before the tree (and the socket with it) is torn down.
		socket.Poll();
		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

		StopServerProcess();

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

	private void StartConnection()
	{
		_shouldMaintainConnection = true;
		_reconnectTimer = 0.0;
		AttemptConnect();
	}

	private void AttemptConnect()
	{
		_hasSentStaticState = false;

		var code = socket.ConnectToUrl("ws://localhost:7000");

		// No fixed wait here — _Process() polls socket state every frame and sends
		// STATIC_STATE as soon as it actually reports Open, however long that takes.
		// If the connection attempt itself fails to even start (as opposed to being
		// refused once attempted), the Closed-state retry loop in _Process() picks
		// it back up on the next tick.
		if (code != Error.Ok)
		{
			GD.PrintErr("Boss AI server is not available.");
		}
	}

	// --- Bundled AI server process (exported builds only) ---

	private void StartServerProcess()
	{
		if (OS.HasFeature("editor"))
		{
			// Running from the Godot editor — assume a server is already running
			// manually (matches the existing dev workflow: `python main.py ...` /
			// `python inference_server.py`). Only exported builds auto-launch the
			// bundled server, since spawning a second one alongside a dev server
			// would just fight over the same port.
			return;
		}

		string serverExePath = OS.GetExecutablePath().GetBaseDir().PathJoin("server").PathJoin("ai_boss_inference_server.exe");

		if (!FileAccess.FileExists(serverExePath))
		{
			GD.PrintErr($"Bundled AI server not found at {serverExePath}");
			return;
		}

		_serverProcessId = OS.CreateProcess(serverExePath, System.Array.Empty<string>());

		if (_serverProcessId <= 0)
		{
			GD.PrintErr("Failed to launch bundled AI server.");
			_serverProcessId = -1;
		}
	}

	private void StopServerProcess()
	{
		if (_serverProcessId > 0)
		{
			OS.Kill((int)_serverProcessId);
			_serverProcessId = -1;
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
		_bossRef.ApplyAction(action);
		GD.Print($"AI action received: X={action.X}, Y={action.Y}, ActionId={action.ActionId}");
	}

	private void SendStaticState()
	{
		var payload = GameStateSerializer.SerializeStatic(globalState.statState);
		SendMessage(NetMsgType.StaticState, payload);
		_hasSentStaticState = true;
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
