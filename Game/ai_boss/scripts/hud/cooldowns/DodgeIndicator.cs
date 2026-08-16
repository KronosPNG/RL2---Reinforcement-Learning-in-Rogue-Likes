using Godot;

public partial class DodgeIndicator : SimpleIndicator
{
	private Color _originalModulate;

	[Export] private Color DodgeModulate = new Color(0, 0, 0, 0.5f); // Semi-transparent black
	[Export] private float DarkenDuration = 0.5f;

	public override void _Ready()
	{
		base._Ready();
		_originalModulate = Modulate;

		EventBus.OnPlayerDodged += OnPlayerDodged;
	}

	private void OnPlayerDodged(Vector2 dodgeDirection)
	{
		_texture.Modulate = DodgeModulate;
		_buttonLabel.Modulate = DodgeModulate;

		var tween = CreateTween();
		tween.Parallel()
			.TweenProperty(_texture, "modulate", _originalModulate, DarkenDuration)
			.SetEase(Tween.EaseType.In);

		tween.Parallel()
			.TweenProperty(_buttonLabel, "modulate", _originalModulate, DarkenDuration)
			.SetEase(Tween.EaseType.In);
	}
}
