using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared screen-space pixel health bar for every EnemyBase boss.
/// The UI is created once on demand when a BossActivationTrigger starts a fight.
/// </summary>
public sealed class BossHealthBarController : MonoBehaviour
{
    private static readonly Vector2[] ShakePattern =
    {
        new Vector2(-1f, 0f),
        new Vector2(1f, 1f),
        new Vector2(-1f, -1f),
        new Vector2(1f, 0f),
        new Vector2(0f, 1f),
        Vector2.zero
    };

    private static BossHealthBarController instance;

    [Header("Layout")]
    [SerializeField] private Vector2 barSize = new Vector2(1000f, 32f);
    [SerializeField] private float topOffset = 150f;

    [Header("Pixel Colors")]
    [SerializeField] private Color outerColor = new Color32(18, 13, 18, 255);
    [SerializeField] private Color frameColor = new Color32(91, 79, 86, 255);
    [SerializeField] private Color emptyColor = new Color32(48, 17, 24, 255);
    [SerializeField] private Color healthColor = new Color32(201, 43, 55, 255);
    [SerializeField] private Color highlightColor = new Color32(255, 109, 105, 255);
    [SerializeField] private Color dividerColor = new Color32(40, 15, 21, 160);

    [Header("Hit Shake")]
    [SerializeField, Min(0f)] private float shakeDuration = 0.14f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 3f;
    [SerializeField, Min(0f)] private float defeatedHoldDuration = 0.75f;

    private RectTransform barRoot;
    private RectTransform fillRect;
    private Text bossNameText;
    private Vector2 restPosition;
    private EnemyBase trackedBoss;
    private Coroutine shakeRoutine;
    private Coroutine hideRoutine;

    public static void ShowFor(EnemyBase boss, string displayName)
    {
        if (boss == null)
            return;

        EnsureInstance();
        instance.Bind(boss, displayName);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject controllerObject = new GameObject(nameof(BossHealthBarController));
        instance = controllerObject.AddComponent<BossHealthBarController>();
        instance.BuildUI();
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject(
            "BossHealthBarCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

        barRoot = CreateRect("PixelBossHealthBar", canvasObject.transform);
        barRoot.anchorMin = new Vector2(0.5f, 1f);
        barRoot.anchorMax = new Vector2(0.5f, 1f);
        barRoot.pivot = new Vector2(0.5f, 1f);
        barRoot.sizeDelta = barSize;
        restPosition = new Vector2(0f, -topOffset);
        barRoot.anchoredPosition = restPosition;

        AddPanel("Outer", barRoot, Vector2.zero, Vector2.zero, outerColor);
        RectTransform frame = AddPanel("Frame", barRoot, new Vector2(4f, 4f), new Vector2(-4f, -4f), frameColor);
        RectTransform empty = AddPanel("Empty", frame, new Vector2(4f, 4f), new Vector2(-4f, -4f), emptyColor);

        fillRect = AddPanel("HealthFill", empty, Vector2.zero, Vector2.zero, healthColor);
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);

        RectTransform highlight = AddPanel("Highlight", fillRect, new Vector2(0f, -2f), new Vector2(0f, 0f), highlightColor);
        highlight.anchorMin = new Vector2(0f, 1f);
        highlight.anchorMax = Vector2.one;

        for (int i = 1; i < 5; i++)
        {
            RectTransform divider = AddPanel($"Divider{i}", empty, Vector2.zero, Vector2.zero, dividerColor);
            float normalizedX = i / 5f;
            divider.anchorMin = new Vector2(normalizedX, 0f);
            divider.anchorMax = new Vector2(normalizedX, 1f);
            divider.sizeDelta = new Vector2(2f, 0f);
            divider.anchoredPosition = Vector2.zero;
        }

        RectTransform nameRect = CreateRect("BossName", barRoot);
        nameRect.anchorMin = Vector2.one;
        nameRect.anchorMax = Vector2.one;
        nameRect.pivot = new Vector2(1f, 0f);
        nameRect.anchoredPosition = new Vector2(-900f, 8f);
        nameRect.sizeDelta = new Vector2(520f, 32f);

        bossNameText = nameRect.gameObject.AddComponent<Text>();
        bossNameText.font = FindVonwaonBitmapFont();
        bossNameText.fontSize = 24;
        bossNameText.alignment = TextAnchor.LowerRight;
        bossNameText.color = new Color32(235, 225, 218, 255);
        bossNameText.raycastTarget = false;
        bossNameText.supportRichText = false;
        bossNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bossNameText.verticalOverflow = VerticalWrapMode.Overflow;

        barRoot.gameObject.SetActive(false);
    }

