using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MapBgmController : MonoBehaviour
{
    [Header("Map References")]
    [SerializeField] private ParallaxManager parallaxManager;
    [SerializeField] private MapParallaxGroup interactionMap;
    [SerializeField] private MapParallaxGroup boss1Map;
    [SerializeField] private MapParallaxGroup boss2Map;

    [Header("Map BGM (MP3)")]
    [SerializeField] private AudioClip interactionBgm;
    [SerializeField] private AudioClip boss1Bgm;
    [SerializeField] private AudioClip boss2Bgm;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float interactionVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float boss1Volume = 1f;
    [SerializeField, Range(0f, 1f)] private float boss2Volume = 1f;
    [SerializeField, Min(0f)] private float initialFadeInDuration = 0.75f;
    [SerializeField, Min(0f)] private float mapChangeFadeDuration = 0.5f;

    [Header("Audio Source")]
    [Tooltip("Optional. If left empty, a dedicated 2D AudioSource is created automatically.")]
    [SerializeField] private AudioSource audioSource;

    private Coroutine fadeRoutine;
    private bool portalTransitionRunning;
    private MapParallaxGroup pendingPortalMap;
    private MapParallaxGroup deferredOpeningMap;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f;

        ResolveParallaxManager();
    }

    private void OnEnable()
    {
        ResolveParallaxManager();
        if (parallaxManager != null)
            parallaxManager.ActiveMapChanged += HandleActiveMapChanged;
    }

    private IEnumerator Start()
    {
        while (GameFlowEndingController.Instance != null &&
               GameFlowEndingController.Instance.IsOpening)
        {
            yield return null;
        }

        MapParallaxGroup initialMap = deferredOpeningMap != null
            ? deferredOpeningMap
            : parallaxManager != null ? parallaxManager.ActiveMap : null;
        deferredOpeningMap = null;
        if (initialMap != null)
            PlayMap(initialMap, initialFadeInDuration);
    }

    private void OnDisable()
    {
        if (parallaxManager != null)
            parallaxManager.ActiveMapChanged -= HandleActiveMapChanged;
    }

    public void BeginPortalTransition(MapParallaxGroup destinationMap, float fadeOutDuration)
    {
        portalTransitionRunning = true;
        pendingPortalMap = destinationMap;
        StartVolumeFade(0f, fadeOutDuration);
    }

    public void SwitchPortalDestinationWhileSilent(MapParallaxGroup destinationMap)
    {
        if (destinationMap != null)
            pendingPortalMap = destinationMap;

        StopFadeRoutine();
        audioSource.volume = 0f;
        AssignMapClip(pendingPortalMap);
    }

    public void EndPortalTransition(MapParallaxGroup destinationMap, float fadeInDuration)
    {
        if (destinationMap != null)
            pendingPortalMap = destinationMap;

        AssignMapClip(pendingPortalMap);
        portalTransitionRunning = false;
        float targetVolume = GetMapVolume(pendingPortalMap);
        pendingPortalMap = null;
        StartVolumeFade(targetVolume, fadeInDuration);
    }

    private void HandleActiveMapChanged(MapParallaxGroup map)
    {
        if (GameFlowEndingController.Instance != null &&
            GameFlowEndingController.Instance.IsOpening)
        {
            deferredOpeningMap = map;
            return;
        }

        if (portalTransitionRunning)
        {
            pendingPortalMap = map;
            return;
        }

        PlayMap(map, mapChangeFadeDuration);
    }

    private void PlayMap(MapParallaxGroup map, float fadeDuration)
    {
        if (map == null)
            return;

        StopFadeRoutine();
        fadeRoutine = StartCoroutine(ChangeMapRoutine(map, fadeDuration));
    }

    private IEnumerator ChangeMapRoutine(MapParallaxGroup map, float duration)
    {
        AudioClip nextClip = GetMapClip(map);
        float targetVolume = GetMapVolume(map);

        if (audioSource.clip == nextClip && audioSource.isPlaying)
        {
            yield return FadeVolumeRoutine(targetVolume, duration);
            fadeRoutine = null;
            yield break;
        }

        float halfDuration = duration * 0.5f;
        yield return FadeVolumeRoutine(0f, halfDuration);
        AssignMapClip(map);
        yield return FadeVolumeRoutine(targetVolume, halfDuration);
        fadeRoutine = null;
    }

    private void AssignMapClip(MapParallaxGroup map)
    {
        AudioClip clip = GetMapClip(map);
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        audioSource.Stop();
        audioSource.clip = clip;
        if (clip != null)
            audioSource.Play();
    }

    private void StartVolumeFade(float targetVolume, float duration)
    {
        StopFadeRoutine();
        fadeRoutine = StartCoroutine(FadeVolumeAndClearRoutine(targetVolume, duration));
    }

    private IEnumerator FadeVolumeAndClearRoutine(float targetVolume, float duration)
    {
        yield return FadeVolumeRoutine(targetVolume, duration);
        fadeRoutine = null;
    }

    private IEnumerator FadeVolumeRoutine(float targetVolume, float duration)
    {
        float startVolume = audioSource.volume;
        if (duration <= 0f)
        {
            audioSource.volume = targetVolume;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    private AudioClip GetMapClip(MapParallaxGroup map)
    {
        if (MatchesMap(map, interactionMap, "map-interaction"))
            return interactionBgm;
        if (MatchesMap(map, boss1Map, "map-boss1"))
            return boss1Bgm;
        if (MatchesMap(map, boss2Map, "map-boss2"))
            return boss2Bgm;
        return null;
    }

    private float GetMapVolume(MapParallaxGroup map)
    {
        if (MatchesMap(map, interactionMap, "map-interaction"))
            return interactionVolume;
        if (MatchesMap(map, boss1Map, "map-boss1"))
            return boss1Volume;
        if (MatchesMap(map, boss2Map, "map-boss2"))
            return boss2Volume;
        return 0f;
    }

    private static bool MatchesMap(
        MapParallaxGroup map,
        MapParallaxGroup configuredMap,
        string fallbackId)
    {
        return map != null &&
               (map == configuredMap ||
                string.Equals(map.MapId, fallbackId, StringComparison.OrdinalIgnoreCase));
    }

    private void ResolveParallaxManager()
    {
        if (parallaxManager == null)
            parallaxManager = FindObjectOfType<ParallaxManager>();
    }

    private void StopFadeRoutine()
    {
        if (fadeRoutine == null)
            return;

        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }
}
