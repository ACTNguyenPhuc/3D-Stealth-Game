using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Bảng Cài đặt dùng chung cho Menu chính và Pause menu.</summary>
    public class SettingsPanel
    {
        public VisualElement Root { get; }
        readonly Action m_OnClose;
        readonly List<Action> m_Refreshers = new();

        public SettingsPanel(VisualElement parent, Action onClose)
        {
            m_OnClose = onClose;
            Root = UIBuild.Overlay("SettingsOverlay", parent, 0.6f);
            var panel = UIBuild.Panel("SettingsPanel", Root);
            panel.style.width = 560;
            UIBuild.Heading("CÀI ĐẶT", panel);

            Section(panel, "Âm thanh");
            Slider(panel, "Âm lượng tổng", 0f, 1f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, v => $"{v * 100:0}%");
            Slider(panel, "Nhạc nền", 0f, 1f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, v => $"{v * 100:0}%");
            Slider(panel, "Hiệu ứng âm thanh", 0f, 1f, () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, v => $"{v * 100:0}%");

            Section(panel, "Điều khiển");
            Slider(panel, "Độ nhạy chuột", 0.2f, 3f, () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => $"{v:0.0}x");
            Toggle(panel, "Đảo trục dọc (Y)", () => GameSettings.InvertY, v => GameSettings.InvertY = v);
            Toggle(panel, "Rung camera", () => GameSettings.CameraShake, v => GameSettings.CameraShake = v);
            Dropdown(panel, "Điều khiển cảm ứng", new List<string> { "Tự động", "Luôn bật", "Tắt" }, () => GameSettings.TouchControlsMode, v => GameSettings.TouchControlsMode = v);

            Section(panel, "Hình ảnh");
            Dropdown(panel, "Đồ họa", GameSettings.GraphicsPresetNames.ToList(), () => GameSettings.GraphicsPreset, v => GameSettings.GraphicsPreset = v);
            if (!Application.isMobilePlatform)
                Toggle(panel, "Toàn màn hình", () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v);
            Toggle(panel, "Hiện FPS", () => GameSettings.ShowFps, v => GameSettings.ShowFps = v);

            var buttons = UIBuild.Row(panel);
            buttons.style.justifyContent = Justify.Center;
            buttons.style.marginTop = 16;
            UIBuild.MenuButton("Mặc định", buttons, () => { GameSettings.ResetDefaults(); Refresh(); }, 200).style.marginRight = 12;
            UIBuild.MenuButton("Đóng", buttons, Close, 200);
        }

        public void Show()
        {
            Refresh();
            Root.style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            Root.style.display = DisplayStyle.None;
            m_OnClose?.Invoke();
        }

        public bool IsOpen => Root.style.display == DisplayStyle.Flex;

        void Refresh()
        {
            foreach (var r in m_Refreshers) r();
        }

        static void Section(VisualElement parent, string title)
        {
            var l = UIBuild.Text(title.ToUpperInvariant(), 14, UITheme.Current.gold, parent, bold: true);
            l.style.marginTop = 10;
            l.style.marginBottom = 4;
        }

        static VisualElement LabeledRow(VisualElement parent, string label)
        {
            var row = UIBuild.Row(parent);
            row.style.height = 34;
            row.style.justifyContent = Justify.SpaceBetween;
            var l = UIBuild.Text(label, 17, Color.white, row);
            l.style.width = 210;
            return row;
        }

        void Slider(VisualElement parent, string label, float min, float max, Func<float> get, Action<float> set, Func<float, string> format)
        {
            var row = LabeledRow(parent, label);
            var slider = new Slider(min, max) { value = get() };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 8;
            slider.style.marginRight = 8;
            row.Add(slider);
            var value = UIBuild.Text(format(get()), 15, UITheme.Current.muted, row);
            value.style.width = 52;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                value.text = format(e.newValue);
            });
            m_Refreshers.Add(() =>
            {
                slider.SetValueWithoutNotify(get());
                value.text = format(get());
            });
        }

        void Toggle(VisualElement parent, string label, Func<bool> get, Action<bool> set)
        {
            var row = LabeledRow(parent, label);
            var toggle = new Toggle { value = get() };
            row.Add(toggle);
            toggle.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                AudioManager.Sfx(SoundId.UIClick);
            });
            m_Refreshers.Add(() => toggle.SetValueWithoutNotify(get()));
        }

        void Dropdown(VisualElement parent, string label, List<string> options, Func<int> get, Action<int> set)
        {
            var row = LabeledRow(parent, label);
            var dd = new DropdownField(options, Mathf.Clamp(get(), 0, options.Count - 1));
            dd.style.width = 250;
            row.Add(dd);
            dd.RegisterValueChangedCallback(_ => set(dd.index));
            m_Refreshers.Add(() => dd.SetValueWithoutNotify(options[Mathf.Clamp(get(), 0, options.Count - 1)]));
        }
    }
}
