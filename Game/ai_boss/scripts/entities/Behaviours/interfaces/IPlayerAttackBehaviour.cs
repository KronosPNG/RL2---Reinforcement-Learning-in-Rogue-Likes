using Godot;

public interface IPlayerAttackBehaviour
{
    // float EvaluateOpportunity(PlayerMimic player);  // 0-1 opportunity
    // bool CanAttack(PlayerMimic player);
    // void PerformAttack(PlayerMimic player);
    // Vector2 GetAimDirection(PlayerMimic player);
}

public enum AttackType { Light, Heavy, ChargedLight, ChargedHeavy }