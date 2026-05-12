using System.Threading.Tasks;
using Godot;

public partial class BossAttackManager : WeaponBase
{
    public BossRL OwnerCharacter { get; protected set; }

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

    // ---- Attack Timing ----
    [ExportGroup("Attack Cooldowns")]
    [Export] public float MeleeAttack1Cooldown { get; set; } = 1f;
    [Export] public float MeleeAttack2Cooldown { get; set; } = .5f;
    [Export] public float MagicAttack1Cooldown { get; set; } = 2f;
    [Export] public float MagicAttack2Cooldown { get; set; } = 1f;

    protected Timer _meleeAttack1CooldownTimer;
    protected Timer _meleeAttack2CooldownTimer;
    protected Timer _magicAttack1CooldownTimer;
    protected Timer _magicAttack2CooldownTimer;

    protected bool _isAttackActive = false;


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
        _meleeAttack1CooldownTimer = new Timer();
        _meleeAttack2CooldownTimer = new Timer();
        _magicAttack1CooldownTimer = new Timer();
        _magicAttack2CooldownTimer = new Timer();
    }

    public override bool CanStartAttack()
    {
        // Check if any attack is currently active
        if (_isAttackActive)
            return false;

        // Check cooldowns based on the current attack type
        switch (CurrentAttack)
        {
            case AttackType.Melee1:
                return _meleeAttack1CooldownTimer.IsStopped();
            case AttackType.Melee2:
                return _meleeAttack2CooldownTimer.IsStopped();
            case AttackType.Magic1:
                return _magicAttack1CooldownTimer.IsStopped();
            case AttackType.Magic2:
                return _magicAttack2CooldownTimer.IsStopped();
            default:
                return false;
        }
    }

    protected override Task StartAttackSequence()
    {
        throw new System.NotImplementedException();
    }

    public override void CloseHitWindow()
    {
        throw new System.NotImplementedException();
    }

    public override void OpenHitWindow()
    {
        throw new System.NotImplementedException();
    }

    public override void ResetWeaponState()
    {
        throw new System.NotImplementedException();
    }

    protected override Task AutoInterrupt(float secs)
    {
        throw new System.NotImplementedException();
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
        throw new System.NotImplementedException();
    }
}