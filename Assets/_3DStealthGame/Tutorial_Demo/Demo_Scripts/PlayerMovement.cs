using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthGame
{
    /// <summary>
    /// Di chuyển nhân vật dựa trên Rigidbody: đi bộ, chạy (tốn thể lực), ngồi (êm, khó bị phát hiện), nhảy.
    /// Hướng đi quy đổi theo camera đang hoạt động (CameraModeManager).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class PlayerMovement : MonoBehaviour
    {
        [Tooltip("Không còn dùng – giữ lại để tương thích prefab cũ. Input đọc từ GameInput.")]
        public InputAction MoveAction;

        [Header("Tốc độ (m/s)")]
        public float walkSpeed = 1.7f;
        public float runSpeed = 3.2f;
        public float crouchSpeed = 0.95f;
        public float acceleration = 14f;
        public float turnSpeed = 12f;

        [Header("Nhảy")]
        public float jumpHeight = 0.85f;
        public float gravityMultiplier = 2.2f;
        public LayerMask groundMask = ~0;

        [Header("Ngồi")]
        public float standHeight = 1.4f;
        public float crouchHeight = 0.95f;

        [Header("Tiếng động (bán kính ma nghe thấy)")]
        public float walkNoise = 2.5f;
        public float runNoise = 7f;
        public float crouchNoise = 0.6f;
        public float landNoise = 5f;

        public event Action Jumped;
        public event Action<float> Landed;

        public bool IsGrounded { get; private set; } = true;
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsMoving => m_PlanarVelocity.sqrMagnitude > 0.04f;
        public float PlanarSpeed => m_PlanarVelocity.magnitude;
        /// <summary>0 = đứng yên, 1 = đi bộ, 2 = chạy.</summary>
        public float SpeedRatio => PlanarSpeed / Mathf.Max(0.01f, walkSpeed);

        Rigidbody m_Rigidbody;
        CapsuleCollider m_Capsule;
        AudioSource m_FootstepSource;
        PlayerStats m_Stats;
        PlayerInventory m_Inventory;
        Vector3 m_PlanarVelocity;
        Vector3 m_FacingTarget;
        float m_FacingOverrideUntil;
        float m_NoiseTimer;
        float m_AirTime;
        float m_LastFallSpeed;
        bool m_JumpQueued;
        float m_LockedUntil;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Capsule = GetComponent<CapsuleCollider>();
            m_FootstepSource = GetComponent<AudioSource>();
            m_Stats = GetComponent<PlayerStats>();
            m_Inventory = GetComponent<PlayerInventory>();

            m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            standHeight = m_Capsule.height;
            m_FacingTarget = transform.forward;
            GameInput.EnsureCreated();
        }

        bool CanControl => GameInput.GameplayEnabled && Time.time >= m_LockedUntil
                           && (m_Stats == null || m_Stats.IsAlive)
                           && !DialogueManager.IsDialogueActive;

        void Update()
        {
            if (!CanControl)
            {
                StopFootsteps();
                return;
            }

            if (GameInput.Pressed(GameInput.Jump) && IsGrounded && !IsCrouching)
                m_JumpQueued = true;

            if (GameInput.Pressed(GameInput.Crouch))
                SetCrouch(!IsCrouching);

            UpdateFootsteps();
        }

        void FixedUpdate()
        {
            CheckGround();

            Vector2 input = CanControl ? GameInput.MoveValue : Vector2.zero;
            var cameras = CameraModeManager.Instance;
            Vector3 wish = cameras != null ? cameras.GetMoveDirection(input) : new Vector3(input.x, 0f, input.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // Chạy: cần giữ Shift, đang di chuyển, không ngồi và còn thể lực
            bool wantSprint = CanControl && GameInput.Held(GameInput.Sprint) && wish.sqrMagnitude > 0.1f && !IsCrouching;
            IsSprinting = wantSprint && (m_Stats == null || m_Stats.TryUseStamina(Time.fixedDeltaTime));

            float speed = IsCrouching ? crouchSpeed : IsSprinting ? runSpeed : walkSpeed;
            Vector3 target = wish * speed;
            float accel = IsGrounded ? acceleration : acceleration * 0.35f;
            m_PlanarVelocity = Vector3.MoveTowards(m_PlanarVelocity, target, accel * Time.fixedDeltaTime);

            Vector3 v = m_Rigidbody.linearVelocity;
            v.x = m_PlanarVelocity.x;
            v.z = m_PlanarVelocity.z;

            if (m_JumpQueued)
            {
                m_JumpQueued = false;
                v.y = Mathf.Sqrt(2f * Physics.gravity.magnitude * gravityMultiplier * jumpHeight);
                IsGrounded = false;
                m_AirTime = 0.01f;
                Jumped?.Invoke();
                AudioManager.SfxAt(SoundId.Jump, transform.position, 0.7f);
            }

            m_Rigidbody.linearVelocity = v;
            if (!IsGrounded)
                m_Rigidbody.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);

            // Hướng quay mặt
            Vector3 facing = m_PlanarVelocity.sqrMagnitude > 0.01f ? m_PlanarVelocity : Vector3.zero;
            if (Time.time < m_FacingOverrideUntil)
                facing = m_FacingTarget;
            else if (cameras != null && cameras.PlayerShouldFaceCamera)
                facing = cameras.GetCameraForwardFlat();

            if (facing.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(facing.normalized, Vector3.up);
                m_Rigidbody.MoveRotation(Quaternion.Slerp(m_Rigidbody.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * Time.fixedDeltaTime)));
            }
        }

        void CheckGround()
        {
            bool wasGrounded = IsGrounded;
            float r = m_Capsule.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (r + 0.05f);
            bool hit = Physics.SphereCast(origin, r, Vector3.down, out var info, 0.15f, groundMask, QueryTriggerInteraction.Ignore)
                       && !info.collider.transform.IsChildOf(transform);
            // Đang bay lên thì không tính là chạm đất
            IsGrounded = hit && m_Rigidbody.linearVelocity.y <= 0.5f;

            if (!IsGrounded)
            {
                m_AirTime += Time.fixedDeltaTime;
                m_LastFallSpeed = Mathf.Max(m_LastFallSpeed, -m_Rigidbody.linearVelocity.y);
            }
            else if (!wasGrounded && m_AirTime > 0.12f)
            {
                Landed?.Invoke(m_LastFallSpeed);
                AudioManager.SfxAt(SoundId.Land, transform.position, 0.8f);
                VFXManager.Play(VFXId.LandDust, transform.position);
                NoiseSystem.Emit(transform.position, IsCrouching ? landNoise * 0.4f : landNoise);
                m_AirTime = 0f;
                m_LastFallSpeed = 0f;
            }
            else if (IsGrounded)
            {
                m_AirTime = 0f;
                m_LastFallSpeed = 0f;
            }
        }

        public void SetCrouch(bool crouch)
        {
            if (crouch == IsCrouching) return;
            if (!crouch)
            {
                // Không đứng dậy được nếu bị trần / vật cản phía trên
                Vector3 bottom = transform.position + Vector3.up * m_Capsule.radius;
                Vector3 top = transform.position + Vector3.up * (standHeight - m_Capsule.radius);
                foreach (var c in Physics.OverlapCapsule(bottom + Vector3.up * 0.05f, top, m_Capsule.radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!c.transform.IsChildOf(transform)) return;
                }
            }
            IsCrouching = crouch;
            float h = crouch ? crouchHeight : standHeight;
            m_Capsule.height = h;
            m_Capsule.center = new Vector3(0f, h * 0.5f, 0f);
        }

        void UpdateFootsteps()
        {
            bool moving = IsGrounded && PlanarSpeed > 0.3f;
            if (m_FootstepSource != null)
            {
                if (moving)
                {
                    m_FootstepSource.pitch = IsSprinting ? 1.45f : IsCrouching ? 0.75f : 1f;
                    m_FootstepSource.volume = (IsCrouching ? 0.25f : IsSprinting ? 0.9f : 0.6f) * GameSettings.SfxVolume;
                    if (!m_FootstepSource.isPlaying) m_FootstepSource.Play();
                }
                else StopFootsteps();
            }

            if (!moving) return;
            m_NoiseTimer -= Time.deltaTime;
            if (m_NoiseTimer <= 0f)
            {
                m_NoiseTimer = IsSprinting ? 0.3f : 0.5f;
                float radius = IsCrouching ? crouchNoise : IsSprinting ? runNoise : walkNoise;
                NoiseSystem.Emit(transform.position, radius);
            }
        }

        void StopFootsteps()
        {
            if (m_FootstepSource != null && m_FootstepSource.isPlaying)
                m_FootstepSource.Stop();
        }

        /// <summary>Quay mặt ngay về hướng chỉ định trong một khoảng thời gian (khi tấn công).</summary>
        public void FaceDirection(Vector3 dir, float duration = 0.35f)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            m_FacingTarget = dir.normalized;
            m_FacingOverrideUntil = Time.time + duration;
            m_Rigidbody.rotation = Quaternion.LookRotation(m_FacingTarget);
        }

        /// <summary>Bị đánh bật lùi + khóa điều khiển ngắn.</summary>
        public void Knockback(Vector3 direction, float force, float lockTime = 0.25f)
        {
            direction.y = 0f;
            m_PlanarVelocity = direction.normalized * force;
            m_LockedUntil = Time.time + lockTime;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            m_Rigidbody.position = position;
            m_Rigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_PlanarVelocity = Vector3.zero;
        }

        // ----- Tương thích với Key / Door / DialogueManager cũ -----
        public void AddKey(string keyName)
        {
            if (m_Inventory != null) m_Inventory.AddKey(keyName);
        }

        public bool OwnKey(string keyName) => m_Inventory != null && m_Inventory.HasKey(keyName);
    }
}
