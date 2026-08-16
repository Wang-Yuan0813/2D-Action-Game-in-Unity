using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class Portal2D : MonoBehaviour
{
    [Header("传送连接")]
    [Tooltip("玩家从当前传送门进入后到达的另一个传送门。")]
    [SerializeField] private Portal2D destinationPortal;

    [Tooltip("其他传送门传送到当前传送门时使用的角色落点。")]
    [SerializeField] private Transform arrivalPoint;

    [Tooltip("当前传送门所属的视差地图。用于跨地图传送后立即激活正确的视差组。")]
    [SerializeField] private MapParallaxGroup owningMap;

    [Header("交互")]
    [SerializeField] private string requiredTag = "Player";

    [Tooltip("传送门是否在场景开始时就可以使用。条件传送门应关闭此项。")]
    [SerializeField] private bool startsOpen = true;

    [Tooltip("使用后删除当前传送门和目标传送门，使这次传送只能执行一次。")]
    [SerializeField] private bool consumePairOnUse = true;

    [Tooltip("可选的“按交互键进入”提示对象。")]
    [SerializeField] private GameObject interactionPrompt;

    [Min(0f)]
    [Tooltip("传送后短时间内禁止再次使用传送门。")]
    [SerializeField] private float interactionCooldown = 0.25f;

    [Tooltip("传送后清除玩家原有的移动和下落速度。")]
    [SerializeField] private bool resetVelocity = true;

    [Header("系统引用")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private ParallaxManager parallaxManager;

    [Header("调试")]
    [SerializeField] private bool logTeleport;

    private readonly HashSet<Collider2D> playerCollidersInRange =
        new HashSet<Collider2D>();

    private Collider2D interactionTrigger;
    private Renderer[] portalRenderers;
    private WorldInteractionPrompt worldInteractionPrompt;
    private Rigidbody2D playerBodyInRange;
    private Rigidbody2D playerBlockedUntilExit;
    private float nextInteractionTime;
    private bool teleportInProgress;
    private bool isOpen;
    private bool isConsumed;

    public Transform ArrivalPoint => arrivalPoint;
    public MapParallaxGroup OwningMap => owningMap;
    public Portal2D DestinationPortal => destinationPortal;
    public bool IsOpen => isOpen && !isConsumed;
    public bool ConsumePairOnUse => consumePairOnUse;

    private void Reset()
    {
        interactionTrigger = GetComponent<Collider2D>();
        interactionTrigger.isTrigger = true;

        if (transform.parent != null)
        {
            Transform defaultArrivalPoint = transform.parent.Find("ArrivalPoint");
            if (defaultArrivalPoint != null)
            {
                arrivalPoint = defaultArrivalPoint;
            }
        }
    }

    private void Awake()
    {
        interactionTrigger = GetComponent<Collider2D>();
        portalRenderers = GetComponentsInChildren<Renderer>(true);
        worldInteractionPrompt = GetComponent<WorldInteractionPrompt>();
        if (worldInteractionPrompt == null)
            worldInteractionPrompt = gameObject.AddComponent<WorldInteractionPrompt>();
        worldInteractionPrompt.SetLocalOffset(new Vector3(0f, 1.35f, 0f));
        ResolveSystemReferences();
        SetOpenState(startsOpen);
    }

    private void Update()
    {
        bool canInteract = CanCurrentPlayerInteract();
        SetInteractionPrompt(canInteract);

        if (canInteract && Input.GetButtonDown("Interact"))
        {
            TeleportCurrentPlayer();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOpen || !IsPlayerCollider(other))
        {
            return;
        }

        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null)
        {
            return;
        }

        playerCollidersInRange.Add(other);
        playerBodyInRange = playerBody;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerCollidersInRange.Remove(other))
        {
            return;
        }

        Rigidbody2D exitingBody = other.attachedRigidbody;
        if (playerCollidersInRange.Count > 0)
        {
            return;
        }

        if (playerBlockedUntilExit == exitingBody)
        {
            playerBlockedUntilExit = null;
        }

        playerBodyInRange = null;
        SetInteractionPrompt(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        playerCollidersInRange.Clear();
        playerBodyInRange = null;
        playerBlockedUntilExit = null;
        teleportInProgress = false;
        SetInteractionPrompt(false);
    }

    private bool CanCurrentPlayerInteract()
    {
        if (!IsOpen
            || teleportInProgress
            || playerBodyInRange == null
            || playerBodyInRange == playerBlockedUntilExit
            || Time.unscaledTime < nextInteractionTime)
        {
            return false;
        }

        if (destinationPortal == null || destinationPortal.ArrivalPoint == null)
        {
            return false;
        }

        if (gameManager != null && !gameManager.playerCanMove)
        {
            return false;
        }

        return dialogueManager == null || !dialogueManager.DialogueIsPlaying;
    }

    private void TeleportCurrentPlayer()
    {
        Rigidbody2D playerBody = playerBodyInRange;
        if (playerBody == null || destinationPortal == null)
        {
            return;
        }

        PortalTransitionController transition = PortalTransitionController.GetOrCreate();
        if (transition != null && transition.BeginTeleport(this, playerBody))
        {
            teleportInProgress = true;
            nextInteractionTime = Time.unscaledTime + interactionCooldown;
            ClosePortal();
        }
    }

    internal bool PerformTeleport(Rigidbody2D playerBody)
    {
        if (playerBody == null || destinationPortal == null)
            return false;

        Transform destinationPoint = destinationPortal.ArrivalPoint;
        if (destinationPoint == null)
        {
            Debug.LogWarning("目标传送门没有配置 Arrival Point。", destinationPortal);
            return false;
        }

        Vector2 oldPosition = playerBody.position;
        Vector2 destinationPosition = destinationPoint.position;
        Vector3 warpDelta = destinationPosition - oldPosition;

        if (resetVelocity)
        {
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        playerBody.position = destinationPosition;
        Physics2D.SyncTransforms();

        NotifyCameraWarp(playerBody.transform, warpDelta);
        if (!consumePairOnUse)
            destinationPortal.PrepareForArrival(playerBody);
        ScheduleParallaxSynchronization();

        if (logTeleport)
        {
            Debug.Log(
                $"玩家从 {name} 传送到 {destinationPortal.name}，落点：{destinationPosition}",
                this);
        }

        return true;
    }

    internal void CancelTeleport()
    {
        teleportInProgress = false;
        if (!isConsumed)
            OpenPortal();
    }

    internal void ConsumePortalPair()
    {
        Portal2D target = destinationPortal;
        MarkConsumedAndDestroy();

        if (target != null && target != this)
            target.MarkConsumedAndDestroy();
    }

    private void MarkConsumedAndDestroy()
    {
        if (isConsumed)
            return;

        isConsumed = true;
        SetOpenState(false);
        Destroy(GetPortalRoot());
    }

    private GameObject GetPortalRoot()
    {
        if (arrivalPoint != null
            && transform.parent != null
            && arrivalPoint.parent == transform.parent)
        {
            return transform.parent.gameObject;
        }

        return gameObject;
    }

    public void OpenPortal()
    {
        if (!isConsumed)
            SetOpenState(true);
    }

    public void ClosePortal()
    {
        SetOpenState(false);
    }

    private void SetOpenState(bool open)
    {
        isOpen = open && !isConsumed;

        if (interactionTrigger == null)
            interactionTrigger = GetComponent<Collider2D>();
        if (interactionTrigger != null)
            interactionTrigger.enabled = isOpen;

        if (portalRenderers == null || portalRenderers.Length == 0)
            portalRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < portalRenderers.Length; i++)
        {
            if (portalRenderers[i] != null)
                portalRenderers[i].enabled = isOpen;
        }

        if (!isOpen)
        {
            playerCollidersInRange.Clear();
            playerBodyInRange = null;
        }

        SetInteractionPrompt(false);
    }

    private void PrepareForArrival(Rigidbody2D playerBody)
    {
        ResolveSystemReferences();
        nextInteractionTime = Mathf.Max(
            nextInteractionTime,
            Time.unscaledTime + interactionCooldown);

        playerBlockedUntilExit = playerBody;
        StartCoroutine(ResolveArrivalLockAfterPhysics(playerBody));
    }

    private IEnumerator ResolveArrivalLockAfterPhysics(Rigidbody2D playerBody)
    {
        yield return new WaitForFixedUpdate();

        if (playerBlockedUntilExit != playerBody)
        {
            yield break;
        }

        if (!IsPlayerOverlappingTrigger(playerBody))
        {
            playerBlockedUntilExit = null;
        }
    }

    private bool IsPlayerOverlappingTrigger(Rigidbody2D playerBody)
    {
        if (interactionTrigger == null || playerBody == null)
        {
            return false;
        }

        Collider2D[] playerColliders =
            playerBody.GetComponentsInChildren<Collider2D>();

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider.enabled
                && !playerCollider.isTrigger
                && interactionTrigger.bounds.Intersects(playerCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void NotifyCameraWarp(Transform playerTransform, Vector3 warpDelta)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        if (virtualCamera == null)
        {
            Debug.LogWarning("没有找到 CinemachineVirtualCamera，镜头可能会带阻尼移动到新地图。", this);
            return;
        }

        Transform followedTarget = virtualCamera.Follow;
        virtualCamera.OnTargetObjectWarped(
            followedTarget != null ? followedTarget : playerTransform,
            warpDelta);
    }

    private void ScheduleParallaxSynchronization()
    {
        if (parallaxManager == null)
        {
            parallaxManager = FindObjectOfType<ParallaxManager>();
        }

        if (parallaxManager != null)
        {
            parallaxManager.ScheduleTeleportSync(destinationPortal.OwningMap);
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        return other != null
            && (string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag));
    }

    private void ResolveSystemReferences()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (dialogueManager == null)
        {
            dialogueManager = FindObjectOfType<DialogueManager>();
        }

        if (virtualCamera == null)
        {
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        }

        if (parallaxManager == null)
        {
            parallaxManager = FindObjectOfType<ParallaxManager>();
        }
    }

    private void SetInteractionPrompt(bool isVisible)
    {
        worldInteractionPrompt?.SetVisible(isVisible);
        if (interactionPrompt != null
            && interactionPrompt.activeSelf != isVisible)
        {
            interactionPrompt.SetActive(isVisible);
        }
    }

    private void OnValidate()
    {
        if (destinationPortal == this)
        {
            Debug.LogWarning("传送门的 Destination Portal 不能指向自身。", this);
            destinationPortal = null;
        }

        Collider2D portalCollider = GetComponent<Collider2D>();
        if (portalCollider != null && !portalCollider.isTrigger)
        {
            Debug.LogWarning(
                "Portal2D 所在对象的 Collider2D 需要勾选 Is Trigger。",
                this);
        }
    }
}
