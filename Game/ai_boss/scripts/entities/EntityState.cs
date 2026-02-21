public enum EntityState
	{
		Idle,
		Walking,
		Wandering = Walking, // Alias for wandering behavior
		Aggro,
		AttackPrepare,
		AttackCharging,
		Attacking,
		Hit,
		DodgePrep,
		Dodging,
		ConsumableUse,
		ConsumableCharging,
		Dying,
		Dead
	}