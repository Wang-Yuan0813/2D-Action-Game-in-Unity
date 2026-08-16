using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player_Control : MonoBehaviour
{
    private Rigidbody2D Rb;
    private GameObject energyBar;
    private EnergyBarControl energyBarControl;
    [Header("全局控制的属性")]
    public bool attackAllow;
    public bool dodgeAllow;

    [Header("移动参数")]
    public float Speed;//10
    public float moveAcc;
    [Header("移动检测属性")]
    public int facedirection;//-1：左,1：右
    public float moveDir;
    [Header("跳跃参数")]
    public float JumpForce;//15
    public float JumpHoldForce;//6
    public float JumpHoldDuration;//0.05
    [Header("跳跃攻击参数")]
    [SerializeField] private float jumpAttackLiftSpeed = 6f;
    [SerializeField, Min(0.1f)] private float jumpAttackFailSafeDuration = 0.75f;
    [Header("跳跃检测属性")]
    public bool JumpPressed;
    public bool JumpHeld;//长按跳跃键
    public bool IsJump;
    private float JumpTime;//跳跃时间记录
    [Header("躲避参数")]

    public float DodgeSpeed;
    [Header("躲避检测属性")]
    public bool isDodge;
    public bool dodge;



    [Header("环境检测")]
    private Physics_Check physicsCheck;

    [Header("战斗属性")]
    public bool preAttack;
    public float counterTime;//counter攻击后收到冲击的时间

    private float preAttackExist;
    private float lastAttack = -10f;
    private float jumpAttackEndTime;
    private float meleeProtectionUntil;

    [Header("受伤属性")]
    [SerializeField, Min(0f)] private float laserKnockback = 8f;
    public bool cantHit;
    [Tooltip("Only parries attacks classified as parryable melee. It is not full invulnerability.")]
    public bool meleeParryActive;
    public bool isTakeHit;
    public bool takeHit;
    public bool getCatched;

    [Header("战斗检测")]
    public bool isAttack;//处于攻击状态
    public bool isJumpAttack;
    public bool canAttack;//可以攻击
    public bool attack;//地面上时攻击被按下
    public bool isCounter;
    public bool attackValid;

    [Header("体力属性设置")]
    private float energyLeft;

    [Header("Player Combat Audio (MP3)")]
    [Tooltip("Two sword-swing sounds. Both ground and aerial attacks use this pair.")]
    [SerializeField] private AudioClip swordSwingSound1;
    [SerializeField] private AudioClip swordSwingSound2;
    [SerializeField, Range(0f, 1f)] private float swordSwingVolume = 1f;
    [Tooltip("Two sounds played only after a successful melee parry.")]
    [SerializeField] private AudioClip successfulParrySound1;
    [SerializeField] private AudioClip successfulParrySound2;
    [SerializeField, Range(0f, 1f)] private float successfulParryVolume = 1f;
    [Tooltip("Optional. If left empty, a dedicated 2D AudioSource is created automatically.")]
    [SerializeField] private AudioSource combatAudioSource;

    private int lastSwordSwingSoundIndex = -1;
    private int lastSuccessfulParrySoundIndex = -1;

    private GameObject cameraControl;
    private GameManager gameManager;
    private PlayerHealth playerHealth;
    private bool interactionMovementLocked;

    public PlayerHealth Health => playerHealth;
    public float LaserKnockback => laserKnockback;
    public bool IsInteractionMovementLocked => interactionMovementLocked;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = gameObject.AddComponent<PlayerHealth>();

        if (combatAudioSource == null)
            combatAudioSource = gameObject.AddComponent<AudioSource>();

        combatAudioSource.playOnAwake = false;
        combatAudioSource.loop = false;
        combatAudioSource.spatialBlend = 0f;
    }

    void Start()
    {
        gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();//获取GameManager
        cameraControl = GameObject.Find("Main Camera");
        Rb = GetComponent<Rigidbody2D>();
        physicsCheck = GetComponent<Physics_Check>();
        energyBar = transform.Find("EnergyBar").gameObject;
        energyBarControl = energyBar.GetComponent<EnergyBarControl>();
        //攻击有关初始化
        canAttack = true;
        preAttackExist = 0.2f;//预输入攻击标志存在时间
        
        //下面这3个之后删掉嗷，现在就是方便调试时候用一下
        attackAllow = true;
        dodgeAllow = true;

        PlayerHealthBarController.ShowFor(playerHealth, "PLAYER");
    }
    void Update()
    {
        if (gameManager != null && !gameManager.playerCanMove)
        {
            EnterInteractionMovementLock();
            MaintainInteractionStand();
            return;
        }

        ExitInteractionMovementLock();
        UpdateJumpAttackState();

        //运动相关
        if (gameManager == null || gameManager.playerCanMove)
        {
            if (!isTakeHit)
            {
                if (Input.GetButtonDown("Jump"))//跳跃被按下时
                {
                    JumpPressed = true;
                }
                JumpHeld = Input.GetButton("Jump");
                if (!Input.GetButton("Jump"))
                {
                    JumpPressed = false;
                }
                if (dodgeAllow)
                {
                    if (Input.GetButtonDown("Dodge"))
                    {
                        if (energyLeft > 0)
                            ReadyToDodge();
                    }
                }
            }
            CheckHorzontalMove();//检测移动的方向
            EnergyUpdate();
            //攻击
            if (attackAllow)
                AttackCheck();
        }
    }
    void FixedUpdate()
    {
        if (gameManager != null && !gameManager.playerCanMove)
        {
            EnterInteractionMovementLock();
            MaintainInteractionStand();
            return;
        }

        Dodge();
        if (!isDodge && !isTakeHit)
        {
            AirMovement();
            FaceDirection();
            GroundMovement();
        }
    }

    private void EnterInteractionMovementLock()
    {
        if (interactionMovementLocked)
            return;

        interactionMovementLocked = true;

        bool wasDodging = isDodge || dodge;
        moveDir = 0f;
        JumpPressed = false;
        JumpHeld = false;
        IsJump = false;
        dodge = false;
        isDodge = false;
        attack = false;
        preAttack = false;
        isAttack = false;
        attackValid = false;
        MeleeParryEnd();
        EndJumpAttack();
        canAttack = false;

        if (wasDodging && !isTakeHit)
            Physics2D.IgnoreLayerCollision(7, 9, false);
    }

    private void MaintainInteractionStand()
    {
        moveDir = 0f;
        JumpPressed = false;
        JumpHeld = false;
        attack = false;
        preAttack = false;
        dodge = false;
        isAttack = false;
        attackValid = false;
        canAttack = false;

        if (Rb != null)
            Rb.velocity = new Vector2(0f, Rb.velocity.y);
    }

    private void ExitInteractionMovementLock()
    {
        if (!interactionMovementLocked)
            return;

        interactionMovementLocked = false;
        if (!isTakeHit && !isDodge && !dodge)
            canAttack = true;
    }
    public void CheckHorzontalMove()
    {
        float h = Input.GetAxisRaw("Horizontal");
        moveDir = h;
    }
    void GroundMovement()//地面移动代码
    {
        if(moveDir == 0)
        {
            float deltaSpeed = Time.deltaTime * moveAcc * -3;
            if (Mathf.Abs(Rb.velocity.x) >= 2.0f)
            {
                if(Rb.velocity.x > 0)
                    Rb.velocity = new Vector2((Mathf.Abs(Rb.velocity.x) + deltaSpeed), Rb.velocity.y);
                if(Rb.velocity.x < 0)
                    Rb.velocity = new Vector2(-(Mathf.Abs(Rb.velocity.x) + deltaSpeed), Rb.velocity.y);
            }
            else
            { 
                Rb.velocity = new Vector2(0, Rb.velocity.y);
            }
        }
        else
        {
            float deltaSpeed = Time.deltaTime * moveAcc;
            if (Mathf.Abs(Rb.velocity.x) >= Speed)
                Rb.velocity = new Vector2(Speed * moveDir, Rb.velocity.y);
            else
            {
                Rb.velocity = new Vector2((Mathf.Abs(Rb.velocity.x) + deltaSpeed) * moveDir, Rb.velocity.y);
            }
        }
    }
    void AirMovement()
    {
        if (JumpPressed && physicsCheck.isGround)
        {
            IsJump = true;

            isAttack = false;//跳跃会打断当前攻击动画
            MeleeParryEnd();

            JumpTime = Time.time + JumpHoldDuration;
            Rb.velocity = new Vector2(Rb.velocity.x, JumpForce);

            JumpPressed = false;
        }
        else if (IsJump)
        {
            if (JumpHeld)
                Rb.AddForce(new Vector2(0f, JumpHoldForce), ForceMode2D.Impulse);
            if (JumpTime < Time.time)
                IsJump = false;
            JumpPressed = false;
        }
    }
    void Dodge()//躲避
    {
        if(isDodge)
        {
            //Physics.IgnoreLayerCollision(7,9);
            Rb.velocity = new Vector2(DodgeSpeed * facedirection, 0);
            ShadowPool.instance.GetFromPool();
        }
    }
    void ReadyToDodge()
    {
        dodge = true;
        energyBar.GetComponent<EnergyBarControl>().EnergyConsume(3);
    }
    void AttackCheck()//检测是否有攻击输入
    {
        if (lastAttack + preAttackExist < Time.time)
        {
            preAttack = false;
        }

        if (Input.GetButtonDown("Fire1"))
        {
            if (energyLeft > 0 && canAttack && !isDodge && !dodge)
            {
                Attack();
            }
            else
            {
                lastAttack = Time.time;
                preAttack = true;
            }
            
        }
        //当没有按键但是处于预攻击状态时
        if(preAttack && energyLeft > 0 && canAttack && !isDodge && !dodge)
        {
            Attack();
        }
    }
    void Attack()
    {
        energyBar.GetComponent<EnergyBarControl>().EnergyConsume(1);//消耗的量
        attack = true;
        canAttack = false;
        preAttack = false;
        PlaySwordSwingSound();
    }

    public void JumpAttackLift()
    {
        if (physicsCheck.isGround || isDodge || isTakeHit)
            return;

        Rb.velocity = new Vector2(Rb.velocity.x, Mathf.Max(Rb.velocity.y, jumpAttackLiftSpeed));
    }

    public void BeginJumpAttack()
    {
        attack = false;
        isJumpAttack = true;
        jumpAttackEndTime = Time.time + jumpAttackFailSafeDuration;
    }

    public void EndJumpAttack()
    {
        if (!isJumpAttack)
            return;

        isJumpAttack = false;
        MeleeParryEnd();
        isAttack = false;
        attack = false;
        attackValid = false;
        preAttack = false;

        if (!isDodge && !dodge && !isTakeHit)
        {
            canAttack = true;
        }
    }

    private void UpdateJumpAttackState()
    {
        if (!isJumpAttack)
            return;

        if ((physicsCheck != null && physicsCheck.isGround) || Time.time >= jumpAttackEndTime)
        {
            EndJumpAttack();
        }
    }

    void EnergyUpdate()
    {
        energyLeft = energyBar.GetComponent<EnergyBarControl>().energyLeft;
    }

    void FaceDirection()//转向控制
    {
        if (moveDir > 0) 
            facedirection = 1;
        if(moveDir < 0)
            facedirection = -1;
        switch (facedirection)
        {
            case 1: transform.localScale = new Vector2(1, 1); break;//朝右,使用sr.flip无法改变碰撞体
            case -1: transform.localScale = new Vector2(-1, 1); break;//朝左
            default: break;
        }
    }
    //受伤动画以及收到的冲击大小
    public void MeleeParryStart()
    {
        meleeParryActive = true;
    }

    public void MeleeParryEnd()
    {
        meleeParryActive = false;
    }

    public void RegisterSuccessfulMeleeParry(float protectionDuration)
    {
        meleeProtectionUntil = Mathf.Max(meleeProtectionUntil, Time.time + Mathf.Max(0f, protectionDuration));
        PlaySuccessfulParrySound();

        if (energyBarControl == null && energyBar != null)
            energyBarControl = energyBar.GetComponent<EnergyBarControl>();
        if (energyBarControl != null)
        {
            energyBarControl.RefillEnergy();
            energyLeft = energyBarControl.energyLeft;
        }
    }

    private void PlaySwordSwingSound()
    {
        PlayCombatSound(
            swordSwingSound1,
            swordSwingSound2,
            swordSwingVolume,
            ref lastSwordSwingSoundIndex);
    }

    private void PlaySuccessfulParrySound()
    {
        PlayCombatSound(
            successfulParrySound1,
            successfulParrySound2,
            successfulParryVolume,
            ref lastSuccessfulParrySoundIndex);
    }

    private void PlayCombatSound(
        AudioClip firstClip,
        AudioClip secondClip,
        float volume,
        ref int lastPlayedIndex)
    {
        if (combatAudioSource == null)
            return;

        AudioClip selectedClip;
        if (firstClip == null && secondClip == null)
            return;
        if (firstClip == null)
        {
            selectedClip = secondClip;
            lastPlayedIndex = 1;
        }
        else if (secondClip == null)
        {
            selectedClip = firstClip;
            lastPlayedIndex = 0;
        }
        else
        {
            int selectedIndex = lastPlayedIndex < 0
                ? Random.Range(0, 2)
                : 1 - lastPlayedIndex;
            selectedClip = selectedIndex == 0 ? firstClip : secondClip;
            lastPlayedIndex = selectedIndex;
        }

        combatAudioSource.PlayOneShot(selectedClip, volume);
    }

    public PlayerDefenseResult ResolveIncomingAttack(EnemyAttackType attackType, bool canBeParried, float attackerX)
    {
        if (cantHit)
            return PlayerDefenseResult.Invulnerable;

        if (attackType != EnemyAttackType.Melee)
            return PlayerDefenseResult.Hit;

        if (canBeParried && meleeParryActive && IsAttackerInFront(attackerX))
            return PlayerDefenseResult.Parried;

        if (Time.time < meleeProtectionUntil)
            return PlayerDefenseResult.Invulnerable;

        return PlayerDefenseResult.Hit;
    }

    private bool IsAttackerInFront(float attackerX)
    {
        float horizontalDistance = attackerX - transform.position.x;
        return Mathf.Abs(horizontalDistance) < 0.05f || horizontalDistance * facedirection > 0f;
    }

    public void TakeHit(float smash, float attackerX)
    {
        float hitDir;
        hitDir = transform.position.x - attackerX;
        takeHit = true;
        if (hitDir > 0)//收到向右侧的冲击
        {
            Rb.velocity = new Vector2(smash, Rb.velocity.y);
        }
        else
        {
            Rb.velocity = new Vector2(-smash, Rb.velocity.y);
        }   
    }

    public bool TakeDamage(int damage, float smash, float attackerX)
    {
        if (playerHealth == null || !playerHealth.TakeDamage(damage))
            return false;

        TakeHit(smash, attackerX);
        return true;
    }
    public void Counter(float smash, float attackerX)
    {
        float hitDir;
        hitDir = transform.position.x - attackerX;
        //isCounter = true;
        if (hitDir > 0)//收到向右侧的冲击
        {
            Rb.velocity = new Vector2(smash, Rb.velocity.y);
        }
        else
        {
            Rb.velocity = new Vector2(-smash, Rb.velocity.y);
        }
    }

    public void GetCatched(float attackerX)
    {
        if (!cantHit)
        {
            float hitDir;
            hitDir = transform.position.x - attackerX;
            getCatched = true;
            
            if (hitDir > 0)//收到向右侧的冲击
                facedirection = -1;
            else
                facedirection = 1;
            switch (facedirection)
            {
                case 1: transform.localScale = new Vector2(1, 1); break;//朝右,使用sr.flip无法改变碰撞体
                case -1: transform.localScale = new Vector2(-1, 1); break;//朝左
                default: break;
            }

        }
    }
    public void ThrowOutStart()
    {
        Rb.velocity = new Vector2(20f * facedirection, 0);
        Rb.drag = 2;
    }
    public void ThrowOutEnd()
    {
        Rb.velocity = new Vector2(0, 0);
        Rb.drag = 0;
    }
    private void CameraZoomIn()
    {

    }
    private void CameraZoomOut()
    {

    }
}
