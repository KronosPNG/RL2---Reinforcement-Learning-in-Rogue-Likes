using Godot;

public partial class WeaponPedestal : PedestalEntity<Weapon>
{
	public override void Interact(Node2D interactor)
	{
		base.Interact(interactor);
	
		if (interactor is PlayerController player)
		{
			// Get the currently equipped weapon scene from the player before swapping
			PackedScene weaponScene = player.EquippedWeaponScene;

			// Swap the player's weapon with the one on the pedestal
			player.EquipWeapon(ItemScene);

			// Put the player's old weapon on the pedestal
			SetItem(weaponScene);
		}

	}
}
