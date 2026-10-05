using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthGame
{
    /// <summary>
    /// Đọc chuột / tay cầm / cảm ứng và điều khiển trục quay của camera Cinemachine:
    ///  - CinemachineOrbitalFollow (góc nhìn thứ ba): xoay quanh nhân vật + zoom.
    ///  - CinemachinePanTilt (góc nhìn thứ nhất): xoay đầu (pan) và ngước/cúi (tilt).
    /// CameraModeManager bật/tắt component này theo camera đang hoạt động.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraLookInput : MonoBehaviour
    {
        public enum MouseActivation
        {
            Always,             // FPS: chuột luôn điều khiển (con trỏ bị khóa)
            HoldRightMouse,     // TPS: giữ chuột phải để xoay
        }

        public MouseActivation mouseActivation = MouseActivation.HoldRightMouse;
        [Tooltip("Độ / pixel chuột")]
        public float mouseSensitivity = 0.15f;
        [Tooltip("Độ / giây khi dùng cần analog phải")]
        public float gamepadSpeed = 140f;
        [Tooltip("Độ / giây khi dùng phím Q / E")]
        public float keyRotateSpeed = 110f;
        [Tooltip("Độ / pixel khi vuốt màn hình")]
        public float touchSensitivity = 0.2f;
        [Tooltip("Mỗi nấc cuộn chuột thay đổi bao nhiêu (hệ số khoảng cách).")]
        public float zoomStep = 0.1f;
        public bool invertY;

        CinemachineOrbitalFollow m_Orbit;
        CinemachinePanTilt m_PanTilt;

        void Awake()
        {
            m_Orbit = GetComponent<CinemachineOrbitalFollow>();
            m_PanTilt = GetComponent<CinemachinePanTilt>();
        }

        /// <summary>Đặt camera ra sau lưng hướng nhìn hiện tại của nhân vật khi vừa chuyển sang.</summary>
        public void AlignBehind(Transform target)
        {
            if (target == null) return;
            float yaw = target.eulerAngles.y;
            if (yaw > 180f) yaw -= 360f;

            if (m_Orbit != null)
            {
                m_Orbit.HorizontalAxis.Value = yaw;
                m_Orbit.VerticalAxis.Value = m_Orbit.VerticalAxis.Center;
            }
            if (m_PanTilt != null)
            {
                m_PanTilt.PanAxis.Value = yaw;
                m_PanTilt.TiltAxis.Value = 0f;
            }
        }

        void Update()
        {
            if (DialogueManager.IsDialogueActive || !GameInput.GameplayEnabled)
                return;

            float dt = Time.unscaledDeltaTime;
            Vector2 look = Vector2.zero;   // độ
            float zoom = 0f;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                // Chỉ xoay khi con trỏ đang bị khóa (đang chơi) hoặc giữ chuột phải
                bool active = (mouseActivation == MouseActivation.Always && Cursor.lockState == CursorLockMode.Locked) || mouse.rightButton.isPressed;
                if (active)
                    look += mouse.delta.ReadValue() * mouseSensitivity * GameSettings.MouseSensitivity;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    zoom -= Mathf.Sign(scroll) * zoomStep;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                look += gamepad.rightStick.ReadValue() * (gamepadSpeed * dt);
            }

            // Vuốt màn hình (TouchControls) để xoay camera
            look += TouchInput.ConsumeLook() * (touchSensitivity * GameSettings.MouseSensitivity);

            if (invertY != GameSettings.InvertY) look.y = -look.y;
            Apply(look, zoom);
        }

        void Apply(Vector2 look, float zoom)
        {
            if (m_Orbit != null)
            {
                m_Orbit.HorizontalAxis.Value = Wrap(m_Orbit.HorizontalAxis.Value + look.x, m_Orbit.HorizontalAxis.Range);
                m_Orbit.VerticalAxis.Value = Clamp(m_Orbit.VerticalAxis.Value - look.y, m_Orbit.VerticalAxis.Range);
                m_Orbit.RadialAxis.Value = Clamp(m_Orbit.RadialAxis.Value + zoom, m_Orbit.RadialAxis.Range);
            }

            if (m_PanTilt != null)
            {
                m_PanTilt.PanAxis.Value = Wrap(m_PanTilt.PanAxis.Value + look.x, m_PanTilt.PanAxis.Range);
                m_PanTilt.TiltAxis.Value = Clamp(m_PanTilt.TiltAxis.Value - look.y, m_PanTilt.TiltAxis.Range);
            }
        }

        static float Clamp(float v, Vector2 range) => Mathf.Clamp(v, range.x, range.y);

        static float Wrap(float v, Vector2 range)
        {
            float size = range.y - range.x;
            if (size <= 0f) return v;
            while (v > range.y) v -= size;
            while (v < range.x) v += size;
            return v;
        }
    }
}
