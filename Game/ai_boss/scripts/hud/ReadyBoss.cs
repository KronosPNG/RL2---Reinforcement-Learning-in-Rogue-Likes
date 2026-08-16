using Godot;

public partial class ReadyBoss : CanvasLayer
{
	private Button _confirmButton;

	public override void _Ready()
	{
		_confirmButton = GetNode<Button>("VBoxContainer/VBoxContainer/ConfirmButton");

		Visible = false;

		AddBackdrop();

		EventBus.OnBossRoomEntered += ShowPrompt;

		_confirmButton.Pressed += OnConfirmPressed;
	}

	private void AddBackdrop()
	{
		var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f) };
		backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(backdrop);
		MoveChild(backdrop, 0);
	}

	// Pauses directly rather than going through EventBus.RaiseGamePaused() — that event
	// also drives PauseMenu/EndScreen, and we don't want those popping up alongside this.
	private void ShowPrompt()
	{
		Visible = true;
		ProcessMode = ProcessModeEnum.Always;
		GetTree().Paused = true;
	}

	private void OnConfirmPressed()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Inherit;
		GetTree().Paused = false;
	}
}
