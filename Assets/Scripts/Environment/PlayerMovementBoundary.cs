using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PlayerMovementBoundary : MonoBehaviour
{
    private const string BoundaryLayerName = "PlayerBoundary";
    private const string PlayerLayerName = "Player";

    [Header("Boundary")]
    [SerializeField, Min(0.05f)] private float wallThickness = 0.3f;

    [Header("Warning")]
    [SerializeField, Min(0.01f)] private float warningDistance = 3f;
    [SerializeField, Min(0f)] private float fullWarningDistance = 0.35f;
    [SerializeField, Min(0f)] private float warningOutsideOffset = 0.65f;
    [SerializeField, Min(0.05f)] private float warningBandThickness = 0.65f;
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.9f;
    [SerializeField, Min(0f)] private float fadeSpeed = 4f;
    [SerializeField] private Color warningColor = new Color32(225, 28, 38, 255);

    [Header("Rendering")]
    [SerializeField] private string warningSortingLayer = "foreground";
    [SerializeField] private int warningSortingOrder = 50;

    private BoxCollider2D boundaryArea;
    private Player_Control trackedPlayer;
    private BoxCollider2D leftWall;
    private BoxCollider2D rightWall;
    private BoxCollider2D topWall;
    private BoxCollider2D bottomWall;
    private SpriteRenderer leftWarning;
    private SpriteRenderer rightWarning;
    private SpriteRenderer topWarning;
    private SpriteRenderer bottomWarning;
    private float leftAlpha;
    private float rightAlpha;
    private float topAlpha;
    private float bottomAlpha;

    private static Sprite warningSprite;

    public Vector2 Size
    {
        get => BoundaryArea.size;
        set
        {
            BoundaryArea.size = new Vector2(Mathf.Max(0.1f, value.x), Mathf.Max(0.1f, value.y));
            RefreshGeometry();
        }
    }

    private BoxCollider2D BoundaryArea
    {
        get
        {
            if (boundaryArea == null)
                boundaryArea = GetComponent<BoxCollider2D>();
            return boundaryArea;
        }
    }

    private void Awake()
    {
        BoundaryArea.isTrigger = true;
        ConfigureCollisionLayers();
        EnsureRuntimeObjects();
        RefreshGeometry();
        SetAllWarningAlpha(0f);
    }

    private void OnEnable()
    {
        if (Application.isPlaying && boundaryArea != null)
        {
            EnsureRuntimeObjects();
            RefreshGeometry();
        }
    }

    private void Update()
    {
        float targetLeft = 0f;
        float targetRight = 0f;
        float targetTop = 0f;
        float targetBottom = 0f;

        if (trackedPlayer != null)
        {
            Vector2 localPosition = transform.InverseTransformPoint(trackedPlayer.transform.position);
            Vector2 center = BoundaryArea.offset;
            Vector2 halfSize = BoundaryArea.size * 0.5f;

            targetLeft = CalculateTargetAlpha(localPosition.x - (center.x - halfSize.x));
            targetRight = CalculateTargetAlpha((center.x + halfSize.x) - localPosition.x);
            targetBottom = CalculateTargetAlpha(localPosition.y - (center.y - halfSize.y));
            targetTop = CalculateTargetAlpha((center.y + halfSize.y) - localPosition.y);
        }

        float alphaStep = fadeSpeed <= 0f ? 1f : fadeSpeed * Time.deltaTime;
        leftAlpha = Mathf.MoveTowards(leftAlpha, targetLeft, alphaStep);
        rightAlpha = Mathf.MoveTowards(rightAlpha, targetRight, alphaStep);
        topAlpha = Mathf.MoveTowards(topAlpha, targetTop, alphaStep);
        bottomAlpha = Mathf.MoveTowards(bottomAlpha, targetBottom, alphaStep);

        SetWarningAlpha(leftWarning, leftAlpha);
        SetWarningAlpha(rightWarning, rightAlpha);
        SetWarningAlpha(topWarning, topAlpha);
        SetWarningAlpha(bottomWarning, bottomAlpha);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTrackPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (trackedPlayer == null)
            TryTrackPlayer(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player_Control exitingPlayer = other.GetComponentInParent<Player_Control>();
        if (exitingPlayer != null && exitingPlayer == trackedPlayer)
            trackedPlayer = null;
    }

    private void TryTrackPlayer(Collider2D other)
    {
        Player_Control player = other.GetComponentInParent<Player_Control>();
        if (player != null)
            trackedPlayer = player;
    }

    private float CalculateTargetAlpha(float distanceToEdge)
    {
        float fullDistance = Mathf.Min(fullWarningDistance, warningDistance);
        float normalized = 1f - Mathf.InverseLerp(fullDistance, warningDistance, distanceToEdge);
        return Mathf.SmoothStep(0f, maximumAlpha, normalized);
    }

    private void ConfigureCollisionLayers()
    {
        int boundaryLayer = LayerMask.NameToLayer(BoundaryLayerName);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        if (boundaryLayer < 0 || playerLayer < 0)
        {
            Debug.LogError($"{name} requires the '{BoundaryLayerName}' and '{PlayerLayerName}' layers.", this);
            return;
        }

        SetLayerRecursively(gameObject, boundaryLayer);
        for (int layer = 0; layer < 32; layer++)
            Physics2D.IgnoreLayerCollision(boundaryLayer, layer, layer != playerLayer);
    }

    private void EnsureRuntimeObjects()
    {
        leftWall = EnsureWall("LeftWall");
        rightWall = EnsureWall("RightWall");
        topWall = EnsureWall("TopWall");
        bottomWall = EnsureWall("BottomWall");

        leftWarning = EnsureWarning("LeftWarning");
        rightWarning = EnsureWarning("RightWarning");
        topWarning = EnsureWarning("TopWarning");
        bottomWarning = EnsureWarning("BottomWarning");
    }

    private BoxCollider2D EnsureWall(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(transform, false);
        }

        child.gameObject.layer = gameObject.layer;
        BoxCollider2D wall = child.GetComponent<BoxCollider2D>();
        if (wall == null)
            wall = child.gameObject.AddComponent<BoxCollider2D>();
        wall.isTrigger = false;
        return wall;
    }

    private SpriteRenderer EnsureWarning(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(transform, false);
        }

        child.gameObject.layer = gameObject.layer;
        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = child.gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = GetWarningSprite();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.sortingLayerName = warningSortingLayer;
        renderer.sortingOrder = warningSortingOrder;
        return renderer;
    }

    private void RefreshGeometry()
    {
        if (!Application.isPlaying || leftWall == null)
            return;

        Vector2 size = BoundaryArea.size;
        Vector2 center = BoundaryArea.offset;
        Vector2 halfSize = size * 0.5f;
        float halfWall = wallThickness * 0.5f;

        ConfigureWall(leftWall, new Vector2(center.x - halfSize.x - halfWall, center.y), new Vector2(wallThickness, size.y + wallThickness * 2f));
        ConfigureWall(rightWall, new Vector2(center.x + halfSize.x + halfWall, center.y), new Vector2(wallThickness, size.y + wallThickness * 2f));
        ConfigureWall(bottomWall, new Vector2(center.x, center.y - halfSize.y - halfWall), new Vector2(size.x, wallThickness));
        ConfigureWall(topWall, new Vector2(center.x, center.y + halfSize.y + halfWall), new Vector2(size.x, wallThickness));

        float outsideDistance = wallThickness + warningOutsideOffset;
        ConfigureWarning(leftWarning, new Vector2(center.x - halfSize.x - outsideDistance, center.y), size.y, true);
        ConfigureWarning(rightWarning, new Vector2(center.x + halfSize.x + outsideDistance, center.y), size.y, true);
        ConfigureWarning(bottomWarning, new Vector2(center.x, center.y - halfSize.y - outsideDistance), size.x, false);
        ConfigureWarning(topWarning, new Vector2(center.x, center.y + halfSize.y + outsideDistance), size.x, false);
    }

    private static void ConfigureWall(BoxCollider2D wall, Vector2 localPosition, Vector2 size)
    {
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = Vector3.one;
        wall.offset = Vector2.zero;
        wall.size = size;
    }

    private void ConfigureWarning(SpriteRenderer renderer, Vector2 localPosition, float length, bool vertical)
    {
        renderer.transform.localPosition = localPosition;
        renderer.transform.localRotation = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
        renderer.transform.localScale = Vector3.one;
        renderer.size = new Vector2(Mathf.Max(0.1f, length), warningBandThickness);
    }

    private void SetAllWarningAlpha(float alpha)
    {
        leftAlpha = rightAlpha = topAlpha = bottomAlpha = alpha;
        SetWarningAlpha(leftWarning, alpha);
        SetWarningAlpha(rightWarning, alpha);
        SetWarningAlpha(topWarning, alpha);
        SetWarningAlpha(bottomWarning, alpha);
    }

    private void SetWarningAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
            return;

        Color color = warningColor;
        color.a = alpha;
        renderer.color = color;
        renderer.enabled = alpha > 0.001f;
    }

    private static Sprite GetWarningSprite()
    {
        if (warningSprite != null)
            return warningSprite;

        const int textureSize = 16;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeBoundaryWarning",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32 solid = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool firstDiagonal = Mathf.Abs(x - y) <= 1;
                bool secondDiagonal = Mathf.Abs((textureSize - 1 - x) - y) <= 1;
                pixels[y * textureSize + x] = firstDiagonal || secondDiagonal ? solid : clear;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        warningSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize,
            0,
            SpriteMeshType.FullRect);
        warningSprite.name = "RuntimeBoundaryWarningSprite";
        warningSprite.hideFlags = HideFlags.HideAndDontSave;
        return warningSprite;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void OnValidate()
    {
        wallThickness = Mathf.Max(0.05f, wallThickness);
        warningDistance = Mathf.Max(0.01f, warningDistance);
        fullWarningDistance = Mathf.Clamp(fullWarningDistance, 0f, warningDistance);
        warningOutsideOffset = Mathf.Max(0f, warningOutsideOffset);
        warningBandThickness = Mathf.Max(0.05f, warningBandThickness);
        fadeSpeed = Mathf.Max(0f, fadeSpeed);

        BoxCollider2D area = BoundaryArea;
        area.isTrigger = true;
        area.size = new Vector2(Mathf.Max(0.1f, area.size.x), Mathf.Max(0.1f, area.size.y));
        RefreshGeometry();
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D area = BoundaryArea;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.9f);
        Gizmos.DrawWireCube(area.offset, area.size);

        Vector2 warningSize = area.size - Vector2.one * warningDistance * 2f;
        if (warningSize.x > 0f && warningSize.y > 0f)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(area.offset, warningSize);
        }

        Gizmos.matrix = previousMatrix;
    }
}
