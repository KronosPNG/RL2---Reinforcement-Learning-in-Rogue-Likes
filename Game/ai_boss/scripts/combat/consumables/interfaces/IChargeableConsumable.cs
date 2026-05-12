using Godot;

public interface IChargeableConsumable
{
	void StartCharging(Consumable consumable, PlayableCharacter player);
	void UpdateCharge(Consumable consumable, PlayableCharacter player, float delta);
	bool CanReleaseCharge();
	float GetCurrentChargeTime();
	float GetMaxChargeTime();
}
