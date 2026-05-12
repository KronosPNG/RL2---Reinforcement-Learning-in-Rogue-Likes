using Godot;

public interface IConsumableEffect
{
	void Execute(Consumable consumable, PlayableCharacter player);
	void Interrupt(Consumable consumable, PlayableCharacter player);
	void Update(Consumable consumable, PlayableCharacter player, float delta);
}
