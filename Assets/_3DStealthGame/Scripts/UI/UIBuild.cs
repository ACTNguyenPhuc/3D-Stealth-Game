using UnityEngine;
using UnityEngine.UIElements;

namespace StealthGame
{
    /// <summary>Tiện ích dựng giao diện UI Toolkit bằng code, dùng chung một phong cách (UITheme).</summary>
    public static class UIBuild
    {
        static Font s_Fallback;
        static bool s_FallbackLoaded;

        static Font Fallback
        {
            get
            {
                if (!s_FallbackLoaded)
                {
                    s_FallbackLoaded = true;
                    try { s_Fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch { s_Fallback = null; }
                }
                return s_Fallback;
            }
        }

        /// <summary>
        /// Font chữ nội dung. Font động dùng font hệ điều hành làm dự phòng
        /// nên hiển thị được tiếng Việt có dấu.
        /// </summary>
        public static void ApplyDefaultFont(VisualElement element)
        {
            var font = UITheme.Current.bodyFont != null ? UITheme.Current.bodyFont : Fallback;
            if (font != null)
                element.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        /// <summary>Font tiêu đề trang trí (chỉ dùng cho chữ không dấu / số).</summary>
        public static void ApplyTitleFont(VisualElement element)
        {
            var font = UITheme.Current.titleFont;
            if (font != null)
                element.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        public static VisualElement Box(string name, VisualElement parent = null)
        {
            var e = new VisualElement { name = name };
            parent?.Add(e);
            return e;
        }

        public static VisualElement Row(VisualElement parent, float gap = 0f)
        {
            var e = Box("Row", parent);
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            return e;
        }

        public static Label Text(string text, int size, Color color, VisualElement parent = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            l.pickingMode = PickingMode.Ignore;
            parent?.Add(l);
            return l;
        }

        public static VisualElement Icon(Texture2D tex, float size, Color tint, VisualElement parent = null)
        {
            var e = Box("Icon", parent);
            e.style.width = size;
            e.style.height = size;
            e.style.flexShrink = 0;
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            e.style.unityBackgroundImageTintColor = tint;
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        public static void Absolute(VisualElement e, float? left = null, float? top = null, float? right = null, float? bottom = null)
        {
            e.style.position = Position.Absolute;
            if (left.HasValue) e.style.left = left.Value;
            if (top.HasValue) e.style.top = top.Value;
            if (right.HasValue) e.style.right = right.Value;
            if (bottom.HasValue) e.style.bottom = bottom.Value;
        }

        public static void Fill(VisualElement e) => Absolute(e, 0, 0, 0, 0);

        public static void Padding(VisualElement e, float vertical, float horizontal)
        {
            e.style.paddingTop = e.style.paddingBottom = vertical;
            e.style.paddingLeft = e.style.paddingRight = horizontal;
        }

        public static void Border(VisualElement e, float width, Color color, float radius)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        /// <summary>Khung bảng tối viền vàng – phong cách chung của toàn bộ UI.</summary>
        public static VisualElement Panel(string name, VisualElement parent, float padV = 18, float padH = 24)
        {
            var p = Box(name, parent);
            p.style.backgroundColor = UITheme.Current.panel;
            Border(p, 2, new Color(UITheme.Current.gold.r, UITheme.Current.gold.g, UITheme.Current.gold.b, 0.85f), 12);
            Padding(p, padV, padH);
            return p;
        }

        /// <summary>Lớp phủ toàn màn hình nền mờ, căn giữa nội dung.</summary>
        public static VisualElement Overlay(string name, VisualElement parent, float alpha = 0.75f)
        {
            var o = Box(name, parent);
            Fill(o);
            o.style.backgroundColor = new Color(0f, 0f, 0.02f, alpha);
            o.style.justifyContent = Justify.Center;
            o.style.alignItems = Align.Center;
            o.style.display = DisplayStyle.None;
            return o;
        }

        public static Button Button(string text, VisualElement parent, System.Action onClick)
        {
            var b = new Button { text = text };
            b.style.fontSize = 18;
            b.style.color = Color.white;
            b.style.backgroundColor = new Color(0.1f, 0.1f, 0.16f, 0.85f);
            Border(b, 2, new Color(0.85f, 0.72f, 0.38f, 0.9f), 8);
            Padding(b, 8, 14);
            b.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                onClick?.Invoke();
            });
            parent?.Add(b);
            return b;
        }

        /// <summary>Nút menu lớn có hiệu ứng hover + âm thanh.</summary>
        public static Button MenuButton(string text, VisualElement parent, System.Action onClick, float width = 320)
        {
            var theme = UITheme.Current;
            var normal = new Color(0.08f, 0.08f, 0.13f, 0.9f);
            var hover = new Color(0.32f, 0.24f, 0.08f, 0.95f);
            var b = Button(text, parent, () =>
            {
                AudioManager.Sfx(SoundId.UIClick);
                onClick?.Invoke();
            });
            b.style.width = width;
            b.style.height = 46;
            b.style.fontSize = 20;
            b.style.marginTop = 6;
            b.style.marginBottom = 6;
            b.style.backgroundColor = normal;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!b.enabledSelf) return;
                b.style.backgroundColor = hover;
                b.style.color = theme.gold;
                AudioManager.Sfx(SoundId.UIHover, 0.6f);
            });
            b.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                b.style.backgroundColor = normal;
                b.style.color = Color.white;
            });
            return b;
        }

        /// <summary>Thanh tiến trình đơn giản; trả về phần "fill" để chỉnh width %.</summary>
        public static VisualElement Bar(VisualElement parent, float width, float height, Color color, out VisualElement fill)
        {
            var track = Box("BarTrack", parent);
            track.style.width = width;
            track.style.height = height;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            Border(track, 1, new Color(1f, 1f, 1f, 0.18f), height * 0.5f);
            track.style.overflow = Overflow.Hidden;
            fill = Box("BarFill", track);
            fill.style.height = Length.Percent(100);
            fill.style.width = Length.Percent(100);
            fill.style.backgroundColor = color;
            return track;
        }

        public static Label Heading(string text, VisualElement parent, int size = 30)
        {
            var l = Text(text, size, UITheme.Current.gold, parent, bold: true);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.marginBottom = 14;
            return l;
        }
    }
}
