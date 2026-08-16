using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum BossEncounterType
{
    None,
    Boss1,
    Boss2
}

[DisallowMultipleComponent]
public sealed class GameFlowEndingController : MonoBehaviour
{
    public static GameFlowEndingController Instance { get; private set; }

    [Header("Boss1 Defeat Texts")]
    [SerializeField, TextArea(2, 5)] private string[] boss1DefeatTexts =
    {
        "刀光熄灭，梦境重新将你吞没。",
        "你没能越过那道守护梦境的身影。"
    };

    [Header("Boss2 Defeat Texts")]
    [SerializeField, TextArea(2, 5)] private string[] boss2DefeatTexts =
    {
        "痛苦的记忆再次织成牢笼。",
        "你在真相触手可及之处倒下了。"
    };

    [Header("Boss1 Victory Texts")]
    [SerializeField, TextArea(2, 5)] private string[] boss1VictoryTexts =
    {
        "刀客的身影逐渐消散，但梦境仍未结束。",
        "第一道枷锁已经断裂。你再次从梦境的起点醒来。"
    };

    [Header("Boss2 Victory Texts")]
    [SerializeField, TextArea(2, 5)] private string[] boss2VictoryTexts =
    {
        "痛苦终于不再支配这个梦。",
        "你触及了梦境深处的真相，也为这段旅程画下了句点。"
    };

    [Header("Game Entry Texts")]
    [SerializeField, TextArea(2, 5)] private string[] gameEntryTexts =
    {
        "梦境再次展开，而你仍在寻找醒来的道路。",
        "有些记忆被埋在梦的深处，等待着你再次触碰。"
    };

    [Header("Game Entry")]
    [SerializeField] private bool playEntryOnSceneLoad = true;
    [SerializeField, Min(0f)] private float entryCharacterInterval = 0.055f;
    [SerializeField, Min(0f)] private float entryTextHoldDuration = 1.5f;
    [SerializeField, Min(0f)] private float entryFadeOutDuration = 0.75f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.55f;
    [SerializeField, Min(0f)] private float characterInterval = 0.055f;
    [SerializeField, Min(0f)] private float textHoldDuration = 2f;
    [SerializeField, Min(0f)] private float fallbackDeathAnimationDuration = 0.6f;

    [Header("Appearance")]
    [SerializeField] private Font pixelFont;
    [SerializeField, Min(12)] private int fontSize = 30;
    [SerializeField] private Color textColor = Color.white;

    [Header("Black Screen Audio")]
    [Tooltip("MP3 imported into Unity. Drag the AudioClip here.")]
    [SerializeField] private AudioClip blackScreenAudioClip;
    [SerializeField, Range(0f, 1f)] private float blackScreenAudioVolume = 1f;
    [Tooltip("Keep the clip playing until the black-screen sequence finishes.")]
    [SerializeField] private bool loopBlackScreenAudio;
    [Tooltip("Optional. If left empty, a dedicated 2D AudioSource is created automatically.")]
    [SerializeField] private AudioSource blackScreenAudioSource;

    [Header("Destinations")]
    [SerializeField] private string gameplaySceneName = "MainGameScene";
    [SerializeField, Min(0)] private int mainMenuBuildIndex;

    private GameManager gameManager;
    private PlayerHealth playerHealth;
    private Player_Control playerControl;
    private Rigidbody2D playerBody;
    private Animator playerAnimator;
    private EnemyBase currentBoss;
    private BossEncounterType currentEncounter;
    private Canvas endingCanvas;
    private Image blackOverlay;
    private Text endingText;
    private bool isEnding;
    private bool isOpening;
    private bool openingPlayerStateCaptured;
    private bool openingPreviousCanMove;
    private bool openingPreviousControlEnabled;
    private bool openingPreviousCantHit;
    private RigidbodyConstraints2D openingPreviousConstraints;
    private float openingPreviousTimeScale = 1f;

    public bool IsEnding => isEnding;
    public bool IsOpening => isOpening;
    public BossEncounterType CurrentEncounter => currentEncounter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        gameManager = GetComponent<GameManager>();
        PrepareBlackScreenAudioSource();
        BuildEndingUi();

