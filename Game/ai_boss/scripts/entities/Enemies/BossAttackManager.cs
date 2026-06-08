using System.Threading.Tasks;
using Godot;

public partial class BossAttackManager : WeaponBase
{
    public new BossRL OwnerCharacter { get; protected set; }

    public enum AttackType
    {
        Melee1,
        Melee2,
        Magic1,
        Magic2
    }

    public AttackType CurrentAttack { get; protected set; }

    // ---- Attack Configuration ----
    [ExportGroup("Attack Configurations")]
    [Export] public AttackBase MeleeAttack1 { get; set; }
    [Export] public AttackBase MeleeAttack2 { get; set; }
    [Export] public AttackBase MagicAttack1 { get; set; }
    [Export] public AttackBase MagicAttack2 { get; set; }

    // ---- Attack Timers ----
    protected Timer _meleeAttack1CooldownTimer;
    protected Timer _meleeAttack2CooldownTimer;
    protected Timer _magicAttack1CooldownTimer;
    protected Timer _magicAttack2CooldownTimer;

    
    private bool _playerAlreadyHit = false;

    public override void _Ready()
    {
        OwnerCharacter = GetParent<BossRL>();

        // Override base class node initialization to match Entity weapon structure
        // In entity weapons, the HitArea is called "AttackArea" and uses CollisionPolygon2D
        HitArea = GetNodeOrNull<Area2D>("AttackArea");
        HitAreaShape = GetNodeOrNull<CollisionPolygon2D>("AttackArea/CollisionPolygon2D");
        Sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

        if (HitArea == null)
            GD.PrintErr($"[BossAttackManager] AttackArea node not found in weapon");

        if (HitAreaShape == null)
            GD.PrintErr($"[BossAttackManager] CollisionPolygon2D not found in weapon/AttackArea");

        if (Sprite == null)
            GD.PrintErr($"[BossAttackManager] Sprite node not found in weapon");

        // Connect hit area signals
        if (HitArea != null)
        {
            HitArea.Monitoring = false;
            HitArea.BodyEntered += OnBodyEntered;
            HitArea.AreaEntered += OnAreaEntered;
        }

        // Initialize cooldown timers
        _meleeAttack1CooldownTimer = GetNodeOrNull<Timer>("Timers/MeleeAttack1CooldownTimer");
        _meleeAttack1CooldownTimer.WaitTime = MeleeAttack1.Cooldown;

        _meleeAttack2CooldownTimer = GetNodeOrNull<Timer>("Timers/MeleeAttack2CooldownTimer");
        _meleeAttack2CooldownTimer.WaitTime = MeleeAttack2.Cooldown;

        _magicAttack1CooldownTimer = GetNodeOrNull<Timer>("Timers/MagicAttack1CooldownTimer");
        _magicAttack1CooldownTimer.WaitTime = MagicAttack1.Cooldown;

        _magicAttack2CooldownTimer = GetNodeOrNull<Timer>("Timers/MagicAttack2CooldownTimer");
        _magicAttack2CooldownTimer.WaitTime = MagicAttack2.Cooldown;
    }

    public override bool CanStartAttack()
    {
        // Check if any attack is currently active
        if (State != WeaponState.Ready)
            return false;

        // Check cooldowns based on the current attack type
        return CurrentAttack switch
        {
            AttackType.Melee1 => _meleeAttack1CooldownTimer.IsStopped(),
            AttackType.Melee2 => _meleeAttack2CooldownTimer.IsStopped(),
            AttackType.Magic1 => _magicAttack1CooldownTimer.IsStopped(),
            AttackType.Magic2 => _magicAttack2CooldownTimer.IsStopped(),
            _ => false,
        };
    }

    protected override async Task StartAttackSequence()
    {
        ResetCurrentAttackCooldown();

        State = WeaponState.Windup;

        float windup = CurrentAttack switch
        {
            AttackType.Melee1 => MeleeAttack1.Windup,
            AttackType.Melee2 => MeleeAttack2.Windup,
            AttackType.Magic1 => MagicAttack1.Windup,
            AttackType.Magic2 => MagicAttack2.Windup,
            _ => 0f,
        };

        await ToSignal(GetTree().CreateTimer(windup), "timeout");

        if (State == WeaponState.Windup)
        {
            OpenHitWindow();
        }
    }

