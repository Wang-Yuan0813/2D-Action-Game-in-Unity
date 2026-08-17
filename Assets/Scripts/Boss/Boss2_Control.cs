using UnityEngine;

/// <summary>Minimal damage receiver for Boss2. Movement and attacks remain independent.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BossWhiteFlash))]
[RequireComponent(typeof(Cinemachine.CinemachineCollisionImpulseSource))]
public sealed class Boss2_Control : EnemyBase, IEnemyAttackOwner
{
    private static readonly int DeadState = Animator.StringToHash("dead");

    [Header("Damage State")]
    [SerializeField] private bool isInvincible;
    [SerializeField] private Vector2 hitEffectOffset = new Vector2(0f, 0.5f);
    [SerializeField] private bool attackValid;

    private BossWhiteFlash whiteFlash;
    private Cinemachine.CinemachineCollisionImpulseSource impulse;
    private GameObject hitFX;
    private Animator animator;
    private Rigidbody2D body;
    private bool isDead;
    private bool deathAnimationFinished;

    public bool IsInvincible => isInvincible;
    public bool AttackValid => attackValid;
    public bool IsDead => isDead;
    public bool DeathAnimationFinished => deathAnimationFinished;

    protected override void Awake()
    {
        base.Awake();
        whiteFlash = GetComponent<BossWhiteFlash>();
        impulse = GetComponent<Cinemachine.CinemachineCollisionImpulseSource>();
        hitFX = Resources.Load<GameObject>("FXPref/HitFX");
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
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

    protected override void Die()
    {
        if (isDead)
            return;

        isDead = true;
        deathAnimationFinished = false;
        isInvincible = true;
        attackValid = false;

        Boss2CombatController combatController = GetComponent<Boss2CombatController>();
        if (combatController != null)
            combatController.enabled = false;

        // OnDisable performs normal combat cleanup, so restore the permanent death state afterwards.
        isInvincible = true;
        attackValid = false;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (animator == null)
        {
            deathAnimationFinished = true;
            return;
        }

        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.speed = 1f;
        animator.Play(DeadState, 0, 0f);
        animator.Update(0f);
    }

    public void Boss2DeadAnimationFinished()
    {
        if (!isDead)
            return;

        deathAnimationFinished = true;
        if (animator != null)
            animator.speed = 0f;
    }
}
