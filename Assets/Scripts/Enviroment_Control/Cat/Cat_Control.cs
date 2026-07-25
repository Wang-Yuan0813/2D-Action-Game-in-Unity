using UnityEngine;

public class Cat_Control : MonoBehaviour
{
    [Header("属性设置")]
    public float walkSpeed;
    public float runSpeed;
    public float idleRange;
    public float walkRange;

    [Header("传送设置")]
    [SerializeField] private float teleportDistance = 12f;
    [SerializeField] private float teleportOffset = 2f;
    [SerializeField] private float groundCheckHeight = 4f;
    [SerializeField] private float groundCheckDistance = 8f;
    [SerializeField] private LayerMask groundLayer;

    [Header("公共变量")]
    public bool idle;
    public bool walk;
    public bool run;
    public float distance;
    public bool isFollowingPlayer;

    private GameObject player;
    private Rigidbody2D rb;
    private Collider2D catCollider;
    private int facedirection;
    

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        catCollider = GetComponent<Collider2D>();

        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }
    }

    private void Start()
    {
        FindPlayer();
        SetIdle();
    }

    private void Update()
    {
        if (player == null && !FindPlayer())
        {
            SetIdle();
            return;
        }

        if (!isFollowingPlayer)
        {
            SetIdle();
            return;
        }

        distance = transform.position.x - player.transform.position.x;

        if (Mathf.Abs(distance) > teleportDistance && TeleportNearPlayer())
        {
            distance = transform.position.x - player.transform.position.x;
        }

        Movement();
    }

    /// <summary>
    /// 开始跟随玩家。未调用此函数时，猫会停留在原地。
    /// </summary>
    public void StartFollowingPlayer()
    {
        isFollowingPlayer = true;
    }

    /// <summary>
    /// 停止跟随玩家，并立即进入静止状态。
    /// </summary>
    public void StopFollowingPlayer()
    {
        isFollowingPlayer = false;
        SetIdle();
    }

    private bool FindPlayer()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        return player != null;
    }

    /// <summary>
    /// 面向玩家。
    /// </summary>
    public void FaceDirection()
    {
        facedirection = distance <= 0 ? 1 : -1;
        transform.localScale = new Vector2(facedirection, 1);
    }

    private void Movement()
    {
        FaceDirection();

        if (Mathf.Abs(distance) <= idleRange)
        {
            SetIdle();
        }
        else if (Mathf.Abs(distance) <= walkRange)
        {
            rb.velocity = new Vector2(walkSpeed * facedirection, rb.velocity.y);
            idle = false;
            walk = true;
            run = false;
        }
        else
        {
            rb.velocity = new Vector2(runSpeed * facedirection, rb.velocity.y);
            idle = false;
            walk = false;
            run = true;
        }
    }

    private bool TeleportNearPlayer()
    {
        int playerDirection = GetPlayerFacingDirection();
        float targetX = player.transform.position.x - playerDirection * teleportOffset;
        Vector2 rayOrigin = new Vector2(targetX, player.transform.position.y + groundCheckHeight);
        RaycastHit2D groundHit = Physics2D.Raycast(rayOrigin, Vector2.down, groundCheckDistance, groundLayer);

        // 找不到地面时继续正常追随，避免把猫传送到悬空或墙体内部。
        if (groundHit.collider == null)
        {
            return false;
        }

        float catHalfHeight = catCollider != null ? catCollider.bounds.extents.y : 0f;
        rb.position = new Vector2(targetX, groundHit.point.y + catHalfHeight);
        rb.velocity = Vector2.zero;
        SetIdle();
        return true;
    }

    private int GetPlayerFacingDirection()
    {
        Player_Control playerControl = player.GetComponent<Player_Control>();
        if (playerControl != null && playerControl.facedirection != 0)
        {
            return playerControl.facedirection;
        }

        return player.transform.localScale.x >= 0 ? 1 : -1;
    }

    private void SetIdle()
    {
        if (rb != null)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }

        idle = true;
        walk = false;
        run = false;
    }
}
