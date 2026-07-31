using Godot;

public partial class WeaponRoom : Room
{
	public override void _Ready()
	{
		base._Ready();
	}

	protected override void SpawnPedestals()
	{
		// Load weapon scenes
		var weaponScenes = new[] {
			GD.Load<PackedScene>("res://scenes/weapons/dagger.tscn"),
			GD.Load<PackedScene>("res://scenes/weapons/bow.tscn"),
			GD.Load<PackedScene>("res://scenes/weapons/staff.tscn")
		};

		SpawnPedestalsWithItems(weaponScenes);
	}
}
