using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Boss2_Control), typeof(Animator), typeof(Rigidbody2D))]
[RequireComponent(typeof(Boss2LandingWarning))]
public sealed class Boss2CombatController : MonoBehaviour
{
    private enum CombatState
    {
        Locomotion,
        Attacking,
        Hidden
    }

    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int WalkState = Animator.StringToHash("walk");
    private static readonly int MeleeState = Animator.StringToHash("melee");
    private static readonly int DisappearState = Animator.StringToHash("disappear");
    private static readonly int AppearState = Animator.StringToHash("appear");

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject meleeAttackArea;
    [SerializeField] private GameObject landingAttackArea;
    [SerializeField] private LaserBarrageAttack laserBarrage;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [SerializeField, Min(0f)] private float stopDistance = 3f;
    [SerializeField] private bool flipXWhenFacingRight;

    [Header("Attack Selection")]
    [SerializeField, Min(0f)] private float attackStartDistance = 8f;
    [SerializeField, Min(0f)] private float meleeRange = 4f;
    [SerializeField, Range(0f, 1f)] private float meleeChanceInRange = 0.65f;
    [SerializeField, Range(0f, 1f)] private float laserChance = 0.4f;
    [SerializeField, Min(0f)] private float initialAttackDelay = 1.5f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.5f;

