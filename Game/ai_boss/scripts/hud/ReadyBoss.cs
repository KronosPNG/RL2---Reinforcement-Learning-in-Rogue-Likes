using Godot;

public partial class ReadyBoss : CanvasLayer
{
	private Button _confirmButton;

	public override void _Ready()
	{
		_confirmButton = GetNode<Button>("VBoxContainer/VBoxContainer/ConfirmButton");

		Visible = false;

		EventBus.OnBossRoomEntered += ShowPrompt;

		_confirmButton.Pressed += OnConfirmPressed;
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
