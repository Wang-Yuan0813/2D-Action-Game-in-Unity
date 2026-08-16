using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossActivationTrigger : MonoBehaviour
{
    [SerializeField] private GameObject boss;
    [SerializeField] private string bossDisplayName = "BOSS";
    [SerializeField] private BossEncounterType encounterType;
    private bool activated;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated || !other.CompareTag("Player"))
            return;

        if (boss == null)
        {
            Debug.LogError("BossActivationTrigger requires a Boss reference.", this);
            return;
        }

        activated = true;

        if (boss.GetComponent<BossWhiteFlash>() == null)
            boss.AddComponent<BossWhiteFlash>();

        boss.SetActive(true);

        EnemyBase bossHealth = boss.GetComponent<EnemyBase>();
        if (bossHealth != null)
        {
            BossHealthBarController.ShowFor(bossHealth, bossDisplayName);
            GameFlowEndingController.Instance?.BeginBossEncounter(encounterType, bossHealth);
        }
        else
            Debug.LogWarning("Activated Boss has no EnemyBase component, so its health bar cannot be shown.", boss);

        gameObject.SetActive(false);
    }
}
