using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the screen fade so a one-shot portal can safely destroy itself while
/// the transition continues.
/// </summary>
public sealed class PortalTransitionController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.2f;
    [SerializeField, Min(0f)] private float blackHoldDuration = 1f;
    [SerializeField, Min(0f)] private float fadeFromBlackDuration = 0.25f;

    private static PortalTransitionController instance;
    private Canvas overlayCanvas;
    private CanvasGroup overlayGroup;
    private bool transitionRunning;

    public bool IsTransitionRunning => transitionRunning;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        CreateOverlay();
    }

    public static PortalTransitionController GetOrCreate()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<PortalTransitionController>();
        if (instance != null)
            return instance;

        GameObject controllerObject = new GameObject("PortalTransitionController");
        return controllerObject.AddComponent<PortalTransitionController>();
    }

    public bool BeginTeleport(Portal2D sourcePortal, Rigidbody2D playerBody)
    {
        if (transitionRunning || sourcePortal == null || playerBody == null)
            return false;

        transitionRunning = true;
        StartCoroutine(RunTeleport(sourcePortal, playerBody));
        return true;
    }

    private IEnumerator RunTeleport(Portal2D sourcePortal, Rigidbody2D playerBody)
    {
        GameManager gameManager = FindObjectOfType<GameManager>();
        MapBgmController mapBgm = FindObjectOfType<MapBgmController>();
        MapParallaxGroup sourceMap = sourcePortal != null ? sourcePortal.OwningMap : null;
        MapParallaxGroup destinationMap = sourcePortal != null && sourcePortal.DestinationPortal != null
            ? sourcePortal.DestinationPortal.OwningMap
            : null;
        bool previousCanMove = gameManager == null || gameManager.playerCanMove;
        RigidbodyConstraints2D previousConstraints = playerBody.constraints;

        if (gameManager != null)
            gameManager.playerCanMove = false;

        playerBody.velocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;

        mapBgm?.BeginPortalTransition(destinationMap, fadeToBlackDuration);
        yield return FadeTo(1f, fadeToBlackDuration);
        mapBgm?.SwitchPortalDestinationWhileSilent(destinationMap);

        bool teleported = sourcePortal != null && sourcePortal.PerformTeleport(playerBody);
        if (teleported && sourcePortal != null && sourcePortal.ConsumePairOnUse)
            sourcePortal.ConsumePortalPair();

        if (teleported)
            yield return WaitForUnscaledSeconds(blackHoldDuration);

        MapParallaxGroup finalMap = teleported ? destinationMap : sourceMap;
        mapBgm?.EndPortalTransition(finalMap, fadeFromBlackDuration);
        yield return FadeTo(0f, fadeFromBlackDuration);

        if (playerBody != null)
        {
            playerBody.constraints = previousConstraints;
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        if (gameManager != null)
            gameManager.playerCanMove = previousCanMove;

        if (!teleported && sourcePortal != null)
            sourcePortal.CancelTeleport();

        transitionRunning = false;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        CreateOverlay();
        overlayCanvas.gameObject.SetActive(true);

        float startAlpha = overlayGroup.alpha;
        if (duration <= 0f)
        {
            overlayGroup.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                overlayGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            overlayGroup.alpha = targetAlpha;
        }

        if (targetAlpha <= 0f)
            overlayCanvas.gameObject.SetActive(false);
    }

    private static IEnumerator WaitForUnscaledSeconds(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void CreateOverlay()
    {
        if (overlayCanvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "PortalTransitionOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);

        overlayCanvas = canvasObject.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        overlayGroup = canvasObject.GetComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = false;

        GameObject imageObject = new GameObject(
            "BlackScreen",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        canvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
