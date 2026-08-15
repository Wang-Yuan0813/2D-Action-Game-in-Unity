using UnityEngine;

public sealed class BossSpeechController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossSpeechData speechData;
    [SerializeField] private BossSpeechBubbleView bubble;
    [SerializeField] private Transform player;

    [Header("Automatic Speech")]
    [SerializeField, Min(0f)] private float displayDistance = 8f;
    [SerializeField, Min(0.1f)] private float minimumInterval = 4f;
    [SerializeField, Min(0.1f)] private float maximumInterval = 6f;

    private float nextSpeechTime;
    private bool speechEnabled = true;
    private bool playerWasInRange;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ScheduleNextSpeech();
    }

    private void Update()
    {
        ResolveReferences();
        bool playerInRange = player != null &&
            Vector2.Distance(transform.position, player.position) <= displayDistance;

        if (playerWasInRange && !playerInRange)
            bubble?.HideImmediate();

        playerWasInRange = playerInRange;
        if (!speechEnabled || !playerInRange || Time.time < nextSpeechTime)
            return;

        SpeakRandom();
        ScheduleNextSpeech();
    }

    public void SpeakRandom()
    {
        if (!CanSpeak() || speechData == null || speechData.lines == null || speechData.lines.Count == 0)
            return;

        bubble.ShowText(speechData.lines[Random.Range(0, speechData.lines.Count)]);
    }

    public void Speak(string content)
    {
        if (CanSpeak())
            bubble.ShowText(content);
    }

    public void SetSpeechEnabled(bool value)
    {
        speechEnabled = value;
        bubble?.SetAvailable(value);

        if (value)
            ScheduleNextSpeech();
    }

    public void HideBubble()
    {
        bubble?.HideImmediate();
    }

    private bool CanSpeak()
    {
        return speechEnabled && bubble != null && player != null &&
            Vector2.Distance(transform.position, player.position) <= displayDistance;
    }

    private void ResolveReferences()
    {
        if (bubble == null)
            bubble = GetComponentInChildren<BossSpeechBubbleView>(true);

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
    }

    private void ScheduleNextSpeech()
    {
        float min = Mathf.Max(0.1f, minimumInterval);
        float max = Mathf.Max(min, maximumInterval);
        nextSpeechTime = Time.time + Random.Range(min, max);
    }

    private void OnDisable()
    {
        bubble?.HideImmediate();
    }
}
