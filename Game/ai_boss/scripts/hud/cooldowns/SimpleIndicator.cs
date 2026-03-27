using Godot;

public partial class SimpleIndicator : Control
{
	protected TextureRect _texture;
    protected Label _buttonLabel;

	public override void _Ready()
	{
		_texture = GetNode<TextureRect>("TextureRect");
        _buttonLabel = GetNode<Label>("ButtonLabel");
	}
}
