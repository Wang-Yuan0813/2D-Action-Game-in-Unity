using UnityEngine;

/// <summary>Minimal damage receiver for Boss2. Movement and attacks remain independent.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BossWhiteFlash))]
[RequireComponent(typeof(Cinemachine.CinemachineCollisionImpulseSource))]
public sealed class Boss2_Control : EnemyBase, IEnemyAttackOwner
{
    [Header("Damage State")]
    [SerializeField] private bool isInvincible;
    [SerializeField] private Vector2 hitEffectOffset = new Vector2(0f, 0.5f);
    [SerializeField] private bool attackValid;

    private BossWhiteFlash whiteFlash;
    private Cinemachine.CinemachineCollisionImpulseSource impulse;
    private GameObject hitFX;

    public bool IsInvincible => isInvincible;
    public bool AttackValid => attackValid;

    protected override void Awake()
    {
        base.Awake();
        whiteFlash = GetComponent<BossWhiteFlash>();
        impulse = GetComponent<Cinemachine.CinemachineCollisionImpulseSource>();
        hitFX = Resources.Load<GameObject>("FXPref/HitFX");
    }

    public override void TakeHit(int damage)
    {
        if (isInvincible || currentHealth <= 0 || damage <= 0)
            return;

        PlayHitFeedback();
        base.TakeHit(damage);
    }

    private void PlayHitFeedback()
    {
        whiteFlash?.PlayFlash();

        if (hitFX != null)
        {
            Vector3 effectPosition = transform.position + (Vector3)hitEffectOffset;
            Instantiate(hitFX, effectPosition, Quaternion.identity);
        }

        impulse?.GenerateImpulse();
    }

    public void SetInvincible(bool value)
    {
        isInvincible = value;
    }

    public void OpenAttackWindow()
    {
        attackValid = true;
    }

    public void CloseAttackWindow()
    {
        attackValid = false;
    }

    public bool TryConsumeAttackWindow()
    {
        if (!attackValid)
            return false;

        attackValid = false;
        return true;
    }

    public void OnAttackParried()
    {
        attackValid = false;
    }
}
