using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Menu chính: Tiếp tục / Chơi mới (chọn độ khó) / Hướng dẫn / Cài đặt / Thành tích / Thoát.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        VisualElement m_Main;
        VisualElement m_Difficulty;
        VisualElement m_HowTo;
        VisualElement m_Records;
        SettingsPanel m_Settings;
        ConfirmDialog m_Confirm;
        Button m_Continue;
        Label m_ContinueInfo;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            GameInput.GameplayEnabled = false;
            AudioManager.Music(SoundId.MusicMenu, 2f);

            var theme = UITheme.Current;
            var root = GetComponent<UIDocument>().rootVisualElement;
            var layer = UIBuild.Box("MenuLayer", root);
            UIBuild.Fill(layer);
            UIBuild.ApplyDefaultFont(layer);

            // Làm tối nền cảnh 3D bên trái để chữ dễ đọc
            var shade = UIBuild.Box("Shade", layer);
            UIBuild.Absolute(shade, 0, 0, null, 0);
            shade.style.width = Length.Percent(48);
            shade.style.backgroundColor = new Color(0f, 0f, 0.03f, 0.72f);

            // --- Menu chính
            m_Main = UIBuild.Box("Main", layer);
            UIBuild.Absolute(m_Main, 70, 0, null, 0);
            m_Main.style.justifyContent = Justify.Center;

            var title = UIBuild.Text("LEMON'S ESCAPE", 64, theme.gold, m_Main, bold: true);
            UIBuild.ApplyTitleFont(title);
            var sub = UIBuild.Text("Thoát khỏi dinh thự ma ám", 22, Color.white, m_Main);
            sub.style.marginBottom = 30;

            m_Continue = UIBuild.MenuButton("Tiếp tục", m_Main, GameServices.ContinueGame);
            m_ContinueInfo = UIBuild.Text("", 14, theme.muted, m_Main);
            m_ContinueInfo.style.marginBottom = 4;
            UIBuild.MenuButton("Chơi mới", m_Main, OnNewGame);
            UIBuild.MenuButton("Hướng dẫn chơi", m_Main, () => m_HowTo.style.display = DisplayStyle.Flex);
            UIBuild.MenuButton("Cài đặt", m_Main, () => m_Settings.Show());
            UIBuild.MenuButton("Thành tích", m_Main, ShowRecords);
            UIBuild.MenuButton("Thoát", m_Main, GameServices.Quit);

            var footer = UIBuild.Text("Đồ án môn học Phát triển game Android • Unity 6 + URP + Cinemachine 3", 13, theme.muted, layer);
            UIBuild.Absolute(footer, 70, null, null, 18);

            BuildDifficulty(layer);
            BuildHowTo(layer);
            BuildRecords(layer);
            m_Settings = new SettingsPanel(layer, null);
            m_Confirm = new ConfirmDialog(layer);

            RefreshContinue();
        }

        void RefreshContinue()
        {
            var save = SaveSystem.HasSave ? SaveSystem.Load() : null;
            m_Continue.SetEnabled(save != null);
            m_Continue.style.opacity = save != null ? 1f : 0.4f;
            m_ContinueInfo.text = save != null
                ? $"{DifficultyProfile.Get(save.difficulty).displayName} • Mảnh hồn {save.relics.Count}/3 • Lưu lúc {save.savedAt}"
                : "Chưa có dữ liệu lưu";
        }

        void OnNewGame()
        {
            if (SaveSystem.HasSave)
                m_Confirm.Show("Bắt đầu lượt mới sẽ xóa dữ liệu đang lưu. Tiếp tục?", () => m_Difficulty.style.display = DisplayStyle.Flex);
            else
                m_Difficulty.style.display = DisplayStyle.Flex;
        }

        void BuildDifficulty(VisualElement layer)
        {
            m_Difficulty = UIBuild.Overlay("Difficulty", layer, 0.6f);
            var panel = UIBuild.Panel("DifficultyPanel", m_Difficulty, 24, 32);
            panel.style.alignItems = Align.Center;
            UIBuild.Heading("CHỌN ĐỘ KHÓ", panel);
            foreach (Difficulty d in DifficultyProfile.MenuOrder)
            {
                var p = DifficultyProfile.Get(d);
                var difficulty = d;
                UIBuild.MenuButton(p.displayName, panel, () => GameServices.StartNewGame(difficulty), 380);
                var desc = UIBuild.Text(p.description, 14, UITheme.Current.muted, panel);
                desc.style.marginBottom = 8;
            }
            UIBuild.MenuButton("Quay lại", panel, () => m_Difficulty.style.display = DisplayStyle.None, 200).style.marginTop = 10;
        }

        void BuildHowTo(VisualElement layer)
        {
            var theme = UITheme.Current;
            m_HowTo = UIBuild.Overlay("HowTo", layer, 0.7f);
            var panel = UIBuild.Panel("HowToPanel", m_HowTo, 22, 30);
            panel.style.width = 900;
            UIBuild.Heading("HƯỚNG DẪN CHƠI", panel);

            void Block(Texture2D icon, Color tint, string head, string body)
            {
                var row = UIBuild.Row(panel);
                row.style.alignItems = Align.FlexStart;
                row.style.marginBottom = 10;
                UIBuild.Icon(icon, 30, tint, row).style.marginRight = 12;
                var col = UIBuild.Box("Col", row);
                col.style.flexShrink = 1;
                UIBuild.Text(head, 17, Color.white, col, bold: true);
                UIBuild.Text(body, 14, theme.muted, col);
            }

            Block(theme.relic, theme.gold, "Mục tiêu",
                "Tìm 3 Mảnh hồn trong dinh thự, phá phong ấn cửa chính ở Cánh Đông và thoát ra trước 06:00 sáng.");
            Block(theme.eye, new Color(1f, 0.85f, 0.3f), "Lén lút",
                "Ma có tầm nhìn hình nón (vùng sáng trên sàn). Ngồi xuống (C) để khó bị thấy, tránh chạy vì gây tiếng động. Tượng Gargoyle sẽ báo động nếu thấy bạn.");
            Block(theme.lantern, theme.energyColor, "Đèn lồng linh hồn",
                "Chuột trái: Linh Quang làm choáng và gây sát thương lên ma (tốn 25 năng lượng). Đánh từ phía sau gây x2. F: bật đèn soi – nhìn rõ hơn nhưng ma thấy bạn từ xa hơn.");
            Block(theme.key, new Color(0.4f, 0.7f, 1f), "Chìa khóa & phong ấn",
                "Chìa khóa đỏ / xanh mở phong ấn cùng màu đang bảo vệ Mảnh hồn. Nói chuyện với Ivan và Whisper để nhận gợi ý và trợ giúp.");
            Block(theme.checkpoint, new Color(1f, 0.6f, 0.3f), "Checkpoint",
                "Đi qua ngọn nến để thắp – game tự lưu. Chết sẽ hồi sinh tại ngọn nến gần nhất.");
            var keys = UIBuild.Text(
                "WASD di chuyển • Shift chạy • C ngồi • Space nhảy • Chuột: xoay camera • Chuột trái: Linh Quang • F: đèn • E: tương tác • H: bình máu • V/1-4: camera • Tab: nhật ký • Esc: tạm dừng",
                14, theme.gold, panel);
            keys.style.marginTop = 6;
            var back = UIBuild.MenuButton("Đóng", panel, () => m_HowTo.style.display = DisplayStyle.None, 200);
            back.style.alignSelf = Align.Center;
            back.style.marginTop = 12;
        }

        void BuildRecords(VisualElement layer)
        {
            m_Records = UIBuild.Overlay("Records", layer, 0.7f);
        }

        void ShowRecords()
        {
            m_Records.Clear();
            var panel = UIBuild.Panel("RecordsPanel", m_Records, 22, 30);
            panel.style.width = 460;
            panel.style.alignItems = Align.Center;
            UIBuild.Heading("THÀNH TÍCH", panel);
            var r = SaveSystem.LoadRecord();
            UIBuild.Text($"Số lần thoát thành công: {r.wins}", 18, Color.white, panel);
            UIBuild.Text($"Điểm cao nhất: {r.bestScore}   (Hạng {r.bestRank})", 18, Color.white, panel);
            UIBuild.Text(r.bestTime > 0 ? $"Thời gian nhanh nhất: {(int)r.bestTime / 60:00}:{(int)r.bestTime % 60:00}" : "Thời gian nhanh nhất: --:--", 18, Color.white, panel);
            UIBuild.MenuButton("Đóng", panel, () => m_Records.style.display = DisplayStyle.None, 200).style.marginTop = 16;
            m_Records.style.display = DisplayStyle.Flex;
        }

        void Update()
        {
            if (GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame())
            {
                if (m_Confirm.IsOpen) m_Confirm.Hide();
                else if (m_Settings.IsOpen) m_Settings.Close();
                m_Difficulty.style.display = DisplayStyle.None;
                m_HowTo.style.display = DisplayStyle.None;
                m_Records.style.display = DisplayStyle.None;
            }
        }
    }
}
