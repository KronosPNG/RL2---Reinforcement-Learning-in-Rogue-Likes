using Godot;

// Interface for any item that can be placed on pedestals
public interface IPedestalItem
{
    string ItemName { get; } // Name of the weapon/item;
    string Description { get; } // Description of the weapon/item
    public AnimatedSprite2D PedestalDisplaySprite { get; set; } // Return the sprite node to copy from
    public Vector2 PedestalDisplayScale { get; set; } // Return the scale to use for pedestal display
    Vector2 GetDisplayScale(); // Return the scale to use for pedestal display
    bool CanSwapWith(IPedestalItem otherItem); // Can this item be swapped with another?
    void OnPickedUp(PlayerController player); // What happens when player takes this item
}