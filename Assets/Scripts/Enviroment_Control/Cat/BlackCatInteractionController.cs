using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class BlackCatInteractionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Cat_Control catControl;
    [SerializeField] private BlackCatChatController chatController;
    [SerializeField] private PortalConditionController portalConditions;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionRadius = 2.4f;
    [SerializeField, Range(0f, 1f)] private float normalBrightness = 0.62f;
    [SerializeField, Min(0f)] private float colorTransitionDuration = 0.2f;

    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private SpriteRenderer spriteRenderer;
    private WorldInteractionPrompt interactionPrompt;
    private ParticleSystem choosingParticle;
    private CircleCollider2D interactionTrigger;
    private Coroutine colorRoutine;
    private Color brightColor;
    private Color darkColor;
    private bool proximityVisualActive;
    private bool resolved;

    private void Awake()
    {
        if (catControl == null)
            catControl = GetComponent<Cat_Control>();
        if (chatController == null)
            chatController = GetComponent<BlackCatChatController>();
        if (portalConditions == null)
            portalConditions = FindObjectOfType<PortalConditionController>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            brightColor = spriteRenderer.color;
            darkColor = new Color(
                brightColor.r * normalBrightness,
                brightColor.g * normalBrightness,
                brightColor.b * normalBrightness,
                brightColor.a);
            spriteRenderer.color = darkColor;
        }

        interactionTrigger = FindInteractionTrigger();
        interactionTrigger.isTrigger = true;
        interactionTrigger.radius = interactionRadius;

        interactionPrompt = GetComponent<WorldInteractionPrompt>();
        if (interactionPrompt == null)
            interactionPrompt = gameObject.AddComponent<WorldInteractionPrompt>();
        interactionPrompt.SetLocalOffset(new Vector3(0f, 1.35f, 0f));
        interactionPrompt.SetVisible(false);

        choosingParticle = CreateChoosingParticle();
        if (choosingParticle != null)
            choosingParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnEnable()
    {
        if (chatController != null)
            chatController.FinalGuessResolved += HandleFinalGuessResolved;
    }

    private void OnDisable()
    {
        if (chatController != null)
            chatController.FinalGuessResolved -= HandleFinalGuessResolved;

        playerColliders.Clear();
        SetProximityVisual(false);
    }

    private void Update()
    {
        bool canShowInteraction = !resolved
            && playerColliders.Count > 0
            && chatController != null
            && !chatController.IsOpen;
        SetProximityVisual(canShowInteraction);

        if (!canShowInteraction || !Input.GetButtonDown("Interact"))
            return;

        GameManager manager = GameManager.GetInstance();
        if (manager == null || manager.playerCanMove)
        {
            SetProximityVisual(false);
            chatController.Open();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            playerColliders.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            playerColliders.Remove(other);
    }

    private CircleCollider2D FindInteractionTrigger()
    {
        CircleCollider2D[] circles = GetComponents<CircleCollider2D>();
        for (int i = 0; i < circles.Length; i++)
        {
            if (circles[i].isTrigger)
                return circles[i];
        }

        return gameObject.AddComponent<CircleCollider2D>();
    }

    private void SetProximityVisual(bool active)
    {
        interactionPrompt?.SetVisible(active);
        if (proximityVisualActive == active)
            return;

        proximityVisualActive = active;
        if (choosingParticle != null)
        {
            if (active)
                choosingParticle.Play();
            else
                choosingParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (spriteRenderer == null)
            return;

        if (colorRoutine != null)
            StopCoroutine(colorRoutine);
        colorRoutine = StartCoroutine(AnimateColor(active ? brightColor : darkColor));
    }

    private IEnumerator AnimateColor(Color target)
    {
        Color start = spriteRenderer.color;
        if (colorTransitionDuration <= 0f)
        {
            spriteRenderer.color = target;
            colorRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < colorTransitionDuration)
        {
            elapsed += Time.deltaTime;
            spriteRenderer.color = Color.Lerp(
                start,
                target,
                Mathf.Clamp01(elapsed / colorTransitionDuration));
            yield return null;
        }

        spriteRenderer.color = target;
        colorRoutine = null;
    }

    private ParticleSystem CreateChoosingParticle()
    {
        ParticleSystem[] particles = FindObjectsOfType<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null || particles[i].name != "ChoosingParticle")
                continue;

            ParticleSystem clone = Instantiate(particles[i], transform);
            clone.name = "ChoosingParticle";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            return clone;
        }

        GameObject particleObject = new GameObject("ChoosingParticle");
        particleObject.transform.SetParent(transform, false);
        ParticleSystem particle = particleObject.AddComponent<ParticleSystem>();
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particle.main;
        main.loop = true;
        main.startLifetime = 0.7f;
        main.startSpeed = 0.25f;
        main.startSize = 0.1f;
        main.startColor = new Color(215f / 255f, 48f / 255f, 64f / 255f, 220f / 255f);
        main.maxParticles = 24;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 8f;
        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.75f;

        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        renderer.sortingLayerName = "player";
        renderer.sortingOrder = 20;
        return particle;
    }

    private void HandleFinalGuessResolved(BlackCatGuessRoute route)
    {
        if (resolved)
            return;

        resolved = true;
        SetProximityVisual(false);
        catControl?.StartFollowingPlayer();

        if (portalConditions == null)
            portalConditions = FindObjectOfType<PortalConditionController>();

        if (route == BlackCatGuessRoute.Boss1)
            portalConditions?.SatisfyCondition1();
        else if (route == BlackCatGuessRoute.Boss2)
            portalConditions?.SatisfyCondition2();
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.5f, interactionRadius);
        colorTransitionDuration = Mathf.Max(0f, colorTransitionDuration);
        if (interactionTrigger != null)
            interactionTrigger.radius = interactionRadius;
    }
}
