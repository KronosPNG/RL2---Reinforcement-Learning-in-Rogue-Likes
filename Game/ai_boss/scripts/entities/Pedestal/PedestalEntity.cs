using Godot;
using System;
public  abstract partial class PedestalEntity<ItemType> : Entity<PedestalEntityState>, IPedestal, IInteractable, IStateful<PedestalEntityState> where ItemType : IPedestalItem
{
	// ---- Signals ----
	[Signal] public delegate void StateChangedEventHandler(string newState);
	
	// ---- Item Scene ----
	[Export] public PackedScene ItemScene; // The item this pedestal offers

	// ---- Node References ----
	protected AnimatedSprite2D _itemSprite;    // The item display sprite
	protected Label _interactPromptLabel;      // The interact prompt label
	protected PlayerController _playerInRange = null; // Reference to player in range (if any)

	// ---- Item Info ----
	protected string _itemName = "Unknown Item";
	protected string _itemDescription = "???";

	// ---- Other Properties ----
	[Export] public string EmptyMessage = "There is nothing on this pedestal.";

	public override void _Ready()
	{
		base._Ready();
		_itemSprite = GetNodeOrNull<AnimatedSprite2D>("ItemSprite");
		_interactPromptLabel = GetNodeOrNull<Label>("InteractPrompt");
		
		if (_itemSprite == null)
			GD.PrintErr("PedestalEntity: Missing ItemSprite (AnimatedSprite2D)");

		if (_interactPromptLabel == null)
			GD.PrintErr("PedestalEntity: Missing InteractPromptLabel (Label)");
		
		// Setup item display
		SetupItemDisplay();

		// Hide interact prompt initially
		if (_interactPromptLabel != null)
			_interactPromptLabel.Visible = false;

		_hitArea.BodyEntered += OnPlayerEntered;
		_hitArea.BodyExited += OnPlayerExited;
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateAI((float)delta);
	}

	protected void SetupItemDisplay()
	{
		if (ItemScene == null)
		{
			ClearItemDisplay();
			return;
		}

		// Instance the item to get its properties
		if (ItemScene.Instantiate() is not Node2D itemInstance)
		{
			GD.PrintErr("PedestalEntity: Failed to instance ItemScene.");
			return;
		}

		if (itemInstance is not IPedestalItem pedestalItem)
		{
			GD.PrintErr("PedestalEntity: Instanced item does not implement IPedestalItem.");
			itemInstance.QueueFree();
			return;
		}

		// Add to tree temporarily to trigger _Ready() and initialize properties
		AddChild(itemInstance);
		
		// Set item sprite and scale
		if (_itemSprite != null)
		{
			_itemName = pedestalItem.ItemName;
			_itemDescription = pedestalItem.Description;

			_itemSprite.SpriteFrames = pedestalItem.PedestalDisplaySprite.SpriteFrames;
			_itemSprite.Animation = pedestalItem.PedestalDisplaySprite.Animation;

			_itemSprite.Scale = pedestalItem.GetDisplayScale();
			_itemSprite.Position = new Vector2(0, -8); // Position above pedestal
			_itemSprite.Visible = true;

			// Play idle animation if it exists
			if (_itemSprite.SpriteFrames.HasAnimation(_itemSprite.Animation))
				_itemSprite.Play(_itemSprite.Animation);
		}

		// Remove and clean up temp item instance
		RemoveChild(itemInstance);
		itemInstance.QueueFree();

		// Update interact prompt
		UpdateInteractPrompt();
	}

	protected void ClearItemDisplay()
	{
		_itemName = EmptyMessage;
		_itemDescription = "There is nothing on this pedestal.";

		if (_itemSprite != null)
		{
			_itemSprite.SpriteFrames = null;
			_itemSprite.Visible = false;
		}

		// Update interact prompt
		UpdateInteractPrompt();
	}

	protected override void UpdateAI(float delta)
	{
		if (StateMachine.CurrentState == PedestalEntityState.PlayerInRange && _playerInRange != null)
		{
			// Check for interaction input
			if (Input.IsActionJustPressed("interact"))
			{
				Interact(_playerInRange);
			}
		}
	}

	protected override void UpdateFacing()
	{
		// Maybe future pedestal types could have facing logic, but for now they are static
		return;
	}

	protected override void UpdateTimers(float delta)
	{
		return;
	}

	public override void HandleStateTransitions()
	{
		// State transitions are handled by player entering/exiting hit area, so no need for additional logic here
		return;
	}

	public override void OnEnterState(PedestalEntityState state)
	{
		switch (state)
		{
			case PedestalEntityState.PlayerOutsideRange:
				ShowInteractPrompt(false);
				
				SetPhysicsProcess(false);
				break;
			case PedestalEntityState.PlayerInRange:
				ShowInteractPrompt(true);

				SetPhysicsProcess(true);
				break;
		}
	}

	public override void OnExitState(PedestalEntityState state)
	{
		return;
	}

	protected override void ApplyMovementByState(float delta)
	{
		return;
	}

	public virtual void Interact(Node2D interactor)
	{
		if (interactor is not PlayerController)
		{
			GD.PrintErr("PedestalEntity: Interactor is not a PlayerController.");
			return;
		}

		if (ItemScene == null) return;
	}

	public void SetItem(PackedScene newItemScene)
	{
		ItemScene = newItemScene;

		if (_itemSprite != null)
		{
			_itemSprite.Visible = true;

			SetupItemDisplay();
		} else
		{
			ClearItemDisplay();
		}

		
	}

	public void OnPlayerEntered(Node2D body)
	{
		_playerInRange = body as PlayerController;
		if (_playerInRange == null) return;

		TransitionToState(PedestalEntityState.PlayerInRange);
	}

	public void OnPlayerExited(Node2D body)
	{
		var player = body as PlayerController;
		if (player == null) return;

		_playerInRange = null;
		TransitionToState(PedestalEntityState.PlayerOutsideRange);
	}

	public void ShowInteractPrompt(bool show)
	{
		_interactPromptLabel.Visible = show;
	}

	public virtual void UpdateInteractPrompt()
	{
		if (ItemScene == null)
		{
			_interactPromptLabel.Text = EmptyMessage;
			return;
		}

		_interactPromptLabel.Text = ItemScene != null ? $"Press [E] to swap for {_itemName}" : EmptyMessage;

	}
}
