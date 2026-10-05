using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    public interface IStunnable
    {
        /// <summary>Kẻ địch đang biết vị trí người chơi (đang đuổi) – đánh lén không được x2.</summary>
        bool IsAwareOfPlayer { get; }
        void Stun(float seconds);
    }

    /// <summary>
    /// Vũ khí của người chơi: Đèn lồng linh hồn.
    ///  - F: bật/tắt đèn soi (tốn năng lượng, nhưng ma thấy bạn từ xa hơn).
    ///  - Chuột trái: "Linh Quang" – luồng sáng hình nón làm choáng + gây sát thương lên ma.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Đèn soi")]
        public Light lanternLight;
        public float lanternDrain = 2.5f;

        [Header("Linh Quang")]
        public Light burstLight;
        public float burstCost = 25f;
        public float burstCooldown = 0.9f;
        public float burstRange = 6.5f;
        public float burstHalfAngle = 40f;
        public float burstDamage = 40f;
        public float burstStun = 2.5f;
        public float sneakMultiplier = 2f;
        public float burstNoise = 7f;

        public bool IsLanternOn { get; private set; }
        public float CooldownRatio => Mathf.Clamp01((Time.time - m_LastBurst) / burstCooldown);

        PlayerStats m_Stats;
        PlayerMovement m_Movement;
        PlayerAnimation m_Animation;
        float m_LastBurst = -10f;
        float m_FlashTimer;
        readonly HashSet<Transform> m_HitRoots = new();

        void Awake()
        {
            m_Stats = GetComponent<PlayerStats>();
            m_Movement = GetComponent<PlayerMovement>();
            m_Animation = GetComponent<PlayerAnimation>();
            if (lanternLight != null)
            {
                lanternLight.enabled = false;
                // Bóng đổ của đèn soi chỉ bật ở đồ họa Cao (tốn GPU trên điện thoại)
                lanternLight.shadows = GameSettings.GraphicsPreset == 2 && !Application.isMobilePlatform ? LightShadows.Soft : LightShadows.None;
            }
            if (burstLight != null) burstLight.enabled = false;
        }

        void Update()
        {
            bool canAct = m_Stats == null || m_Stats.IsAlive;

            if (canAct && GameInput.Pressed(GameInput.Lantern))
                SetLantern(!IsLanternOn);

            if (IsLanternOn)
            {
                m_Stats.DrainEnergy(lanternDrain * Time.deltaTime);
                if (m_Stats.Energy <= 0f || !canAct)
                {
                    SetLantern(false);
                    if (canAct) GameEvents.RaiseToast("Đèn đã cạn năng lượng.");
                }
                if (lanternLight != null)
                    lanternLight.intensity = Mathf.Lerp(lanternLight.intensity, 6f + Mathf.PerlinNoise(Time.time * 6f, 0f) * 1.5f, 0.3f);
            }

            if (canAct && GameInput.Pressed(GameInput.Attack))
                TryBurst();

            if (m_FlashTimer > 0f && burstLight != null)
            {
                m_FlashTimer -= Time.deltaTime;
                burstLight.intensity = Mathf.Max(0f, m_FlashTimer / 0.25f) * 14f;
                burstLight.enabled = m_FlashTimer > 0f;
            }
        }

        public void SetLantern(bool on)
        {
            if (on && m_Stats != null && m_Stats.Energy <= 1f)
            {
                AudioManager.Sfx(SoundId.EnergyEmpty);
                GameEvents.RaiseToast("Không đủ năng lượng để bật đèn.");
                return;
            }
            IsLanternOn = on;
            if (lanternLight != null) lanternLight.enabled = on;
            AudioManager.SfxAt(SoundId.LanternToggle, transform.position, 0.6f);
        }

        public bool TryBurst()
        {
            if (Time.time - m_LastBurst < burstCooldown) return false;
            if (m_Stats != null && !m_Stats.TryUseEnergy(burstCost))
            {
                AudioManager.Sfx(SoundId.EnergyEmpty);
                GameEvents.RaiseToast("Không đủ năng lượng! Tìm dầu đèn (vàng).");
                m_LastBurst = Time.time - burstCooldown * 0.5f;
                return false;
            }
            m_LastBurst = Time.time;

            // Hướng tấn công: theo camera khi ở góc nhìn thứ ba / thứ nhất
            Vector3 dir = transform.forward;
            var cams = CameraModeManager.Instance;
            if (cams != null && cams.Current != null && cams.Current.moveBasis == MoveBasis.CameraYaw)
                dir = cams.GetCameraForwardFlat();
            if (m_Movement != null) m_Movement.FaceDirection(dir);

            Vector3 origin = transform.position + Vector3.up * 0.8f;
            VFXManager.Play(VFXId.LanternBurst, origin + dir * 0.4f, Quaternion.LookRotation(dir));
            AudioManager.SfxAt(SoundId.LanternBurst, origin);
            CameraShake.Shake(0.25f);
            NoiseSystem.Emit(transform.position, burstNoise);
            m_FlashTimer = 0.25f;
            if (m_Animation != null) m_Animation.PlayAttack();

            HitTargets(origin, dir);
            return true;
        }

        void HitTargets(Vector3 origin, Vector3 dir)
        {
            m_HitRoots.Clear();
            foreach (var col in Physics.OverlapSphere(origin, burstRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var stunnable = col.GetComponentInParent<IStunnable>();
                if (stunnable == null) continue;
                var root = ((Component)stunnable).transform;
                if (root == transform || !m_HitRoots.Add(root)) continue;

                Vector3 targetPoint = col.bounds.center;
                Vector3 to = targetPoint - origin;
                if (Vector3.Angle(dir, new Vector3(to.x, 0f, to.z)) > burstHalfAngle) continue;
                if (!HasLineOfSight(origin, targetPoint, root)) continue;

                bool sneak = !stunnable.IsAwareOfPlayer;
                stunnable.Stun(burstStun);

                if (stunnable is IDamageable damageable && damageable.IsAlive)
                {
                    float dmg = burstDamage * (sneak ? sneakMultiplier : 1f);
                    damageable.TakeDamage(dmg, transform.position);
                    if (sneak) GameEvents.RaiseToast("Đánh lén! Sát thương x2");
                }
            }
        }

        bool HasLineOfSight(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 d = to - from;
            foreach (var hit in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }
    }
}
