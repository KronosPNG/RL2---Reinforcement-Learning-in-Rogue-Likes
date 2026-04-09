using Godot;

public partial class PauseMenu : CanvasLayer
{
	private Button _resumeButton;
	private Button _exitButton;

	[Export] private float _inputWaitTime = .5f; // Time to wait before accepting input after pausing
	private float _inputTimer;

	public override void _Ready()
	{
		_resumeButton = GetNode<Button>("VBoxContainer/ResumeButton");
		_exitButton = GetNode<Button>("VBoxContainer/ExitButton");

		SetProcess(false);

		_inputTimer = _inputWaitTime;

		EventBus.OnGamePaused += PauseGame;

		_resumeButton.Pressed += ResumeGame;
		_exitButton.Pressed += ExitGame;
	}

	public override void _Process(double delta)
	{
		if (Visible)
		{	
			// Prevent accepting input immediately after pausing
			if (_inputTimer > 0)
			{
				_inputTimer -= (float)delta;
			}

			else if (Input.IsActionJustPressed("menu_unpause"))
			{
				ResumeGame();
				_inputTimer = _inputWaitTime; // Reset timer for next pause
			}
		}
		
	}

	private void ResumeGame()
	{
		Visible = false;
		SetProcess(false);
		ProcessMode = ProcessModeEnum.Inherit;
		EventBus.RaiseGameResumed();
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
