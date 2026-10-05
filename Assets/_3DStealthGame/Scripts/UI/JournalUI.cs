using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Nhật ký (Tab): hành trang, nhiệm vụ (kèm gợi ý), cốt truyện và bảng phím.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class JournalUI : MonoBehaviour
    {
        public static JournalUI Instance { get; private set; }

        VisualElement m_Overlay;
        VisualElement m_Items;
        VisualElement m_Quests;
        Label m_Story;
        bool m_Open;
        int m_OpenedFrame;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var t = UITheme.Current;
            var root = GetComponent<UIDocument>().rootVisualElement;
            m_Overlay = UIBuild.Overlay("Journal", root, 0.72f);
            UIBuild.ApplyDefaultFont(m_Overlay);

            var panel = UIBuild.Panel("JournalPanel", m_Overlay, 20, 28);
            panel.style.width = 1000;
            panel.style.maxHeight = Length.Percent(92);
            UIBuild.Heading("NHẬT KÝ CỦA JOHN LEMON", panel, 30);

            var cols = UIBuild.Row(panel);
            cols.style.alignItems = Align.FlexStart;

            var left = UIBuild.Box("Left", cols);
            left.style.width = Length.Percent(42);
            left.style.marginRight = 24;
            UIBuild.Text("HÀNH TRANG", 16, t.gold, left, bold: true).style.marginBottom = 8;
            m_Items = UIBuild.Box("Items", left);

            UIBuild.Text("ĐIỀU KHIỂN", 16, t.gold, left, bold: true).style.marginTop = 16;
            var controls = UIBuild.Text(
                "WASD: di chuyển     Shift: chạy     C/Ctrl: ngồi     Space: nhảy\n" +
                "Chuột trái: Linh Quang     F: bật/tắt đèn     H: uống bình máu\n" +
                "E: tương tác     V / 1-4: đổi camera     Tab: nhật ký     Esc: tạm dừng\n" +
                "Điện thoại: joystick trái di chuyển (đẩy hết = chạy), vuốt phải xoay camera, nút bên phải để hành động",
                14, t.muted, left);
            controls.style.marginTop = 6;

            var right = UIBuild.Box("Right", cols);
            right.style.flexGrow = 1;
            right.style.flexShrink = 1;
            UIBuild.Text("NHIỆM VỤ", 16, t.gold, right, bold: true).style.marginBottom = 8;
            m_Quests = UIBuild.Box("Quests", right);
            UIBuild.Text("CÂU CHUYỆN", 16, t.gold, right, bold: true).style.marginTop = 16;
            m_Story = UIBuild.Text("", 14, t.muted, right);
            m_Story.style.marginTop = 6;

            var close = UIBuild.MenuButton("Đóng (Tab)", panel, Close, 220);
            close.style.alignSelf = Align.Center;
            close.style.marginTop = 16;
        }

        public void Toggle()
        {
            if (m_Open) Close();
            else Open();
        }

        public void Open()
        {
            if (m_Open || m_Overlay == null) return;
            m_Open = true;
            m_OpenedFrame = Time.frameCount;
            Refresh();
            m_Overlay.style.display = DisplayStyle.Flex;
            AudioManager.Sfx(SoundId.UIOpen, 0.6f);
            if (GameSession.Instance != null) GameSession.Instance.PushUI();
        }

        public void Close()
        {
            if (!m_Open) return;
            m_Open = false;
            m_Overlay.style.display = DisplayStyle.None;
            if (GameSession.Instance != null) GameSession.Instance.PopUI();
        }

        void Update()
        {
            if (!m_Open || Time.frameCount == m_OpenedFrame) return;
            if ((GameInput.Journal != null && GameInput.Journal.WasPressedThisFrame()) ||
                (GameInput.Pause != null && GameInput.Pause.WasPressedThisFrame()))
                Close();
        }

        void Refresh()
        {
            var t = UITheme.Current;
            m_Items.Clear();
            var player = PlayerStats.Instance;
            var inv = player != null ? player.GetComponent<PlayerInventory>() : null;
            if (inv != null)
            {
                Item(t.relic, t.gold, $"Mảnh hồn: {inv.RelicCount}/{PlayerInventory.RelicsRequired}", "Đủ 3 mảnh để phá phong ấn cửa chính.");
                Item(t.shard, new Color(0.5f, 0.9f, 1f), $"Mảnh vụn linh hồn: {inv.Shards}", "Tiền tệ – đổi đồ với Ivan. Cũng cộng điểm.");
                Item(t.potion, new Color(1f, 0.4f, 0.45f), $"Bình máu: {inv.Potions}", $"Nhấn H để hồi {player.potionHeal:0} máu.");
                Item(t.lantern, t.energyColor, $"Năng lượng đèn: {player.Energy:0}/{player.maxEnergy:0}", "Linh Quang tốn 25. Nhặt dầu đèn (vàng) để nạp.");
                if (inv.HasKey("keyRed")) Item(t.key, new Color(1f, 0.3f, 0.3f), "Chìa khóa đỏ", "Mở Phong ấn Đỏ và cửa đỏ.");
                if (inv.HasKey("keyBlue")) Item(t.key, new Color(0.3f, 0.6f, 1f), "Chìa khóa xanh", "Mở Phong ấn Xanh và cửa xanh.");
            }

            m_Quests.Clear();
            var quests = QuestManager.Instance;
            if (quests != null)
            {
                quests.Evaluate();
                foreach (var s in quests.States)
                {
                    if (!s.visible) continue;
                    var line = UIBuild.Text((s.complete ? "[x] " : "[  ] ") + (s.objective.optional ? "(Phụ) " : "") + s.Text, 15,
                        s.complete ? t.muted : Color.white, m_Quests);
                    line.style.marginTop = 4;
                    if (!s.complete && !string.IsNullOrEmpty(s.objective.hint))
                    {
                        var hint = UIBuild.Text("      " + s.objective.hint, 13, t.muted, m_Quests);
                        hint.style.unityFontStyleAndWeight = FontStyle.Italic;
                    }
                }
                m_Story.text = quests.quest != null ? quests.quest.story : "";
            }
        }

        void Item(Texture2D icon, Color tint, string title, string desc)
        {
            var row = UIBuild.Row(m_Items);
            row.style.marginBottom = 8;
            row.style.alignItems = Align.FlexStart;
            UIBuild.Icon(icon, 28, tint, row).style.marginRight = 10;
            var col = UIBuild.Box("Col", row);
            UIBuild.Text(title, 16, Color.white, col, bold: true);
            UIBuild.Text(desc, 13, UITheme.Current.muted, col);
        }
    }
}
