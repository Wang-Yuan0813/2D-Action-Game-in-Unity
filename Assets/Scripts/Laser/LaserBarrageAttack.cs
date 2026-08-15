using System;
using System.Collections;
using UnityEngine;

/// <summary>Reusable three-stage laser barrage. It has no dependency on Boss animation or AI.</summary>
public sealed class LaserBarrageAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LaserPool laserPool;
    [SerializeField] private Transform target;

    [Header("Overall Timing")]
    [SerializeField, Min(4f)] private float defaultTotalDuration = 8f;
    [SerializeField, Min(1f)] private float referenceDuration = 8f;
    [SerializeField, Range(0.1f, 1f)] private float stage1Portion = 0.38f;
    [SerializeField, Range(0f, 1f)] private float firstDelayPortion = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float stage2Portion = 0.28f;
    [SerializeField, Range(0f, 1f)] private float secondDelayPortion = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float stage3Portion = 0.18f;

    [Header("Stage 1 - Rotating Tracking")]
    [SerializeField, Min(1)] private int rotatingShotCount = 6;
    [SerializeField, Range(0f, 180f)] private float rotatingStartAngle = 15f;
    [SerializeField, Min(0f)] private float rotatingAngleStep = 20f;
    [SerializeField] private bool increaseAngle = true;

    [Header("Stage 2 - Horizontal Tracking")]
    [SerializeField, Min(1)] private int horizontalShotCount = 4;

    [Header("Stage 3 - Vertical Formation")]
    [SerializeField, Min(2)] private int verticalLaserCount = 6;
    [SerializeField, Min(0.1f)] private float verticalSpacing = 2.25f;

    private Coroutine barrageRoutine;

    public bool IsRunning => barrageRoutine != null;
    public float DefaultTotalDuration => defaultTotalDuration;

    public event Action BarrageStarted;
    public event Action<int> StageStarted;
    public event Action BarrageFinished;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        defaultTotalDuration = Mathf.Max(4f, defaultTotalDuration);
        referenceDuration = Mathf.Max(1f, referenceDuration);
        rotatingShotCount = Mathf.Max(1, rotatingShotCount);
        horizontalShotCount = Mathf.Max(1, horizontalShotCount);
        verticalLaserCount = Mathf.Max(2, verticalLaserCount);
        verticalSpacing = Mathf.Max(0.1f, verticalSpacing);
    }

    [ContextMenu("Start Laser Barrage")]
    public void StartLaserBarrage()
    {
        if (Application.isPlaying)
            StartLaserBarrage(defaultTotalDuration);
    }

    public bool StartLaserBarrage(float totalDuration)
    {
        if (IsRunning)
            return false;

        ResolveReferences();
        if (laserPool == null || target == null)
        {
            Debug.LogError("LaserBarrageAttack requires both a LaserPool and a target.", this);
            return false;
        }

        float safeDuration = Mathf.Max(4f, totalDuration);
        barrageRoutine = StartCoroutine(RunBarrage(safeDuration));
        return true;
    }

    public void StopLaserBarrage()
    {
        if (barrageRoutine == null)
            return;

        StopCoroutine(barrageRoutine);
        barrageRoutine = null;
    }

    private void OnDisable()
    {
        StopLaserBarrage();
    }

    private IEnumerator RunBarrage(float totalDuration)
    {
        BarrageStarted?.Invoke();

        float totalWeight = stage1Portion + firstDelayPortion + stage2Portion + secondDelayPortion + stage3Portion;
        totalWeight = Mathf.Max(0.01f, totalWeight);
        float unitDuration = totalDuration / totalWeight;
        float timingScale = totalDuration / referenceDuration;

        StageStarted?.Invoke(1);
        yield return FireRotatingStage(stage1Portion * unitDuration, timingScale);
        yield return WaitForDuration(firstDelayPortion * unitDuration);

        StageStarted?.Invoke(2);
        yield return FireHorizontalStage(stage2Portion * unitDuration, timingScale);
        yield return WaitForDuration(secondDelayPortion * unitDuration);

        StageStarted?.Invoke(3);
        FireVerticalFormation(timingScale);
        yield return WaitForDuration(stage3Portion * unitDuration);

        barrageRoutine = null;
        BarrageFinished?.Invoke();
    }

    private IEnumerator FireRotatingStage(float duration, float timingScale)
    {
        float interval = duration / rotatingShotCount;
        float direction = increaseAngle ? 1f : -1f;

        for (int i = 0; i < rotatingShotCount; i++)
        {
            if (target == null)
                yield break;

            float angle = Mathf.Repeat(rotatingStartAngle + direction * rotatingAngleStep * i, 180f);
            laserPool.Spawn(target.position, angle, timingScale);
            yield return WaitForDuration(interval);
        }
    }

    private IEnumerator FireHorizontalStage(float duration, float timingScale)
    {
        float interval = duration / horizontalShotCount;
        for (int i = 0; i < horizontalShotCount; i++)
        {
            if (target == null)
                yield break;

            laserPool.Spawn(target.position, 0f, timingScale);
            yield return WaitForDuration(interval);
        }
    }

    private void FireVerticalFormation(float timingScale)
    {
        if (target == null)
            return;

        int count = verticalLaserCount % 2 == 0 ? verticalLaserCount : verticalLaserCount + 1;
        Vector2 center = target.position;
        for (int i = 0; i < count; i++)
        {
            float offsetX = (i - (count - 1) * 0.5f) * verticalSpacing;
            laserPool.Spawn(center + Vector2.right * offsetX, 90f, timingScale);
        }
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

    private void ResolveReferences()
    {
        if (laserPool == null)
            laserPool = LaserPool.Instance != null ? LaserPool.Instance : FindObjectOfType<LaserPool>();

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }
    }
}
