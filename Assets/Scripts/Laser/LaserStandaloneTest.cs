using UnityEngine;

/// <summary>Keyboard-only harness for testing the laser pool without any Boss logic.</summary>
public sealed class LaserStandaloneTest : MonoBehaviour
{
    [SerializeField] private LaserPool laserPool;
    [SerializeField] private Camera targetCamera;
    [SerializeField, Range(0f, 180f)] private float angle;
    [SerializeField] private Vector2 centerOffset;
    [SerializeField, Min(1f)] private float angleStep = 15f;

    private void Awake()
    {
        if (laserPool == null)
            laserPool = FindObjectOfType<LaserPool>();
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            angle = Mathf.Repeat(angle - angleStep, 180f);
        if (Input.GetKeyDown(KeyCode.RightArrow))
            angle = Mathf.Repeat(angle + angleStep, 180f);

        if (Input.GetKeyDown(KeyCode.Space))
            SpawnAtScreenCenter(angle);
        if (Input.GetKeyDown(KeyCode.R))
            SpawnAtScreenCenter(Random.Range(0f, 180f));
        if (Input.GetKeyDown(KeyCode.C) && laserPool != null)
            laserPool.ReleaseAll();
    }

    [ContextMenu("Spawn Test Laser")]
    public void SpawnTestLaser()
    {
        if (Application.isPlaying)
            SpawnAtScreenCenter(angle);
    }

    private void SpawnAtScreenCenter(float spawnAngle)
    {
        if (laserPool == null)
        {
            Debug.LogError("LaserStandaloneTest could not find a LaserPool.", this);
            return;
        }

        Vector2 center = transform.position;
        if (targetCamera != null)
        {
            float depth = Mathf.Abs(targetCamera.transform.position.z);
            center = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        }

        laserPool.Spawn(center + centerOffset, spawnAngle);
    }

    private void OnGUI()
    {
        if (laserPool == null)
            return;

        GUI.Label(new Rect(12f, 12f, 520f, 24f),
            $"Laser test: Space=spawn, R=random, Left/Right=angle, C=release all | Angle {angle:0} | Active {laserPool.ActiveCount} | Ready {laserPool.AvailableCount}");
    }
}
