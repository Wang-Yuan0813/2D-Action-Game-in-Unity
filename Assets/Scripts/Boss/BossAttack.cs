using System.Collections.Generic;
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

    private IEnemyAttackOwner attackOwner;
    private Boss_Control boss;
    private Collider2D attackCollider;
    private readonly List<Collider2D> overlapResults = new List<Collider2D>(8);
    private ContactFilter2D overlapFilter;

    public EnemyAttackType AttackType => attackType;
    public bool CanBeParried => canBeParried;
    public float CounterSmash => counterSmash;
    public float AttackerX => transform.parent != null ? transform.parent.position.x : transform.position.x;

    private void Awake()
    {
        attackCollider = GetComponent<Collider2D>();
        overlapFilter = new ContactFilter2D();
        overlapFilter.NoFilter();
        overlapFilter.useTriggers = true;
        ResolveAttackOwner();
    }

    private void OnEnable()
    {
        ResolveAttackOwner();
    }

    private void ResolveAttackOwner()
    {
        if (attackOwner != null)
            return;

        foreach (MonoBehaviour behaviour in GetComponentsInParent<MonoBehaviour>(true))
        {
            if (!(behaviour is IEnemyAttackOwner owner))
                continue;

            attackOwner = owner;
            boss = behaviour as Boss_Control;
            break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryResolveHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // An animated attack area can become active while already overlapping a stationary player.
        TryResolveHit(other);
    }

    private void FixedUpdate()
    {
        ResolveAttackOwner();
        if (attackCollider == null || !attackCollider.enabled || attackOwner == null || !attackOwner.AttackValid)
            return;

        // Trigger callbacks can be skipped when the player Rigidbody2D is asleep and an
        // already-overlapping attack area is enabled. Query the attack shape explicitly so
        // a stationary player is evaluated exactly like a moving player.
        attackCollider.OverlapCollider(overlapFilter, overlapResults);
        for (int i = 0; i < overlapResults.Count && attackOwner.AttackValid; i++)
        {
            Collider2D candidate = overlapResults[i];
            if (candidate != null)
                TryResolveHit(candidate);
        }

        overlapResults.Clear();
    }

    private void TryResolveHit(Collider2D other)
    {
        ResolveAttackOwner();
        if (!other.CompareTag("Player") || attackOwner == null || !attackOwner.AttackValid)
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
        attackOwner.TryConsumeAttackWindow();
        if (defense == PlayerDefenseResult.Invulnerable)
            return;

        if (!isStab)
        {
            player.TakeHit(smash, attackerX);
        }
        else
        {
            player.GetCatched(attackerX);
            if (boss != null)
            {
                boss.catchPlayer = true;
                boss.isCatchPlayer = true;
            }
        }
    }

    public bool TryConsumeAsParried()
    {
        if (attackOwner == null || !attackOwner.AttackValid || attackType != EnemyAttackType.Melee || !canBeParried)
            return false;

        return attackOwner.TryConsumeAttackWindow();
    }

    public void ApplyParryReaction()
    {
        attackOwner?.OnAttackParried();
    }
}
