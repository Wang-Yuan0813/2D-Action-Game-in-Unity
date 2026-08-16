using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable world-space E-key prompt. The view is generated at runtime so it
/// can be attached to NPCs, portals and other interactable objects.
/// </summary>
public sealed class WorldInteractionPrompt : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.8f, 0f);
    [SerializeField, Min(0.001f)] private float worldScale = 0.012f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.12f;

    private Canvas promptCanvas;
    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;
    private bool visible;

    private void Awake()
    {
        EnsureView();
        ApplyAlpha(0f);
    }

    private void LateUpdate()
    {
        if (promptCanvas == null)
            return;

        Transform view = promptCanvas.transform;
        // Treat the configured offset and scale as world-space values. NPCs and
        // portal visuals use very different authored scales, so inheriting the
        // parent scale can move the prompt several screen-heights away.
        view.position = transform.position + localOffset;
        view.rotation = Quaternion.identity;

        Vector3 parentScale = transform.lossyScale;
        float safeScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float safeScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        float safeScaleZ = Mathf.Max(0.0001f, Mathf.Abs(parentScale.z));
        float parentSignX = parentScale.x < 0f ? -1f : 1f;
        float parentSignY = parentScale.y < 0f ? -1f : 1f;
        float parentSignZ = parentScale.z < 0f ? -1f : 1f;
        view.localScale = new Vector3(
            worldScale * parentSignX / safeScaleX,
            worldScale * parentSignY / safeScaleY,
            worldScale * parentSignZ / safeScaleZ);
    }

    public void SetVisible(bool shouldShow)
    {
        EnsureView();
        if (visible == shouldShow)
            return;

        visible = shouldShow;
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(shouldShow ? 1f : 0f));
    }

    public void SetLocalOffset(Vector3 offset)
    {
        localOffset = offset;
    }

    private IEnumerator FadeTo(float target)
    {
        float start = canvasGroup.alpha;
        if (fadeDuration <= 0f)
        {
            ApplyAlpha(target);
            fadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration)));
            yield return null;
        }

        ApplyAlpha(target);
        fadeRoutine = null;
    }

    private void ApplyAlpha(float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
        promptCanvas.enabled = alpha > 0.001f;
    }

    private void EnsureView()
    {
        if (promptCanvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "InteractionPromptE",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        promptCanvas = canvasObject.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.WorldSpace;
        promptCanvas.overrideSorting = true;
        promptCanvas.sortingLayerName = "player";
        promptCanvas.sortingOrder = 100;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(54f, 48f);
        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject backgroundObject = CreateUiObject("Background", canvasObject.transform);
        Image background = backgroundObject.AddComponent<Image>();
        background.color = new Color32(24, 19, 25, 235);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(38f, 38f);
        backgroundRect.anchoredPosition = Vector2.zero;

        Outline backgroundOutline = backgroundObject.AddComponent<Outline>();
        backgroundOutline.effectColor = new Color32(202, 46, 55, 255);
        backgroundOutline.effectDistance = new Vector2(2f, -2f);

        GameObject labelObject = CreateUiObject("Label", backgroundObject.transform);
        Text label = labelObject.AddComponent<Text>();
        label.text = "E";
        label.font = FindPixelFont();
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        Stretch(labelObject.GetComponent<RectTransform>());
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static Font FindPixelFont()
    {
        Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name.Contains("VonwaonBitmap"))
                return fonts[i];
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
