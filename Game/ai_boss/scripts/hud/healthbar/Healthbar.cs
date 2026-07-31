using System.Linq;
using Godot;

public partial class Healthbar : HBoxContainer
{
    // ---- Node references ----
    private HeartContainer[] _heartContainers;

    // ---- Health data ----
    private int _maxHealth;
    private int _currentHealth;

    // ---- Visual effects ----
    [Export] private VisualEffect _healEffect;
    [Export] private VisualEffect _damageEffect;

    public override void _Ready()
    {
        _heartContainers = GetChildren().OfType<HeartContainer>().ToArray();
        foreach (var heart in _heartContainers)
        {
            _maxHealth += heart.MaxHealthPerHeart;
        }

        _currentHealth = _maxHealth; // Initialize current health to max health
        
        _healEffect.InitializeVisuals(this);
        _damageEffect.InitializeVisuals(this);
        
        EventBus.OnPlayerHealthChanged += OnPlayerHealthChanged;
        EventBus.OnPlayerSpawned += (player) => Reset();
    }

    public void OnPlayerHealthChanged(float playerHealth)
    {
        int healthDifference = (int)System.Math.Ceiling(playerHealth - _currentHealth);

        if(healthDifference > 0)
        {
            Heal(healthDifference);
        }
        else if(healthDifference < 0)
        {
            // Convert to positive value for damage application
            TakeDamage(-healthDifference);
        }
    }

    public void Heal(int amount)
    {
        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);

        for (int i = 0; i < _heartContainers.Length; i++)
        {
            if (amount <= 0)
                break;

            amount = _heartContainers[i].HealHeart(amount);
        }

        _healEffect.PlayEffect(this);
    }

    public void TakeDamage(int amount)
    {        
        _currentHealth = Mathf.Max(_currentHealth - amount, 0);

        for (int i = _heartContainers.Length - 1; i >= 0; i--)
        {
            if (amount <= 0)
                break;

            amount = _heartContainers[i].DamageHeart(amount);
        }

        _damageEffect.PlayEffect(this);
    }

    private void Reset()
    {
        _currentHealth = _maxHealth;
        for (int i = 0; i < _heartContainers.Length; i++)
        {
            _heartContainers[i].HealHeart(_heartContainers[i].MaxHealthPerHeart);
        }
    }
}