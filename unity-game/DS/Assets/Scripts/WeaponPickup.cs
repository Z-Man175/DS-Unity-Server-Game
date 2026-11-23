using Unity.Netcode;
using UnityEngine;

public class WeaponPickup : NetworkBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsServer || !IsSpawned) return;
        if (other.TryGetComponent(out Player p) && !p.hasWeapon.Value)
        {
            p.hasWeapon.Value = true;
            NetworkObject.Despawn();
        }
    }
}