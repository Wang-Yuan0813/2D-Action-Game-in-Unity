using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Temporary local service used before the real local-model endpoint is wired.
/// Replace this component with another IBlackCatReasoningService implementation.
/// </summary>
public sealed class BlackCatMockReasoningService : MonoBehaviour, IBlackCatReasoningService
{
    [SerializeField] private BlackCatGuessRoute finalGuessRoute = BlackCatGuessRoute.Boss1;
    [SerializeField, Min(0f)] private float responseDelay = 0.35f;
    [SerializeField] private string questionReply = "无法分辨……但你可以继续询问。";

    public void Ask(string question, Action<string> onSuccess, Action<string> onError)
    {
        StartCoroutine(ReplyAfterDelay(questionReply, onSuccess));
    }

    public void SubmitFinalGuess(
        string guess,
        Action<BlackCatFinalGuessResult> onSuccess,
        Action<string> onError)
    {
        StartCoroutine(FinalGuessAfterDelay(onSuccess));
    }

    private IEnumerator ReplyAfterDelay(string reply, Action<string> callback)
    {
        yield return WaitUnscaled(responseDelay);
        callback?.Invoke(reply);
    }

    private IEnumerator FinalGuessAfterDelay(Action<BlackCatFinalGuessResult> callback)
    {
        yield return WaitUnscaled(responseDelay);

        BlackCatGuessRoute route = finalGuessRoute == BlackCatGuessRoute.Unresolved
            ? BlackCatGuessRoute.Boss1
            : finalGuessRoute;
        string message = route == BlackCatGuessRoute.Boss1
            ? "我明白你的答案了。左侧的道路已经开启。"
            : "我明白你的答案了。另一条道路已经开启。";
        callback?.Invoke(new BlackCatFinalGuessResult(route, message));
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