    [Header("Hidden Attacks")]
    [SerializeField, Min(0f)] private float jumpHiddenDelay = 0.65f;
    [SerializeField, Min(0.1f)] private float landingWarningDuration = 0.8f;
    [SerializeField, Min(4f)] private float laserBarrageDuration = 8f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Min(0.1f)] private float groundRayHeight = 4f;
    [SerializeField, Min(0.1f)] private float groundRayDistance = 12f;
    [SerializeField] private float bossGroundOffset = 0.05f;

    [Header("Animation Safety")]
    [SerializeField, Min(0.1f)] private float meleeTimeout = 2f;
    [SerializeField, Min(0.1f)] private float disappearTimeout = 1.6f;
    [SerializeField, Min(0.1f)] private float appearTimeout = 1.2f;

    private Boss2_Control boss;
    private Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D hurtCollider;
    private Boss2LandingWarning landingWarning;
    private BossSpeechController speechController;
    private GameObject visualLight;
    private Coroutine attackRoutine;
    private CombatState state;
    private float nextAttackTime;
    private int locomotionState;
    private bool meleeFinished;
    private bool disappearFinished;
    private bool appearFinished;
    private readonly Dictionary<Sprite, Sprite> centeredSpriteCache = new Dictionary<Sprite, Sprite>();
    private readonly HashSet<Sprite> generatedCenteredSprites = new HashSet<Sprite>();

    public bool IsAttacking => state != CombatState.Locomotion;

    private void Awake()
    {
        boss = GetComponent<Boss2_Control>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hurtCollider = GetComponent<Collider2D>();
        landingWarning = GetComponent<Boss2LandingWarning>();
        speechController = GetComponent<BossSpeechController>();
        visualLight = transform.Find("Light 2D")?.gameObject;

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (meleeAttackArea == null)
            meleeAttackArea = transform.Find("MeleeAttackArea")?.gameObject;
        if (landingAttackArea == null)
            landingAttackArea = transform.Find("LandingAttackArea")?.gameObject;
        if (laserBarrage == null)
            laserBarrage = GetComponent<LaserBarrageAttack>();

        SetAttackAreasInactive();
        state = CombatState.Locomotion;
        nextAttackTime = Time.time + initialAttackDelay;
        PlayLocomotion(IdleState);
    }

    private void Update()
    {
        if (state != CombatState.Locomotion || player == null || Time.time < nextAttackTime)
            return;

        float distance = Mathf.Abs(player.position.x - transform.position.x);
        if (distance <= attackStartDistance)
            SelectAndStartAttack(distance);
    }

    private void LateUpdate()
    {
        CenterCurrentSpritePivotIfNeeded();
    }

    private void CenterCurrentSpritePivotIfNeeded()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Sprite source = spriteRenderer.sprite;
        if (generatedCenteredSprites.Contains(source))
            return;

        if (Mathf.Abs(source.pivot.x - source.rect.width * 0.5f) < 0.01f)
            return;

        if (!centeredSpriteCache.TryGetValue(source, out Sprite centered))
        {
            float normalizedPivotY = source.rect.height > 0f ? source.pivot.y / source.rect.height : 0.5f;
            centered = Sprite.Create(
                source.texture,
                source.rect,
                new Vector2(0.5f, normalizedPivotY),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                source.border,
                false);
            centered.name = source.name + "_Centered";
            centeredSpriteCache.Add(source, centered);
            generatedCenteredSprites.Add(centered);
        }

        spriteRenderer.sprite = centered;
    }

    private void FixedUpdate()
    {
        if (state != CombatState.Locomotion || player == null)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        float deltaX = player.position.x - transform.position.x;
        FacePlayer(deltaX);

        if (Mathf.Abs(deltaX) > stopDistance)
        {
            rb.velocity = new Vector2(Mathf.Sign(deltaX) * walkSpeed, 0f);
            PlayLocomotion(WalkState);
        }
        else
        {
            rb.velocity = Vector2.zero;
            PlayLocomotion(IdleState);
        }
    }

    private void SelectAndStartAttack(float distance)
    {
        if (attackRoutine != null)
            return;

        if (distance <= meleeRange && UnityEngine.Random.value < meleeChanceInRange)
        {
            attackRoutine = StartCoroutine(RunMeleeAttack());
            return;
        }

        attackRoutine = UnityEngine.Random.value < laserChance && laserBarrage != null
            ? StartCoroutine(RunLaserAttack())
            : StartCoroutine(RunJumpAttack());
    }

    private IEnumerator RunMeleeAttack()
    {
        BeginAttack();
        meleeFinished = false;
        boss.OpenAttackWindow();
        animator.Play(MeleeState, 0, 0f);

        yield return WaitForSignal(() => meleeFinished, meleeTimeout, "melee");

        boss.CloseAttackWindow();
        SetAttackAreasInactive();
        FinishAttack();
    }

    private IEnumerator RunJumpAttack()
    {
        BeginAttack();
        yield return DisappearAndHide();
        yield return WaitForDuration(jumpHiddenDelay);
        yield return WarnAndAppear();
        FinishAttack();
    }

    private IEnumerator RunLaserAttack()
    {
        BeginAttack();
        yield return DisappearAndHide();

        if (laserBarrage != null && laserBarrage.StartLaserBarrage(laserBarrageDuration))
            yield return new WaitUntil(() => !laserBarrage.IsRunning);

        yield return WarnAndAppear();
        FinishAttack();
    }

    private IEnumerator DisappearAndHide()
    {
        speechController?.SetSpeechEnabled(false);
        boss.SetInvincible(true);
        boss.CloseAttackWindow();
        disappearFinished = false;
        animator.Play(DisappearState, 0, 0f);
        yield return WaitForSignal(() => disappearFinished, disappearTimeout, "disappear");

        state = CombatState.Hidden;
        spriteRenderer.enabled = false;
        if (visualLight != null)
            visualLight.SetActive(false);
        if (hurtCollider != null)
            hurtCollider.enabled = false;
    }

    private IEnumerator WarnAndAppear()
    {
        Vector2 landingPoint = FindPlayerGroundPoint();
        yield return landingWarning.Show(landingPoint, landingWarningDuration);

        rb.position = landingPoint;
        FacePlayer(player != null ? player.position.x - transform.position.x : 1f);

        if (hurtCollider != null)
            hurtCollider.enabled = true;
        spriteRenderer.enabled = true;
        if (visualLight != null)
            visualLight.SetActive(true);

        state = CombatState.Attacking;
        appearFinished = false;
        boss.OpenAttackWindow();
        animator.Play(AppearState, 0, 0f);
        yield return WaitForSignal(() => appearFinished, appearTimeout, "appear");

        boss.CloseAttackWindow();
        SetAttackAreasInactive();
        boss.SetInvincible(false);
        speechController?.SetSpeechEnabled(true);
    }

    private Vector2 FindPlayerGroundPoint()
    {
        if (player == null)
            return transform.position;

        Vector2 origin = (Vector2)player.position + Vector2.up * groundRayHeight;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayDistance, groundLayer);
        if (hit.collider != null)
            return new Vector2(player.position.x, hit.point.y + bossGroundOffset);

        Debug.LogWarning("Boss2 could not find ground below the player; using the Boss current height.", this);
        return new Vector2(player.position.x, transform.position.y);
    }

    private void BeginAttack()
    {
        state = CombatState.Attacking;
        locomotionState = 0;
        rb.velocity = Vector2.zero;
        if (player != null)
            FacePlayer(player.position.x - transform.position.x);
    }

    private void FinishAttack()
    {
        boss.CloseAttackWindow();
        boss.SetInvincible(false);
        state = CombatState.Locomotion;
        nextAttackTime = Time.time + attackCooldown;
        attackRoutine = null;
        PlayLocomotion(IdleState);
    }

    private void FacePlayer(float deltaX)
    {
        if (Mathf.Approximately(deltaX, 0f))
            return;

        bool facingRight = deltaX > 0f;
        spriteRenderer.flipX = facingRight ? flipXWhenFacingRight : !flipXWhenFacingRight;

        if (meleeAttackArea != null)
        {
            Vector3 scale = meleeAttackArea.transform.localScale;
            scale.x = facingRight ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            meleeAttackArea.transform.localScale = scale;
        }
    }

    private void PlayLocomotion(int targetState)
    {
        if (locomotionState == targetState)
            return;

        locomotionState = targetState;
        animator.Play(targetState, 0, 0f);
    }

    private IEnumerator WaitForSignal(Func<bool> signal, float timeout, string animationName)
    {
        float elapsed = 0f;
        while (!signal() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!signal())
            Debug.LogWarning($"Boss2 {animationName} animation event timed out; recovering automatically.", this);
    }

    private static IEnumerator WaitForDuration(float duration)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private void SetAttackAreasInactive()
    {
        if (meleeAttackArea != null)
            meleeAttackArea.SetActive(false);
        if (landingAttackArea != null)
            landingAttackArea.SetActive(false);
    }

    public void Boss2MeleeFinished()
    {
        meleeFinished = true;
    }

    public void Boss2DisappearFinished()
    {
        disappearFinished = true;
    }

    public void Boss2AppearFinished()
    {
        appearFinished = true;
    }

    private void OnDisable()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        laserBarrage?.StopLaserBarrage();
        landingWarning?.Hide();
        speechController?.SetSpeechEnabled(false);
        boss?.CloseAttackWindow();
        boss?.SetInvincible(false);
        SetAttackAreasInactive();

        if (spriteRenderer != null)
            spriteRenderer.enabled = true;
        if (visualLight != null)
            visualLight.SetActive(true);
        if (hurtCollider != null)
            hurtCollider.enabled = true;

        state = CombatState.Locomotion;
    }

    private void OnEnable()
    {
        speechController?.SetSpeechEnabled(true);
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in centeredSpriteCache.Values)
        {
            if (sprite != null)
                Destroy(sprite);
        }

        centeredSpriteCache.Clear();
        generatedCenteredSprites.Clear();
    }
}
