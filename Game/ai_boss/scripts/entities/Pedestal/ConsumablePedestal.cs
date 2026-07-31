using Godot;

public partial class ConsumablePedestal : PedestalEntity<Consumable>
{
    public override void Interact(Node2D interactor)
    {
        base.Interact(interactor);
    
        if (interactor is PlayerController player)
        {
            // Get the currently equipped consumable scene from the player before swapping
            PackedScene consumableScene = player.EquippedConsumableScene;

            // Swap the player's consumable with the one on the pedestal
            player.EquipConsumable(ItemScene);

            // Put the player's old consumable on the pedestal
            SetItem(consumableScene);
        }

    }
}