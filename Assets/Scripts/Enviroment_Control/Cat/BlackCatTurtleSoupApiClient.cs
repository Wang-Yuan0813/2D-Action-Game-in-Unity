using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class BlackCatTurtleSoupApiClient : MonoBehaviour
{
    [SerializeField] private string baseUrl = "http://127.0.0.1:8998";
    [SerializeField] private string puzzleId = "cat-dream-v2";
    [SerializeField, Min(1)] private int timeoutSeconds = 60;

    public string PuzzleId => puzzleId;

    [Serializable]
    private sealed class AskRequest
    {
        public string puzzle_id;
        public string question;
    }

    [Serializable]
    private sealed class FinalGuessRequest
    {
        public string puzzle_id;
        public string summary;
    }

    [Serializable]
    public sealed class AskResponse
    {
        public string puzzle_id;
        public string answer;
        public string label;
        public bool valid_model_output;
        public string raw_model_output;
        public string quality;
    }

    [Serializable]
    public sealed class PointVector
    {
        public int K1;
        public int K2;
        public int K3;
        public int K4;
        public int K5;
    }

    [Serializable]
    public sealed class IdentityVector
    {
        public int M1;
        public int M2;
    }

    [Serializable]
    public sealed class FinalGuessResponse
    {
        public string puzzle_id;
        public bool correct;
        public string verdict;
        public string message;
        public bool valid_extraction;
        public float coverage_score;
        public PointVector point_vector;
        public IdentityVector identity_vector;
        public int contradiction;
        public string raw_extraction;
        public string error;
        public string decision_rule;
        public string quality;
    }

    public IEnumerator Ask(
        string question,
        Action<AskResponse> onSuccess,
        Action<string> onError)
    {
        AskRequest payload = new AskRequest
        {
            puzzle_id = puzzleId,
            question = question
        };
        yield return PostJson("/v1/turtle-soup/ask", JsonUtility.ToJson(payload), onSuccess, onError);
    }

    public IEnumerator SubmitFinalGuess(
        string summary,
        Action<FinalGuessResponse> onSuccess,
        Action<string> onError)
    {
        FinalGuessRequest payload = new FinalGuessRequest
        {
            puzzle_id = puzzleId,
            summary = summary
        };
        yield return PostJson("/v1/turtle-soup/final-guess", JsonUtility.ToJson(payload), onSuccess, onError);
    }

    private IEnumerator PostJson<T>(
        string path,
        string json,
        Action<T> onSuccess,
        Action<string> onError)
    {
        string url = baseUrl.TrimEnd('/') + path;
        byte[] body = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string details = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;
                onError?.Invoke(
                    $"推理服务请求失败（HTTP {request.responseCode}）：{request.error}"
                    + (string.IsNullOrEmpty(details) ? string.Empty : $"\n{details}"));
                yield break;
            }

            T response;
            try
            {
                response = JsonUtility.FromJson<T>(request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                onError?.Invoke($"无法解析推理服务响应：{exception.Message}");
                yield break;
            }

            if (response == null)
            {
                onError?.Invoke("推理服务返回了空响应。");
                yield break;
            }

            onSuccess?.Invoke(response);
        }
    }
}
