using Godot;

public partial class EndScreen : CanvasLayer
{
	// true on WinScreen (shows when the boss dies), false on DeathScreen (shows when
	// the player dies) — same script, set per-scene in the Inspector.
	[Export] public bool ShowOnBossDefeated = false;

	// Wait this long after the outcome event before actually showing the screen, so a
	// death animation isn't cut off mid-play. Boss's "dead" clip is 1.8s (boss_rl.tscn) —
	// set on WinScreen. The player has no dedicated death animation (bean.tscn only has
	// hit/dodge/idle/walking), so DeathScreen defaults this to 0.
	[Export] public float DelayBeforeShow = 0f;

	private Button _exitButton;

	public override void _Ready()
	{
		_exitButton = GetNode<Button>("VBoxContainer/VBoxContainer/ExitButton");

		AddBackdrop();

		if (ShowOnBossDefeated)
			EventBus.OnBossKilled += OnOutcomeReached;
		else
			EventBus.OnPlayerDied += OnOutcomeReached;

		_exitButton.Pressed += ExitGame;
	}

	// Full-screen dim behind the content, matching the modal feel PauseMenu is missing
	// too (it has no backdrop currently either).
	private void AddBackdrop()
	{
		var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
		backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(backdrop);
		MoveChild(backdrop, 0); // behind the VBoxContainer content
	}

	private async void OnOutcomeReached()
	{
		if (DelayBeforeShow > 0f)
			await ToSignal(GetTree().CreateTimer(DelayBeforeShow), "timeout");

		ShowScreen();
	}

	// Pauses directly rather than going through EventBus.RaiseGamePaused() — that event
	// also drives PauseMenu, and we don't want that popping up alongside this.
	private void ShowScreen()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Always;
		GetTree().Paused = true;
	}

	private void ExitGame()
	{
		EventBus.RaiseGameExit();
	}
}
