using Godot;

public partial class BossRL : Entity<BossState>, IDamageable, IHasHealth, INavigable, IStateful<BossState>, IAnimatable<BossState>
{
    public float CurrentHealth { get; private set; }

    public float MaxHealth => 2000f;

    public bool IsAlive => CurrentHealth > 0;

    public bool IsInvulnerable { get; private set; }

    public NavigationAgent2D NavAgent { get; private set; }
    public BossAttackManager AttackManager { get; set; }

    [Export] private float _cooldownDuration = .25f;
    public float CooldownTimer { get; private set; }

    public override void OnEnterState(BossState state)
    {
        throw new System.NotImplementedException();
    }

    public override void OnExitState(BossState state)
    {
        throw new System.NotImplementedException();
    }

    public override void HandleStateTransitions()
    {
        throw new System.NotImplementedException();
    }



    public void UpdateAnimationIfNeeded()
    {
        throw new System.NotImplementedException();
    }

    protected override void ApplyMovementByState(float delta)
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateAI(float delta)
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateFacing()
    {
        throw new System.NotImplementedException();
    }

    protected override void UpdateTimers(float delta)
    {
        throw new System.NotImplementedException();
    }



    public void ApplyDamage(float damage, Node2D attacker, float knockbackStrength)
    {
        throw new System.NotImplementedException();
    }

    public void Die()
    {
        throw new System.NotImplementedException();
    }

    public void Heal(float amount)
    {
        throw new System.NotImplementedException();
    }


    public void OnAnimationFinished()
    {
        throw new System.NotImplementedException();
    }

    public void PlayDeathEffect()
    {
        throw new System.NotImplementedException();
    }

    public void PlayHitEffect(Vector2 hitPosition)
    {
        throw new System.NotImplementedException();
    }


    public void ShowDamageNumber(float damage)
    {
        throw new System.NotImplementedException();
    }
}