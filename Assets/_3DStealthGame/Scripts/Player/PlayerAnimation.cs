using UnityEngine;

namespace StealthGame
{
    /// <summary>
    /// Điều khiển animation nhân vật:
    ///  - Animator (blend tree Idle / Walk / Run theo tham số Speed).
    ///  - Lớp procedural cộng thêm trên mô hình: ngồi (co người), nhảy (kéo giãn),
    ///    tấn công (lao tới), trúng đòn (giật lùi), chết (ngã xuống).
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Transform mô hình con – nhận chuyển động procedural.")]
        public Transform model;

        static readonly int k_Speed = Animator.StringToHash("Speed");
        static readonly int k_Grounded = Animator.StringToHash("Grounded");
        static readonly int k_IsWalking = Animator.StringToHash("IsWalking");

        PlayerMovement m_Movement;
        PlayerStats m_Stats;
        bool m_HasSpeed, m_HasGrounded, m_HasIsWalking;

        Vector3 m_BaseScale = Vector3.one;
        Vector3 m_BasePos;
        float m_Crouch;          // 0..1
        float m_Stretch;         // >0 kéo giãn khi nhảy, <0 nén khi tiếp đất
        float m_Attack;          // 1 -> 0
        float m_Hit;             // 1 -> 0
        float m_Death;           // 0 -> 1
        bool m_Dead;

        void Awake()
        {
            m_Movement = GetComponent<PlayerMovement>();
            m_Stats = GetComponent<PlayerStats>();
            if (animator == null) animator = GetComponent<Animator>();
            if (animator != null)
            {
                foreach (var p in animator.parameters)
                {
                    if (p.nameHash == k_Speed) m_HasSpeed = true;
                    if (p.nameHash == k_Grounded) m_HasGrounded = true;
                    if (p.nameHash == k_IsWalking) m_HasIsWalking = true;
                }
            }
            if (model != null)
            {
                m_BaseScale = model.localScale;
                m_BasePos = model.localPosition;
            }
        }

        void OnEnable()
        {
            if (m_Movement != null)
            {
                m_Movement.Jumped += OnJump;
                m_Movement.Landed += OnLand;
            }
            if (m_Stats != null)
            {
                m_Stats.Damaged += OnDamaged;
                m_Stats.Died += OnDied;
            }
        }

        void OnDisable()
        {
            if (m_Movement != null)
            {
                m_Movement.Jumped -= OnJump;
                m_Movement.Landed -= OnLand;
            }
            if (m_Stats != null)
            {
                m_Stats.Damaged -= OnDamaged;
                m_Stats.Died -= OnDied;
            }
        }

        void OnJump() => m_Stretch = 1f;
        void OnLand(float fallSpeed) => m_Stretch = -Mathf.Clamp01(fallSpeed / 6f) - 0.3f;
        void OnDamaged(float _) => m_Hit = 1f;
        void OnDied() => m_Dead = true;
        public void PlayAttack() => m_Attack = 1f;

        public void ResetPose()
        {
            m_Dead = false;
            m_Death = 0f;
            if (animator != null) animator.speed = 1f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (animator != null && m_Movement != null)
            {
                float speed = m_Dead ? 0f : m_Movement.SpeedRatio;
                if (m_HasSpeed) animator.SetFloat(k_Speed, speed, 0.08f, dt);
                if (m_HasGrounded) animator.SetBool(k_Grounded, m_Movement.IsGrounded);
                if (m_HasIsWalking) animator.SetBool(k_IsWalking, speed > 0.1f);
                animator.speed = m_Dead ? 0f : m_Movement.IsCrouching ? 0.75f : !m_Movement.IsGrounded ? 0.3f : 1f;
            }

            if (model == null) return;

            m_Crouch = Mathf.MoveTowards(m_Crouch, m_Movement != null && m_Movement.IsCrouching ? 1f : 0f, dt * 6f);
            m_Stretch = Mathf.MoveTowards(m_Stretch, 0f, dt * 4f);
            m_Attack = Mathf.MoveTowards(m_Attack, 0f, dt * 4f);
            m_Hit = Mathf.MoveTowards(m_Hit, 0f, dt * 5f);
            if (m_Dead) m_Death = Mathf.MoveTowards(m_Death, 1f, dt * 1.6f);

            // Co / giãn: giữ thể tích tương đối
            float sy = 1f - m_Crouch * 0.25f + m_Stretch * 0.12f;
            float sxz = 1f + m_Crouch * 0.08f - m_Stretch * 0.05f;
            model.localScale = Vector3.Scale(m_BaseScale, new Vector3(sxz, sy, sxz));

            // Lao tới khi tấn công, giật lùi khi trúng đòn
            float lunge = Mathf.Sin(m_Attack * Mathf.PI) * 0.15f - Mathf.Sin(m_Hit * Mathf.PI) * 0.12f;
            float pitch = Mathf.Sin(m_Attack * Mathf.PI) * 12f - Mathf.Sin(m_Hit * Mathf.PI) * 15f + m_Crouch * 8f;

            // Chết: ngã ngửa ra sau và chìm nhẹ
            float fall = Mathf.SmoothStep(0f, 1f, m_Death);
            pitch -= fall * 85f;
            model.localPosition = m_BasePos + new Vector3(0f, -fall * 0.15f, lunge);
            model.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
