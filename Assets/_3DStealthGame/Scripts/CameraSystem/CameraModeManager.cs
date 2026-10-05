using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace StealthGame
{
    /// <summary>Hệ trục dùng để quy đổi input WASD thành hướng di chuyển.</summary>
    public enum MoveBasis
    {
        World,              // W luôn là +Z của thế giới (kiểu top-down cổ điển)
        CameraYaw,          // W là hướng camera đang nhìn (TPS / FPS)
        CameraYawLatched,   // Như trên nhưng "chốt" hướng khi bắt đầu bấm – tránh đổi hướng đột ngột khi camera cắt cảnh (CCTV)
    }

    [Serializable]
    public class CameraModeDefinition
    {
        public string displayName = "Camera";
        [TextArea(1, 3)] public string description;
        [Tooltip("CinemachineCamera hoặc camera quản lý (ClearShot, StateDriven...)")]
        public CinemachineVirtualCameraBase virtualCamera;
        public MoveBasis moveBasis = MoveBasis.CameraYaw;
        [Tooltip("Thân nhân vật luôn quay theo hướng camera (FPS).")]
        public bool faceCameraDirection;
        public bool lockCursor;
        [Tooltip("Ẩn mô hình người chơi (chỉ còn bóng) – tránh camera nhìn xuyên thân khi FPS.")]
        public bool hidePlayerModel;
        public bool showSecurityOverlay;
        public CameraLookInput lookInput;
    }

    /// <summary>
    /// Quản lý nhiều chế độ camera Cinemachine. Chuyển chế độ bằng cách đổi Priority:
    /// camera có Priority cao nhất sẽ được CinemachineBrain chọn và blend mượt sang.
    ///
    /// Phím: V = chế độ kế tiếp, 1..9 = chọn trực tiếp, nhấn cần phải (gamepad) = kế tiếp.
    /// Khi hội thoại, camera hội thoại (priority cao hơn) tạm thời chiếm quyền.
    /// </summary>
    public class CameraModeManager : MonoBehaviour
    {
        public static CameraModeManager Instance { get; private set; }

        public List<CameraModeDefinition> modes = new();
        public int startModeIndex;

        [Header("Dialogue shot")]
        public CinemachineCamera dialogueCamera;
        public CinemachineTargetGroup dialogueTargetGroup;

        [Header("Player")]
        public Transform player;

        [Header("Priorities")]
        public int activePriority = 20;
        public int inactivePriority = 0;
        public int dialoguePriority = 50;

        public event Action<CameraModeDefinition, int> ModeChanged;

        public int CurrentIndex { get; private set; } = -1;
        public CameraModeDefinition Current => CurrentIndex >= 0 && CurrentIndex < modes.Count ? modes[CurrentIndex] : null;
        public bool IsInDialogueShot { get; private set; }

        Camera m_OutputCamera;
        Renderer[] m_PlayerRenderers;
        Vector3 m_LatchedForward = Vector3.forward;
        bool m_HasLatched;

        void Awake()
        {
            Instance = this;
            m_OutputCamera = Camera.main;

            if (player == null)
            {
                var movement = FindAnyObjectByType<PlayerMovement>();
                if (movement != null) player = movement.transform;
            }
            if (player != null)
                m_PlayerRenderers = player.GetComponentsInChildren<Renderer>();

            // Mọi camera ảo nhận rung (Cinemachine Impulse)
            var cams = new List<CinemachineCamera>(GetComponentsInChildren<CinemachineCamera>(true));
            foreach (var m in modes)
                if (m.virtualCamera != null) cams.AddRange(m.virtualCamera.GetComponentsInChildren<CinemachineCamera>(true));
            foreach (var c in cams)
                if (c.GetComponent<CinemachineImpulseListener>() == null) c.gameObject.AddComponent<CinemachineImpulseListener>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            SetMode(Mathf.Clamp(startModeIndex, 0, modes.Count - 1));
        }

        void Update()
        {
            if (IsInDialogueShot || DialogueManager.IsBlockingInput || modes.Count == 0 || !GameInput.GameplayEnabled)
                return;

            if (GameInput.Pressed(GameInput.CameraNext))
                NextMode();

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < modes.Count && i < 9; i++)
                {
                    if (keyboard[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame)
                        SetMode(i);
                }
            }
        }

        // ------------------------------------------------------------------ Switching

        public void NextMode()
        {
            if (modes.Count == 0 || IsInDialogueShot) return;
            SetMode((CurrentIndex + 1) % modes.Count);
        }

        public void SetMode(int index)
        {
            if (index < 0 || index >= modes.Count)
                return;

            var previous = Current;
            CurrentIndex = index;
            var mode = modes[index];

            for (int i = 0; i < modes.Count; i++)
            {
                var m = modes[i];
                bool active = i == index;
                if (m.virtualCamera != null)
                    m.virtualCamera.Priority = active ? activePriority : inactivePriority;
                if (m.lookInput != null)
                    m.lookInput.enabled = active;
            }

            // Camera điều khiển bằng chuột: bắt đầu từ sau lưng nhân vật để chuyển cảnh tự nhiên
            if (mode != previous && mode.lookInput != null)
                mode.lookInput.AlignBehind(player);

            ApplyModeSideEffects(mode);
            ModeChanged?.Invoke(mode, index);
        }

        void ApplyModeSideEffects(CameraModeDefinition mode)
        {
            // Con trỏ chuột do GameSession quản lý tập trung
            SetPlayerModelVisible(!mode.hidePlayerModel);
            m_HasLatched = false;
        }

        void SetPlayerModelVisible(bool visible)
        {
            if (m_PlayerRenderers == null) return;
            foreach (var r in m_PlayerRenderers)
            {
                if (r != null)
                    r.shadowCastingMode = visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            }
        }

        // ------------------------------------------------------------------ Movement helpers

        /// <summary>Hướng phẳng (XZ) mà camera đầu ra đang nhìn.</summary>
        public Vector3 GetCameraForwardFlat()
        {
            if (m_OutputCamera == null)
                m_OutputCamera = Camera.main;
            if (m_OutputCamera == null)
                return Vector3.forward;

            Transform t = m_OutputCamera.transform;
            Vector3 f = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (f.sqrMagnitude < 0.001f)  // camera nhìn thẳng xuống
                f = Vector3.ProjectOnPlane(t.up, Vector3.up);
            return f.normalized;
        }

        /// <summary>Đổi input 2D (WASD / joystick) sang hướng di chuyển trong thế giới theo chế độ camera.</summary>
        public Vector3 GetMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                m_HasLatched = false;
                return Vector3.zero;
            }

            var basis = Current != null ? Current.moveBasis : MoveBasis.World;
            Vector3 forward;
            switch (basis)
            {
                case MoveBasis.CameraYaw:
                    forward = GetCameraForwardFlat();
                    break;
                case MoveBasis.CameraYawLatched:
                    if (!m_HasLatched)
                    {
                        m_LatchedForward = GetCameraForwardFlat();
                        m_HasLatched = true;
                    }
                    forward = m_LatchedForward;
                    break;
                default:
                    forward = Vector3.forward;
                    break;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return forward * input.y + right * input.x;
        }

        public bool PlayerShouldFaceCamera => !IsInDialogueShot && Current != null && Current.faceCameraDirection;

        // ------------------------------------------------------------------ Dialogue shot

        /// <summary>
        /// Đặt camera hội thoại qua vai người chơi, nhìn vào nhóm mục tiêu (người chơi + NPC).
        /// Thử vài vị trí và chọn vị trí đầu tiên không bị tường che.
        /// </summary>
        public void BeginDialogueShot(Transform npcFocus, Transform playerFocus)
        {
            if (dialogueCamera == null || npcFocus == null || playerFocus == null)
                return;

            IsInDialogueShot = true;

            if (dialogueTargetGroup != null)
            {
                dialogueTargetGroup.Targets.Clear();
                dialogueTargetGroup.AddMember(playerFocus, 1f, 0.35f);
                dialogueTargetGroup.AddMember(npcFocus, 1.5f, 0.35f);
                dialogueCamera.Target.LookAtTarget = dialogueTargetGroup.transform;
                dialogueCamera.Target.CustomLookAtTarget = true;
                dialogueCamera.Target.TrackingTarget = dialogueTargetGroup.transform;
            }
            else
            {
                dialogueCamera.Target.TrackingTarget = npcFocus;
            }

            dialogueCamera.transform.position = FindDialogueCameraPosition(playerFocus, npcFocus);
            dialogueCamera.transform.rotation = Quaternion.LookRotation(npcFocus.position - dialogueCamera.transform.position);
            dialogueCamera.PreviousStateIsValid = false;
            dialogueCamera.Priority = dialoguePriority;

            foreach (var m in modes)
            {
                if (m.lookInput != null) m.lookInput.enabled = false;
            }

            SetPlayerModelVisible(true);
        }

        public void EndDialogueShot()
        {
            if (!IsInDialogueShot) return;
            IsInDialogueShot = false;

            if (dialogueCamera != null)
                dialogueCamera.Priority = inactivePriority;

            var mode = Current;
            if (mode != null)
            {
                if (mode.lookInput != null) mode.lookInput.enabled = true;
                ApplyModeSideEffects(mode);
            }
        }

        Vector3 FindDialogueCameraPosition(Transform playerFocus, Transform npcFocus)
        {
            Vector3 p = playerFocus.position;
            Vector3 n = npcFocus.position;
            Vector3 toNpc = Vector3.ProjectOnPlane(n - p, Vector3.up);
            float distance = Mathf.Max(toNpc.magnitude, 0.5f);
            Vector3 dir = toNpc.sqrMagnitude > 0.0001f ? toNpc.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = (p + n) * 0.5f;

            Vector3[] candidates =
            {
                p - dir * 1.0f + side * 0.55f + Vector3.up * 0.25f,      // qua vai phải
                p - dir * 1.0f - side * 0.55f + Vector3.up * 0.25f,      // qua vai trái
                mid + side * (distance + 1.2f) + Vector3.up * 0.3f,      // góc ngang (two-shot)
                mid - side * (distance + 1.2f) + Vector3.up * 0.3f,
                p - dir * 0.4f + Vector3.up * 0.9f,                      // từ trên cao sau lưng
            };

            Transform playerRoot = player != null ? player : playerFocus.root;
            var npc = npcFocus.GetComponentInParent<NPCDialogue>();
            Transform npcRoot = npc != null ? npc.transform : npcFocus.root;

            foreach (var c in candidates)
            {
                if (IsClear(p, c, playerRoot, npcRoot) && IsClear(c, n, playerRoot, npcRoot))
                    return c;
            }
            return candidates[candidates.Length - 1];
        }

        static bool IsClear(Vector3 from, Vector3 to, Transform ignoreA, Transform ignoreB)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.001f) return true;

            var hits = Physics.SphereCastAll(from, 0.12f, delta / length, length, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Transform t = hit.collider.transform;
                if ((ignoreA != null && t.IsChildOf(ignoreA)) || (ignoreB != null && t.IsChildOf(ignoreB)))
                    continue;
                return false;
            }
            return true;
        }
    }
}
