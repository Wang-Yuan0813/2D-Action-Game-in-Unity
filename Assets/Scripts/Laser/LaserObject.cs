using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class LaserObject : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float warningDuration = 0.8f;
    [SerializeField, Min(0.02f)] private float beamDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float ignitionDuration = 0.08f;
    [SerializeField, Min(0f)] private float visualFadeDuration = 0.12f;

    [Header("Dimensions")]
    [SerializeField, Min(1f)] private float minimumLength = 40f;
    [SerializeField, Min(0f)] private float screenPadding = 4f;
    [SerializeField] private bool coverMainCamera = true;
    [SerializeField, Min(0.01f)] private float warningWidth = 0.08f;
    [SerializeField, Min(0.01f)] private float coreWidth = 0.11f;
    [SerializeField, Min(0.01f)] private float beamWidth = 0.3f;
    [SerializeField, Min(0.01f)] private float glowWidth = 0.65f;
    [SerializeField, Min(0.05f)] private float lockRadius = 0.48f;

    [Header("Damage")]
    [SerializeField, Min(0.01f)] private float damageWidth = 0.28f;
    [SerializeField, Min(1)] private int damage = 10;
    [SerializeField, Min(0f)] private float fallbackKnockback = 8f;

    [Header("Colors")]
    [SerializeField] private Color warningColor = new Color(1f, 0.08f, 0.04f, 0.55f);
    [SerializeField] private Color glowColor = new Color(1f, 0.03f, 0.01f, 0.42f);
    [SerializeField] private Color innerBeamColor = new Color(1f, 0.24f, 0.04f, 0.95f);
    [SerializeField] private Color beamColor = new Color(1f, 0.94f, 0.82f, 1f);

    private const int LockSegments = 36;
    private const float BurstDuration = 0.16f;

    private static Material sharedWarningMaterial;
    private static Material sharedBeamMaterial;

    private readonly HashSet<int> hitReceivers = new HashSet<int>();
    private BoxCollider2D damageCollider;
    private LineRenderer warningLine;
    private LineRenderer beamGlow;
    private LineRenderer beamInner;
    private LineRenderer beamCore;
    private LineRenderer lockRing;
    private Action<LaserObject> returnToPool;
    private bool isFiring;
    private int lifecycleId;
    private float flickerOffset;
    private float timingScale = 1f;

    public bool IsFiring => isFiring;

    private void Awake()
    {
        EnsureComponents();
        HideAll();
    }

    private void OnValidate()
    {
        warningDuration = Mathf.Max(0f, warningDuration);
        beamDuration = Mathf.Max(0.02f, beamDuration);
        ignitionDuration = Mathf.Max(0.01f, ignitionDuration);
        visualFadeDuration = Mathf.Max(0f, visualFadeDuration);
        minimumLength = Mathf.Max(1f, minimumLength);
        damageWidth = Mathf.Max(0.01f, damageWidth);
        lockRadius = Mathf.Max(0.05f, lockRadius);
    }

    /// <summary>Called by LaserPool. The angle is normalized to the undirected 0-180 range.</summary>
    public void Play(Vector2 center, float angle, Action<LaserObject> releaseCallback)
    {
        Play(center, angle, 1f, releaseCallback);
    }

    /// <summary>Plays one laser with per-instance timing without modifying prefab settings.</summary>
    public void Play(Vector2 center, float angle, float lifecycleTimingScale, Action<LaserObject> releaseCallback)
    {
        StopAllCoroutines();
        EnsureComponents();

        lifecycleId++;
        returnToPool = releaseCallback;
        hitReceivers.Clear();
        isFiring = false;
        flickerOffset = lifecycleId * 1.618f;
        timingScale = Mathf.Max(0.05f, lifecycleTimingScale);

        transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, Mathf.Repeat(angle, 180f)));

        float length = CalculateLockedLength(center);
        ConfigureGeometry(length);
        ShowWarning();
        StartCoroutine(RunAttack(lifecycleId));
    }

    public void ReleaseNow()
    {
        if (!gameObject.activeSelf)
            return;

        lifecycleId++;
        StopAllCoroutines();
        HideAll();

        Action<LaserObject> callback = returnToPool;
        returnToPool = null;
        callback?.Invoke(this);
    }

    private IEnumerator RunAttack(int expectedLifecycleId)
    {
        yield return AnimateWarning(expectedLifecycleId);

        if (expectedLifecycleId != lifecycleId)
            yield break;

        ShowBeam();
        yield return AnimateBeam(expectedLifecycleId);

        if (expectedLifecycleId != lifecycleId)
            yield break;

        DisableDamage();
        yield return AnimateBeamFade(expectedLifecycleId);

        if (expectedLifecycleId == lifecycleId)
            ReleaseNow();
    }

    private IEnumerator AnimateWarning(int expectedLifecycleId)
    {
        float scaledWarningDuration = warningDuration * timingScale;
        float elapsed = 0f;
        while (elapsed < scaledWarningDuration && expectedLifecycleId == lifecycleId)
        {
            float progress = scaledWarningDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / scaledWarningDuration);
            float frequency = Mathf.Lerp(2.5f, 11f, progress * progress);
            float pulse = Mathf.Sin(elapsed * frequency * Mathf.PI * 2f + flickerOffset) * 0.5f + 0.5f;
            float urgency = Mathf.SmoothStep(0f, 1f, progress);

            float alpha = Mathf.Lerp(0.22f, warningColor.a, Mathf.Lerp(pulse, 1f, urgency * 0.55f));
            Color animatedWarning = warningColor;
            animatedWarning.a = alpha;
            if (progress > 0.9f && pulse > 0.58f)
                animatedWarning = Color.Lerp(animatedWarning, new Color(1f, 0.82f, 0.72f, 1f), 0.6f);

            float width = warningWidth * Mathf.Lerp(0.65f, 1.4f, urgency) * Mathf.Lerp(0.9f, 1.08f, pulse);
            SetLineAppearance(warningLine, width, animatedWarning);

            float animatedRadius = Mathf.Lerp(lockRadius * 1.45f, lockRadius * 0.68f, urgency);
            animatedRadius *= Mathf.Lerp(0.94f, 1.05f, pulse);
            UpdateLockMarker(animatedRadius, animatedWarning);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator AnimateBeam(int expectedLifecycleId)
    {
        float scaledBeamDuration = beamDuration * timingScale;
        float scaledIgnitionDuration = ignitionDuration * timingScale;
        float scaledBurstDuration = BurstDuration * timingScale;
        float elapsed = 0f;
        while (elapsed < scaledBeamDuration && expectedLifecycleId == lifecycleId)
        {
            float ignition = Mathf.Clamp01(elapsed / scaledIgnitionDuration);
            float expansion = Mathf.Lerp(1.45f, 1f, Mathf.SmoothStep(0f, 1f, ignition));
            float flicker = 1f
                + Mathf.Sin((elapsed + flickerOffset) * 73f) * 0.025f
                + Mathf.Sin((elapsed + flickerOffset) * 31f) * 0.018f;

            SetLineAppearance(beamGlow, glowWidth * expansion * flicker, glowColor);
            SetLineAppearance(beamInner, beamWidth * expansion * flicker, innerBeamColor);
            SetLineAppearance(beamCore, coreWidth * expansion, beamColor);

            if (elapsed < scaledBurstDuration)
            {
                float burstProgress = Mathf.Clamp01(elapsed / scaledBurstDuration);
                Color burstColor = beamColor;
                burstColor.a = 1f - burstProgress;
                lockRing.enabled = true;
                UpdateRing(lockRadius * Mathf.Lerp(0.55f, 2.7f, burstProgress), burstColor, coreWidth * 0.65f);
            }
            else
            {
                lockRing.enabled = false;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator AnimateBeamFade(int expectedLifecycleId)
    {
        beamCore.enabled = false;
        lockRing.enabled = false;

        float scaledFadeDuration = visualFadeDuration * timingScale;
        if (scaledFadeDuration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < scaledFadeDuration && expectedLifecycleId == lifecycleId)
        {
            float progress = Mathf.Clamp01(elapsed / scaledFadeDuration);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);

            Color fadedInner = innerBeamColor;
            fadedInner.a *= alpha;
            Color fadedGlow = glowColor;
            fadedGlow.a *= alpha * alpha;
            SetLineAppearance(beamInner, beamWidth * Mathf.Lerp(1f, 0.25f, progress), fadedInner);
            SetLineAppearance(beamGlow, glowWidth * Mathf.Lerp(1f, 1.35f, progress), fadedGlow);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isFiring)
            return;

        MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (!(behaviour is ILaserDamageReceiver receiver))
                continue;

            int receiverId = behaviour.GetInstanceID();
            if (hitReceivers.Contains(receiverId))
                return;

            if (receiver.ReceiveLaserDamage(damage, CalculateKnockbackDirection(other.transform.position)))
                hitReceivers.Add(receiverId);

            return;
        }

        // Temporary compatibility with the current player implementation, which has knockback but no HP.
        Player_Control player = other.GetComponentInParent<Player_Control>();
        if (player == null || player.cantHit)
            return;

        int playerId = player.GetInstanceID();
        if (!hitReceivers.Add(playerId))
            return;

        Vector2 direction = CalculateKnockbackDirection(player.transform.position);
        float attackerX = player.transform.position.x - (Mathf.Approximately(direction.x, 0f) ? 1f : Mathf.Sign(direction.x));
        player.TakeHit(fallbackKnockback, attackerX);
    }

    private Vector2 CalculateKnockbackDirection(Vector2 targetPosition)
    {
        Vector2 beamDirection = transform.right;
        Vector2 normal = new Vector2(-beamDirection.y, beamDirection.x);
        if (Vector2.Dot(targetPosition - (Vector2)transform.position, normal) < 0f)
            normal = -normal;

        return normal.normalized;
    }

    private float CalculateLockedLength(Vector2 center)
    {
        float result = minimumLength;
        if (!coverMainCamera || Camera.main == null || !Camera.main.orthographic)
            return result;

        Camera camera = Camera.main;
        float depth = Mathf.Abs(camera.transform.position.z);
        Vector2[] corners =
        {
            camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth)),
            camera.ViewportToWorldPoint(new Vector3(0f, 1f, depth)),
            camera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)),
            camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth))
        };

        float furthestCorner = 0f;
        foreach (Vector2 corner in corners)
            furthestCorner = Mathf.Max(furthestCorner, Vector2.Distance(center, corner));

        return Mathf.Max(result, furthestCorner * 2f + screenPadding);
    }

    private void ConfigureGeometry(float length)
    {
        Vector3 start = new Vector3(-length * 0.5f, 0f, 0f);
        Vector3 end = new Vector3(length * 0.5f, 0f, 0f);
        ConfigureLine(warningLine, start, end, warningWidth, warningColor);
        ConfigureLine(beamGlow, start, end, glowWidth, glowColor);
        ConfigureLine(beamInner, start, end, beamWidth, innerBeamColor);
        ConfigureLine(beamCore, start, end, coreWidth, beamColor);

        damageCollider.offset = Vector2.zero;
        damageCollider.size = new Vector2(length, damageWidth);
        UpdateLockMarker(lockRadius, warningColor);
    }

    private void ConfigureLine(LineRenderer line, Vector3 start, Vector3 end, float width, Color color)
    {
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        SetLineAppearance(line, width, color);
    }

    private static void SetLineAppearance(LineRenderer line, float width, Color color)
    {
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
    }

    private void UpdateLockMarker(float radius, Color color)
    {
        UpdateRing(radius, color, warningWidth * 0.7f);
    }

    private void UpdateRing(float radius, Color color, float width)
    {
        for (int i = 0; i <= LockSegments; i++)
        {
            float radians = i / (float)LockSegments * Mathf.PI * 2f;
            lockRing.SetPosition(i, new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f));
        }
        SetLineAppearance(lockRing, width, color);
    }

    private void ShowWarning()
    {
        warningLine.enabled = true;
        lockRing.enabled = true;
        beamGlow.enabled = false;
        beamInner.enabled = false;
        beamCore.enabled = false;
        damageCollider.enabled = false;
        isFiring = false;
    }

    private void ShowBeam()
    {
        warningLine.enabled = false;
        beamGlow.enabled = true;
        beamInner.enabled = true;
        beamCore.enabled = true;
        damageCollider.enabled = true;
        isFiring = true;
    }

    private void DisableDamage()
    {
        damageCollider.enabled = false;
        isFiring = false;
    }

    private void HideAll()
    {
        isFiring = false;
        SetEnabled(warningLine, false);
        SetEnabled(beamGlow, false);
        SetEnabled(beamInner, false);
        SetEnabled(beamCore, false);
        SetEnabled(lockRing, false);
        if (damageCollider != null)
            damageCollider.enabled = false;
    }

    private static void SetEnabled(LineRenderer line, bool value)
    {
        if (line != null)
            line.enabled = value;
    }

    private void EnsureComponents()
    {
        if (damageCollider == null)
        {
            damageCollider = GetComponent<BoxCollider2D>();
            damageCollider.isTrigger = true;
        }

        warningLine = EnsureLine(warningLine, "WarningLine", 20, false, 2);
        beamGlow = EnsureLine(beamGlow, "BeamGlow", 21, true, 2);
        beamInner = EnsureLine(beamInner, "BeamInner", 22, true, 2);
        beamCore = EnsureLine(beamCore, "BeamCore", 23, true, 2);
        lockRing = EnsureLine(lockRing, "LockRing", 24, false, LockSegments + 1);
    }

    private LineRenderer EnsureLine(LineRenderer current, string childName, int sortingOrder, bool beamMaterial, int positionCount)
    {
        if (current != null)
            return current;

        Transform child = transform.Find(childName);
        GameObject lineObject;
        if (child == null)
        {
            lineObject = new GameObject(childName);
            lineObject.layer = gameObject.layer;
            lineObject.transform.SetParent(transform, false);
        }
        else
        {
            lineObject = child.gameObject;
        }

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
            line = lineObject.AddComponent<LineRenderer>();

        line.positionCount = positionCount;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = positionCount == 2 ? 0 : 2;
        line.numCornerVertices = positionCount == 2 ? 0 : 2;
        line.sortingLayerName = "player";
        line.sortingOrder = sortingOrder;
        line.sharedMaterial = GetSharedLineMaterial(beamMaterial);
        return line;
    }

    private static Material GetSharedLineMaterial(bool beamMaterial)
    {
        Material current = beamMaterial ? sharedBeamMaterial : sharedWarningMaterial;
        if (current != null)
            return current;

        Shader shader = Shader.Find("Action2DGame/LaserBeam");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
        {
            Debug.LogError("LaserObject could not find a compatible unlit shader.");
            return null;
        }

        Material material = new Material(shader)
        {
            name = beamMaterial ? "Laser Shared Beam Material" : "Laser Shared Warning Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (material.HasProperty("_Intensity"))
            material.SetFloat("_Intensity", beamMaterial ? 3.2f : 1.15f);
        if (material.HasProperty("_FlowStrength"))
            material.SetFloat("_FlowStrength", beamMaterial ? 0.32f : 0.08f);
        if (material.HasProperty("_FlowSpeed"))
            material.SetFloat("_FlowSpeed", beamMaterial ? 13f : 4f);

        if (beamMaterial)
            sharedBeamMaterial = material;
        else
            sharedWarningMaterial = material;
        return material;
    }
}
