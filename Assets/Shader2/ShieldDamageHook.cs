using UnityEngine;
using Unity.FPS.Game; // so we can see Health

public class ShieldDamageHook : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Health playerHealth;                 // drag the player's Health component
    [SerializeField] ShieldImpactReceiver shieldReceiver; // drag the ShieldImpactReceiver on the shield
    [SerializeField] Collider shieldCollider;             // drag the shield's collider (SphereCollider or MeshCollider)
    [SerializeField] Transform playerCenter;              // typically the player root or chest/head transform

    [Header("Raycast")]
    [SerializeField] LayerMask shieldLayer; // set to only the Shield layer for reliable hits
    [SerializeField] float rayMaxDist = 200f;

    void Awake()
    {
        if (!playerHealth) playerHealth = GetComponentInParent<Health>();
    }

    void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged += OnDamaged; // (damage, damageSource)
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDamaged -= OnDamaged;
    }

    void OnDamaged(float damage, GameObject damageSource)
    {
        if (!shieldReceiver) return;

        // 1) pick an origin (attacker/projectile if known; else camera or forward of player)
        Vector3 origin;
        if (damageSource)
            origin = damageSource.transform.position;
        else if (Camera.main)
            origin = Camera.main.transform.position;
        else
            origin = transform.position + transform.forward * 2f;

        // 2) pick a target point on/near the player to aim through the shield
        Vector3 target = playerCenter ? playerCenter.position : transform.position;

        Vector3 dir = (target - origin).normalized;

        // 3) Raycast to shield layer to get exact impact on the shield surface
        if (Physics.Raycast(origin, dir, out RaycastHit hit, rayMaxDist, shieldLayer, QueryTriggerInteraction.Ignore))
        {
            shieldReceiver.AddHit(hit.point);
            return;
        }

        // 4) Fallback: compute closest point on shield collider along that ray
        if (shieldCollider)
        {
            // Project a point along the ray and clamp to collider’s surface
            Vector3 guess = origin + dir * 3f;
            Vector3 closest = shieldCollider.ClosestPoint(guess);
            shieldReceiver.AddHit(closest);
        }
    }
}
