using Godot;

public partial class PlayerVisualController : EntityVisualController<EntityState>
{
	//---- Node References ----
	private Node2D _spriteContainer;
	private Sprite2D _bodyArmorSprite;
	private Sprite2D _helmetArmorSprite;
	public Node2D HandNode { get; set; }

	[Export] private VisualFlash _damageFlash;
	[Export] private VisualFlash _dodgeFlash;

	public override void _Ready()
	{
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		_spriteContainer = GetNodeOrNull<Node2D>("BodyLayers");
		_baseSprite = GetNodeOrNull<Sprite2D>("BodyLayers/BaseSprite");
		_bodyArmorSprite = GetNodeOrNull<Sprite2D>("BodyLayers/BodyArmor");
		_helmetArmorSprite = GetNodeOrNull<Sprite2D>("BodyLayers/Helmet");

		// Initialize visual effects
		if (_damageFlash == null)
			_damageFlash = new VisualFlash();
		if (_dodgeFlash == null)
			_dodgeFlash = new VisualFlash();
			
		_damageFlash.InitializeVisuals(_baseSprite);
		_dodgeFlash.InitializeVisuals(_baseSprite);

		_currentState = EntityState.Idle;
		_previousState = _currentState;

		if (_animationPlayer != null)
		{
			_animationPlayer.AnimationFinished += OnAnimationFinished;
		}
	}

	public override string GetLibraryForFacing()
	{
		if (FacingDirection.X == 0)
		{
			if (FacingDirection.Y > 0)
				return "down";
			else if (FacingDirection.Y < 0)
				return "up";
			else
				return "horizontal"; // no vertical facing, return base animation
		}

		else
		{
			if (FacingDirection.Y > 0)
				return "horizontal"; // horizontal facing takes precedence over vertical, so return base animation
			else if (FacingDirection.Y < 0)
				return "up";
			else
				return "horizontal";
		}	
	}

	public override string GetAnimationNameForState(EntityState state)
	{
		return state switch
		{
			EntityState.Dodging => "dodge",
			EntityState.Walking => "walking",
			EntityState.Idle => "idle",
			EntityState.Attacking => IsMoving ? "walking" : "idle",
			EntityState.AttackCharging => IsMoving ? "charge_walking" : "charge_idle",
			EntityState.ConsumableCharging => IsMoving ? "charge_walking" : "charge_idle",
			EntityState.ConsumableUse => IsMoving ? "walking" : "idle",
			EntityState.Hit => "hit",
			EntityState.Dead => "dead",
			EntityState.DodgePrep => "idle", 
			_ => "idle" // Default to idle for any unhandled state
		};
	}

	public override void PlayState(EntityState state)
	{
	   if(state == EntityState.Dead)
		{
			_animationPlayer.Play(GetAnimationNameForState(state));
			return; // Skip the rest of the method to avoid changing animations after death
		} 

		base.PlayState(state);

		switch (_previousState)
		{
			case EntityState.Dodging:
			case EntityState.DodgePrep:
			case EntityState.ConsumableUse:
			case EntityState.ConsumableCharging:
				HandNode?.Visible = true; // Show hand when exiting dodge state
				break;
			default:
				break;
		}

		switch (state)
		{
			case EntityState.Hit:
				_damageFlash.PlayEffect(_spriteContainer);
				break;
			case EntityState.Dodging:
			case EntityState.DodgePrep:
				_dodgeFlash.PlayEffect(_spriteContainer);
				HandNode?.Visible = false; // Hide hand during dodge
				break;
			case EntityState.ConsumableUse:
			case EntityState.ConsumableCharging:
			case EntityState.Dead:
				HandNode?.Visible = false; // Hide hand on death
				break;
			default:
				break;

		}
	}

	public override void UpdateAnimationIfNeeded()
	{
		base.UpdateAnimationIfNeeded();

		if (FacingDirection.Y < 0)
		{
			HandNode.ZIndex = -1; // behind body when facing up
		} else
		{
			HandNode.ZIndex = 0; // in front of body when facing down or horizontal
		}
	}


	public override void UpdateFlip(bool shouldFlip)
	{
		_spriteContainer.Scale = new Vector2(
			shouldFlip ? -1 : 1, 
			1
		);
	}

	public void UpdateArmorVisuals(Armor armor)
	{
		if (armor != null)
		{
			// Copy visual properties from armor sprites to player sprites
			if (_bodyArmorSprite != null && armor.BodySprite != null)
			{
				_bodyArmorSprite.Texture = armor.BodySprite.Texture;
				_bodyArmorSprite.RegionEnabled = armor.BodySprite.RegionEnabled;
				_bodyArmorSprite.RegionRect = armor.BodySprite.RegionRect;
				_bodyArmorSprite.Visible = true;
			}
			
			if (_helmetArmorSprite != null && armor.HelmetSprite != null)
			{
				_helmetArmorSprite.Texture = armor.HelmetSprite.Texture;
				_helmetArmorSprite.RegionEnabled = armor.HelmetSprite.RegionEnabled;
				_helmetArmorSprite.RegionRect = armor.HelmetSprite.RegionRect;
				_helmetArmorSprite.Visible = true;
			}
		}
		else
		{
			// Hide armor sprites when no armor is equipped
			if (_bodyArmorSprite != null)
				_bodyArmorSprite.Visible = false;
			if (_helmetArmorSprite != null)
				_helmetArmorSprite.Visible = false;
		}
	}

	public override void ClearEffects()
	{
		_damageFlash.ClearEffect(_spriteContainer);
		_dodgeFlash.ClearEffect(_spriteContainer);
	}
}
