using UnityEngine;
using UnityEngine.InputSystem;

namespace StealthGame
{
    /// <summary>
    /// Toàn bộ phím điều khiển gameplay khai báo một chỗ (bàn phím + chuột + tay cầm).
    /// Các hệ thống đọc qua GameInput thay vì đọc thiết bị trực tiếp.
    /// </summary>
    public static class GameInput
    {
        static InputActionMap s_Map;

        public static InputAction Move { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Crouch { get; private set; }
        public static InputAction Attack { get; private set; }
        public static InputAction Lantern { get; private set; }
        public static InputAction Interact { get; private set; }
        public static InputAction UsePotion { get; private set; }
        public static InputAction Journal { get; private set; }
        public static InputAction Pause { get; private set; }
        public static InputAction CameraNext { get; private set; }

        /// <summary>False khi đang ở menu / hội thoại / pause → nhân vật không phản hồi.</summary>
        public static bool GameplayEnabled { get; set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Map?.Disable();
            s_Map = null;
            GameplayEnabled = true;
        }

        public static void EnsureCreated()
        {
            if (s_Map != null) return;
            s_Map = new InputActionMap("Gameplay");

            Move = s_Map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            Sprint = Button("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            Crouch = Button("Crouch", "<Keyboard>/c", "<Gamepad>/buttonEast");
            Crouch.AddBinding("<Keyboard>/leftCtrl");
            Attack = Button("Attack", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            Lantern = Button("Lantern", "<Keyboard>/f", "<Gamepad>/leftShoulder");
            Interact = Button("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            UsePotion = Button("UsePotion", "<Keyboard>/h", "<Gamepad>/dpad/up");
            Journal = Button("Journal", "<Keyboard>/tab", "<Gamepad>/select");
            Journal.AddBinding("<Keyboard>/j");
            Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");
            Pause.AddBinding("<Keyboard>/p");
            CameraNext = Button("CameraNext", "<Keyboard>/v", "<Gamepad>/rightStickPress");

            s_Map.Enable();
        }

        static InputAction Button(string name, string key, string pad)
        {
            var a = s_Map.AddAction(name, InputActionType.Button);
            a.AddBinding(key);
            a.AddBinding(pad);
            return a;
        }

        // Tiện ích: chỉ trả về true khi gameplay đang hoạt động
        public static bool Pressed(InputAction action) => GameplayEnabled && action != null && action.WasPressedThisFrame();
        public static bool Held(InputAction action) => GameplayEnabled && action != null && action.IsPressed();
        public static Vector2 MoveValue => GameplayEnabled && Move != null ? Move.ReadValue<Vector2>() : Vector2.zero;
    }
}
