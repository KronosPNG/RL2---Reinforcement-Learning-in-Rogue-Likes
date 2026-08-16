using Godot;

public partial class SimpleIndicator : Control
{
	protected TextureRect _texture;
    protected Control _buttonLabel;

	public override void _Ready()
	{
		_texture = GetNode<TextureRect>("TextureRect");
        _buttonLabel = GetNode<Control>("ButtonLabel");
	}
}
