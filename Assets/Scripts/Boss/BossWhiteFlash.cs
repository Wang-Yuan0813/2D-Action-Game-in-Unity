using System.Collections;
using UnityEngine;

/// <summary>
/// Applies the Boss white-flash shader while Boss_Control reports a hit.
/// Added automatically at runtime so existing Boss prefabs need no changes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Boss_Control))]
public sealed class BossWhiteFlash : MonoBehaviour
{
    private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");

    [SerializeField, Min(0.01f)] private float flashDuration = 0.2f;

    private Boss_Control boss;
    private SpriteRenderer spriteRenderer;
    private Material materialInstance;
    private bool wasTakingHit;

    private void Awake()
    {
        boss = GetComponent<Boss_Control>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        Material flashMaterial = Resources.Load<Material>("Materials/BossWhiteFlash");
        if (flashMaterial == null)
        {
            Debug.LogError("Missing Boss white-flash material at Resources/Materials/BossWhiteFlash.", this);
            enabled = false;
            return;
        }

        spriteRenderer.material = flashMaterial;
        materialInstance = spriteRenderer.material;
        materialInstance.SetFloat(FlashAmount, 0f);
    }

    private void Update()
    {
        if (boss.isTakeHit && !wasTakingHit)
        {
            StopAllCoroutines();
            StartCoroutine(Flash());
        }

        wasTakingHit = boss.isTakeHit;
    }

    private IEnumerator Flash()
    {
        materialInstance.SetFloat(FlashAmount, 1f);
        yield return new WaitForSeconds(flashDuration);
        materialInstance.SetFloat(FlashAmount, 0f);
    }

    private void OnDisable()
    {
        if (materialInstance != null)
            materialInstance.SetFloat(FlashAmount, 0f);
    }
}

/// <summary>Installs the white-flash controller on any Boss_Control loaded in a scene.</summary>
public static class BossWhiteFlashInstaller
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        foreach (Boss_Control boss in Object.FindObjectsOfType<Boss_Control>())
        {
            if (boss.GetComponent<BossWhiteFlash>() == null)
                boss.gameObject.AddComponent<BossWhiteFlash>();
        }
    }
}
