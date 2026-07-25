using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ParallaxMapTrigger : MonoBehaviour
{
    [SerializeField] private ParallaxManager parallaxManager;
    [SerializeField] private MapParallaxGroup targetMap;

    [Tooltip("只有带有该 Tag 的对象会触发地图切换。留空表示不检查 Tag。")]
    [SerializeField] private string requiredTag = "Player";

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;
        targetMap = GetComponentInParent<MapParallaxGroup>();
    }

    private void Awake()
    {
        if (parallaxManager == null)
        {
            parallaxManager = FindObjectOfType<ParallaxManager>();
        }

        if (targetMap == null)
        {
            targetMap = GetComponentInParent<MapParallaxGroup>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            return;
        }

        parallaxManager?.ActivateMap(targetMap);
    }
}
