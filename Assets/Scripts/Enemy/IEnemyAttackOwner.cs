public interface IEnemyAttackOwner
{
    bool AttackValid { get; }
    bool TryConsumeAttackWindow();
    void OnAttackParried();
}
