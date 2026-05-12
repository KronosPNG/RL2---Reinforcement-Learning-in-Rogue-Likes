using Godot;
using System;

public enum ConsumableState { Available, Windup, InUse }

public partial class Consumable : Node2D, IPedestalItem
{
	public AnimatedSprite2D ActiveSprite { get; private set; }
	public PlayableCharacter OwnerCharacter { get; protected set; }

	// ---- Signals ----
	[Signal] public delegate void EquippedEventHandler();
	[Signal] public delegate void UnequippedEventHandler();
	[Signal] public delegate void ConsumableUsedEventHandler(string consumableName);
	[Signal] public delegate void ConsumableCompletedEventHandler(string consumableName);
	[Signal] public delegate void ChargeStartedEventHandler(string consumableName);
	[Signal] public delegate void ChargeUpdatedEventHandler(float chargeLevel);
	[Signal] public delegate void ChargeReleasedEventHandler(string consumableName);
	[Signal] public delegate void ChargeCancelledEventHandler(string consumableName);

	// ---- Non Mechanical Properties ----
	[Export] public string ItemName { get; set; } = "Unnamed Consumable Item";
	[Export] public string Description { get; set; } = "No description available.";

	// ---- Pedestal Item Properties ----
	[Export]public AnimatedSprite2D PedestalDisplaySprite { get; set; }
	[Export] public Vector2 PedestalDisplayScale { get; set; } = new Vector2(1f, 1f);

	// ---- Effect Configuration ----
	[Export] public ConsumableEffectBase EffectConfig { get; set; }
	[Export] public bool IsSingleUse { get; set; } = false;

	// ---- States ----
	public ConsumableState State { get; private set; } = ConsumableState.Available;
	private ConsumableEffectBase _activeEffect = null;
	private bool _isChargingConsumable = false;

	// ---- Timers ----
	private float _cooldownTimer = 0f;
	private float _effectTimer = 0f;
	
	// ---- Cancellation ----
	private System.Threading.CancellationTokenSource _windupCancellationSource = null;

