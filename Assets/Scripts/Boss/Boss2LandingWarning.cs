using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class Boss2LandingWarning : MonoBehaviour
{
    [Header("Attack Area")]
    [SerializeField] private BoxCollider2D landingAttackCollider;
    [SerializeField, Min(0.1f)] private float fallbackWidth = 5.2f;

    [Header("Frame Shape")]
    [SerializeField, Min(0.01f)] private float frameLineWidth = 0.1f;
    [SerializeField, Min(0.05f)] private float sideMarkerHeight = 0.65f;
    [SerializeField, Min(0.01f)] private float centerMarkerWidth = 0.08f;
    [SerializeField, Min(0.05f)] private float centerMarkerHeight = 2.2f;
    [SerializeField, Min(0.05f)] private float fillHeight = 0.62f;

    [Header("Appearance")]
    [SerializeField] private Color warningColor = new Color(1f, 0.04f, 0.02f, 1f);
    [SerializeField, Range(0f, 1f)] private float frameAlpha = 0.92f;
    [SerializeField, Range(0f, 1f)] private float fillStartAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float fillEndAlpha = 0.84f;
    [SerializeField] private AnimationCurve fillProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float groundVisualOffset = 0.03f;
    [SerializeField] private string warningSortingLayer = "player";
    [SerializeField] private int warningSortingOrder = 30;

    private GameObject warningObject;
    private Mesh frameMesh;
    private Mesh centerMarkerMesh;
    private MeshRenderer frameRenderer;
    private MeshRenderer centerMarkerRenderer;
    private SpriteRenderer fillRenderer;
    private Material frameMaterial;
    private Material centerMaterial;
    private readonly Vector3[] frameVertices = new Vector3[12];
    private readonly int[] frameTriangles = new int[18];
    private readonly Vector3[] centerVertices = new Vector3[4];
    private static readonly int[] QuadTriangles = { 0, 1, 2, 0, 2, 3 };
    private static Sprite fillSprite;
    private float warningWidth;
    private float warningCenterOffsetX;

    private void Awake()
    {
        ResolveAttackCollider();
        CreateWarningRenderer();
    }

    private void ResolveAttackCollider()
    {
        if (landingAttackCollider != null)
            return;

        Transform attackArea = transform.Find("LandingAttackArea");
        if (attackArea != null)
            landingAttackCollider = attackArea.GetComponent<BoxCollider2D>();
    }

    public IEnumerator Show(Vector2 groundPosition, float duration)
    {
        ResolveAttackCollider();
        CreateWarningRenderer();
        UpdateAttackAreaLayout();
        BuildStaticGeometry();

        warningObject.transform.position = new Vector3(
            groundPosition.x + warningCenterOffsetX,
            groundPosition.y + groundVisualOffset,
            0f);
        warningObject.transform.rotation = Quaternion.identity;
        warningObject.transform.localScale = Vector3.one;
        warningObject.SetActive(true);

        UpdateVisual(0f);
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
            float progress = Mathf.Clamp01(fillProgressCurve.Evaluate(normalizedTime));
            UpdateVisual(progress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Keep the completed warning visible for one rendered frame before Boss2 lands.
        UpdateVisual(1f);
        yield return null;
        warningObject.SetActive(false);
    }

    public void Hide()
    {
        if (warningObject != null)
            warningObject.SetActive(false);
    }

    private void UpdateAttackAreaLayout()
    {
        warningWidth = fallbackWidth;
        warningCenterOffsetX = 0f;

        if (landingAttackCollider == null)
            return;

        Vector3 localCenter = transform.InverseTransformPoint(
            landingAttackCollider.transform.TransformPoint(landingAttackCollider.offset));
        warningCenterOffsetX = localCenter.x;

        float rootScaleX = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        float colliderScaleX = Mathf.Abs(landingAttackCollider.transform.lossyScale.x);
        warningWidth = Mathf.Max(0.1f, landingAttackCollider.size.x * colliderScaleX / rootScaleX);
    }

    private void CreateWarningRenderer()
    {
        if (warningObject != null)
            return;

        warningObject = new GameObject("Boss2LandingWarningProgress");
        warningObject.transform.SetParent(transform, false);

        int sortingLayerId = SortingLayer.NameToID(warningSortingLayer);
        int baseSortingOrder = warningSortingOrder;

        GameObject fillObject = new GameObject("CenterFill");
        fillObject.transform.SetParent(warningObject.transform, false);
        fillRenderer = fillObject.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = GetFillSprite();
        fillRenderer.sortingLayerID = sortingLayerId;
        fillRenderer.sortingOrder = baseSortingOrder;
        frameRenderer = CreateMeshPart(
            "UFrame",
            sortingLayerId,
            baseSortingOrder + 1,
            out frameMesh);
        centerMarkerRenderer = CreateMeshPart(
            "CenterMarker",
            sortingLayerId,
            baseSortingOrder + 2,
            out centerMarkerMesh);

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (shader != null)
        {
            frameMaterial = CreateMaterial(shader, "Boss2 Landing Warning Frame");
            centerMaterial = CreateMaterial(shader, "Boss2 Landing Warning Center");
            frameRenderer.sharedMaterial = frameMaterial;
            centerMarkerRenderer.sharedMaterial = centerMaterial;
        }

        frameMesh.name = "Boss2 Landing Warning U Frame Mesh";
        centerMarkerMesh.name = "Boss2 Landing Warning Center Marker Mesh";
        warningObject.SetActive(false);
    }

    private MeshRenderer CreateMeshPart(
        string partName,
        int sortingLayerId,
        int sortingOrder,
        out Mesh mesh)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(warningObject.transform, false);
        MeshFilter filter = part.AddComponent<MeshFilter>();
        MeshRenderer renderer = part.AddComponent<MeshRenderer>();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;

        mesh = new Mesh();
        filter.sharedMesh = mesh;
        return renderer;
    }

    private static Material CreateMaterial(Shader shader, string materialName)
    {
        return new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Sprite GetFillSprite()
    {
        if (fillSprite != null)
            return fillSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Boss2 Landing Warning Fill Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);

        fillSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f,
            0,
            SpriteMeshType.FullRect);
        fillSprite.name = "Boss2 Landing Warning Fill Sprite";
        fillSprite.hideFlags = HideFlags.HideAndDontSave;
        return fillSprite;
    }

    private void BuildStaticGeometry()
    {
        float halfWidth = warningWidth * 0.5f;
        float safeLineWidth = Mathf.Min(frameLineWidth, halfWidth);

        SetQuad(frameVertices, 0, -halfWidth, halfWidth, 0f, safeLineWidth);
        SetQuad(frameVertices, 4, -halfWidth, -halfWidth + safeLineWidth, 0f, sideMarkerHeight);
        SetQuad(frameVertices, 8, halfWidth - safeLineWidth, halfWidth, 0f, sideMarkerHeight);

        for (int quad = 0; quad < 3; quad++)
        {
            int vertex = quad * 4;
            int triangle = quad * 6;
            frameTriangles[triangle] = vertex;
            frameTriangles[triangle + 1] = vertex + 1;
            frameTriangles[triangle + 2] = vertex + 2;
            frameTriangles[triangle + 3] = vertex;
            frameTriangles[triangle + 4] = vertex + 2;
            frameTriangles[triangle + 5] = vertex + 3;
        }

        frameMesh.Clear();
        frameMesh.vertices = frameVertices;
        frameMesh.triangles = frameTriangles;
        frameMesh.RecalculateBounds();

        float halfCenterWidth = centerMarkerWidth * 0.5f;
        SetQuad(centerVertices, 0, -halfCenterWidth, halfCenterWidth, 0f, centerMarkerHeight);
        centerMarkerMesh.Clear();
        centerMarkerMesh.vertices = centerVertices;
        centerMarkerMesh.triangles = QuadTriangles;
        centerMarkerMesh.RecalculateBounds();
    }

    private void UpdateVisual(float progress)
    {
        float halfWidth = warningWidth * 0.5f;
        float maximumHalfFillWidth = Mathf.Max(0f, halfWidth - frameLineWidth);
        float visibleFillHeight = Mathf.Max(0.01f, fillHeight - frameLineWidth);
        float fillWidth = maximumHalfFillWidth * 2f * Mathf.Clamp01(progress);

        Transform fillTransform = fillRenderer.transform;
        fillTransform.localPosition = new Vector3(
            0f,
            frameLineWidth + visibleFillHeight * 0.5f,
            0f);
        // Never create a zero-sized renderer. It can retain empty bounds and remain culled
        // after the warning begins expanding.
        fillTransform.localScale = new Vector3(
            Mathf.Max(0.001f, fillWidth),
            visibleFillHeight,
            1f);

        Color frameColor = warningColor;
        frameColor.a = frameAlpha;
        Color fillColor = warningColor;
        fillColor.a = Mathf.Lerp(fillStartAlpha, fillEndAlpha, progress);

        if (frameMaterial != null)
            frameMaterial.color = frameColor;
        if (centerMaterial != null)
            centerMaterial.color = frameColor;
        fillRenderer.color = fillColor;
        fillRenderer.enabled = progress > 0.001f;
    }

    private static void SetQuad(
        Vector3[] target,
        int startIndex,
        float left,
        float right,
        float bottom,
        float top)
    {
        target[startIndex] = new Vector3(left, bottom, 0f);
        target[startIndex + 1] = new Vector3(left, top, 0f);
        target[startIndex + 2] = new Vector3(right, top, 0f);
        target[startIndex + 3] = new Vector3(right, bottom, 0f);
    }

    private void OnValidate()
    {
        fallbackWidth = Mathf.Max(0.1f, fallbackWidth);
        frameLineWidth = Mathf.Max(0.01f, frameLineWidth);
        sideMarkerHeight = Mathf.Max(frameLineWidth, sideMarkerHeight);
        centerMarkerWidth = Mathf.Max(0.01f, centerMarkerWidth);
        centerMarkerHeight = Mathf.Max(sideMarkerHeight, centerMarkerHeight);
        fillHeight = Mathf.Max(frameLineWidth + 0.01f, fillHeight);
        frameAlpha = Mathf.Clamp01(frameAlpha);
        fillStartAlpha = Mathf.Clamp01(fillStartAlpha);
        fillEndAlpha = Mathf.Clamp(fillEndAlpha, fillStartAlpha, 1f);
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(frameMesh);
        DestroyRuntimeObject(centerMarkerMesh);
        DestroyRuntimeObject(frameMaterial);
        DestroyRuntimeObject(centerMaterial);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target != null)
            Object.Destroy(target);
    }
}
