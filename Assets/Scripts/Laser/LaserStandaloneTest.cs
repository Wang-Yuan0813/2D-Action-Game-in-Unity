using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Keyboard-only harness for testing the laser pool without any Boss logic.</summary>
public sealed class LaserStandaloneTest : MonoBehaviour
{
    [SerializeField] private LaserPool laserPool;
    [SerializeField] private LaserBarrageAttack laserBarrage;
    [SerializeField] private Transform target;
    [SerializeField, Range(0f, 180f)] private float angle;
    [FormerlySerializedAs("centerOffset")]
    [SerializeField] private Vector2 targetOffset;
    [SerializeField, Min(1f)] private float angleStep = 15f;
    [SerializeField] private KeyCode barrageKey = KeyCode.B;

    private void Awake()
    {
        if (laserPool == null)
            laserPool = FindObjectOfType<LaserPool>();
        if (laserBarrage == null)
            laserBarrage = GetComponent<LaserBarrageAttack>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            angle = Mathf.Repeat(angle - angleStep, 180f);
        if (Input.GetKeyDown(KeyCode.RightArrow))
            angle = Mathf.Repeat(angle + angleStep, 180f);

        if (Input.GetKeyDown(KeyCode.L))
            SpawnAtTarget(angle);
        if (Input.GetKeyDown(KeyCode.R))
            SpawnAtTarget(Random.Range(0f, 180f));
        if (Input.GetKeyDown(barrageKey) && laserBarrage != null)
            laserBarrage.StartLaserBarrage();
        if (Input.GetKeyDown(KeyCode.C))
        {
            laserBarrage?.StopLaserBarrage();
            laserPool?.ReleaseAll();
        }
    }

    [ContextMenu("Spawn Test Laser")]
    public void SpawnTestLaser()
    {
        if (Application.isPlaying)
            SpawnAtTarget(angle);
    }

    private void SpawnAtTarget(float spawnAngle)
    {
        if (laserPool == null)
        {
            Debug.LogError("LaserStandaloneTest could not find a LaserPool.", this);
            return;
        }

        Transform spawnTarget = target != null ? target : transform;
        laserPool.Spawn(spawnTarget, targetOffset, spawnAngle);
    }

    private void OnGUI()
    {
        if (laserPool == null)
            return;

        string targetName = target != null ? target.name : $"{name} (self fallback)";
        string barrageState = laserBarrage != null && laserBarrage.IsRunning ? "Running" : "Ready";
        GUI.Label(new Rect(12f, 12f, 900f, 24f),
            $"Laser test: L=spawn, {barrageKey}=barrage, R=random, Left/Right=angle, C=stop all | Barrage {barrageState} | Target {targetName} | Angle {angle:0} | Active {laserPool.ActiveCount} | Ready {laserPool.AvailableCount}");
    }
}
