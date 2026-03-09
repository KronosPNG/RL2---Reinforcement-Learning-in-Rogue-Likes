using Godot;

public partial class Armor : Node2D, IPedestalItem
{
    public PlayerController OwnerCharacter { get; protected set; }

    // ---- Signals ----
    [Signal] public delegate void EquippedEventHandler();
    [Signal] public delegate void UnequippedEventHandler();
    [Signal] public delegate void DamageReducedEventHandler(float originalDamage, float reducedDamage);
    [Signal] public delegate void KnockbackReducedEventHandler(float originalKnockback, float reducedKnockback);

    [Export] public string ItemName { get; set; } = "Unnamed Armor";
    [Export] public string Description { get; set; } = "No description available.";


    [ExportGroup("Armor Modifiers")]
    [Export(PropertyHint.Range, "0,1.5,0.1")]
    public float KnockbackModifier { get; set; } = 0.8f; // Reduces knockback by 20%

    [Export(PropertyHint.Range, "0,1,0.1")]
    public float DamageModifier { get; set; } = 0.9f; // Reduces damage by 10%

    [Export(PropertyHint.Range, "0,1.5,0.05")]
    public float SpeedModifier { get; set; } = 0.95f; // Reduces speed by 5%    

    [ExportGroup("Visuals")]
    [Export] public Sprite2D BodySprite { get; set; }
    [Export] public Sprite2D HelmetSprite { get; set; }

    [Export] public AnimatedSprite2D PedestalDisplaySprite { get; set; }
    [Export] public Vector2 PedestalDisplayScale { get; set; } = new Vector2(7f, 7f);

    public override void _Ready()
    {
        BodySprite = GetNodeOrNull<Sprite2D>("BodySprite");
        HelmetSprite = GetNodeOrNull<Sprite2D>("HelmetSprite");

        if (PedestalDisplaySprite == null)
        {
            GD.PrintErr("Armor: could not find PedestalDisplaySprite");
            return;
        }
        else
        {
            PedestalDisplaySprite.Visible = false;
        }
    }
        
    public bool CanSwapWith(IPedestalItem otherItem)
    {
        return otherItem == null || otherItem is Armor;
    }

    public void Equip(PlayerController player)
    {
        OwnerCharacter = player;
        
        // Logic to equip armor to the player
        player.UpdateArmorVisuals(this);
        EmitSignal(SignalName.Equipped);
    }

    public void Unequip()
    {
        if (OwnerCharacter != null)
        {
            // Logic to unequip armor from the player
            GD.Print("Armor unequipped from player.");
            OwnerCharacter.UpdateArmorVisuals(null);
            OwnerCharacter = null;
            EmitSignal(SignalName.Unequipped);
        }

    }

    public Vector2 GetDisplayScale()
    {
		if (PedestalDisplaySprite != null)
		{
			return PedestalDisplayScale;
		}

		return new Vector2(7f, 7f);
    }

    public void OnPickedUp(PlayerController player)
    {
        return;
    }
}