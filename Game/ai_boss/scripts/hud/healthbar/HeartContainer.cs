using Godot;

public partial class HeartContainer : Control
{
    [Export] public Texture2D TextureEmpty;
    [Export] public Texture2D TextureQuarter;
    [Export] public Texture2D TextureHalf;
    [Export] public Texture2D TextureThreeQuarters;
    [Export] public Texture2D TextureFull;

    private TextureRect _heartTexture;
    private HeartStateEnum _heartState;
    public int MaxHealthPerHeart = 4;

    public override void _Ready()
    {
        _heartTexture = GetNode<TextureRect>("TextureRect");
        SetState(HeartStateEnum.Full); // Initialize heart to full health
    }

    public int HealHeart(int healAmount)
    {
        int currentHealth = GetCurrentHealth();

        if(currentHealth >= MaxHealthPerHeart)
            return healAmount; // No healing needed, return the full amount
        
        // Calculate how much healing can be applied to this heart without exceeding the maximum
        int healingApplied = Mathf.Min(healAmount, MaxHealthPerHeart - currentHealth);

        // Update the heart's health and state based on the healing applied
        int newHealth = Mathf.Clamp(currentHealth + healingApplied, 0, 4);
        SetState((HeartStateEnum)newHealth);

        return healAmount - healingApplied; // Return amount of healing left after filling the heart
    }

    public int DamageHeart(int damageAmount)
    {
        int currentHealth = GetCurrentHealth();

        if(currentHealth <= 0)
            return damageAmount; // No damage can be applied, return the full amount
        
        // Calculate how much damage can be applied to this heart without going below zero
        int damageApplied = Mathf.Min(damageAmount, currentHealth);

        // Update the heart's health and state based on the damage applied
        int newHealth = Mathf.Clamp(currentHealth - damageApplied, 0, 4);
        SetState((HeartStateEnum)newHealth);

        return damageAmount - damageApplied; // Return amount of damage left after depleting the heart
    }

    public void SetState(HeartStateEnum state)
    {
        _heartState = state;

        _heartTexture.Texture = state switch
        {
            HeartStateEnum.Empty         => TextureEmpty,
            HeartStateEnum.Quarter       => TextureQuarter,
            HeartStateEnum.Half          => TextureHalf,
            HeartStateEnum.ThreeQuarters => TextureThreeQuarters,
            HeartStateEnum.Full          => TextureFull,
            _                        => TextureEmpty
        };
    }

    public int GetCurrentHealth()
    {
        return (int)_heartState;
    }
}