using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Shared IMGUI styling. The whole front end is drawn in immediate mode so
    /// the project needs no canvas prefabs, no UI package and no font asset —
    /// panels are flat generated textures and type is the built-in font.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Acid = Util.Hex(0x9fd93a);
        public static readonly Color AcidDark = Util.Hex(0x5f8a1e);
        public static readonly Color Ink = Util.Hex(0xe8e6df);
        public static readonly Color Dim = Util.Hex(0x8a8f8c);
        public static readonly Color Blood = Util.Hex(0xc0392b);
        public static readonly Color Amber = Util.Hex(0xe8a33d);
        public static readonly Color Panel = new Color(0.04f, 0.05f, 0.05f, 0.88f);
        public static readonly Color PanelSoft = new Color(0.08f, 0.1f, 0.1f, 0.72f);

        static GUIStyle _title, _h1, _h2, _label, _small, _mono, _menuItem, _menuItemSel, _button, _stat, _statVal;
        static bool _built;
        static float _scale = 1f;

        /// <summary>Everything is authored against a 1080p canvas and scaled.</summary>
        public static float Scale { get { return _scale; } }

        public static void Begin()
        {
            _scale = Mathf.Max(0.55f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
            Build();
        }

        static int S(float v) { return Mathf.Max(1, Mathf.RoundToInt(v * _scale)); }
        public static float Px(float v) { return v * _scale; }

        static GUIStyle Base(int size, Color color, TextAnchor anchor, FontStyle style)
        {
            var s = new GUIStyle();
            s.fontSize = S(size);
            s.normal = new GUIStyleState { textColor = color };
            s.alignment = anchor;
            s.fontStyle = style;
            s.wordWrap = false;
            return s;
        }

        static void Build()
        {
            // Rebuilt whenever the window is resized so type scales with it.
            if (_built && Mathf.Abs(_title.fontSize - S(64)) < 1) return;
            _built = true;
            _title = Base(64, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            _h1 = Base(34, Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            _h2 = Base(18, Acid, TextAnchor.UpperLeft, FontStyle.Bold);
            _label = Base(16, Ink, TextAnchor.MiddleLeft, FontStyle.Normal);
            _small = Base(13, Dim, TextAnchor.MiddleLeft, FontStyle.Normal);
            _mono = Base(15, Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            _stat = Base(11, Dim, TextAnchor.UpperLeft, FontStyle.Normal);
            _statVal = Base(26, Ink, TextAnchor.UpperLeft, FontStyle.Bold);

            _menuItem = Base(22, Util.Hex(0xcfd3cc), TextAnchor.MiddleLeft, FontStyle.Bold);
            _menuItem.padding = new RectOffset(S(18), 0, 0, 0);
            _menuItemSel = Base(22, Util.Hex(0x0a0d08), TextAnchor.MiddleLeft, FontStyle.Bold);
            _menuItemSel.padding = new RectOffset(S(26), 0, 0, 0);

            _button = Base(15, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        public static GUIStyle Title { get { return _title; } }
        public static GUIStyle H1 { get { return _h1; } }
        public static GUIStyle H2 { get { return _h2; } }
        public static GUIStyle Label { get { return _label; } }
        public static GUIStyle Small { get { return _small; } }
        public static GUIStyle Mono { get { return _mono; } }
        public static GUIStyle StatKey { get { return _stat; } }
        public static GUIStyle StatVal { get { return _statVal; } }

        public static GUIStyle Text(int size, Color color, TextAnchor anchor)
        {
            var s = Base(size, color, anchor, FontStyle.Normal);
            return s;
        }

        public static GUIStyle TextBold(int size, Color color, TextAnchor anchor)
        {
            return Base(size, color, anchor, FontStyle.Bold);
        }

        // ------------------------------------------------------------ drawing
        public static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Art.Solid(Color.white));
            GUI.color = prev;
        }

        public static void Frame(Rect r, Color c, float thickness)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), c);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            Fill(new Rect(r.x, r.y, thickness, r.height), c);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        public static void Bar(Rect r, float fraction, Color fill, Color back)
        {
            Fill(r, back);
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), fill);
        }

        /// <summary>A menu row. Returns true when clicked.</summary>
        public static bool MenuItem(Rect r, string index, string label, bool selected)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            bool active = selected || hover;
            Fill(r, active ? Acid : new Color(0.08f, 0.1f, 0.09f, 0.72f));
            Fill(new Rect(r.x, r.y, Px(2), r.height), active ? Acid : new Color(1, 1, 1, 0.08f));
            GUI.Label(new Rect(r.x + Px(8), r.y, Px(30), r.height),
                index, Text(11, active ? new Color(0, 0, 0, 0.55f) : new Color(1, 1, 1, 0.4f), TextAnchor.MiddleLeft));
            GUI.Label(r, label, active ? _menuItemSel : _menuItem);
            return hover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
        }

        public static bool Button(Rect r, string label, bool primary)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color bg = primary
                ? (hover ? Color.Lerp(Acid, Color.white, 0.15f) : Acid)
                : (hover ? Acid : new Color(1, 1, 1, 0.06f));
            Fill(r, bg);
            Frame(r, primary || hover ? Acid : new Color(1, 1, 1, 0.18f), Px(1));
            var style = TextBold(15, primary || hover ? Util.Hex(0x08110a) : Ink, TextAnchor.MiddleCenter);
            GUI.Label(r, label, style);
            return hover && Event.current.type == EventType.MouseDown && Event.current.button == 0;
        }

        public static void PanelBox(Rect r)
        {
            Fill(r, Panel);
            Fill(new Rect(r.x, r.y, r.width, Px(2)), Acid);
        }

        /// <summary>Slider row: label, track, value. Returns the new value.</summary>
        public static float SliderRow(Rect r, string label, float value, float min, float max, string display)
        {
            GUI.Label(new Rect(r.x, r.y, r.width * 0.34f, r.height), label, Label);
            var track = new Rect(r.x + r.width * 0.36f, r.y + r.height * 0.5f - Px(3), r.width * 0.46f, Px(6));
            Fill(track, new Color(1, 1, 1, 0.12f));
            float v = GUI.HorizontalSlider(new Rect(track.x, r.y, track.width, r.height), value, min, max);
            Fill(new Rect(track.x, track.y, track.width * Mathf.InverseLerp(min, max, v), track.height), Acid);
            GUI.Label(new Rect(r.xMax - r.width * 0.14f, r.y, r.width * 0.14f, r.height), display, Mono);
            return v;
        }

        public static bool ToggleRow(Rect r, string label, bool value)
        {
            GUI.Label(new Rect(r.x, r.y, r.width * 0.34f, r.height), label, Label);
            var box = new Rect(r.x + r.width * 0.36f, r.y + r.height * 0.5f - Px(12), Px(52), Px(24));
            Fill(box, value ? new Color(0.62f, 0.85f, 0.23f, 0.22f) : new Color(1, 1, 1, 0.08f));
            Frame(box, value ? Acid : new Color(1, 1, 1, 0.16f), Px(1));
            Fill(new Rect(value ? box.xMax - Px(22) : box.x + Px(2), box.y + Px(2), Px(20), Px(20)),
                 value ? Acid : Util.Hex(0x6b736c));
            bool clicked = box.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDown && Event.current.button == 0;
            return clicked ? !value : value;
        }
    }
}
