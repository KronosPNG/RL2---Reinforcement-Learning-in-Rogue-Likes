using Godot;

public partial class CooldownIndicator : SimpleIndicator
{
	private Label _timerLabel;
	private float _cooldownDuration; // Duration of the cooldown in seconds
	private Color _originalModulate;

	[Export] private Color CooldownModulate = new Color(0, 0, 0, 0.5f); // Semi-transparent black by default

	public override void _Ready()
	{
		base._Ready();
		_originalModulate = this.Modulate;

		_timerLabel = GetNode<Label>("Label");
	}

	public void StartCooldown()
	{
		if (_cooldownDuration <= 0)
		{
			GD.PrintErr($"[CooldownIndicator] Cooldown duration not set for {Name}. Cannot start cooldown.");
			return; // No cooldown to display
		}

		GD.Print($"[CooldownIndicator] Starting cooldown for {Name} with duration {_cooldownDuration} seconds.");

		_timerLabel.Text = $"{_cooldownDuration:0.00}s";
		_timerLabel.Visible = true;

		_texture.Modulate = CooldownModulate;
		_buttonLabel.Modulate = CooldownModulate;

		// Animate the cooldown fill over the duration
		var tween = CreateTween();
		tween.Parallel()
			.TweenProperty(_texture, "modulate", _originalModulate, _cooldownDuration)
		 	.SetEase(Tween.EaseType.In);
			
		tween.Parallel()
			.TweenProperty(_buttonLabel, "modulate", _originalModulate, _cooldownDuration)
			.SetEase(Tween.EaseType.In);

		tween.Parallel()
			.TweenMethod(
				Callable.From<float>(v => _timerLabel.Text = $"{v:0.00}s"),
				Variant.From<float>(_cooldownDuration),  // from
				Variant.From<float>(0.0f),               // to
				_cooldownDuration   // duration matches the value so it's always 1:1
			)
			.SetTrans(Tween.TransitionType.Linear);
			

		tween.TweenCallback(Callable.From(OnCooldownComplete));
	}

	public void SetCooldownDuration(float duration)
	{
		_cooldownDuration = duration;
		_timerLabel.Visible = false;
	}

	private void OnCooldownComplete()
	{
		GD.Print($"[CooldownIndicator] Cooldown complete for {Name}.");// Hide the timer label when cooldown is complete  
		_timerLabel.Visible = false;
	}
}
