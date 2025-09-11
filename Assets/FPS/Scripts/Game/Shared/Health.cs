using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public class Health : MonoBehaviour
    {
        [Tooltip("Maximum amount of health")] public float MaxHealth = 10f;

        [Tooltip("Health ratio at which the critical health vignette starts appearing")]
        public float CriticalHealthRatio = 0.3f;

        public UnityAction<float, GameObject> OnDamaged;
        public UnityAction<float> OnHealed;
        public UnityAction OnDie;

        public float CurrentHealth { get; set; }
        public bool Invincible { get; set; }
        public bool CanPickup() => CurrentHealth < MaxHealth;

        public float GetRatio() => CurrentHealth / MaxHealth;
        public bool IsCritical() => GetRatio() <= CriticalHealthRatio;

        bool m_IsDead;

        // ---------------------------
        // NEW: Optional Shield Ripple
        // ---------------------------
        [Header("Shield Ripple (Optional)")]
        [Tooltip("Renderer of your shield sphere (uses Shader Graph with _Hit1.._Hit4 and _RippleIntensity).")]
        [SerializeField] Renderer ShieldRenderer;

        [Tooltip("Shader property names for hit slots (Vector4: local xyz + time).")]
        [SerializeField] string[] HitPropertyNames = { "_Hit1", "_Hit2", "_Hit3", "_Hit4" };

        [Tooltip("Layer mask used to raycast from attacker to player and find the shield surface.")]
        [SerializeField] LayerMask ShieldLayer = ~0;

        [Tooltip("Max distance for the raycast toward the shield.")]
        [SerializeField] float RayMaxDist = 200f;

        [Tooltip("Target point near player center; if null, uses this transform.")]
        [SerializeField] Transform PlayerCenter;

        [Tooltip("Optionally scale _RippleIntensity by damage for a short pulse.")]
        [SerializeField] bool UseDamageToScaleIntensity = true;

        [Tooltip("Shader float property that controls ripple intensity in your Graph.")]
        [SerializeField] string RippleIntensityProp = "_RippleIntensity";

        [Tooltip("How much to scale intensity per 1 damage (final = base * (1 + damage * scale)).")]
        [SerializeField] float DamageToRippleScale = 0.05f;

        Material _shieldMat;
        int _nextShieldSlot;
        float _baseRippleIntensity = -1f;

        void Awake()
        {
            // Cache shield material & base intensity
            if (ShieldRenderer)
            {
                _shieldMat = ShieldRenderer.material; // instanced per object
                if (!string.IsNullOrEmpty(RippleIntensityProp) && _shieldMat.HasProperty(RippleIntensityProp))
                {
                    _baseRippleIntensity = _shieldMat.GetFloat(RippleIntensityProp);
                }
            }
        }

        void Start()
        {
            CurrentHealth = MaxHealth;
        }

        public void Heal(float healAmount)
        {
            float healthBefore = CurrentHealth;
            CurrentHealth += healAmount;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnHealed action
            float trueHealAmount = CurrentHealth - healthBefore;
            if (trueHealAmount > 0f)
                OnHealed?.Invoke(trueHealAmount);
        }

        public void TakeDamage(float damage, GameObject damageSource)
        {
            if (Invincible)
                return;

            float healthBefore = CurrentHealth;
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);

            // call OnDamaged action
            float trueDamageAmount = healthBefore - CurrentHealth;
            if (trueDamageAmount > 0f)
            {
                // NEW: ping shield ripple
                TryPingShield(trueDamageAmount, damageSource);
                OnDamaged?.Invoke(trueDamageAmount, damageSource);
            }

            HandleDeath();
        }

        public void Kill()
        {
            CurrentHealth = 0f;

            // call OnDamaged action
            OnDamaged?.Invoke(MaxHealth, null);

            // NEW: also ping ripple once on kill (optional visual)
            TryPingShield(MaxHealth, null);

            HandleDeath();
        }

        void HandleDeath()
        {
            if (m_IsDead)
                return;

            // call OnDie action
            if (CurrentHealth <= 0f)
            {
                m_IsDead = true;
                OnDie?.Invoke();
            }
        }

        // ---------------------------
        // NEW: Shield ripple helpers
        // ---------------------------
        void TryPingShield(float damage, GameObject damageSource)
        {
            if (_shieldMat == null || HitPropertyNames == null || HitPropertyNames.Length == 0 || ShieldRenderer == null)
                return;

            // 1) Determine ray origin
            Vector3 origin;
            if (damageSource) origin = damageSource.transform.position;
            else if (Camera.main) origin = Camera.main.transform.position;
            else origin = transform.position - transform.forward * 2f;

            // 2) Aim at player center/root
            Vector3 target = PlayerCenter ? PlayerCenter.position : transform.position;
            Vector3 dir = (target - origin).normalized;

            // 3) Raycast to locate exact point on shield
            if (Physics.Raycast(origin, dir, out RaycastHit hit, RayMaxDist, ShieldLayer, QueryTriggerInteraction.Ignore))
            {
                WriteHitAndPulse(hit.point, damage);
                return;
            }

            // 4) Fallback if raycast misses: use closest point on shield collider (if any)
            var col = ShieldRenderer.GetComponent<Collider>();
            if (col)
            {
                Vector3 guess = origin + dir * 3f;
                Vector3 closest = col.ClosestPoint(guess);
                WriteHitAndPulse(closest, damage);
            }
        }

        void WriteHitAndPulse(Vector3 worldHitPos, float damage)
        {
            // Convert to local space of the shield because Shader Graph InverseTransformPoint expects local positions
            Transform tr = ShieldRenderer.transform;
            Vector3 local = tr.InverseTransformPoint(worldHitPos);

            string prop = HitPropertyNames[_nextShieldSlot];
            _shieldMat.SetVector(prop, new Vector4(local.x, local.y, local.z, Time.time));
            _nextShieldSlot = (_nextShieldSlot + 1) % HitPropertyNames.Length;

            if (UseDamageToScaleIntensity && _baseRippleIntensity >= 0f && !string.IsNullOrEmpty(RippleIntensityProp) && _shieldMat.HasProperty(RippleIntensityProp))
            {
                StopCoroutine(nameof(PulseRippleIntensity));
                StartCoroutine(PulseRippleIntensity(damage));
            }
        }

        IEnumerator PulseRippleIntensity(float damage)
        {
            float target = _baseRippleIntensity * (1f + Mathf.Max(0f, damage) * DamageToRippleScale);

            // Quick rise
            const float upTime = 0.05f;
            float t = 0f;
            while (t < upTime)
            {
                float k = t / upTime;
                _shieldMat.SetFloat(RippleIntensityProp, Mathf.Lerp(_baseRippleIntensity, target, k));
                t += Time.deltaTime;
                yield return null;
            }
            _shieldMat.SetFloat(RippleIntensityProp, target);

            // Smooth decay
            const float downTime = 0.25f;
            t = 0f;
            while (t < downTime)
            {
                float k = t / downTime;
                _shieldMat.SetFloat(RippleIntensityProp, Mathf.Lerp(target, _baseRippleIntensity, k));
                t += Time.deltaTime;
                yield return null;
            }
            _shieldMat.SetFloat(RippleIntensityProp, _baseRippleIntensity);
        }
    }
}
