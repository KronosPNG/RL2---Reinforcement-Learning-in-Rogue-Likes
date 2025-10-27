using Godot;

public interface IConsumableEffect
{
	void Execute(Consumable consumable, PlayerController player);
	void Interrupt(Consumable consumable, PlayerController player);
	void Update(Consumable consumable, PlayerController player, float delta);
}
