using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player_Control))]
public sealed class PlayerHealth : MonoBehaviour, ILaserDamageReceiver
{
    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 100;

    private int currentHealth;
    private Player_Control playerControl;

    public event Action<PlayerHealth, int, int> HealthChanged;
    public event Action<PlayerHealth, int> Damaged;
    public event Action<PlayerHealth> Died;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;

    private void Awake()
    {
        playerControl = GetComponent<Player_Control>();
        currentHealth = maxHealth;
    }

    public bool TakeDamage(int damage)
    {
        int requestedDamage = Mathf.Max(0, damage);
        if (IsDead || requestedDamage == 0)
            return false;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - requestedDamage);
        int actualDamage = previousHealth - currentHealth;

        Damaged?.Invoke(this, actualDamage);
        HealthChanged?.Invoke(this, currentHealth, maxHealth);

        if (currentHealth == 0)
            Died?.Invoke(this);

        return actualDamage > 0;
    }

    public void Restore(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        if (currentHealth != previousHealth)
            HealthChanged?.Invoke(this, currentHealth, maxHealth);
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke(this, currentHealth, maxHealth);
    }

    public bool ReceiveLaserDamage(int damage, Vector2 knockbackDirection)
    {
        if (playerControl == null ||
            playerControl.ResolveIncomingAttack(EnemyAttackType.Beam, false, transform.position.x) != PlayerDefenseResult.Hit)
        {
            return false;
        }

        if (!TakeDamage(damage))
            return false;

        float horizontalDirection = Mathf.Approximately(knockbackDirection.x, 0f)
            ? 1f
            : Mathf.Sign(knockbackDirection.x);
        float attackerX = transform.position.x - horizontalDirection;
        playerControl.TakeHit(playerControl.LaserKnockback, attackerX);
        return true;
    }
}
