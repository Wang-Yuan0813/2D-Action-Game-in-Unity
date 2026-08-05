using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [Header("攻击设置")]
    [SerializeField, Min(1)] private int attackDamage = 10;

    private Player_Control player;
    private PlayerParryController parryController;

    private void Awake()
    {
        player = GetComponentInParent<Player_Control>();
        parryController = GetComponentInParent<PlayerParryController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (player == null)
            return;

        if (other.CompareTag("EnemyAttack") && player.attackValid)
        {
            BossAttack incomingAttack = other.GetComponentInParent<BossAttack>();
            if (parryController != null && parryController.TryParry(incomingAttack))
            {
                // A weapon clash consumes this player attack, so it cannot also damage the Boss.
                player.attackValid = false;
                return;
            }
        }

        if (other.CompareTag("Enemy") && !player.isCounter && player.attackValid)
        {
            EnemyBase enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy == null)
            {
                Debug.LogWarning("Enemy-tagged object is missing an EnemyBase component.", other);
                return;
            }

            player.attackValid = false;
            enemy.TakeHit(attackDamage);
        }
    }
}
