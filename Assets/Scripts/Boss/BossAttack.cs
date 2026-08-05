using UnityEngine;

public class BossAttack : MonoBehaviour
{
    [Header("攻击属性")]
    public float smash;
    public float counterSmash;
    public float attackerX;
    public bool isStab;

    [Header("防御交互")]
    [SerializeField] private EnemyAttackType attackType = EnemyAttackType.Melee;
    [SerializeField] private bool canBeParried = true;

    private Boss_Control boss;

    public EnemyAttackType AttackType => attackType;
    public bool CanBeParried => canBeParried;
    public float CounterSmash => counterSmash;
    public float AttackerX => transform.parent != null ? transform.parent.position.x : transform.position.x;

    private void Awake()
    {
        boss = GetComponentInParent<Boss_Control>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || boss == null || !boss.attackValid)
            return;

        Player_Control player = other.GetComponentInParent<Player_Control>();
        if (player == null)
            return;

        attackerX = AttackerX;
        PlayerDefenseResult defense = player.ResolveIncomingAttack(attackType, canBeParried, attackerX);

        if (defense == PlayerDefenseResult.Parried)
        {
            PlayerParryController parryController = player.GetComponent<PlayerParryController>();
            if (parryController != null && parryController.TryParry(this))
                return;

            // Missing feedback controller must never turn a valid parry window into damage.
            TryConsumeAsParried();
            return;
        }

        // Invulnerability and successful-parry protection consume this Boss hit without damage.
        boss.attackValid = false;
        if (defense == PlayerDefenseResult.Invulnerable)
            return;

        if (!isStab)
        {
            player.TakeHit(smash, attackerX);
        }
        else
        {
            player.GetCatched(attackerX);
            boss.catchPlayer = true;
            boss.isCatchPlayer = true;
        }
    }

    public bool TryConsumeAsParried()
    {
        if (boss == null || !boss.attackValid || attackType != EnemyAttackType.Melee || !canBeParried)
            return false;

        boss.attackValid = false;
        return true;
    }

    public void ApplyParryReaction()
    {
        if (boss != null)
            boss.OnParried();
    }
}