    private static Font FindVonwaonBitmapFont()
    {
        Font fallback = null;
        foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
        {
            if (font == null)
                continue;

            string normalizedName = font.name.Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
            if (normalizedName == "vonwaonbitmap16px")
                return font;
            if (fallback == null && normalizedName == "vonwaonbitmap12px")
                fallback = font;
        }

        if (fallback != null)
            return fallback;

        Debug.LogWarning("VonwaonBitmap font is not loaded; Boss health bar is using Unity's fallback font.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform AddPanel(
        string objectName,
        RectTransform parent,
        Vector2 offsetMin,
        Vector2 offsetMax,
        Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private void Bind(EnemyBase boss, string displayName)
    {
        Unbind();

        trackedBoss = boss;
        trackedBoss.HealthChanged += HandleHealthChanged;
        trackedBoss.Damaged += HandleDamaged;
        trackedBoss.Died += HandleDied;

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        barRoot.anchoredPosition = restPosition;
        bossNameText.text = string.IsNullOrWhiteSpace(displayName) ? trackedBoss.gameObject.name : displayName;
        UpdateFill(trackedBoss.CurrentHealth, trackedBoss.MaxHealth);
        barRoot.gameObject.SetActive(true);
    }

    private void Unbind()
    {
        if (trackedBoss == null)
            return;

        trackedBoss.HealthChanged -= HandleHealthChanged;
        trackedBoss.Damaged -= HandleDamaged;
        trackedBoss.Died -= HandleDied;
        trackedBoss = null;
    }

    private void HandleHealthChanged(EnemyBase boss, int currentHealth, int maxHealth)
    {
        if (boss == trackedBoss)
            UpdateFill(currentHealth, maxHealth);
    }

    private void HandleDamaged(EnemyBase boss, int damage)
    {
        if (boss != trackedBoss || damage <= 0)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        barRoot.anchoredPosition = restPosition;
        shakeRoutine = StartCoroutine(Shake());
    }

    private void HandleDied(EnemyBase boss)
    {
        if (boss != trackedBoss)
            return;

        UpdateFill(0, boss.MaxHealth);
        Unbind();
        hideRoutine = StartCoroutine(HideAfterDefeat());
    }

    private void UpdateFill(int currentHealth, int maxHealth)
    {
        float normalized = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        fillRect.anchorMax = new Vector2(normalized, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private IEnumerator Shake()
    {
        if (shakeDuration <= 0f || shakeAmplitude <= 0f)
        {
            barRoot.anchoredPosition = restPosition;
            shakeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        int step = 0;
        while (elapsed < shakeDuration)
        {
            Vector2 offset = ShakePattern[step % ShakePattern.Length] * shakeAmplitude;
            barRoot.anchoredPosition = restPosition + new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
            step++;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        barRoot.anchoredPosition = restPosition;
        shakeRoutine = null;
    }

    private IEnumerator HideAfterDefeat()
    {
        float elapsed = 0f;
        while (elapsed < defeatedHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (barRoot != null)
        {
            barRoot.anchoredPosition = restPosition;
            barRoot.gameObject.SetActive(false);
        }

        hideRoutine = null;
    }

    private void OnDestroy()
    {
        Unbind();
        if (instance == this)
            instance = null;
    }
}
