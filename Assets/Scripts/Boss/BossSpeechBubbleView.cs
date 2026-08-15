using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Boss version of the existing NPC bubble visual, with safe repeated playback.</summary>
public sealed class BossSpeechBubbleView : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float displayDuration = 3f;
    [SerializeField, Min(0.01f)] private float expandDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

    [Header("References")]
    [SerializeField] private Image background;
    [SerializeField] private Text content;
    [SerializeField] private SpriteRenderer arrow;

    private RectTransform backgroundRect;
    private Coroutine lifecycleRoutine;
    private Vector3 baseLocalScale;
    private bool available = true;

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        baseLocalScale = new Vector3(
            Mathf.Abs(transform.localScale.x),
            Mathf.Abs(transform.localScale.y),
            Mathf.Abs(transform.localScale.z));
        HideImmediate();
    }

    private void LateUpdate()
    {
        // Boss1 turns by negating its root scale. Counter-flip the bubble so text stays readable.
        float parentSign = transform.parent != null && transform.parent.lossyScale.x < 0f ? -1f : 1f;
        transform.localScale = new Vector3(baseLocalScale.x * parentSign, baseLocalScale.y, baseLocalScale.z);
    }

    public void ShowText(string text)
    {
        if (!available || string.IsNullOrWhiteSpace(text) || !ResolveReferences())
            return;

        if (lifecycleRoutine != null)
            StopCoroutine(lifecycleRoutine);

        content.text = text;
        SetVisible(true);
        ResetAlpha();
        lifecycleRoutine = StartCoroutine(PlayLifecycle());
    }

    public void SetAvailable(bool value)
    {
        available = value;
        if (!available)
            HideImmediate();
    }

    public void HideImmediate()
    {
        if (lifecycleRoutine != null)
        {
            StopCoroutine(lifecycleRoutine);
            lifecycleRoutine = null;
        }

        if (ResolveReferences())
            SetVisible(false);
    }

    private IEnumerator PlayLifecycle()
    {
        float targetWidth = content.preferredWidth / 10f + 5f;
        float targetHeight = content.preferredHeight / 10f + 3f;
        backgroundRect.sizeDelta = new Vector2(targetWidth, 0f);

        float elapsed = 0f;
        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float height = Mathf.Lerp(0f, targetHeight, Mathf.Clamp01(elapsed / expandDuration));
            backgroundRect.sizeDelta = new Vector2(targetWidth, height);
            yield return null;
        }

        backgroundRect.sizeDelta = new Vector2(targetWidth, targetHeight);
        yield return new WaitForSeconds(displayDuration);

        Color backgroundColor = background.color;
        Color textColor = content.color;
        Color arrowColor = arrow.color;
        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            background.color = WithAlpha(backgroundColor, backgroundColor.a * alpha);
            content.color = WithAlpha(textColor, textColor.a * alpha);
            arrow.color = WithAlpha(arrowColor, arrowColor.a * alpha);
            yield return null;
        }

        background.color = backgroundColor;
        content.color = textColor;
        arrow.color = arrowColor;
        SetVisible(false);
        lifecycleRoutine = null;
    }

    private bool ResolveReferences()
    {
        if (background == null)
            background = transform.Find("Canvas/BG")?.GetComponent<Image>();
        if (content == null)
            content = transform.Find("Canvas/BG/Text")?.GetComponent<Text>();
        if (arrow == null)
            arrow = transform.Find("Arrow")?.GetComponent<SpriteRenderer>();
        if (backgroundRect == null && background != null)
            backgroundRect = background.rectTransform;

        return background != null && content != null && arrow != null && backgroundRect != null;
    }

    private void SetVisible(bool value)
    {
        background.enabled = value;
        content.enabled = value;
        arrow.enabled = value;
        IsVisible = value;
    }

    private void ResetAlpha()
    {
        background.color = WithAlpha(background.color, 0.45490196f);
        content.color = WithAlpha(content.color, 1f);
        arrow.color = WithAlpha(arrow.color, 0.5019608f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void OnDisable()
    {
        HideImmediate();
    }
}
