using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>
    /// Điều khiển cảm ứng cho Android:
    ///  - Nửa trái: joystick động (chạm ở đâu, joystick hiện ở đó). Đẩy hết cỡ = chạy.
    ///  - Nửa phải: vuốt để xoay camera.
    ///  - Nút: Linh Quang (lớn), Nhảy, Ngồi, Tương tác, Đèn, Bình máu; Tạm dừng / Nhật ký / Camera ở góc trên.
    /// Mọi nút ghi vào tay cầm ảo (TouchInput) nên gameplay không cần sửa.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }

        public float joystickRadius = 90f;
        public float sprintThreshold = 0.92f;
        public float lookSensitivity = 1.6f;

        VisualElement m_Root;
        VisualElement m_SafeArea;
        VisualElement m_StickBase, m_StickKnob;
        VisualElement m_InteractButton;
        Label m_InteractLabel, m_PotionCount;
        VisualElement m_CrouchButton, m_LanternButton;

        int m_StickPointer = -1;
        Vector2 m_StickOrigin;
        Vector2 m_StickValue;
        int m_LookPointer = -1;
        Vector2 m_LastLook;
        Rect m_LastSafeArea;
        readonly List<(VisualElement el, TouchInput.Button button)> m_Buttons = new();

        public bool IsVisible => m_Root != null && m_Root.style.display == DisplayStyle.Flex;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            TouchInput.ReleaseAll();
        }

        void Start()
        {
            var doc = GetComponent<UIDocument>();
            Build(doc.rootVisualElement);
        }

        // ------------------------------------------------------------------ Dựng giao diện

        void Build(VisualElement docRoot)
        {
            var t = UITheme.Current;
            m_Root = UIBuild.Box("TouchRoot", docRoot);
            UIBuild.Fill(m_Root);
            m_Root.pickingMode = PickingMode.Ignore;
            UIBuild.ApplyDefaultFont(m_Root);

            m_SafeArea = UIBuild.Box("SafeArea", m_Root);
            UIBuild.Fill(m_SafeArea);
            m_SafeArea.pickingMode = PickingMode.Ignore;

            // Vùng vuốt camera (nửa phải) – đặt trước để các nút nằm đè lên trên
            var lookZone = UIBuild.Box("LookZone", m_SafeArea);
            UIBuild.Absolute(lookZone, null, 0, 0, 0);
            lookZone.style.width = Length.Percent(58);
            lookZone.RegisterCallback<PointerDownEvent>(OnLookDown);
            lookZone.RegisterCallback<PointerMoveEvent>(OnLookMove);
            lookZone.RegisterCallback<PointerUpEvent>(OnLookUp);
            lookZone.RegisterCallback<PointerCancelEvent>(_ => m_LookPointer = -1);

            // Vùng joystick (nửa trái, dưới thanh chỉ số)
            var stickZone = UIBuild.Box("StickZone", m_SafeArea);
            UIBuild.Absolute(stickZone, 0, 170, null, 0);
            stickZone.style.width = Length.Percent(42);
            stickZone.RegisterCallback<PointerDownEvent>(OnStickDown);
            stickZone.RegisterCallback<PointerMoveEvent>(OnStickMove);
            stickZone.RegisterCallback<PointerUpEvent>(OnStickUp);
            stickZone.RegisterCallback<PointerCancelEvent>(_ => ResetStick());

            m_StickBase = UIBuild.Box("StickBase", stickZone);
            m_StickBase.style.position = Position.Absolute;
            m_StickBase.style.width = m_StickBase.style.height = joystickRadius * 2f;
            m_StickBase.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            UIBuild.Border(m_StickBase, 3, new Color(1f, 1f, 1f, 0.35f), joystickRadius);
            m_StickBase.pickingMode = PickingMode.Ignore;
            m_StickKnob = UIBuild.Box("StickKnob", m_StickBase);
            m_StickKnob.style.position = Position.Absolute;
            m_StickKnob.style.width = m_StickKnob.style.height = joystickRadius;
            m_StickKnob.style.backgroundColor = new Color(1f, 1f, 1f, 0.35f);
            UIBuild.Border(m_StickKnob, 2, new Color(t.gold.r, t.gold.g, t.gold.b, 0.8f), joystickRadius * 0.5f);
            m_StickKnob.pickingMode = PickingMode.Ignore;
            ResetStick();

            // Nút hành động (góc dưới phải)
            var attack = RoundButton("LINH\nQUANG", t.lantern, t.energyColor, 150f, TouchInput.Button.Attack);
            Place(attack, right: 40, bottom: 50);
            Place(RoundButton("NHẢY", null, Color.white, 96f, TouchInput.Button.Jump), right: 210, bottom: 30);
            m_CrouchButton = RoundButton("NGỒI", null, new Color(0.6f, 0.85f, 1f), 96f, TouchInput.Button.Crouch);
            Place(m_CrouchButton, right: 205, bottom: 145);
            m_InteractButton = RoundButton("DÙNG", null, t.gold, 110f, TouchInput.Button.Interact);
            m_InteractLabel = m_InteractButton.Q<Label>();
            Place(m_InteractButton, right: 70, bottom: 225);
            m_LanternButton = RoundButton("ĐÈN", t.lantern, t.energyColor, 84f, TouchInput.Button.Lantern);
            Place(m_LanternButton, right: 320, bottom: 40);
            var potion = RoundButton("MÁU", t.potion, new Color(1f, 0.45f, 0.5f), 84f, TouchInput.Button.Potion);
            Place(potion, right: 315, bottom: 145);
            m_PotionCount = UIBuild.Text("0", 15, Color.white, potion, bold: true);
            UIBuild.Absolute(m_PotionCount, null, -2, -2);
            m_PotionCount.style.backgroundColor = new Color(0.7f, 0.1f, 0.15f, 0.9f);
            UIBuild.Padding(m_PotionCount, 1, 7);
            UIBuild.Border(m_PotionCount, 0, Color.clear, 9);

            // Nút hệ thống (dưới thanh chỉ số, góc trên trái)
            var sys = UIBuild.Row(m_SafeArea);
            UIBuild.Absolute(sys, 16, 112);
            foreach (var (label, button) in new[] { ("II", TouchInput.Button.Pause), ("NHẬT KÝ", TouchInput.Button.Journal), ("CAMERA", TouchInput.Button.CameraNext) })
            {
                var b = PillButton(label, button);
                b.style.marginRight = 8;
                sys.Add(b);
            }

            m_Root.style.display = DisplayStyle.None;
        }

        VisualElement RoundButton(string text, Texture2D icon, Color tint, float size, TouchInput.Button button)
        {
            var b = UIBuild.Box("Btn_" + button, m_SafeArea);
            b.style.position = Position.Absolute;
            b.style.width = b.style.height = size;
            b.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.55f);
            UIBuild.Border(b, 3, new Color(tint.r, tint.g, tint.b, 0.75f), size * 0.5f);
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            if (icon != null)
            {
                var i = UIBuild.Icon(icon, size * 0.34f, tint, b);
                i.style.marginBottom = 2;
            }
            var l = UIBuild.Text(text, Mathf.RoundToInt(size * 0.15f), Color.white, b, bold: true);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            Hook(b, button);
            return b;
        }

        VisualElement PillButton(string text, TouchInput.Button button)
        {
            var b = UIBuild.Box("Pill_" + button, null);
            b.style.height = 52;
            b.style.minWidth = 64;
            b.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.6f);
            UIBuild.Border(b, 2, new Color(1f, 1f, 1f, 0.3f), 26);
            UIBuild.Padding(b, 0, 16);
            b.style.justifyContent = Justify.Center;
            b.style.alignItems = Align.Center;
            var l = UIBuild.Text(text, 16, Color.white, b, bold: true);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            Hook(b, button);
            return b;
        }

        static void Place(VisualElement e, float right, float bottom)
        {
            e.style.right = right;
            e.style.bottom = bottom;
        }

        void Hook(VisualElement b, TouchInput.Button button)
        {
            m_Buttons.Add((b, button));
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                b.CapturePointer(e.pointerId);
                TouchInput.SetButton(button, true);
                b.style.scale = new Scale(new Vector3(0.9f, 0.9f, 1f));
                e.StopPropagation();
            });
            void Release(int pointerId)
            {
                if (b.HasPointerCapture(pointerId)) b.ReleasePointer(pointerId);
                TouchInput.SetButton(button, false);
                b.style.scale = new Scale(Vector3.one);
            }
            b.RegisterCallback<PointerUpEvent>(e => { Release(e.pointerId); e.StopPropagation(); });
            b.RegisterCallback<PointerCancelEvent>(e => Release(e.pointerId));
        }

        // ------------------------------------------------------------------ Joystick

        void OnStickDown(PointerDownEvent e)
        {
            if (m_StickPointer >= 0) return;
            m_StickPointer = e.pointerId;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            m_StickOrigin = e.localPosition;
            m_StickBase.style.bottom = StyleKeyword.Auto;
            m_StickBase.style.left = m_StickOrigin.x - joystickRadius;
            m_StickBase.style.top = m_StickOrigin.y - joystickRadius;
            m_StickBase.style.opacity = 1f;
            UpdateStick(e.localPosition);
        }

        void OnStickMove(PointerMoveEvent e)
        {
            if (e.pointerId == m_StickPointer) UpdateStick(e.localPosition);
        }

        void OnStickUp(PointerUpEvent e)
        {
            if (e.pointerId != m_StickPointer) return;
            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
            ResetStick();
        }

        void UpdateStick(Vector2 local)
        {
            Vector2 d = local - m_StickOrigin;
            Vector2 clamped = Vector2.ClampMagnitude(d, joystickRadius);
            m_StickKnob.style.left = joystickRadius * 0.5f + clamped.x;
            m_StickKnob.style.top = joystickRadius * 0.5f + clamped.y;
            m_StickValue = new Vector2(clamped.x, -clamped.y) / joystickRadius;
        }

        void ResetStick()
        {
            m_StickPointer = -1;
            m_StickValue = Vector2.zero;
            // Vị trí nghỉ gợi ý
            m_StickOrigin = new Vector2(150f, 0f);
            if (m_StickBase == null) return;
            m_StickBase.style.left = 70;
            m_StickBase.style.top = StyleKeyword.Auto;
            m_StickBase.style.bottom = 60;
            m_StickBase.style.opacity = 0.45f;
            m_StickKnob.style.left = joystickRadius * 0.5f;
            m_StickKnob.style.top = joystickRadius * 0.5f;
        }

        // ------------------------------------------------------------------ Vuốt camera

        void OnLookDown(PointerDownEvent e)
        {
            if (m_LookPointer >= 0) return;
            m_LookPointer = e.pointerId;
            ((VisualElement)e.currentTarget).CapturePointer(e.pointerId);
            m_LastLook = e.position;
        }

        void OnLookMove(PointerMoveEvent e)
        {
            if (e.pointerId != m_LookPointer) return;
            Vector2 p = e.position;
            Vector2 d = p - m_LastLook;
            m_LastLook = p;
            // Toạ độ panel: y hướng xuống → đảo dấu
            TouchInput.AddLook(new Vector2(d.x, -d.y) * lookSensitivity);
        }

        void OnLookUp(PointerUpEvent e)
        {
            if (e.pointerId != m_LookPointer) return;
            ((VisualElement)e.currentTarget).ReleasePointer(e.pointerId);
            m_LookPointer = -1;
        }

        // ------------------------------------------------------------------ Cập nhật

        void Update()
        {
            if (m_Root == null) return;

            var session = GameSession.Instance;
            var player = PlayerStats.Instance;
            bool show = TouchInput.Enabled && session != null && session.CurrentPhase == GameSession.Phase.Playing
                        && !session.IsUIOpen && !DialogueManager.IsDialogueActive && player != null && player.IsAlive;
            var display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (m_Root.style.display != display)
            {
                m_Root.style.display = display;
                if (!show)
                {
                    ResetStick();
                    m_LookPointer = -1;
                    TouchInput.ReleaseAll();
                }
            }

            ApplySafeArea();

            if (show)
            {
                TouchInput.SetStick(m_StickValue);
                TouchInput.SetButton(TouchInput.Button.Sprint, m_StickValue.magnitude >= sprintThreshold);

                var interactor = player.GetComponent<PlayerInteractor>();
                bool canUse = interactor != null && interactor.Current != null;
                m_InteractButton.style.opacity = canUse ? 1f : 0.35f;
                var inv = player.GetComponent<PlayerInventory>();
                if (inv != null) m_PotionCount.text = inv.PotionsLabel;
                var move = player.GetComponent<PlayerMovement>();
                m_CrouchButton.style.backgroundColor = move != null && move.IsCrouching
                    ? new Color(0.2f, 0.4f, 0.6f, 0.75f) : new Color(0.05f, 0.05f, 0.1f, 0.55f);
                var combat = player.GetComponent<PlayerCombat>();
                m_LanternButton.style.backgroundColor = combat != null && combat.IsLanternOn
                    ? new Color(0.5f, 0.4f, 0.1f, 0.75f) : new Color(0.05f, 0.05f, 0.1f, 0.55f);
            }
            TouchInput.Flush();
        }

        // Bỏ phần vuốt chưa dùng (ví dụ camera hiện tại không xoay được)
        void LateUpdate() => TouchInput.ConsumeLook();

        /// <summary>Chừa vùng tai thỏ / camera đục lỗ (Screen.safeArea).</summary>
        void ApplySafeArea()
        {
            // Chỉ cần trên điện thoại (tai thỏ / camera đục lỗ / thanh điều hướng)
            if (!Application.isMobilePlatform && !TouchInput.ForcedByArgs) return;
            var safe = Screen.safeArea;
            if (safe == m_LastSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            var size = m_Root.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0f || size.y <= 0f) return;
            m_LastSafeArea = safe;

            float sx = size.x / Screen.width, sy = size.y / Screen.height;
            float Inset(float px, float max, float scale) => Mathf.Clamp(px, 0f, max) * scale;
            m_SafeArea.style.left = Inset(safe.xMin, Screen.width * 0.15f, sx);
            m_SafeArea.style.right = Inset(Screen.width - safe.xMax, Screen.width * 0.15f, sx);
            m_SafeArea.style.top = Inset(Screen.height - safe.yMax, Screen.height * 0.15f, sy);
            m_SafeArea.style.bottom = Inset(safe.yMin, Screen.height * 0.15f, sy);
            Debug.Log($"[Touch] safeArea={safe} screen={Screen.width}x{Screen.height} panel={size} insets L{m_SafeArea.style.left.value.value:0} R{m_SafeArea.style.right.value.value:0} T{m_SafeArea.style.top.value.value:0} B{m_SafeArea.style.bottom.value.value:0}");
        }

        /// <summary>Dùng cho kiểm thử tự động: đẩy joystick theo hướng chỉ định.</summary>
        public void DebugSetStick(Vector2 value) => m_StickValue = Vector2.ClampMagnitude(value, 1f);
    }
}
