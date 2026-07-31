using Godot;

public interface IInteractable
{
    public void Interact(Node2D interactor);
    // private void HandleInteraction(PlayerController player);

    public void OnPlayerEntered(Node2D body);

	public void OnPlayerExited(Node2D body);

    public void ShowInteractPrompt(bool show);

    public void UpdateInteractPrompt();
}