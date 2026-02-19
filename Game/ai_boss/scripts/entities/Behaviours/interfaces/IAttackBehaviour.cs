public interface IAttackBehaviour
{
    void OnEnterAttack(EnemyEntity entity);
    void OnExitAttack(EnemyEntity entity);
    bool CanAttack(EnemyEntity entity);
    void PerformAttack(EnemyEntity entity);
}