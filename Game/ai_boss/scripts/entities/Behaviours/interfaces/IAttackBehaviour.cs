public interface IAttackBehaviour
{
    void OnEnterAttack(Entity entity);
    void OnExitAttack(Entity entity);
    bool CanAttack(Entity entity);
    void PerformAttack(Entity entity);
}