        if (playEntryOnSceneLoad)
            PrepareOpeningScreen();
    }

    private void Start()
    {
        BindPlayer();
        if (playEntryOnSceneLoad)
            StartCoroutine(RunOpening());
    }

    private void OnDestroy()
    {
        UnbindPlayer();
        UnbindBoss();
        if (isOpening)
            Time.timeScale = openingPreviousTimeScale;
        if (Instance == this)
            Instance = null;
    }

    public void BeginBossEncounter(BossEncounterType encounter, EnemyBase boss)
    {
        if (isEnding || isOpening || encounter == BossEncounterType.None || boss == null)
            return;

        BindPlayer();
        UnbindBoss();
        currentEncounter = encounter;
        currentBoss = boss;
        currentBoss.Died += HandleBossDied;
    }

    private void BindPlayer()
    {
        if (playerHealth != null)
            return;

        playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth == null)
            return;

        playerHealth.Died += HandlePlayerDied;
        playerControl = playerHealth.GetComponent<Player_Control>();
        playerBody = playerHealth.GetComponent<Rigidbody2D>();
        playerAnimator = playerHealth.GetComponent<Animator>();
    }

    private void UnbindPlayer()
    {
        if (playerHealth != null)
            playerHealth.Died -= HandlePlayerDied;
        playerHealth = null;
    }

    private void UnbindBoss()
    {
        if (currentBoss != null)
            currentBoss.Died -= HandleBossDied;
        currentBoss = null;
    }

    private void HandlePlayerDied(PlayerHealth health)
    {
        if (!isEnding && !isOpening)
            StartCoroutine(RunEnding(false));
    }

    private void HandleBossDied(EnemyBase boss)
    {
        if (isEnding || isOpening || boss != currentBoss)
            return;

        StartCoroutine(RunEnding(true));
    }

    private IEnumerator RunEnding(bool victory)
    {
        isEnding = true;
        FreezeWorld();

        endingCanvas.enabled = true;
        endingText.text = string.Empty;
        SetOverlayAlpha(0f);

        if (!victory)
            yield return PlayDeathAnimation();

        PlayBlackScreenAudio();
        yield return FadeToBlack();

        string message = SelectEndingText(victory);
        yield return TypeText(message, characterInterval);
        yield return WaitUnscaled(textHoldDuration);

        Time.timeScale = 1f;
        if (victory && currentEncounter == BossEncounterType.Boss2)
            SceneManager.LoadScene(mainMenuBuildIndex);
        else
            SceneManager.LoadScene(gameplaySceneName);
    }

    private void PrepareOpeningScreen()
    {
        isOpening = true;
        openingPreviousTimeScale = Time.timeScale;
        if (gameManager != null)
        {
            openingPreviousCanMove = gameManager.playerCanMove;
            gameManager.playerCanMove = false;
        }

        Time.timeScale = 0f;
        endingCanvas.enabled = true;
        endingText.text = string.Empty;
        SetOverlayAlpha(1f);
    }

    private IEnumerator RunOpening()
    {
        CaptureAndLockPlayerForOpening();
        PlayBlackScreenAudio();

        string message = SelectRandomText(
            gameEntryTexts,
            "梦境再次展开，而你仍在寻找醒来的道路。");
        yield return TypeText(message, entryCharacterInterval);
        yield return WaitUnscaled(entryTextHoldDuration);

        endingText.text = string.Empty;
        yield return FadeFromBlack();
        StopBlackScreenAudio();

        RestorePlayerAfterOpening();
        endingCanvas.enabled = false;
        isOpening = false;
    }

    private void CaptureAndLockPlayerForOpening()
    {
        if (openingPlayerStateCaptured)
            return;

        openingPlayerStateCaptured = true;
        if (playerControl != null)
        {
            openingPreviousControlEnabled = playerControl.enabled;
            openingPreviousCantHit = playerControl.cantHit;
            playerControl.cantHit = true;
            playerControl.enabled = false;
        }

        if (playerBody != null)
        {
            openingPreviousConstraints = playerBody.constraints;
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private void RestorePlayerAfterOpening()
    {
        if (playerBody != null)
        {
            playerBody.constraints = openingPreviousConstraints;
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        if (playerControl != null)
        {
            playerControl.cantHit = openingPreviousCantHit;
            playerControl.enabled = openingPreviousControlEnabled;
        }

        if (gameManager != null)
            gameManager.playerCanMove = openingPreviousCanMove;
        Time.timeScale = openingPreviousTimeScale;
    }

    private void FreezeWorld()
    {
        if (gameManager != null)
            gameManager.playerCanMove = false;

        if (playerControl != null)
        {
            playerControl.cantHit = true;
            playerControl.enabled = false;
        }

        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        Time.timeScale = 0f;
    }

    private IEnumerator PlayDeathAnimation()
    {
        float duration = fallbackDeathAnimationDuration;
        if (playerAnimator == null)
        {
            yield return WaitUnscaled(duration);
            yield break;
        }

        RuntimeAnimatorController controller = playerAnimator.runtimeAnimatorController;
        if (controller != null)
        {
            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == "Died")
                {
                    duration = clips[i].length;
                    break;
                }
            }
        }

        playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        playerAnimator.speed = 1f;
        playerAnimator.Play("Died", 0, 0f);
        playerAnimator.Update(0f);
        yield return WaitUnscaled(duration);
        playerAnimator.speed = 0f;
    }

    private IEnumerator FadeToBlack()
    {
        if (fadeToBlackDuration <= 0f)
        {
            SetOverlayAlpha(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeToBlackDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(Mathf.Clamp01(elapsed / fadeToBlackDuration));
            yield return null;
        }

        SetOverlayAlpha(1f);
    }

    private IEnumerator FadeFromBlack()
    {
        if (entryFadeOutDuration <= 0f)
        {
            SetOverlayAlpha(0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < entryFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetOverlayAlpha(1f - Mathf.Clamp01(elapsed / entryFadeOutDuration));
            yield return null;
        }

        SetOverlayAlpha(0f);
    }

    private IEnumerator TypeText(string message, float interval)
    {
        endingText.text = string.Empty;
        if (string.IsNullOrEmpty(message))
            yield break;

        if (interval <= 0f)
        {
            endingText.text = message;
            yield break;
        }

        StringBuilder visibleText = new StringBuilder(message.Length);
        for (int i = 0; i < message.Length; i++)
        {
            visibleText.Append(message[i]);
            endingText.text = visibleText.ToString();
            yield return WaitUnscaled(interval);
        }
    }

    private string SelectEndingText(bool victory)
    {
        string[] candidates;
        if (currentEncounter == BossEncounterType.Boss2)
            candidates = victory ? boss2VictoryTexts : boss2DefeatTexts;
        else if (currentEncounter == BossEncounterType.Boss1)
            candidates = victory ? boss1VictoryTexts : boss1DefeatTexts;
        else
            return victory ? "战斗结束了。" : "你倒在了梦境之中。";

        if (candidates == null || candidates.Length == 0)
            return victory ? "战斗结束了。" : "你倒在了梦境之中。";

        int startIndex = Random.Range(0, candidates.Length);
        for (int offset = 0; offset < candidates.Length; offset++)
        {
            string candidate = candidates[(startIndex + offset) % candidates.Length];
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }

        return victory ? "战斗结束了。" : "你倒在了梦境之中。";
    }

    private static string SelectRandomText(string[] candidates, string fallback)
    {
        if (candidates == null || candidates.Length == 0)
            return fallback;

        int startIndex = Random.Range(0, candidates.Length);
        for (int offset = 0; offset < candidates.Length; offset++)
        {
            string candidate = candidates[(startIndex + offset) % candidates.Length];
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }

        return fallback;
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void PrepareBlackScreenAudioSource()
    {
        if (blackScreenAudioSource == null)
            blackScreenAudioSource = gameObject.AddComponent<AudioSource>();

        blackScreenAudioSource.playOnAwake = false;
        blackScreenAudioSource.loop = loopBlackScreenAudio;
        blackScreenAudioSource.spatialBlend = 0f;
    }

    private void PlayBlackScreenAudio()
    {
        if (blackScreenAudioClip == null || blackScreenAudioSource == null)
            return;

        blackScreenAudioSource.Stop();
        blackScreenAudioSource.clip = blackScreenAudioClip;
        blackScreenAudioSource.volume = blackScreenAudioVolume;
        blackScreenAudioSource.loop = loopBlackScreenAudio;
        blackScreenAudioSource.Play();
    }

    private void StopBlackScreenAudio()
    {
        if (blackScreenAudioSource != null)
            blackScreenAudioSource.Stop();
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color color = blackOverlay.color;
        color.a = alpha;
        blackOverlay.color = color;
    }

    private void BuildEndingUi()
    {
        if (pixelFont == null)
            pixelFont = WorldInteractionPrompt.FindPixelFont();

        GameObject canvasObject = new GameObject(
            "GameFlowEndingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        endingCanvas = canvasObject.GetComponent<Canvas>();
        endingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        endingCanvas.overrideSorting = true;
        endingCanvas.sortingOrder = 32760;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlayObject = new GameObject("BlackOverlay", typeof(RectTransform), typeof(Image));
        overlayObject.transform.SetParent(canvasObject.transform, false);
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        Stretch(overlayRect);
        blackOverlay = overlayObject.GetComponent<Image>();
        blackOverlay.color = new Color(0f, 0f, 0f, 0f);
        blackOverlay.raycastTarget = true;

        GameObject textObject = new GameObject("EndingText", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(overlayObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.16f, 0.28f);
        textRect.anchorMax = new Vector2(0.84f, 0.72f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        endingText = textObject.GetComponent<Text>();
        endingText.font = pixelFont;
        endingText.fontSize = fontSize;
        endingText.color = textColor;
        endingText.alignment = TextAnchor.MiddleCenter;
        endingText.horizontalOverflow = HorizontalWrapMode.Wrap;
        endingText.verticalOverflow = VerticalWrapMode.Overflow;
        endingText.raycastTarget = false;

        endingCanvas.enabled = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
