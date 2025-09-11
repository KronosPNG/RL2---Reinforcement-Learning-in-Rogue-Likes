using Godot;
using System;

public partial class Main : Node2D
{
	private PackedScene beanScene;
	private PackedScene weaponScene;
	private Node2D sortSceneElements;
	
	public override void _Ready()
	{
		// Load scenes
		beanScene = ResourceLoader.Load<PackedScene>("res://scenes/bean.tscn");
		weaponScene = ResourceLoader.Load<PackedScene>("res://scenes/weapons/bow.tscn");
		
		// Get the SortSceneElements node
		sortSceneElements = GetNode<Node2D>("SortSceneElements");
		
		// Instantiate Bean in sorting area
		PlayerController beanInstance = beanScene.Instantiate<PlayerController>();
		beanInstance.Position = new Vector2(0, 0); // Center position
		sortSceneElements.AddChild(beanInstance);
		
		// Wait for Bean to be ready, then spawn sword in hand
		CallDeferred(nameof(SpawnWeaponInBeanHand), beanInstance);
	}

	private void SpawnWeaponInBeanHand(PlayerController bean)
	{
		bean.CallDeferred("EquipWeapon", weaponScene);
	}
}
