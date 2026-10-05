using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>
    /// Lớp thông báo của HUD: tên chế độ camera, overlay camera an ninh (REC),
    /// gợi ý tương tác "[E] ..." và thông báo nổi (toast).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        public float toastDuration = 2.8f;

        VisualElement m_Root;
        VisualElement m_CameraChip;
        Label m_CameraName;
        VisualElement m_SecurityOverlay;
        Label m_SecurityLabel;
        Label m_RecDot;
        Label m_Prompt;
        VisualElement m_Toasts;

        readonly List<(Label label, float until)> m_ActiveToasts = new();
        CameraModeDefinition m_Mode;
        string m_PendingPrompt;
        bool m_InDialogue;

        void Awake() => Instance = this;

        void OnEnable() => GameEvents.Toast += Toast;

        void OnDisable() => GameEvents.Toast -= Toast;

        void Start()
        {
            Build(GetComponent<UIDocument>().rootVisualElement);

            var cameras = CameraModeManager.Instance;
            if (cameras != null)
            {
                cameras.ModeChanged += OnCameraModeChanged;
                if (cameras.Current != null)
                    OnCameraModeChanged(cameras.Current, cameras.CurrentIndex);
            }
            SetInteractionPrompt(m_PendingPrompt);
            foreach (var t in m_PendingToasts) Toast(t);
            m_PendingToasts.Clear();
        }

        void OnDestroy()
        {
            if (CameraModeManager.Instance != null)
                CameraModeManager.Instance.ModeChanged -= OnCameraModeChanged;
            if (Instance == this) Instance = null;
        }

        void Build(VisualElement docRoot)
        {
            var theme = UITheme.Current;
            m_Root = UIBuild.Box("MessageRoot", docRoot);
            UIBuild.Fill(m_Root);
            m_Root.pickingMode = PickingMode.Ignore;
            UIBuild.ApplyDefaultFont(m_Root);

            // --- Overlay camera an ninh
            m_SecurityOverlay = UIBuild.Box("SecurityOverlay", m_Root);
            UIBuild.Fill(m_SecurityOverlay);
            m_SecurityOverlay.pickingMode = PickingMode.Ignore;
            UIBuild.Border(m_SecurityOverlay, 6, new Color(0.9f, 0.1f, 0.1f, 0.35f), 0);
            m_SecurityOverlay.style.backgroundColor = new Color(0.1f, 0.3f, 0.1f, 0.08f);
            var recRow = UIBuild.Row(m_SecurityOverlay);
            UIBuild.Absolute(recRow, null, null, 26, 50);
            m_RecDot = UIBuild.Text("●", 26, new Color(1f, 0.15f, 0.15f), recRow);
            m_SecurityLabel = UIBuild.Text("REC  CAM-01", 22, Color.white, recRow, bold: true);
            m_SecurityLabel.style.marginLeft = 8;
            m_SecurityOverlay.style.display = DisplayStyle.None;

            // --- Chip chế độ camera (góc dưới trái)
            m_CameraChip = UIBuild.Row(m_Root);
            UIBuild.Absolute(m_CameraChip, 16, null, null, 14);
            m_CameraChip.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            UIBuild.Border(m_CameraChip, 1, new Color(1f, 1f, 1f, 0.15f), 8);
            UIBuild.Padding(m_CameraChip, 5, 10);
            m_CameraChip.pickingMode = PickingMode.Ignore;
            UIBuild.Text("CAMERA (V)  ", 13, theme.muted, m_CameraChip, bold: true);
            m_CameraName = UIBuild.Text("-", 16, theme.gold, m_CameraChip, bold: true);

            // --- Gợi ý tương tác
            m_Prompt = UIBuild.Text("", 21, Color.white, m_Root, bold: true);
            UIBuild.Absolute(m_Prompt, null, null, null, 110);
            m_Prompt.style.alignSelf = Align.Center;
            m_Prompt.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            UIBuild.Border(m_Prompt, 2, new Color(0.85f, 0.72f, 0.38f, 0.9f), 8);
            UIBuild.Padding(m_Prompt, 8, 18);

            // --- Thông báo
            m_Toasts = UIBuild.Box("Toasts", m_Root);
            UIBuild.Absolute(m_Toasts, 0, 96, 0);
            m_Toasts.style.alignItems = Align.Center;
            m_Toasts.pickingMode = PickingMode.Ignore;
        }

        void OnCameraModeChanged(CameraModeDefinition mode, int index)
        {
            m_Mode = mode;
            if (m_Root == null) return;
            m_CameraName.text = $"[{index + 1}] {mode.displayName}";
            RefreshSecurityOverlay();
        }

        void RefreshSecurityOverlay()
        {
            bool show = !m_InDialogue && m_Mode != null && m_Mode.showSecurityOverlay;
            m_SecurityOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetInteractionPrompt(string text)
        {
            m_PendingPrompt = text;
            if (m_Prompt == null) return;
            bool show = !string.IsNullOrEmpty(text);
            m_Prompt.text = text ?? string.Empty;
            m_Prompt.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetDialogueMode(bool inDialogue)
        {
            m_InDialogue = inDialogue;
            if (m_Root == null) return;
            m_CameraChip.style.display = inDialogue ? DisplayStyle.None : DisplayStyle.Flex;
            if (inDialogue) SetInteractionPrompt(null);
            RefreshSecurityOverlay();
        }

        readonly List<string> m_PendingToasts = new();

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (m_Toasts == null) { m_PendingToasts.Add(message); return; }

            // Bỏ qua thông báo trùng đang hiển thị
            foreach (var (l, _) in m_ActiveToasts)
                if (l.text == message) return;

            var label = UIBuild.Text(message, 19, Color.white, m_Toasts, bold: true);
            label.style.backgroundColor = new Color(0.05f, 0.05f, 0.1f, 0.82f);
            UIBuild.Border(label, 1, new Color(0.85f, 0.72f, 0.38f, 0.7f), 6);
            UIBuild.Padding(label, 6, 14);
            label.style.marginBottom = 6;
            m_ActiveToasts.Add((label, Time.unscaledTime + toastDuration));

            while (m_ActiveToasts.Count > 4)
            {
                m_ActiveToasts[0].label.RemoveFromHierarchy();
                m_ActiveToasts.RemoveAt(0);
            }
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = m_ActiveToasts.Count - 1; i >= 0; i--)
            {
                var (label, until) = m_ActiveToasts[i];
                float remaining = until - now;
                if (remaining <= 0f)
                {
                    label.RemoveFromHierarchy();
                    m_ActiveToasts.RemoveAt(i);
                }
                else label.style.opacity = Mathf.Clamp01(remaining / 0.5f);
            }

            if (m_Mode != null && m_Mode.showSecurityOverlay && m_SecurityLabel != null)
            {
                string camName = "CAM";
                if (m_Mode.virtualCamera is CinemachineClearShot clearShot && clearShot.LiveChild != null)
                    camName = clearShot.LiveChild.Name;
                m_SecurityLabel.text = $"REC   {camName}   {System.DateTime.Now:HH:mm:ss}";
                m_RecDot.style.opacity = Mathf.Repeat(now, 1f) < 0.5f ? 1f : 0.15f;
            }
        }
    }
}
