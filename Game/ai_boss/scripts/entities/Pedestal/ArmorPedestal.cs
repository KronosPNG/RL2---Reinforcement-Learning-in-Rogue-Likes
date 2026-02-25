using Godot;

public partial class ArmorPedestal : PedestalEntity<Armor>
{
    public override void Interact(Node2D interactor)
    {
        base.Interact(interactor);
    
        if (interactor is PlayerController player)
        {
            // Get the currently equipped armor scene from the player before swapping
            PackedScene armorScene = player.EquippedArmorScene;

            // Swap the player's armor with the one on the pedestal
            player.EquipArmor(ItemScene);

            // Put the player's old armor on the pedestal
            SetItem(armorScene);
        }

    }
}