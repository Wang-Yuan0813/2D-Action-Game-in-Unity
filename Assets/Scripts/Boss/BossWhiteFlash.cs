using System.Collections;
using UnityEngine;

/// <summary>
/// Applies the Boss white-flash shader when PlayFlash is called by a damage receiver.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BossWhiteFlash : MonoBehaviour
{
    private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");

    [SerializeField, Min(0.01f)] private float flashDuration = 0.2f;

    private SpriteRenderer spriteRenderer;
    private Material materialInstance;
    private Coroutine flashRoutine;

    private void Awake()
    {
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

    public void PlayFlash()
    {
        if (materialInstance == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        materialInstance.SetFloat(FlashAmount, 1f);
        yield return new WaitForSeconds(flashDuration);
        materialInstance.SetFloat(FlashAmount, 0f);
        flashRoutine = null;
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (materialInstance != null)
            materialInstance.SetFloat(FlashAmount, 0f);
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
            Destroy(materialInstance);
    }
}
