using UnityEngine;

public class EnemyBase : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField, Min(1)] protected int maxHealth = 100;
    [SerializeField, Min(0)] protected int attackPower = 10;

    protected int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public int AttackPower => attackPower;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeHit(int damage)
    {
        if (currentHealth <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));

        if (currentHealth == 0)
            Die();
    }

    protected virtual void Die()
    {
        Destroy(gameObject);
    }
}
