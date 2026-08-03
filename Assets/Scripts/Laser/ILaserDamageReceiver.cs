using UnityEngine;

public interface ILaserDamageReceiver
{
    /// <summary>Returns true when this firing window actually dealt damage.</summary>
    bool ReceiveLaserDamage(int damage, Vector2 knockbackDirection);
}
