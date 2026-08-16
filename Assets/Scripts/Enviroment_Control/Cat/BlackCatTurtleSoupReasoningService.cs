using System;
using UnityEngine;

public sealed class BlackCatTurtleSoupReasoningService : MonoBehaviour,
    IBlackCatReasoningService,
    IBlackCatPuzzleInfo
{
    [SerializeField] private BlackCatTurtleSoupApiClient apiClient;

    [Header("Puzzle")]
    [SerializeField, TextArea(2, 5)] private string puzzleSurface =
        "小猫每天都会出现在这里。一个男人常常用刀杀死这只小猫。小猫死前都会和他说：谢谢你。";

    [Header("Portal Route Mapping")]
    [SerializeField] private BlackCatGuessRoute correctGuessRoute = BlackCatGuessRoute.Boss2;
    [SerializeField] private BlackCatGuessRoute incorrectGuessRoute = BlackCatGuessRoute.Boss1;

    public string PuzzleSurface => puzzleSurface;

    public void Ask(string question, Action<string> onSuccess, Action<string> onError)
    {
        InteractionDialogueLogger logger = InteractionDialogueLogger.Instance;
        string requestId = logger != null
            ? logger.BeginRequest("ASK", question)
            : string.Empty;
        Action<string> errorHandler = error =>
        {
            if (logger != null)
                logger.RecordFailure(requestId, "ASK", error);
            onError?.Invoke(error);
        };

        if (!TryGetClient(errorHandler))
            return;

        StartCoroutine(apiClient.Ask(
            question,
            response =>
            {
                Debug.Log(
                    $"Black cat ask: label={response.label}, "
                    + $"valid={response.valid_model_output}, quality={response.quality}");

                string answer = string.IsNullOrWhiteSpace(response.answer)
                    ? "无法判断"
                    : response.answer.Trim();
                if (logger != null)
                {
                    string diagnostics =
                        $"label: {response.label}\n" +
                        $"valid_model_output: {response.valid_model_output}\n" +
                        $"quality: {response.quality}";
                    logger.RecordSuccess(
                        requestId,
                        "ASK",
                        answer,
                        diagnostics,
                        response.raw_model_output);
                }
                onSuccess?.Invoke(answer);
            },
            errorHandler));
    }

    public void SubmitFinalGuess(
        string guess,
        Action<BlackCatFinalGuessResult> onSuccess,
        Action<string> onError)
    {
        InteractionDialogueLogger logger = InteractionDialogueLogger.Instance;
        string requestId = logger != null
            ? logger.BeginRequest("FINAL_GUESS", guess)
            : string.Empty;
        Action<string> errorHandler = error =>
        {
            if (logger != null)
                logger.RecordFailure(requestId, "FINAL_GUESS", error);
            onError?.Invoke(error);
        };

        if (!TryGetClient(errorHandler))
            return;

        StartCoroutine(apiClient.SubmitFinalGuess(
            guess,
            response =>
            {
                Debug.Log(
                    $"Black cat final guess: correct={response.correct}, "
                    + $"valid={response.valid_extraction}, coverage={response.coverage_score:F2}, "
                    + $"verdict={response.verdict}, rule={response.decision_rule}");

                string message = FirstNonEmpty(response.message, response.error);
                BlackCatGuessRoute route;
                if (!response.valid_extraction)
                {
                    if (string.IsNullOrEmpty(message))
                        message = "没有理解你的最终猜测，请描述得更完整一些。";
                    route = BlackCatGuessRoute.Unresolved;
                }
                else
                {
                    route = response.correct
                        ? correctGuessRoute
                        : incorrectGuessRoute;
                    if (route == BlackCatGuessRoute.Unresolved)
                        message = "最终猜测的传送门路线尚未配置。";
                    else if (string.IsNullOrEmpty(message))
                        message = response.correct ? "你的推理是正确的。" : "这并不是真正的答案。";
                }

                if (logger != null)
                {
                    string diagnostics =
                        $"correct: {response.correct}\n" +
                        $"route: {route}\n" +
                        $"valid_extraction: {response.valid_extraction}\n" +
                        $"coverage_score: {response.coverage_score:F2}\n" +
                        $"verdict: {response.verdict}\n" +
                        $"decision_rule: {response.decision_rule}\n" +
                        $"quality: {response.quality}\n" +
                        $"contradiction: {response.contradiction}";
                    logger.RecordSuccess(
                        requestId,
                        "FINAL_GUESS",
                        message,
                        diagnostics,
                        response.raw_extraction);
                }

                onSuccess?.Invoke(new BlackCatFinalGuessResult(route, message));
            },
            errorHandler));
    }

    private bool TryGetClient(Action<string> onError)
    {
        if (apiClient == null)
            apiClient = GetComponent<BlackCatTurtleSoupApiClient>();
        if (apiClient != null)
            return true;

        onError?.Invoke("没有配置海龟汤推理服务客户端。");
        return false;
    }

    private static string FirstNonEmpty(string first, string second)
    {
        if (!string.IsNullOrWhiteSpace(first))
            return first.Trim();
        return string.IsNullOrWhiteSpace(second) ? string.Empty : second.Trim();
    }
}
