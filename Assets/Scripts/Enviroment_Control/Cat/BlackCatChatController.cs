using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BlackCatChatController : MonoBehaviour
{
    [Header("Reasoning Service")]
    [SerializeField] private MonoBehaviour reasoningServiceBehaviour;

    [Header("Appearance")]
    [SerializeField] private Font pixelFont;
    [SerializeField] private Color panelColor = new Color32(25, 20, 28, 248);
    [SerializeField] private Color accentColor = new Color32(201, 43, 55, 255);

    private IBlackCatReasoningService reasoningService;
    private Canvas canvas;
    private GameObject panel;
    private Text transcriptText;
    private InputField playerInput;
    private Button askButton;
    private Button finalGuessButton;
    private Button closeButton;
    private ScrollRect transcriptScroll;
    private GameManager gameManager;
    private Rigidbody2D playerBody;
    private RigidbodyConstraints2D previousConstraints;
    private bool previousCanMove;
    private bool waiting;
    private bool isOpen;
    private bool finalGuessResolved;

    public event Action<BlackCatGuessRoute> FinalGuessResolved;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        ResolveService();
        BuildUi();
        panel.SetActive(false);
    }

    private void Update()
    {
        if (isOpen && !waiting && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public void Open()
    {
        if (isOpen || finalGuessResolved)
            return;

        ResolveService();
        if (reasoningService == null)
        {
            Append("系统：没有配置小猫的推理服务。");
            return;
        }

        isOpen = true;
        panel.SetActive(true);
        LockPlayer();

        if (string.IsNullOrEmpty(transcriptText.text))
            Append("小猫：你似乎有很多问题。问吧。");

        StartCoroutine(FocusInputNextFrame());
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        panel.SetActive(false);
        UnlockPlayer();
    }

    private void SubmitQuestion()
    {
        if (!TryBeginRequest(out string question))
            return;

        Append($"玩家：{question}");
        reasoningService.Ask(
            question,
            answer =>
            {
                if (this == null)
                    return;
                Append($"小猫：{answer}");
                FinishRequest();
            },
            HandleError);
    }

    private void SubmitFinalGuess()
    {
        if (!TryBeginRequest(out string guess))
            return;

        Append($"玩家的最终猜测：{guess}");
        reasoningService.SubmitFinalGuess(
            guess,
            result =>
            {
                if (this == null)
                    return;

                Append($"小猫：{result.Message}");
                waiting = false;

                if (result.Route == BlackCatGuessRoute.Unresolved)
                {
                    Append("系统：结果无法确定，请修改最终猜测后重试。");
                    SetInputInteractable(true);
                    FocusInput();
                    return;
                }

                finalGuessResolved = true;
                SetInputInteractable(false);
                FinalGuessResolved?.Invoke(result.Route);
                StartCoroutine(CloseAfterResult());
            },
            HandleError);
    }

    private bool TryBeginRequest(out string text)
    {
        text = playerInput != null ? playerInput.text.Trim() : string.Empty;
        if (!isOpen || waiting || finalGuessResolved || reasoningService == null || string.IsNullOrEmpty(text))
            return false;

        waiting = true;
        SetInputInteractable(false);
        playerInput.text = string.Empty;
        return true;
    }

    private void FinishRequest()
    {
        waiting = false;
        SetInputInteractable(true);
        FocusInput();
    }

    private void HandleError(string error)
    {
        if (this == null)
            return;

        Append($"系统：{error}");
        FinishRequest();
    }

    private IEnumerator CloseAfterResult()
    {
        float elapsed = 0f;
        while (elapsed < 0.8f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Close();
    }

    private IEnumerator FocusInputNextFrame()
    {
        yield return null;
        FocusInput();
    }

    private void FocusInput()
    {
        if (playerInput == null || !playerInput.interactable)
            return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(playerInput.gameObject);
        playerInput.ActivateInputField();
    }

    private void SetInputInteractable(bool value)
    {
        if (playerInput != null)
            playerInput.interactable = value;
        if (askButton != null)
            askButton.interactable = value;
        if (finalGuessButton != null)
            finalGuessButton.interactable = value;
        if (closeButton != null)
            closeButton.interactable = value;
    }

    private void LockPlayer()
    {
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            previousCanMove = gameManager.playerCanMove;
            gameManager.playerCanMove = false;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        if (playerBody == null)
            return;

        previousConstraints = playerBody.constraints;
        playerBody.velocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void UnlockPlayer()
    {
        if (playerBody != null)
        {
            playerBody.constraints = previousConstraints;
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        if (gameManager != null)
            gameManager.playerCanMove = previousCanMove;

        playerBody = null;
        gameManager = null;
    }

    private void ResolveService()
    {
        reasoningService = reasoningServiceBehaviour as IBlackCatReasoningService;
        if (reasoningService != null)
            return;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IBlackCatReasoningService service)
            {
                reasoningService = service;
                reasoningServiceBehaviour = behaviours[i];
                return;
            }
        }
    }

    private void Append(string line)
    {
        if (transcriptText == null)
            return;

        if (transcriptText.text.Length > 0)
            transcriptText.text += "\n\n";
        transcriptText.text += line;

        Canvas.ForceUpdateCanvases();
        if (transcriptScroll != null)
            transcriptScroll.verticalNormalizedPosition = 0f;
    }

    private void BuildUi()
    {
        if (pixelFont == null)
            pixelFont = WorldInteractionPrompt.FindPixelFont();

        GameObject canvasObject = new GameObject(
            "BlackCatChatCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(null, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        panel = CreateUiObject("ChatPanel", canvasObject.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(920f, 590f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(4f, -4f);

        Text title = CreateText("Title", panel.transform, "黑猫 · 海龟汤", 30, TextAnchor.MiddleLeft);
        SetAnchors(title.rectTransform, new Vector2(0.04f, 0.91f), new Vector2(0.88f, 0.98f));

        closeButton = CreateButton("CloseButton", panel.transform, "关闭", new Vector2(0.88f, 0.91f), new Vector2(0.96f, 0.98f));
        closeButton.onClick.AddListener(Close);

        GameObject puzzleObject = CreateUiObject("PuzzleSurface", panel.transform);
        RectTransform puzzleRect = puzzleObject.GetComponent<RectTransform>();
        SetAnchors(puzzleRect, new Vector2(0.04f, 0.77f), new Vector2(0.96f, 0.89f));
        Image puzzleImage = puzzleObject.AddComponent<Image>();
        puzzleImage.color = new Color32(42, 28, 34, 245);

        string puzzleSurface = reasoningService is IBlackCatPuzzleInfo puzzleInfo
            ? puzzleInfo.PuzzleSurface
            : "汤面尚未配置。";
        Text puzzleText = CreateText(
            "PuzzleText",
            puzzleObject.transform,
            $"汤面：{puzzleSurface}",
            21,
            TextAnchor.MiddleLeft);
        puzzleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        puzzleText.verticalOverflow = VerticalWrapMode.Truncate;
        Stretch(puzzleText.rectTransform);
        puzzleText.rectTransform.offsetMin = new Vector2(16f, 8f);
        puzzleText.rectTransform.offsetMax = new Vector2(-16f, -8f);

        GameObject viewportObject = CreateUiObject("TranscriptViewport", panel.transform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetAnchors(viewportRect, new Vector2(0.04f, 0.31f), new Vector2(0.96f, 0.75f));
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color32(10, 9, 13, 210);
        viewportObject.AddComponent<Mask>().showMaskGraphic = true;

        GameObject contentObject = CreateUiObject("TranscriptContent", viewportObject.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        transcriptText = contentObject.AddComponent<Text>();
        transcriptText.font = pixelFont;
        transcriptText.fontSize = 24;
        transcriptText.color = Color.white;
        transcriptText.alignment = TextAnchor.UpperLeft;
        transcriptText.horizontalOverflow = HorizontalWrapMode.Wrap;
        transcriptText.verticalOverflow = VerticalWrapMode.Overflow;
        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        transcriptScroll = viewportObject.AddComponent<ScrollRect>();
        transcriptScroll.viewport = viewportRect;
        transcriptScroll.content = contentRect;
        transcriptScroll.horizontal = false;
        transcriptScroll.vertical = true;
        transcriptScroll.movementType = ScrollRect.MovementType.Clamped;

        playerInput = CreateInputField(panel.transform);
        askButton = CreateButton("AskButton", panel.transform, "询问", new Vector2(0.58f, 0.05f), new Vector2(0.76f, 0.13f));
        finalGuessButton = CreateButton("FinalGuessButton", panel.transform, "最终猜测", new Vector2(0.78f, 0.05f), new Vector2(0.96f, 0.13f));
        askButton.onClick.AddListener(SubmitQuestion);
        finalGuessButton.onClick.AddListener(SubmitFinalGuess);
    }

    private InputField CreateInputField(Transform parent)
    {
        GameObject inputObject = CreateUiObject("PlayerInput", parent);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        SetAnchors(inputRect, new Vector2(0.04f, 0.15f), new Vector2(0.96f, 0.28f));
        Image inputImage = inputObject.AddComponent<Image>();
        inputImage.color = new Color32(44, 37, 48, 255);

        InputField input = inputObject.AddComponent<InputField>();
        Text inputText = CreateText("Text", inputObject.transform, string.Empty, 23, TextAnchor.UpperLeft);
        RectTransform textRect = inputText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        Text placeholder = CreateText("Placeholder", inputObject.transform, "输入问题或最终猜测……", 22, TextAnchor.MiddleLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.38f);
        RectTransform placeholderRect = placeholder.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(14f, 8f);
        placeholderRect.offsetMax = new Vector2(-14f, -8f);

        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.MultiLineNewline;
        return input;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetAnchors(rect, anchorMin, anchorMax);
        Image image = buttonObject.AddComponent<Image>();
        image.color = accentColor;
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.78f, 0.78f, 1f);
        colors.pressedColor = new Color(0.68f, 0.68f, 0.68f, 1f);
        button.colors = colors;

        Text text = CreateText("Label", buttonObject.transform, label, 22, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return button;
    }

    private Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = pixelFont;
        text.fontSize = size;
        text.text = value;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private void OnDestroy()
    {
        if (isOpen)
            UnlockPlayer();

        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}
