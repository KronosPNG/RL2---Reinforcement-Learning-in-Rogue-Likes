using Godot;

public partial class ArmorRoom : Room
{
    public override void _Ready()
    {
        base._Ready();
    }

    protected override void SpawnPedestals()
    {
        // Load armor scenes
        var armorScenes = new[] {
            GD.Load<PackedScene>("res://scenes/armors/light_armor.tscn"),
            GD.Load<PackedScene>("res://scenes/armors/medium_armor.tscn"),
            GD.Load<PackedScene>("res://scenes/armors/heavy_armor.tscn")
        };

        SpawnPedestalsWithItems(armorScenes);
    }
}