using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StealthGame
{
    /// <summary>
    /// Cầu nối giữa điều khiển cảm ứng và hệ thống input:
    ///  - Các nút / joystick cảm ứng ghi vào một "tay cầm ảo" (Gamepad của Input System),
    ///    nên toàn bộ gameplay (GameInput) hoạt động y hệt khi dùng tay cầm thật.
    ///  - Vuốt để xoay camera được cộng dồn vào LookDelta (CameraLookInput đọc mỗi frame).
    /// </summary>
    public static class TouchInput
    {
        public enum Button { Jump, Sprint, Crouch, Attack, Lantern, Interact, Potion, Journal, Pause, CameraNext }

        static Gamepad s_Pad;
        static GamepadState s_State;
        static bool s_Dirty;
        static Vector2 s_Look;
        static bool? s_ForcedByArgs;

        public static bool ForcedByArgs => Array.IndexOf(Environment.GetCommandLineArgs(), "-touch") >= 0;

        /// <summary>Điều khiển cảm ứng có đang bật không (tự động trên điện thoại hoặc theo cài đặt).</summary>
        public static bool Enabled
        {
            get
            {
                int mode = GameSettings.TouchControlsMode;   // 0 = tự động, 1 = bật, 2 = tắt
                if (mode == 1) return true;
                if (mode == 2) return false;
                if (s_ForcedByArgs == null)
                    s_ForcedByArgs = Array.IndexOf(Environment.GetCommandLineArgs(), "-touch") >= 0;
                return Application.isMobilePlatform || s_ForcedByArgs.Value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Pad = null;
            s_State = default;
            s_Look = Vector2.zero;
            s_ForcedByArgs = null;
        }

        static void EnsurePad()
        {
            if (s_Pad != null && s_Pad.added) return;
            s_Pad = InputSystem.AddDevice<Gamepad>("TouchGamepad");
            s_State = new GamepadState();
        }

        public static void SetStick(Vector2 value)
        {
            EnsurePad();
            value = Vector2.ClampMagnitude(value, 1f);
            if (s_State.leftStick == value) return;
            s_State.leftStick = value;
            s_Dirty = true;
        }

        public static void SetButton(Button button, bool pressed)
        {
            EnsurePad();
            if (button == Button.Attack)
            {
                // Cò phải là trục analog, không nằm trong bitmask nút
                float v = pressed ? 1f : 0f;
                if (Mathf.Approximately(s_State.rightTrigger, v)) return;
                s_State.rightTrigger = v;
                s_Dirty = true;
                return;
            }
            var b = Map(button);
            bool current = (s_State.buttons & (1u << (int)b)) != 0;
            if (current == pressed) return;
            s_State = s_State.WithButton(b, pressed);
            s_Dirty = true;
        }

        static GamepadButton Map(Button b) => b switch
        {
            Button.Jump => GamepadButton.South,
            Button.Crouch => GamepadButton.East,
            Button.Interact => GamepadButton.West,
            Button.Sprint => GamepadButton.LeftStick,
            Button.Attack => GamepadButton.RightTrigger,
            Button.Lantern => GamepadButton.LeftShoulder,
            Button.Potion => GamepadButton.DpadUp,
            Button.Journal => GamepadButton.Select,
            Button.Pause => GamepadButton.Start,
            Button.CameraNext => GamepadButton.RightStick,
            _ => GamepadButton.North,
        };

        /// <summary>Gửi trạng thái tay cầm ảo cho Input System (gọi mỗi frame từ TouchControls).</summary>
        public static void Flush()
        {
            if (!s_Dirty || s_Pad == null) return;
            s_Dirty = false;
            InputSystem.QueueStateEvent(s_Pad, s_State);
        }

        public static void ReleaseAll()
        {
            if (s_Pad == null) return;
            s_State = new GamepadState();
            s_Dirty = true;
            Flush();
        }

        public static void AddLook(Vector2 pixels) => s_Look += pixels;

        public static Vector2 ConsumeLook()
        {
            var v = s_Look;
            s_Look = Vector2.zero;
            return v;
        }
    }
}
