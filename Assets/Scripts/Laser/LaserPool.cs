using System.Collections.Generic;
using UnityEngine;

public sealed class LaserPool : MonoBehaviour
{
    [SerializeField] private LaserObject laserPrefab;
    [SerializeField, Min(1)] private int initialSize = 24;
    [SerializeField, Min(1)] private int expandBy = 8;

    private readonly Queue<LaserObject> available = new Queue<LaserObject>();
    private readonly HashSet<LaserObject> active = new HashSet<LaserObject>();
    private readonly List<LaserObject> releaseBuffer = new List<LaserObject>();

    public static LaserPool Instance { get; private set; }
    public int AvailableCount => available.Count;
    public int ActiveCount => active.Count;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
            Debug.LogWarning("Multiple LaserPool instances exist. Static calls will use the first one.", this);

        if (laserPrefab == null)
        {
            Debug.LogError("LaserPool requires a LaserObject prefab.", this);
            enabled = false;
            return;
        }

        Expand(initialSize);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public LaserObject Spawn(Vector2 center, float angle)
    {
        if (laserPrefab == null)
            return null;

        if (available.Count == 0)
            Expand(expandBy);

        LaserObject laser = available.Dequeue();
        active.Add(laser);
        laser.gameObject.SetActive(true);
        laser.Play(center, angle, Release);
        return laser;
    }

    /// <summary>
    /// Captures the target's world position at spawn time. The laser does not retain or follow the target.
    /// </summary>
    public LaserObject Spawn(Transform target, float angle)
    {
        return Spawn(target, Vector2.zero, angle);
    }

    /// <summary>
    /// Captures the target's world position plus a world-space offset at spawn time.
    /// </summary>
    public LaserObject Spawn(Transform target, Vector2 offset, float angle)
    {
        if (target == null)
        {
            Debug.LogWarning("LaserPool cannot spawn a target-locked laser because the target is null.", this);
            return null;
        }

        Vector2 lockedCenter = (Vector2)target.position + offset;
        return Spawn(lockedCenter, angle);
    }

    public void Release(LaserObject laser)
    {
        if (laser == null || !active.Remove(laser))
            return;

        laser.gameObject.SetActive(false);
        laser.transform.SetParent(transform, false);
        available.Enqueue(laser);
    }

    public void ReleaseAll()
    {
        releaseBuffer.Clear();
        releaseBuffer.AddRange(active);
        foreach (LaserObject laser in releaseBuffer)
            laser.ReleaseNow();
        releaseBuffer.Clear();
    }

    private void Expand(int count)
    {
        for (int i = 0; i < count; i++)
        {
            LaserObject laser = Instantiate(laserPrefab, transform);
            laser.name = $"Laser_{available.Count + active.Count:00}";
            laser.gameObject.SetActive(false);
            available.Enqueue(laser);
        }
    }
}
