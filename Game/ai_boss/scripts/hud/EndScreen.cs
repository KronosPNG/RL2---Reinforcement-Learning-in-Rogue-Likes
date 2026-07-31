using Godot;

public partial class EndScreen : CanvasLayer
{
	private Button _retryButton;
	private Button _exitButton;

	public override void _Ready()
	{
		_retryButton = GetNode<Button>("VBoxContainer/RestartButton");
		_exitButton = GetNode<Button>("VBoxContainer/ExitButton");

		SetProcess(false);

		EventBus.OnGamePaused += PauseGame;

		_exitButton.Pressed += ExitGame;
	}


	private void PauseGame()
	{
		Visible = true;
		SetProcess(true);
		ProcessMode = ProcessModeEnum.Always;
	}

	private void ExitGame()
	{
		EventBus.RaiseGameExit();
	}
}
