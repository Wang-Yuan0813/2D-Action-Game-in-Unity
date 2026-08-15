using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class Boss2LandingWarning : MonoBehaviour
{
    [SerializeField, Min(12)] private int segments = 48;
    [SerializeField, Min(0.1f)] private float radiusX = 2.5f;
    [SerializeField, Min(0.05f)] private float radiusY = 0.55f;
    [SerializeField, Min(0.01f)] private float lineWidth = 0.1f;
    [SerializeField] private Color warningColor = new Color(1f, 0.08f, 0.02f, 0.85f);

    private GameObject warningObject;
    private LineRenderer warningLine;
    private Material warningMaterial;

    private void Awake()
    {
        CreateWarningRenderer();
    }

    public IEnumerator Show(Vector2 groundPosition, float duration)
    {
        CreateWarningRenderer();
        warningObject.SetActive(true);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            float pulse = Mathf.Sin(elapsed * Mathf.Lerp(8f, 22f, progress) * Mathf.PI) * 0.5f + 0.5f;
            float scale = Mathf.Lerp(1.35f, 0.9f, Mathf.SmoothStep(0f, 1f, progress));

            Color color = warningColor;
            color.a *= Mathf.Lerp(0.45f, 1f, Mathf.Max(progress, pulse));
            warningLine.startColor = color;
            warningLine.endColor = color;
            warningLine.widthMultiplier = lineWidth * Mathf.Lerp(0.75f, 1.35f, progress);
            UpdateEllipse(groundPosition, radiusX * scale, radiusY * scale);

            elapsed += Time.deltaTime;
            yield return null;
        }

        warningObject.SetActive(false);
    }

    public void Hide()
    {
        if (warningObject != null)
            warningObject.SetActive(false);
    }

    private void CreateWarningRenderer()
    {
        if (warningLine != null)
            return;

        warningObject = new GameObject("Boss2LandingWarningRing");
        warningObject.transform.SetParent(transform, false);
        warningLine = warningObject.AddComponent<LineRenderer>();
        warningLine.useWorldSpace = true;
        warningLine.loop = true;
        warningLine.positionCount = segments;
        warningLine.textureMode = LineTextureMode.Stretch;
        warningLine.numCornerVertices = 2;
        warningLine.numCapVertices = 2;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            warningMaterial = new Material(shader) { name = "Boss2 Landing Warning Material" };
            warningLine.material = warningMaterial;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        warningLine.sortingLayerID = spriteRenderer.sortingLayerID;
        warningLine.sortingOrder = spriteRenderer.sortingOrder - 1;
        warningObject.SetActive(false);
    }

    private void UpdateEllipse(Vector2 center, float currentRadiusX, float currentRadiusY)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            warningLine.SetPosition(i, new Vector3(
                center.x + Mathf.Cos(angle) * currentRadiusX,
                center.y + 0.03f + Mathf.Sin(angle) * currentRadiusY,
                0f));
        }
    }

    private void OnDestroy()
    {
        if (warningMaterial != null)
            Destroy(warningMaterial);
    }
}
