using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>
    /// Khung hội thoại kiểu điện ảnh: viền đen trên/dưới (letterbox), bảng thoại,
    /// hiệu ứng gõ chữ (typewriter) và danh sách lựa chọn.
    /// Chỉ hiển thị – mọi logic nằm ở DialogueManager.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DialogueUI : MonoBehaviour
    {
        public struct ChoiceView
        {
            public string text;
            public bool locked;
            public string lockedHint;
        }

        public float charactersPerSecond = 45f;
        public Color npcNameColor = new(0.95f, 0.78f, 0.35f);
        public Color playerNameColor = new(0.55f, 0.85f, 1f);

        public event Action<int> ChoiceSelected;
        public event Action AdvanceRequested;

        public bool IsTyping => m_Typing;

        VisualElement m_Root;
        VisualElement m_TopBar;
        VisualElement m_BottomBar;
        VisualElement m_Panel;
        Label m_Speaker;
        Label m_Text;
        Label m_ContinueHint;
        VisualElement m_Choices;

        string m_FullText = string.Empty;
        float m_VisibleChars;
        bool m_Typing;
        bool m_IsEndNode;
        readonly List<ChoiceView> m_PendingChoices = new();

        void Start()
        {
            var doc = GetComponent<UIDocument>();
            Build(doc.rootVisualElement);
            Close();
        }

        void Build(VisualElement docRoot)
        {
            m_Root = UIBuild.Box("DialogueRoot", docRoot);
            UIBuild.Fill(m_Root);
            m_Root.pickingMode = PickingMode.Ignore;
            UIBuild.ApplyDefaultFont(m_Root);

            // Letterbox
            m_TopBar = UIBuild.Box("TopBar", m_Root);
            UIBuild.Absolute(m_TopBar, 0, 0, 0);
            m_TopBar.style.height = 56;
            m_TopBar.style.backgroundColor = Color.black;

            m_BottomBar = UIBuild.Box("BottomBar", m_Root);
            UIBuild.Absolute(m_BottomBar, 0, null, 0, 0);
            m_BottomBar.style.height = 40;
            m_BottomBar.style.backgroundColor = Color.black;

            // Bảng thoại
            m_Panel = UIBuild.Box("DialoguePanel", m_Root);
            UIBuild.Absolute(m_Panel, null, null, null, 28);
            m_Panel.style.left = Length.Percent(10);
            m_Panel.style.right = Length.Percent(10);
            m_Panel.style.backgroundColor = new Color(0.04f, 0.04f, 0.08f, 0.92f);
            UIBuild.Border(m_Panel, 2, new Color(0.85f, 0.72f, 0.38f, 0.9f), 12);
            UIBuild.Padding(m_Panel, 14, 22);
            m_Panel.RegisterCallback<ClickEvent>(_ => AdvanceRequested?.Invoke());

            m_Speaker = UIBuild.Text("", 22, npcNameColor, m_Panel, bold: true);
            m_Speaker.style.marginBottom = 6;

            m_Text = UIBuild.Text("", 21, new Color(0.95f, 0.95f, 0.95f), m_Panel);
            m_Text.style.minHeight = 56;

            m_Choices = UIBuild.Box("Choices", m_Panel);
            m_Choices.style.marginTop = 10;

            m_ContinueHint = UIBuild.Text("Space / Click  ▶", 15, new Color(1f, 1f, 1f, 0.6f), m_Panel);
            m_ContinueHint.style.alignSelf = Align.FlexEnd;
            m_ContinueHint.style.marginTop = 6;
        }

        public void Open()
        {
            if (m_Root != null)
                m_Root.style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            if (m_Root != null)
                m_Root.style.display = DisplayStyle.None;
            m_Typing = false;
        }

        public void ShowLine(string speaker, bool isPlayer, string text, List<ChoiceView> choices, bool isEndNode)
        {
            if (m_Root == null)
                return;

            m_Speaker.text = speaker;
            m_Speaker.style.color = isPlayer ? playerNameColor : npcNameColor;

            m_FullText = text ?? string.Empty;
            m_VisibleChars = 0f;
            m_Typing = m_FullText.Length > 0;
            m_Text.text = string.Empty;
            m_IsEndNode = isEndNode;

            m_PendingChoices.Clear();
            m_PendingChoices.AddRange(choices);
            m_Choices.Clear();
            m_ContinueHint.style.display = DisplayStyle.None;

            if (!m_Typing)
                OnTypingFinished();
        }

        public void FinishTyping()
        {
            if (!m_Typing) return;
            m_VisibleChars = m_FullText.Length;
            m_Text.text = m_FullText;
            m_Typing = false;
            OnTypingFinished();
        }

        void Update()
        {
            if (!m_Typing)
                return;

            // timeScale = 0 khi hội thoại => dùng unscaledDeltaTime
            m_VisibleChars += charactersPerSecond * Time.unscaledDeltaTime;
            int count = Mathf.Min(m_FullText.Length, Mathf.FloorToInt(m_VisibleChars));
            m_Text.text = m_FullText.Substring(0, count);

            if (count >= m_FullText.Length)
            {
                m_Typing = false;
                OnTypingFinished();
            }
        }

        void OnTypingFinished()
        {
            if (m_PendingChoices.Count == 0)
            {
                string key = TouchInput.Enabled ? "chạm" : "Space";
                m_ContinueHint.text = m_IsEndNode ? $"Kết thúc  [{key}]" : $"Tiếp tục  [{key}]  ▶";
                m_ContinueHint.style.display = DisplayStyle.Flex;
                return;
            }

            for (int i = 0; i < m_PendingChoices.Count; i++)
            {
                int index = i;
                var view = m_PendingChoices[i];
                string label = $"{i + 1}.  {view.text}";
                if (view.locked)
                    label += string.IsNullOrEmpty(view.lockedHint) ? "   [Khóa]" : $"   [Khóa: {view.lockedHint}]";

                var button = UIBuild.Button(label, m_Choices, () => ChoiceSelected?.Invoke(index));
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginTop = 4;
                button.style.fontSize = 19;
                button.style.whiteSpace = WhiteSpace.Normal;

                if (view.locked)
                {
                    button.style.color = new Color(1f, 1f, 1f, 0.4f);
                    button.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
                    UIBuild.Border(button, 1, new Color(0.5f, 0.5f, 0.5f, 0.5f), 8);
                }
                else
                {
                    button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = new Color(0.3f, 0.24f, 0.1f, 0.95f));
                    button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = new Color(0.1f, 0.1f, 0.16f, 0.85f));
                }
            }
        }
    }
}