    public override void OpenHitWindow()
    {
        if (State == WeaponState.Active)
            return;

        State = WeaponState.Active;
        _playerAlreadyHit = false;

        switch (CurrentAttack)
        {
            case AttackType.Melee1:
                MeleeAttack1.Execute(this, GetAimDirection(), FacingLeft);
                break;

            case AttackType.Melee2:
                MeleeAttack2.Execute(this, GetAimDirection(), FacingLeft);
                break;
            
            case AttackType.Magic1:
                MagicAttack1.Execute(this, GetAimDirection(), FacingLeft);
                break;

            case AttackType.Magic2:
                MagicAttack2.Execute(this, GetAimDirection(), FacingLeft);
                break;
        }

        float activeDuration = CurrentAttack switch
        {
            AttackType.Melee1 => MeleeAttack1.Active,
            AttackType.Melee2 => MeleeAttack2.Active,
            AttackType.Magic1 => MagicAttack1.Active,
            AttackType.Magic2 => MagicAttack2.Active,
            _ => 0f,
        };

        _ = AutoInterrupt(activeDuration);
    }

    public override void CloseHitWindow()
    {
        ResetWeaponState();
    }

    public override void ResetWeaponState()
    {
        State = WeaponState.Ready;
        _playerAlreadyHit = false;
    }

    protected async override Task AutoInterrupt(float secs)
    {
        await ToSignal(GetTree().CreateTimer(secs), "timeout");

        if (State == WeaponState.Active)
        {
            var attack = GetAttackFromType(CurrentAttack);
            attack.Interrupt(this);
        }
    }

    protected override Vector2 GetAimDirection()
    {
        if (OwnerCharacter == null)
		{
			GD.PrintErr("WeaponEntity: GetAimDirection() - OwnerCharacter is null!");
			return Vector2.Right; // Default direction
		}

		if (!IsInstanceValid(OwnerCharacter.Target))
		{
			GD.PrintErr("WeaponEntity: GetAimDirection() - OwnerCharacter is not valid!");
			return Vector2.Right; // Default direction
		}

        Vector2 direction = OwnerCharacter.Target.GlobalPosition - OwnerCharacter.GlobalPosition;
        direction = direction.Normalized();

        switch(CurrentAttack)
        {
            case AttackType.Melee1:
            case AttackType.Melee2:
                // Melee attacks snap to cardinal directions
                if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
                    return new Vector2(Mathf.Sign(direction.X), 0);
                else
                    return new Vector2(0, Mathf.Sign(direction.Y));

            case AttackType.Magic1:
            case AttackType.Magic2:
                return direction;
            default:
                return Vector2.Right; // Default direction
        }
    }

    protected override void OnBodyEntered(Node body)
    {
        if (body == null) return;
        if(_playerAlreadyHit) return;

        _playerAlreadyHit = true;

        var attack = GetAttackFromType(CurrentAttack);

        float damage = attack.Damage;
        float knockback = attack.Knockback;

        if (body is IDamageable damageable)
        {
            damageable.ApplyDamage(damage, OwnerCharacter, knockback);
        }
    }

    // --- Helper Methods ---

    private void ResetCurrentAttackCooldown()
    {
        switch (CurrentAttack)
        {
            case AttackType.Melee1:
                _meleeAttack1CooldownTimer.Start();
                break;
            case AttackType.Melee2:
                _meleeAttack2CooldownTimer.Start();
                break;
            case AttackType.Magic1:
                _magicAttack1CooldownTimer.Start();
                break;
            case AttackType.Magic2:
                _magicAttack2CooldownTimer.Start();
                break;
        }
    }

    private AttackBase GetAttackFromType(AttackType type)
    {
        return type switch
        {
            AttackType.Melee1 => MeleeAttack1,
            AttackType.Melee2 => MeleeAttack2,
            AttackType.Magic1 => MagicAttack1,
            AttackType.Magic2 => MagicAttack2,
            _ => null,
        };
    }
}