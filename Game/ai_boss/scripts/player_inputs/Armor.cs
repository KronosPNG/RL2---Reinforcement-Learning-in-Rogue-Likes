using Godot;

public partial class Armor : Node2D
{
    // Armor properties can be defined 

    public void Equip(PlayerController player)
    {
        // Logic to equip armor to the player
        GD.Print("Armor equipped to player.");
    }
    
    public void Unequip()
    {
        // Logic to unequip armor from the player
        GD.Print("Armor unequipped from player.");
    }
}