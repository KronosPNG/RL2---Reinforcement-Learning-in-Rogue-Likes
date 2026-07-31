using Godot;

public partial class HorizontalEntityVisualController: EntityVisualController<EntityState>
{
	[Export] private VisualEffect _damageEffect;
	[Export] private VisualEffect _deathEffect;

	public override void _Ready()
	{
		base._Ready();
		
		// Initialize visual effects with sprite's original modulate
		if (_baseSprite != null)
		{
			_damageEffect?.InitializeVisuals(_baseSprite);
			_deathEffect?.InitializeVisuals(_baseSprite);
		}
	}

    public override string GetAnimationNameForState(EntityState state)
	{
		return state switch
		{
			EntityState.Idle => "idle",
			EntityState.Wandering => "walk",
			EntityState.Aggro => "walk",
			EntityState.AttackPrepare => "attack_prepare",
			EntityState.AttackCharging => "attack_charge",
			EntityState.Attacking => "attack",
			EntityState.Hit => "hit",
			EntityState.Dying => "die",
			EntityState.Dead => "die",
			_ => "idle"
		};
	}

	public override void UpdateAnimationIfNeeded()
	{
		// Update flip based on facing direction every frame
		UpdateFlip(FacingDirection.X < 0);
		
		// Call base to handle animation state changes
		base.UpdateAnimationIfNeeded();
	}

	public override void PlayState(EntityState state)
	{
		base.PlayState(state); // Call base to handle animation changes
		switch (state)
		{
			case EntityState.Hit:
				if (_damageEffect != null)
					_damageEffect.PlayEffect(_baseSprite);
				break;

			case EntityState.Dying:
				if (_deathEffect != null)
					_deathEffect.PlayEffect(_baseSprite);
				break;
		}
	}

	public override void ClearEffects()
	{
		_damageEffect?.ClearEffect(_baseSprite);
			
		_deathEffect?.ClearEffect(_baseSprite);
	}
}