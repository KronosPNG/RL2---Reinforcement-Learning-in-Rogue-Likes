using Godot;

public interface IChargeableConsumable
{
	void StartCharging(Consumable consumable, PlayerController player);
	void UpdateCharge(Consumable consumable, PlayerController player, float delta);
	bool CanReleaseCharge();
	float GetCurrentChargeTime();
	float GetMaxChargeTime();
}
