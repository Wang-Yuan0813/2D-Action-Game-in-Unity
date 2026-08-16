using System;
using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField, Min(1)] protected int maxHealth = 100;
    [SerializeField, Min(0)] protected int attackPower = 10;

    protected int currentHealth;

    public event Action<EnemyBase, int, int> HealthChanged;
    public event Action<EnemyBase, int> Damaged;
    public event Action<EnemyBase> Died;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int AttackPower => attackPower;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeHit(int damage)
    {
        int appliedDamage = Mathf.Max(0, damage);
        if (currentHealth <= 0 || appliedDamage == 0)
            return;

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
        int actualDamage = previousHealth - currentHealth;

        Damaged?.Invoke(this, actualDamage);
        HealthChanged?.Invoke(this, currentHealth, maxHealth);

        if (currentHealth == 0)
        {
            Died?.Invoke(this);
            Die();
        }
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
