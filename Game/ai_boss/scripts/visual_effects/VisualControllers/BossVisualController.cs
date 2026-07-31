using Godot;

public partial class BossVisualController : EntityVisualController<BossState>
{
    public BossRL.BossAttackType CurrentAttackType { private get; set; }

    [Export] private VisualEffect _hitEffect;
    [Export] private VisualEffect _dashEffect;

    public override void _Ready()
    {
        _animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        _baseSprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        
        _hitEffect.InitializeVisuals(_baseSprite);
        _dashEffect.InitializeVisuals(_baseSprite);

        _currentState = BossState.Idle;
        CurrentAttackType = BossRL.BossAttackType.Melee1; // default attack type
   
        if (_animationPlayer != null)
        {
            _animationPlayer.AnimationFinished += OnAnimationFinished;
        }
    }

    public override string GetLibraryForFacing()
	{

        if(_currentState == BossState.Dying || _currentState == BossState.Dead)
            return "";

		Vector2 dir = FacingDirection;

		// During attacks, snap to cardinal so diagonal RL output picks the correct animation library
		if (_currentState == BossState.AttackPrepare || _currentState == BossState.Attacking)
		{
			dir = Mathf.Abs(dir.X) > Mathf.Abs(dir.Y)
				? new Vector2(Mathf.Sign(dir.X), 0)
				: new Vector2(0, Mathf.Sign(dir.Y));
		}

		if (dir.X == 0)
		{
			if (dir.Y > 0) return "down";
			if (dir.Y < 0) return "up";
			return "horizontal";
		}

		return dir.Y < 0 ? "up" : "horizontal";
	}

    public override string GetAnimationNameForState(BossState state)
    {
        string prefix = "";

        if (state == BossState.AttackPrepare || state == BossState.Attacking)
        {
            prefix = CurrentAttackType switch
            {
                BossRL.BossAttackType.Melee1 => "melee1_",
                BossRL.BossAttackType.Melee2 => "melee2_",
                BossRL.BossAttackType.Magic1 => "magic1_",
                BossRL.BossAttackType.Magic2 => "magic2_",
                _ => ""
            };
        }

        string stateName = state switch
        {
            BossState.Idle => "idle",
            BossState.Walking => "walk",
            BossState.Dashing => "dash",
            BossState.Cooldown => IsMoving ? "walk" : "idle",
            BossState.AttackPrepare => "attack_prepare",
            BossState.Attacking => "attack",
            BossState.Dead => "dead",
            _ => "idle"
        };

        return prefix + stateName;
    }

    public override void PlayState(BossState state)
    {
        base.PlayState(state);

        if (state == BossState.Dashing)
        {
            _dashEffect.PlayEffect(_baseSprite);
        }
    }

    public void PlayHitEffect()
    {
        _hitEffect.PlayEffect(_baseSprite);
    }

    
}