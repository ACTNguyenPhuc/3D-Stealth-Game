using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Hộp thoại xác nhận Có/Không dùng chung.</summary>
    public class ConfirmDialog
    {
        readonly VisualElement m_Root;
        readonly Label m_Message;
        Action m_OnYes;

        public bool IsOpen => m_Root.style.display == DisplayStyle.Flex;

        public ConfirmDialog(VisualElement parent)
        {
            m_Root = UIBuild.Overlay("Confirm", parent, 0.65f);
            var panel = UIBuild.Panel("ConfirmPanel", m_Root);
            panel.style.width = 480;
            panel.style.alignItems = Align.Center;
            m_Message = UIBuild.Text("", 19, Color.white, panel);
            m_Message.style.unityTextAlign = TextAnchor.MiddleCenter;
            m_Message.style.marginBottom = 16;
            var row = UIBuild.Row(panel);
            UIBuild.MenuButton("Đồng ý", row, () => { Hide(); m_OnYes?.Invoke(); }, 180).style.marginRight = 12;
            UIBuild.MenuButton("Hủy", row, Hide, 180);
        }

        public void Show(string message, Action onYes)
        {
            m_Message.text = message;
            m_OnYes = onYes;
            m_Root.style.display = DisplayStyle.Flex;
        }

        public void Hide() => m_Root.style.display = DisplayStyle.None;
    }
}
