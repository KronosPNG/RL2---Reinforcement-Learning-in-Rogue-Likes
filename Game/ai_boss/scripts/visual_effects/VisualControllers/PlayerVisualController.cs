using Godot;

public partial class PlayerVisualController : EntityVisualController<EntityState>
{
	//---- Node References ----
	private Node2D _spriteContainer;
	private Sprite2D _bodyArmorSprite;
	private Sprite2D _helmetArmorSprite;
	public Node2D HandNode { get; set; }

	[Export] private VisualEffect _damageEffect;
	[Export] private VisualEffect _dodgeEffect;

	public override void _Ready()
	{
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

		_spriteContainer = GetNodeOrNull<Node2D>("BodyLayers");
		_baseSprite = GetNodeOrNull<Sprite2D>("BodyLayers/BaseSprite");
		_bodyArmorSprite = GetNodeOrNull<Sprite2D>("BodyLayers/BodyArmor");
		_helmetArmorSprite = GetNodeOrNull<Sprite2D>("BodyLayers/Helmet");

		// Initialize visual effects
		if (_damageEffect == null)
			_damageEffect = new VisualFlash();
		if (_dodgeEffect == null)
			_dodgeEffect = new VisualFlash();
			
		_damageEffect.InitializeVisuals(_baseSprite);
		_dodgeEffect.InitializeVisuals(_baseSprite);

		_currentState = EntityState.Idle;

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
			HandNode?.Visible = false; // Hide hand on death
			_animationPlayer.Play(GetAnimationNameForState(state));
			return; // Skip the rest of the method to avoid changing animations after death
		} 

		base.PlayState(state);

		switch (state)
		{
			case EntityState.Hit:
				_damageEffect.PlayEffect(_spriteContainer);
				break;
			case EntityState.Dodging:
			case EntityState.DodgePrep:
				_dodgeEffect.PlayEffect(_spriteContainer);
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

		switch (_currentState)
		{
			case EntityState.Dodging:
			case EntityState.DodgePrep:
			case EntityState.ConsumableUse:
			case EntityState.ConsumableCharging:
				HandNode?.Visible = false;
				break;
			default:
				HandNode?.Visible = true;
				break;
		}

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
		_damageEffect.ClearEffect(_spriteContainer);
		_dodgeEffect.ClearEffect(_spriteContainer);
	}
}