	public override void _Ready()
	{
		ActiveSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

		if (ActiveSprite == null)
		{
			GD.PrintErr($"[Consumable] AnimatedSprite2D node not found in consumable: {Name}");
			throw new System.Exception("AnimatedSprite2D node is required for Consumable");
		}

		if (PedestalDisplaySprite == null)
		{
			PedestalDisplaySprite = ActiveSprite; // Use main sprite if no pedestal sprite
		}
		else
		{
			// Hide pedestal display sprite by default
			PedestalDisplaySprite.Visible = false;
		}

		// Hide the active sprite by default (consumables should not be visible when not in use)
		ActiveSprite.Visible = false;

		// Disable physics processing until equipped
		SetPhysicsProcess(false);

		// Adjust animation speed to match usage time
		if (EffectConfig.UsageTime > 0 && ActiveSprite.SpriteFrames != null && ActiveSprite.SpriteFrames.HasAnimation("use"))
		{
			int frameCount = ActiveSprite.SpriteFrames.GetFrameCount("use");
			if (frameCount > 0)
			{
				// Calculate FPS needed for animation to complete in UsageTime
				float desiredFps = frameCount / EffectConfig.UsageTime;
				ActiveSprite.SpriteFrames.SetAnimationSpeed("use", desiredFps);
				// GD.Print($"[Consumable] Set animation FPS to {desiredFps:F2} ({frameCount} frames / {EffectConfig.UsageTime}s)");
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Update cooldown timer
		if (_cooldownTimer > 0)
		{
			_cooldownTimer = Math.Max(0, _cooldownTimer - (float)delta);
		}

		// Update charging state
		if (_isChargingConsumable && _activeEffect is IChargeableConsumable chargeableEffect)
		{
			chargeableEffect.UpdateCharge(this, OwnerCharacter, (float)delta);

			// Emit charge level updates for UI/feedback
			float chargeLevel = chargeableEffect.GetCurrentChargeTime() / chargeableEffect.GetMaxChargeTime();
			EmitSignal(nameof(ChargeUpdated), chargeLevel);

			// Auto-release when max charge is reached
			if (chargeableEffect.GetCurrentChargeTime() >= chargeableEffect.GetMaxChargeTime())
			{
				// GD.Print("[Consumable] Max charge reached, auto-releasing");
				ExecuteCharged();
			}
		}

		// Update active effect (for effects like regeneration)
		if (State == ConsumableState.InUse && _activeEffect != null)
		{
			_activeEffect.Update(this, OwnerCharacter, (float)delta);
		}
	}

	// ---- Equip/Unequip ----
	public virtual void Equip(PlayableCharacter player)
	{
		OwnerCharacter = player;
		SetPhysicsProcess(true);
		EmitSignal(nameof(Equipped));
	}

	public virtual void Unequip()
	{
		// Cancel any active windup
		_windupCancellationSource?.Cancel();
		_windupCancellationSource?.Dispose();
		_windupCancellationSource = null;

		// Cancel any active effects
		if (State == ConsumableState.InUse && _activeEffect != null)
		{
			_activeEffect.Interrupt(this, OwnerCharacter);
		}

		SetPhysicsProcess(false);
		OwnerCharacter = null;
		EmitSignal(nameof(Unequipped));
	}

	// ---- Usage API ----
	public void Use()
	{
		if (!CanUse())
		{
			// GD.Print($"[Consumable] Cannot use {ItemName}: state={State}, cooldown={_cooldownTimer}");
			return;
		}

		if (EffectConfig == null)
		{
			GD.PrintErr($"[Consumable] No effect configured for {ItemName}");
			return;
		}

		// Check if this is a chargeable consumable
		if (EffectConfig is IChargeableConsumable)
		{
			StartCharging();
		}
		else
		{
			// Instant use
			_ = StartUseSequence();
		}
	}

	public bool CanUse()
	{
		return State == ConsumableState.Available && _cooldownTimer <= 0f && EffectConfig != null;
	}

	// ---- Usage Sequence ----
	private async System.Threading.Tasks.Task StartUseSequence()
	{
		// Cancel any existing windup sequence
		_windupCancellationSource?.Cancel();
		_windupCancellationSource?.Dispose();
		_windupCancellationSource = new System.Threading.CancellationTokenSource();
		
		var cancellationToken = _windupCancellationSource.Token;

		State = ConsumableState.Windup;
		_activeEffect = EffectConfig;

		EmitSignal(nameof(ConsumableUsed), ItemName);

		// Show sprite and play usage animation if configured
		if (ActiveSprite != null)
		{
			ActiveSprite.Visible = true;
			ActiveSprite.Play("use");
		}

		// Wait for usage time (windup)
		if (EffectConfig.UsageTime > 0)
		{
			try
			{
				await ToSignal(GetTree().CreateTimer(EffectConfig.UsageTime), "timeout");
			}
			catch (System.Threading.Tasks.TaskCanceledException)
			{
				// GD.Print($"[Consumable] Windup task cancelled for {ItemName}");
				return;
			}
		}

		// Check if the windup was cancelled (e.g., player got hit)
		if (cancellationToken.IsCancellationRequested)
		{
			// GD.Print($"[Consumable] Windup cancelled for {ItemName}");
			return;
		}

		// Set cooldown now (right before execution)
		_cooldownTimer = EffectConfig.Cooldown;

		// Execute the effect
		State = ConsumableState.InUse;
		_activeEffect.Execute(this, OwnerCharacter);

		// Clean up cancellation token
		_windupCancellationSource?.Dispose();
		_windupCancellationSource = null;

		// If it's an instant effect, it will call CompleteEffect immediately
		// Otherwise, the effect will manage its own completion (e.g., regeneration)
	}

	// Called by effects when they complete
	public void CompleteEffect()
	{
		if (_activeEffect != null)
		{
			_activeEffect = null;
		}

		EmitSignal(nameof(ConsumableCompleted), ItemName);

		// If single-use, remove the consumable
		if (IsSingleUse)
		{
			RemoveConsumable();
		}
		else
		{
			ResetConsumableState();
		}
	}

	private void RemoveConsumable()
	{
		// Clean up cancellation token
		_windupCancellationSource?.Cancel();
		_windupCancellationSource?.Dispose();
		_windupCancellationSource = null;

		// Clear references in player if currently equipped
		if (OwnerCharacter != null)
		{
			// The signals will be automatically disconnected when QueueFree is called
			OwnerCharacter.EquippedConsumable = null;
			OwnerCharacter.EquippedConsumableScene = null;
			OwnerCharacter = null;
		}

		// Remove from scene tree
		QueueFree();
	}

	public void ResetConsumableState()
	{
		State = ConsumableState.Available;
		_activeEffect = null;
		_isChargingConsumable = false;

		// Hide sprite when not in use
		if (ActiveSprite != null)
		{
			ActiveSprite.Visible = false;
		}
	}

	// ---- Charging Logic ----
	public bool HasChargeableEffect()
	{
		return EffectConfig is IChargeableConsumable;
	}

	public void StartCharging()
	{
		if (State != ConsumableState.Available || EffectConfig is not IChargeableConsumable chargeableEffect)
			return;

		_isChargingConsumable = true;
		_activeEffect = EffectConfig;
		State = ConsumableState.Windup;

		// Show sprite when charging starts
		if (ActiveSprite != null)
		{
			ActiveSprite.Visible = true;
		}

		chargeableEffect.StartCharging(this, OwnerCharacter);
		EmitSignal(nameof(ChargeStarted), ItemName);
	}

	public bool CanReleaseCharge()
	{
		return _isChargingConsumable && _activeEffect is IChargeableConsumable chargeable && chargeable.CanReleaseCharge();
	}

	public void ExecuteCharged()
	{
		if (!_isChargingConsumable || _activeEffect is not IChargeableConsumable)
		{
			// GD.Print("[Consumable] Cannot execute charge: not charging");
			return;
		}

		// Set cooldown
		_cooldownTimer = EffectConfig.Cooldown;

		// GD.Print($"[Consumable] Executing charged consumable: {ItemName}");
		
		EmitSignal(nameof(ConsumableUsed), ItemName);
		EmitSignal(nameof(ChargeReleased), ItemName);

		// Execute the charged effect
		State = ConsumableState.InUse;
		_activeEffect.Execute(this, OwnerCharacter);

		// Clean up charging state
		_isChargingConsumable = false;
		
		// GD.Print($"[Consumable] Charged consumable execution complete: {ItemName}, waiting for effect completion");
	}

	public void CancelCharge()
	{
		if (!_isChargingConsumable) return;

		if (_activeEffect != null)
		{
			_activeEffect.Interrupt(this, OwnerCharacter);
		}

		EmitSignal(nameof(ChargeCancelled), ItemName);

		// Reset animation
		ActiveSprite.Stop();

		_isChargingConsumable = false;
		_activeEffect = null;
		State = ConsumableState.Available;

		// Hide sprite when charge is cancelled
		if (ActiveSprite != null)
		{
			ActiveSprite.Visible = false;
		}
	}

	// ---- Interruption ----
	/// <summary>
	/// Cancels consumable use if player gets hit during windup or active use
	/// </summary>
	public void InterruptUse()
	{
		// GD.Print($"[Consumable] Interrupting consumable use: {ItemName}, State: {State}");

		// If we're in windup phase, cancel the async task
		if (State == ConsumableState.Windup)
		{
			_windupCancellationSource?.Cancel();
		}

		// If we're in active use phase, interrupt the effect
		if (State == ConsumableState.InUse && _activeEffect != null)
		{
			_activeEffect.Interrupt(this, OwnerCharacter);
		}

		// Clean up state
		_activeEffect = null;
		State = ConsumableState.Available;

		// Hide sprite and stop animation
		if (ActiveSprite != null)
		{
			ActiveSprite.Visible = false;
			ActiveSprite.Stop();
		}

		// GD.Print($"[Consumable] Use interrupted for {ItemName}");
	}

	// ---- IPedestalItem Implementation ----
	public bool CanSwapWith(IPedestalItem otherItem)
	{
		return otherItem == null || otherItem is Consumable;
	}

	public Vector2 GetDisplayScale()
	{
		if (PedestalDisplaySprite != null)
		{
			return PedestalDisplayScale;
		}

		return new Vector2(7f, 7f);
	}

	public void OnPickedUp(PlayerController player)
	{
		// Possibly play a sound or effect here
	}
}