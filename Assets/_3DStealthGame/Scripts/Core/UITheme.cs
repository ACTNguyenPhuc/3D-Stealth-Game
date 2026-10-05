using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Tài nguyên giao diện dùng chung (font, icon, ảnh nền, bảng màu) – một chỗ để đổi phong cách.</summary>
    [CreateAssetMenu(menuName = "Stealth Game/UI Theme")]
    public class UITheme : ScriptableObject
    {
        public PanelSettings panelSettings;
        public Font titleFont;
        public Font bodyFont;

        [Header("Icons")]
        public Texture2D heart, stamina, lantern, shard, potion, relic, key, ghost, clock, skull, eye, quest, checkpoint, trophy, energy;

        [Header("Images")]
        public Texture2D wonScreen;
        public Texture2D caughtScreen;
        public Texture2D vignette;

        [Header("Colors")]
        public Color gold = new(0.88f, 0.69f, 0.31f);
        public Color panel = new(0.05f, 0.05f, 0.09f, 0.92f);
        public Color danger = new(0.89f, 0.33f, 0.28f);
        public Color health = new(0.85f, 0.22f, 0.25f);
        public Color staminaColor = new(0.45f, 0.82f, 0.5f);
        public Color energyColor = new(0.98f, 0.82f, 0.35f);
        public Color text = new(0.95f, 0.95f, 0.95f);
        public Color muted = new(0.66f, 0.69f, 0.78f);

        static UITheme s_Instance;
        public static UITheme Current
        {
            get
            {
                if (s_Instance == null) s_Instance = Resources.Load<UITheme>("UITheme");
                if (s_Instance == null) s_Instance = CreateInstance<UITheme>();
                return s_Instance;
            }
        }
    }
}
