using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Màn hình GAME OVER (bị bắt / bình minh) và CHIẾN THẮNG (thống kê, xếp hạng, kỷ lục).</summary>
    [RequireComponent(typeof(UIDocument))]
    public class EndScreenUI : MonoBehaviour
    {
        public static EndScreenUI Instance { get; private set; }

        VisualElement m_Root;
        VisualElement m_Background;
        Label m_Title, m_Subtitle, m_Rank;
        VisualElement m_Stats;
        VisualElement m_Buttons;
        float m_FadeT;
        bool m_Showing;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            m_Root = UIBuild.Overlay("EndScreen", root, 0.0f);
            UIBuild.ApplyDefaultFont(m_Root);
            m_Root.style.backgroundColor = new Color(0f, 0f, 0f, 0.88f);

            m_Background = UIBuild.Box("Bg", m_Root);
            UIBuild.Fill(m_Background);
            m_Background.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            m_Background.style.opacity = 0.35f;

            var panel = UIBuild.Panel("EndPanel", m_Root, 24, 40);
            panel.style.alignItems = Align.Center;
            panel.style.minWidth = 560;
            m_Title = UIBuild.Heading("", panel, 44);
            m_Subtitle = UIBuild.Text("", 18, Color.white, panel);
            m_Subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_Subtitle.style.marginBottom = 14;
            m_Rank = UIBuild.Text("", 72, UITheme.Current.gold, panel, bold: true);
            m_Stats = UIBuild.Box("Stats", panel);
            m_Stats.style.width = 460;
            m_Stats.style.marginBottom = 14;
            m_Buttons = UIBuild.Box("Buttons", panel);
            m_Buttons.style.alignItems = Align.Center;
        }

        void Update()
        {
            if (!m_Showing) return;
            m_FadeT = Mathf.MoveTowards(m_FadeT, 1f, Time.unscaledDeltaTime * 1.5f);
            m_Root.style.opacity = m_FadeT;
        }

        void Open()
        {
            m_Showing = true;
            m_FadeT = 0f;
            m_Root.style.opacity = 0f;
            m_Root.style.display = DisplayStyle.Flex;
            m_Stats.Clear();
            m_Buttons.Clear();
        }

        void Stat(string label, string value, bool highlight = false)
        {
            var row = UIBuild.Row(m_Stats);
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 3;
            UIBuild.Text(label, 17, UITheme.Current.muted, row);
            UIBuild.Text(value, 17, highlight ? UITheme.Current.gold : Color.white, row, bold: true);
        }

        static string FormatTime(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";

        public void ShowGameOver(GameSession.EndReason reason)
        {
            if (m_Root == null) return;
            Open();
            var s = GameSession.Instance;
            var theme = UITheme.Current;
            m_Background.style.backgroundImage = theme.caughtScreen != null ? new StyleBackground(theme.caughtScreen) : StyleKeyword.None;
            m_Title.text = reason == GameSession.EndReason.Dawn ? "BÌNH MINH ĐÃ ĐẾN" : "BẠN ĐÃ BỊ BẮT";
            m_Title.style.color = theme.danger;
            m_Subtitle.text = reason == GameSession.EndReason.Dawn
                ? "Dinh thự tan biến cùng ánh nắng – John Lemon mãi mãi bị giam cầm."
                : "Lũ ma đã tóm được John Lemon...";
            m_Rank.text = "";

            Stat("Thời gian sống sót", FormatTime(s.Elapsed));
            Stat("Mảnh hồn", $"{PlayerStats.Instance.GetComponent<PlayerInventory>().RelicCount}/{PlayerInventory.RelicsRequired}");
            Stat("Ma đã trục xuất", s.Banished.ToString());
            Stat("Số lần chết", s.Deaths.ToString());
            Stat("Điểm hiện tại", s.Score.ToString(), true);

            if (reason == GameSession.EndReason.Caught && SaveSystem.HasSave)
                UIBuild.MenuButton("Hồi sinh tại checkpoint", m_Buttons, () => s.RespawnAtCheckpoint());
            UIBuild.MenuButton("Chơi lại từ đầu", m_Buttons, () => s.RestartNewGame());
            UIBuild.MenuButton("Về menu chính", m_Buttons, () => s.QuitToMenu());
        }

        public void ShowVictory(GameSession.RunResult r)
        {
            if (m_Root == null) return;
            Open();
            var s = GameSession.Instance;
            var theme = UITheme.Current;
            m_Background.style.backgroundImage = theme.wonScreen != null ? new StyleBackground(theme.wonScreen) : StyleKeyword.None;
            m_Title.text = "THOÁT KHỎI DINH THỰ!";
            m_Title.style.color = theme.gold;
            m_Subtitle.text = $"John Lemon đã thoát ra trước bình minh.  Độ khó: {s.Profile.displayName}";
            m_Rank.text = "HẠNG " + r.rank;

            Stat("Thời gian", FormatTime(s.Elapsed));
            Stat("Điểm thu thập", r.baseScore.ToString());
            Stat("Thưởng thời gian", "+" + r.timeBonus);
            Stat("Thưởng lén lút", "+" + r.stealthBonus);
            Stat($"Số lần chết ({s.Deaths})", "-" + r.deathPenalty);
            Stat("Bị phát hiện", s.Detections + " lần");
            Stat("Ma đã trục xuất", s.Banished.ToString());
            Stat("TỔNG ĐIỂM", r.total + (r.newRecord ? "  (KỶ LỤC MỚI!)" : ""), true);

            UIBuild.MenuButton("Chơi lại", m_Buttons, () => s.RestartNewGame());
            UIBuild.MenuButton("Về menu chính", m_Buttons, () => s.QuitToMenu());
        }
    }
}
