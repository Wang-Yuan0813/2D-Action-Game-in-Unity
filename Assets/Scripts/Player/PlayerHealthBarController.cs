using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space pixel health bar shown only while a boss map is active.</summary>
public sealed class PlayerHealthBarController : MonoBehaviour
{
    private static readonly Vector2[] ShakePattern =
    {
        new Vector2(-1f, 0f),
        new Vector2(1f, 1f),
        new Vector2(-1f, -1f),
        new Vector2(1f, 0f),
        Vector2.zero
    };

    private static PlayerHealthBarController instance;

    [Header("Layout")]
    [SerializeField] private Vector2 barSize = new Vector2(420f, 30f);
    [SerializeField] private Vector2 bottomLeftOffset = new Vector2(50f, 70f);

    [Header("Pixel Colors")]
    [SerializeField] private Color outerColor = new Color32(18, 13, 18, 255);
    [SerializeField] private Color frameColor = new Color32(91, 79, 86, 255);
    [SerializeField] private Color emptyColor = new Color32(48, 17, 24, 255);
    [SerializeField] private Color healthColor = new Color32(255, 255, 255, 255);
    [SerializeField] private Color highlightColor = new Color32(255, 255, 255, 255);
    [SerializeField] private Color dividerColor = new Color32(40, 15, 21, 160);

    [Header("Hit Shake")]
    [SerializeField, Min(0f)] private float shakeDuration = 0.12f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 2f;

    private RectTransform barRoot;
    private RectTransform fillRect;
    private Text playerNameText;
    private Text healthValueText;
    private Vector2 restPosition;
    private PlayerHealth trackedHealth;
    private ParallaxManager parallaxManager;
    private Coroutine shakeRoutine;

    public static void ShowFor(PlayerHealth health, string displayName)
    {
        if (health == null)
            return;

        EnsureInstance();
        instance.Bind(health, displayName);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject controllerObject = new GameObject(nameof(PlayerHealthBarController));
        instance = controllerObject.AddComponent<PlayerHealthBarController>();
        instance.BuildUI();
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject(
            "PlayerHealthBarCanvas",
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

        barRoot = CreateRect("PixelPlayerHealthBar", canvasObject.transform);
        barRoot.anchorMin = Vector2.zero;
        barRoot.anchorMax = Vector2.zero;
        barRoot.pivot = Vector2.zero;
        barRoot.sizeDelta = barSize;
        restPosition = bottomLeftOffset;
        barRoot.anchoredPosition = restPosition;

        AddPanel("Outer", barRoot, Vector2.zero, Vector2.zero, outerColor);
        RectTransform frame = AddPanel("Frame", barRoot, new Vector2(4f, 4f), new Vector2(-4f, -4f), frameColor);
        RectTransform empty = AddPanel("Empty", frame, new Vector2(4f, 4f), new Vector2(-4f, -4f), emptyColor);

        fillRect = AddPanel("HealthFill", empty, Vector2.zero, Vector2.zero, healthColor);
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);

        RectTransform highlight = AddPanel("Highlight", fillRect, new Vector2(0f, -2f), Vector2.zero, highlightColor);
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

        Font font = FindVonwaonBitmapFont();
        playerNameText = CreateLabel("PlayerName", barRoot, font, TextAnchor.LowerLeft);
        RectTransform nameRect = playerNameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = Vector2.zero;
        nameRect.anchoredPosition = new Vector2(4f, 8f);
        nameRect.sizeDelta = new Vector2(240f, 30f);

        healthValueText = CreateLabel("HealthValue", barRoot, font, TextAnchor.LowerRight);
        RectTransform valueRect = healthValueText.rectTransform;
        valueRect.anchorMin = Vector2.one;
        valueRect.anchorMax = Vector2.one;
        valueRect.pivot = new Vector2(1f, 0f);
        valueRect.anchoredPosition = new Vector2(-4f, 8f);
        valueRect.sizeDelta = new Vector2(180f, 30f);

        barRoot.gameObject.SetActive(false);
    }

    private void Bind(PlayerHealth health, string displayName)
    {
        UnbindHealth();
        trackedHealth = health;
        trackedHealth.HealthChanged += HandleHealthChanged;
        trackedHealth.Damaged += HandleDamaged;

        playerNameText.text = string.IsNullOrWhiteSpace(displayName) ? "PLAYER" : displayName;
        UpdateFill(trackedHealth.CurrentHealth, trackedHealth.MaxHealth);
        BindMapManager();
        RefreshVisibility(parallaxManager != null ? parallaxManager.ActiveMap : null);
    }

    private void BindMapManager()
    {
        if (parallaxManager != null)
            parallaxManager.ActiveMapChanged -= HandleActiveMapChanged;

        parallaxManager = FindObjectOfType<ParallaxManager>();
        if (parallaxManager != null)
            parallaxManager.ActiveMapChanged += HandleActiveMapChanged;
    }

    private void HandleActiveMapChanged(MapParallaxGroup map)
    {
        RefreshVisibility(map);
    }

    private void RefreshVisibility(MapParallaxGroup map)
    {
        bool isBossMap = map != null &&
            (string.Equals(map.MapId, "map-boss1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(map.MapId, "map-boss2", StringComparison.OrdinalIgnoreCase));

        barRoot.anchoredPosition = restPosition;
        barRoot.gameObject.SetActive(isBossMap);
    }

    private void HandleHealthChanged(PlayerHealth health, int currentHealth, int maxHealth)
    {
        if (health == trackedHealth)
            UpdateFill(currentHealth, maxHealth);
    }

    private void HandleDamaged(PlayerHealth health, int damage)
    {
        if (health != trackedHealth || damage <= 0 || !barRoot.gameObject.activeInHierarchy)
            return;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        barRoot.anchoredPosition = restPosition;
        shakeRoutine = StartCoroutine(Shake());
    }

    private void UpdateFill(int currentHealth, int maxHealth)
    {
        float normalized = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
        fillRect.anchorMax = new Vector2(normalized, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        healthValueText.text = $"{currentHealth} / {maxHealth}";
    }

    private IEnumerator Shake()
    {
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

    private static Text CreateLabel(string objectName, Transform parent, Font font, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = 22;
        label.alignment = alignment;
        label.color = new Color32(235, 225, 218, 255);
        label.raycastTarget = false;
        label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
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

        return fallback != null ? fallback : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    private void UnbindHealth()
    {
        if (trackedHealth == null)
            return;

        trackedHealth.HealthChanged -= HandleHealthChanged;
        trackedHealth.Damaged -= HandleDamaged;
        trackedHealth = null;
    }

    private void OnDestroy()
    {
        UnbindHealth();
        if (parallaxManager != null)
            parallaxManager.ActiveMapChanged -= HandleActiveMapChanged;
        if (instance == this)
            instance = null;
    }
}
