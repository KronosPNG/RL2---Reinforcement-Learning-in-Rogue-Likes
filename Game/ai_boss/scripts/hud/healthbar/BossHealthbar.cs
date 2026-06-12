using Godot;

public partial class BossHealthbar : CanvasLayer
{
    private TextureProgressBar _progressBar;

    // ---- Health data ----
    private float _maxHealth;
    private float _currentHealth;

    public override void _Ready()
    {
        _progressBar = GetNode<TextureProgressBar>("ProgressBar");

        EventBus.OnBossSpawned += SetHealth;
        EventBus.OnBossDamaged += UpdateHealth;
    }

    private void SetHealth(BossRL boss)
    {
        _maxHealth = boss.MaxHealth;
        _currentHealth = boss.MaxHealth;
    }

    private void UpdateHealth(float amount)
    {
        Visible = true;

        _currentHealth -= amount;
        _currentHealth = _currentHealth < 0 ? 0 : _currentHealth;

        _progressBar.Value = _currentHealth / _maxHealth;
    }
}