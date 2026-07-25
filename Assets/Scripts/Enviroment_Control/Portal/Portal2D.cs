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
    private Rigidbody2D playerBodyInRange;
    private Rigidbody2D playerBlockedUntilExit;
    private float nextInteractionTime;
    private bool teleportInProgress;

    public Transform ArrivalPoint => arrivalPoint;
    public MapParallaxGroup OwningMap => owningMap;

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
        ResolveSystemReferences();
        SetInteractionPrompt(false);
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
        if (!IsPlayerCollider(other))
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
        if (teleportInProgress
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

        Transform destinationPoint = destinationPortal.ArrivalPoint;
        if (destinationPoint == null)
        {
            Debug.LogWarning("目标传送门没有配置 Arrival Point。", destinationPortal);
            return;
        }

        teleportInProgress = true;
        nextInteractionTime = Time.unscaledTime + interactionCooldown;

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
        destinationPortal.PrepareForArrival(playerBody);
        ScheduleParallaxSynchronization();

        if (logTeleport)
        {
            Debug.Log(
                $"玩家从 {name} 传送到 {destinationPortal.name}，落点：{destinationPosition}",
                this);
        }

        teleportInProgress = false;
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
