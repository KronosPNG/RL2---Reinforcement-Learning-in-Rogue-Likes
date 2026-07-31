using Godot;

public partial class ConsumableRoom : Room
{
    public override void _Ready()
    {
        base._Ready();
    }

    protected override void SpawnPedestals()
    {
        // Load consumable scenes
        var consumableScenes = new[] {
            GD.Load<PackedScene>("res://scenes/consumables/medkit.tscn"),
            GD.Load<PackedScene>("res://scenes/consumables/potion.tscn"),
        };

        SpawnPedestalsWithItems(consumableScenes);
    }
}