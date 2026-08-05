using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Player_Control))]
public sealed class PlayerParryController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float meleeProtectionDuration = 0.2f;

    private Player_Control player;
    private Camera_Control cameraControl;
    private Cinemachine.CinemachineCollisionImpulseSource impulse;
    private GameObject counterFx;
    private GameObject counterFx1;
    private Coroutine counterRoutine;

    private void Awake()
    {
        player = GetComponent<Player_Control>();
        cameraControl = FindObjectOfType<Camera_Control>();
        impulse = GetComponentInChildren<Cinemachine.CinemachineCollisionImpulseSource>(true);
        counterFx = Resources.Load<GameObject>("FXPref/CounterFX");
        counterFx1 = Resources.Load<GameObject>("FXPref/CounterFX1");
    }

    /// <summary>Resolves one parry at most once, regardless of trigger callback order.</summary>
    public bool TryParry(BossAttack incomingAttack)
    {
        if (incomingAttack == null || player == null)
            return false;

        PlayerDefenseResult defense = player.ResolveIncomingAttack(
            incomingAttack.AttackType,
            incomingAttack.CanBeParried,
            incomingAttack.AttackerX);

        if (defense != PlayerDefenseResult.Parried || !incomingAttack.TryConsumeAsParried())
            return false;

        // The clash consumes the player's active strike even when the Boss trigger callback runs first.
        player.attackValid = false;
        player.RegisterSuccessfulMeleeParry(meleeProtectionDuration);
        incomingAttack.ApplyParryReaction();
        PlayFeedback(incomingAttack.CounterSmash, incomingAttack.AttackerX);
        return true;
    }

    private void PlayFeedback(float counterSmash, float attackerX)
    {
        Vector3 effectPosition = transform.position + new Vector3(0.5f * player.facedirection, 0.5f, 0f);
        if (counterFx != null)
            Instantiate(counterFx, effectPosition, Quaternion.identity);
        if (counterFx1 != null)
            Instantiate(counterFx1, effectPosition, Quaternion.identity);

        cameraControl?.HitPause(0.1f);
        impulse?.GenerateImpulse();
        player.Counter(counterSmash, attackerX);

        if (counterRoutine != null)
            StopCoroutine(counterRoutine);
        counterRoutine = StartCoroutine(CounterWindow());
    }

    private IEnumerator CounterWindow()
    {
        player.isCounter = true;
        player.dodgeAllow = false;

        float remaining = meleeProtectionDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }

        player.isCounter = false;
        player.dodgeAllow = true;
        counterRoutine = null;
    }
}
