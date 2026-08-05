using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// A hand-authored 5x7 bitmap font, used to bake readable signage textures
    /// for wall buys, perk machines and doors. Keeps the project free of any
    /// imported font asset while still letting the world label itself.
    /// </summary>
    public static class PixelFont
    {
        // Each glyph is 7 rows of 5 bits, stored as 7 hex bytes.
        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            { ' ', "00000000000000" },
            { 'A', "0E11111F111111" },
            { 'B', "1E11111E11111E" },
            { 'C', "0E11101010110E" },
            { 'D', "1E11111111111E" },
            { 'E', "1F10101E10101F" },
            { 'F', "1F10101E101010" },
            { 'G', "0E11101711110F" },
            { 'H', "1111111F111111" },
            { 'I', "0E04040404040E" },
            { 'J', "0702020202120C" },
            { 'K', "11121418141211" },
            { 'L', "1010101010101F" },
            { 'M', "111B1515111111" },
            { 'N', "11191513111111" },
            { 'O', "0E11111111110E" },
            { 'P', "1E11111E101010" },
            { 'Q', "0E11111115120D" },
            { 'R', "1E11111E141211" },
            { 'S', "0F10100E01011E" },
            { 'T', "1F040404040404" },
            { 'U', "1111111111110E" },
            { 'V', "11111111110A04" },
            { 'W', "11111115151B11" },
            { 'X', "11110A040A1111" },
            { 'Y', "11110A04040404" },
            { 'Z', "1F01020408101F" },
            { '0', "0E11131519110E" },
            { '1', "040C040404040E" },
            { '2', "0E11010204081F" },
            { '3', "1F02040201110E" },
            { '4', "02060A121F0202" },
            { '5', "1F101E0101110E" },
            { '6', "0608101E11110E" },
            { '7', "1F010204080808" },
            { '8', "0E11110E11110E" },
            { '9', "0E11110F01020C" },
            { '-', "0000001F000000" },
            { '.', "00000000000C0C" },
            { ':', "000C0C000C0C00" },
            { '/', "01020204080810" },
            { '?', "0E110102040004" },
            { '+', "0004041F040400" },
            { '!', "04040404040004" },
            { '%', "191A0204080B13" },
            { 'x', "0000110A040A11" },
            { '\'', "04040800000000" },
        };

        const int GlyphW = 5, GlyphH = 7, Advance = 6;

        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static int MeasureWidth(string text) { return Mathf.Max(1, text.Length * Advance - 1); }

        /// <summary>
        /// Bakes a framed sign: a title line, an optional subtitle, a border in
        /// the accent colour, on a dark plate.
        /// </summary>
        public static Texture2D Sign(string title, string sub, Color fg, Color bg, int width = 256, int height = 128)
        {
            string key = "sign|" + title + "|" + sub + "|" + fg.r + "," + fg.g + "," + fg.b + "|" + width + "x" + height;
            Texture2D t;
            if (_cache.TryGetValue(key, out t)) return t;

            t = new Texture2D(width, height, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // Border.
            for (int x = 0; x < width; x++)
            {
                for (int k = 0; k < 3; k++)
                {
                    px[(3 + k) * width + x] = fg;
                    px[(height - 4 - k) * width + x] = fg;
                }
            }
            for (int y = 3; y < height - 3; y++)
            {
                for (int k = 0; k < 3; k++)
                {
                    px[y * width + 3 + k] = fg;
                    px[y * width + width - 4 - k] = fg;
                }
            }

            bool hasSub = !string.IsNullOrEmpty(sub);
            int titleScale = Mathf.Max(1, Mathf.Min((width - 24) / Mathf.Max(1, MeasureWidth(title)),
                                                    (hasSub ? height / 3 : height / 2) / GlyphH));
            DrawText(px, width, height, title, fg, titleScale,
                     (width - MeasureWidth(title) * titleScale) / 2,
                     hasSub ? Mathf.RoundToInt(height * 0.24f) : (height - GlyphH * titleScale) / 2);

            if (hasSub)
            {
                int subScale = Mathf.Max(1, Mathf.Min((width - 32) / Mathf.Max(1, MeasureWidth(sub)), height / 6 / GlyphH));
                Color dim = Color.Lerp(fg, Color.white, 0.35f) * 0.85f;
                DrawText(px, width, height, sub, dim, subScale,
                         (width - MeasureWidth(sub) * subScale) / 2,
                         Mathf.RoundToInt(height * 0.60f));
            }

            t.SetPixels(px);
            t.Apply();
            _cache[key] = t;
            return t;
        }

        /// <summary>Draws text into a pixel buffer. Origin is top-left.</summary>
        public static void DrawText(Color[] px, int w, int h, string text, Color col, int scale, int x0, int y0)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = text.ToUpperInvariant();
            for (int c = 0; c < text.Length; c++)
            {
                string g;
                if (!Glyphs.TryGetValue(text[c], out g)) Glyphs.TryGetValue('?', out g);
                if (g == null) continue;
                for (int row = 0; row < GlyphH; row++)
                {
                    int bits = System.Convert.ToInt32(g.Substring(row * 2, 2), 16);
                    for (int col2 = 0; col2 < GlyphW; col2++)
                    {
                        if ((bits & (1 << (GlyphW - 1 - col2))) == 0) continue;
                        int bx = x0 + (c * Advance + col2) * scale;
                        int by = y0 + row * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            int yy = h - 1 - (by + sy);   // flip: textures are bottom-up
                            if (yy < 0 || yy >= h) continue;
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int xx = bx + sx;
                                if (xx < 0 || xx >= w) continue;
                                px[yy * w + xx] = col;
                            }
                        }
                    }
                }
            }
        }
    }
}
