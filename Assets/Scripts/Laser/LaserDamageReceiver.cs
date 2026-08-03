using UnityEngine;

/// <summary>
/// A small, standalone damage target used to verify lasers without a Boss.
/// It can later be replaced by the project's final player-health component.
/// </summary>
public sealed class LaserDamageReceiver : MonoBehaviour, ILaserDamageReceiver
{
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField] private bool invulnerable;
    [SerializeField] private bool destroyOnDeath;

    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public bool ReceiveLaserDamage(int damage, Vector2 knockbackDirection)
    {
        if (invulnerable || currentHealth <= 0 || damage <= 0)
            return false;

        Player_Control player = GetComponentInParent<Player_Control>();
        if (player != null && player.cantHit)
            return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"{name} took {damage} laser damage. HP: {currentHealth}/{maxHealth}", this);

        if (currentHealth == 0 && destroyOnDeath)
            Destroy(gameObject);

        return true;
    }

    public void RestoreFullHealth()
    {
        currentHealth = maxHealth;
    }
}
