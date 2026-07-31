using Godot;

public partial class BossHealthbar : CanvasLayer
{
	private TextureProgressBar _progressBar;

	// ---- Health data ----
	private float _maxHealth;
	private float _currentHealth;
	[Export] VisualEffect fx;

	public override void _Ready()
	{
		_progressBar = GetNode<TextureProgressBar>("ProgressBar");
		fx.InitializeVisuals(_progressBar);

		EventBus.OnBossSpawned += SetHealth;
		EventBus.OnBossRoomEntered += () => Visible = true;
	}

	private void SetHealth(BossRL boss)
	{
		_maxHealth = boss.MaxHealth;
		_currentHealth = boss.MaxHealth;

		_progressBar.MaxValue = boss.MaxHealth;
		_progressBar.Value = boss.MaxHealth;

		// Re-subscribe each episode so damage updates survive across boss respawns.
		EventBus.OnBossDamaged -= UpdateHealth;
		EventBus.OnBossDamaged += UpdateHealth;
	}

	private void UpdateHealth(float amount)
	{
		_currentHealth -= amount;
		_currentHealth = _currentHealth < 0 ? 0 : _currentHealth;

		_progressBar.Value = _currentHealth;
		fx.PlayEffect(_progressBar);
	}
}
