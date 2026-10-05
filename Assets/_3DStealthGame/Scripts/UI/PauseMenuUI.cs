using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Menu tạm dừng (Esc): tiếp tục, nhật ký, cài đặt, hồi sinh tại checkpoint, về menu, thoát.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuUI : MonoBehaviour
    {
        public static PauseMenuUI Instance { get; private set; }

        VisualElement m_Overlay;
        SettingsPanel m_Settings;
        ConfirmDialog m_Confirm;
        Label m_Info;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            var layer = UIBuild.Box("PauseLayer", root);
            UIBuild.Fill(layer);
            layer.pickingMode = PickingMode.Ignore;
            UIBuild.ApplyDefaultFont(layer);

            m_Overlay = UIBuild.Overlay("Pause", layer, 0.7f);
            var panel = UIBuild.Panel("PausePanel", m_Overlay, 24, 36);
            panel.style.alignItems = Align.Center;
            UIBuild.Heading("TẠM DỪNG", panel, 34);
            m_Info = UIBuild.Text("", 15, UITheme.Current.muted, panel);
            m_Info.style.marginBottom = 12;
            m_Info.style.unityTextAlign = TextAnchor.MiddleCenter;

            UIBuild.MenuButton("Tiếp tục", panel, Resume);
            UIBuild.MenuButton("Nhật ký & hành trang", panel, () =>
            {
                Resume();
                if (JournalUI.Instance != null) JournalUI.Instance.Toggle();
            });
            UIBuild.MenuButton("Cài đặt", panel, () => m_Settings.Show());
            UIBuild.MenuButton("Hồi sinh tại checkpoint", panel, () =>
                m_Confirm.Show("Quay lại checkpoint gần nhất?\nTiến trình sau checkpoint sẽ mất.", () => GameSession.Instance.RespawnAtCheckpoint()));
            UIBuild.MenuButton("Về menu chính", panel, () =>
                m_Confirm.Show("Về menu chính?\nBạn có thể Tiếp tục từ checkpoint gần nhất.", () => GameSession.Instance.QuitToMenu()));
            UIBuild.MenuButton("Thoát game", panel, () =>
                m_Confirm.Show("Thoát game?\nTiến trình tại checkpoint đã được lưu.", GameServices.Quit));

            m_Settings = new SettingsPanel(layer, null);
            m_Confirm = new ConfirmDialog(layer);
        }

        public bool IsOpen => m_Overlay != null && m_Overlay.style.display == DisplayStyle.Flex;

        public void Show()
        {
            if (m_Overlay == null) return;
            var s = GameSession.Instance;
            if (s != null)
                m_Info.text = $"Độ khó: {s.Profile.displayName}   •   Giờ trong game: {s.ClockText}   •   Điểm: {s.Score}";
            m_Overlay.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (m_Overlay == null) return;
            m_Overlay.style.display = DisplayStyle.None;
            m_Settings.Root.style.display = DisplayStyle.None;
            m_Confirm.Hide();
        }

        /// <summary>Esc khi đang pause: đóng bảng con trước, sau đó tiếp tục chơi.</summary>
        public void Back()
        {
            if (m_Confirm.IsOpen) m_Confirm.Hide();
            else if (m_Settings.IsOpen) m_Settings.Close();
            else Resume();
        }

        void Resume()
        {
            if (GameSession.Instance != null) GameSession.Instance.SetPaused(false);
            else Hide();
        }
    }
}
