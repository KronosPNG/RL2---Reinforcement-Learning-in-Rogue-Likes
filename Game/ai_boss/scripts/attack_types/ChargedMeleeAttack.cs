using Godot;

[GlobalClass]
public partial class ChargedMeleeAttack : ChargedAttack, IAttack, IChargeable
{
	[ExportGroup("Melee Attack")]
	[Export] public AttackBase MeleeAttack { get; set; } // The melee attack to use

	protected override async void ExecuteChargedAttack(Weapon weapon, Vector2 target, bool facingLeft, float chargedDamage)
	{
		if (MeleeAttack == null)
		{
			GD.PrintErr("ChargedMeleeAttack: MeleeAttack is null!");
			weapon.ResetWeaponState();
			return;
		}

		// Play attack animation first
		if (weapon.Sprite != null)
		{
			// GD.Print("[ChargedMeleeAttack] Playing attack animation");
			string animName = weapon.IsCurrentAttackHeavy ? "heavy_attack" : "light_attack";
			weapon.Sprite.Play(animName);
		}

		// Store original damage and temporarily set charged damage
		float originalDamage = MeleeAttack.Damage;
		MeleeAttack.Damage = chargedDamage;

		// Execute the melee attack with charged damage
		MeleeAttack.Execute(weapon, target, facingLeft);

		// Restore original damage
		MeleeAttack.Damage = originalDamage;

		// Calculate total attack duration (windup + active time)
		float attackDuration = Windup + Active;
		
		// Wait for the attack to complete, then reset weapon state
		await weapon.ToSignal(weapon.GetTree().CreateTimer(attackDuration), "timeout");
		
		Interrupt(weapon);
	}

	public override void Interrupt(WeaponBase weapon)
	{
		base.Interrupt(weapon);
		if (weapon is Weapon playerWeapon)
		{
			MeleeAttack?.Interrupt(weapon);
			// Ensure weapon state is reset when interrupted
			playerWeapon.ResetWeaponState();
		}
	}

}
