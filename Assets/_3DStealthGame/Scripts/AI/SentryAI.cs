using System.Collections.Generic;
using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Tượng Gargoyle canh gác: đứng yên, quay đầu quét qua lại. Không thể tiêu diệt.
    /// Phát hiện người chơi → hú báo động, mọi hồn ma trong bán kính kéo tới.
    /// Linh Quang chỉ làm nó "lóa mắt" vài giây.
    /// </summary>
    public class SentryAI : MonoBehaviour, IStunnable
    {
        public static readonly List<SentryAI> All = new();

        public float visionRange = 6.5f;
        public float visionAngle = 60f;
        public float sweepAngle = 70f;
        public float sweepPeriod = 7f;
        public float detectionRate = 1.1f;
        public float alarmCooldown = 7f;
        public float eyeHeight = 1.4f;
        public VisionCone visionCone;

        public float Awareness { get; private set; }
        public bool IsBlinded => Time.time < m_BlindUntil;
        public bool IsAwareOfPlayer => Awareness >= 1f;

        float m_BaseYaw;
        float m_BlindUntil;
        float m_LastAlarm = -100f;
        float m_Phase;
        DifficultyProfile m_Diff;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            m_BaseYaw = transform.eulerAngles.y;
            m_Phase = Random.value * Mathf.PI * 2f;
            m_Diff = DifficultyProfile.Get(GameSession.CurrentDifficulty);
            if (visionCone != null) visionCone.Configure(visionRange * m_Diff.enemyVision, visionAngle);
        }

        public void Stun(float seconds)
        {
            m_BlindUntil = Time.time + seconds * 2f;
            Awareness = 0f;
            GameEvents.RaiseToast("Tượng gác bị lóa mắt!");
        }

        void Update()
        {
            var player = PlayerStats.Instance;
            float dt = Time.deltaTime;

            if (!IsBlinded)
            {
                float yaw = m_BaseYaw + Mathf.Sin(Time.time * Mathf.PI * 2f / sweepPeriod + m_Phase) * sweepAngle * 0.5f;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            bool sees = player != null && player.IsAlive && !IsBlinded && CanSee(player.transform);
            if (sees)
            {
                var move = player.GetComponent<PlayerMovement>();
                float stealth = move != null && move.IsCrouching ? 0.5f : 1f;
                Awareness += detectionRate * m_Diff.detectionRate * stealth * dt;
            }
            else Awareness -= 0.4f * dt;
            Awareness = Mathf.Clamp01(Awareness);

            if (Awareness >= 1f && Time.time - m_LastAlarm > alarmCooldown)
            {
                m_LastAlarm = Time.time;
                AudioManager.SfxAt(SoundId.Alarm, transform.position);
                GameEvents.RaiseToast("Tượng Gargoyle báo động! Lũ ma đang kéo tới!");
                GameEvents.RaiseAlarm(player.transform.position);
                CameraShake.Shake(0.4f);
            }

            if (visionCone != null)
            {
                Color c = IsBlinded ? new Color(0.4f, 0.6f, 1f, 0.08f)
                    : Awareness >= 1f ? new Color(1f, 0.15f, 0.1f, 0.35f)
                    : Color.Lerp(new Color(1f, 0.55f, 0.2f, 0.16f), new Color(1f, 0.25f, 0.1f, 0.35f), Awareness);
                visionCone.SetColor(c);
            }
        }

        bool CanSee(Transform player)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = player.position + Vector3.up * 0.8f;
            Vector3 to = target - eye;
            float dist = to.magnitude;
            if (dist > visionRange * m_Diff.enemyVision) return false;
            if (Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z)) > visionAngle * 0.5f) return false;
            if (Physics.Raycast(eye, to / dist, out var hit, dist + 0.2f, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform.IsChildOf(player);
            return true;
        }
    }
}
