using UnityEngine;

namespace Rotgrid
{
    /// <summary>The in-game heads-up display, drawn in immediate mode.</summary>
    public static class Hud
    {
        public static void Draw(GameRun run)
        {
            if (run == null) return;
            var p = run.player;
            float w = Screen.width, h = Screen.height;
            float pad = UiKit.Px(28);

            DrawRoundBlock(run, pad);
            DrawScore(run, w, pad);
            DrawVitals(run, h, pad);
            DrawWeapon(run, w, h, pad);
            DrawCentre(run, w, h);
            DrawDamage(run, w, h);
            DrawToasts(run, w, h);
            DrawPowerUps(run, w, h);
            if (p.downed) DrawDowned(run, w, h);
            if (run.bannerT > 0f) DrawBanner(run, w, h);
        }

        // ------------------------------------------------------------ corners
        static void DrawRoundBlock(GameRun run, float pad)
        {
            GUI.Label(new Rect(pad, pad, UiKit.Px(200), UiKit.Px(24)), "ROUND",
                UiKit.Text(13, UiKit.Dim, TextAnchor.UpperLeft));
            GUI.Label(new Rect(pad + UiKit.Px(74), pad - UiKit.Px(16), UiKit.Px(220), UiKit.Px(70)),
                Mathf.Max(1, run.rounds.round).ToString(),
                UiKit.TextBold(52, UiKit.Blood, TextAnchor.UpperLeft));
            GUI.Label(new Rect(pad, pad + UiKit.Px(28), UiKit.Px(260), UiKit.Px(20)),
                Mathf.Max(0, run.rounds.Remaining) + "  HOSTILES",
                UiKit.Text(13, Util.Hex(0x8d9389), TextAnchor.UpperLeft));
        }

        static void DrawScore(GameRun run, float w, float pad)
        {
            float x = w - pad - UiKit.Px(320);
            GUI.Label(new Rect(x, pad, UiKit.Px(320), UiKit.Px(20)),
                string.IsNullOrEmpty(GameSettings.PlayerName) ? "OPERATOR" : GameSettings.PlayerName,
                UiKit.Text(13, UiKit.Dim, TextAnchor.UpperRight));
            GUI.Label(new Rect(x, pad + UiKit.Px(14), UiKit.Px(320), UiKit.Px(56)),
                Util.FormatNumber(run.player.points),
                UiKit.TextBold(40, UiKit.Acid, TextAnchor.UpperRight));
            GUI.Label(new Rect(x, pad + UiKit.Px(58), UiKit.Px(320), UiKit.Px(18)), "POINTS",
                UiKit.Text(10, UiKit.Dim, TextAnchor.UpperRight));
            GUI.Label(new Rect(x, pad + UiKit.Px(76), UiKit.Px(320), UiKit.Px(22)),
                Util.FormatTime(run.elapsed),
                UiKit.Text(15, Util.Hex(0xb9bdb4), TextAnchor.UpperRight));
        }

        static void DrawVitals(GameRun run, float h, float pad)
        {
            var p = run.player;
            float barW = UiKit.Px(300);
            float y = h - pad - UiKit.Px(18);

            // Perk chips sit above the bars.
            float cx = pad;
            foreach (var id in p.Perks)
            {
                var def = Perks.Get(id);
                var r = new Rect(cx, y - UiKit.Px(52), UiKit.Px(38), UiKit.Px(38));
                UiKit.Fill(r, new Color(0.04f, 0.06f, 0.05f, 0.72f));
                UiKit.Frame(r, def.Color, UiKit.Px(1));
                GUI.Label(r, def.letter, UiKit.TextBold(16, def.Color, TextAnchor.MiddleCenter));
                cx += UiKit.Px(45);
            }

            var armorRect = new Rect(pad, y - UiKit.Px(8), barW, UiKit.Px(5));
            UiKit.Bar(armorRect, p.armor / 100f, Util.Hex(0x4aa3ff), new Color(0, 0, 0, 0.55f));

            var hpRect = new Rect(pad, y, barW, UiKit.Px(9));
            float hp = Mathf.Clamp01(p.health / p.maxHealth);
            UiKit.Bar(hpRect, hp, hp < 0.45f ? UiKit.Blood : Util.Hex(0x8fd13f), new Color(0, 0, 0, 0.55f));
            UiKit.Frame(hpRect, new Color(1, 1, 1, 0.14f), 1f);

            // Stamina reads as a thin line under health while sprinting matters.
            if (p.stamina < 0.999f)
                UiKit.Bar(new Rect(pad, y + UiKit.Px(12), barW * 0.6f, UiKit.Px(3)),
                    p.stamina, Util.Hex(0xb479ff), new Color(0, 0, 0, 0.4f));
        }

        static void DrawWeapon(GameRun run, float w, float h, float pad)
        {
            var wp = run.weapons.Current;
            float x = w - pad - UiKit.Px(340);
            float y = h - pad - UiKit.Px(74);

            GUI.Label(new Rect(x, y, UiKit.Px(340), UiKit.Px(20)), wp.Name,
                UiKit.Text(14, Util.Hex(0xd4d8d0), TextAnchor.UpperRight));

            string mag = wp.reloading ? "--" : wp.mag.ToString();
            var magStyle = UiKit.TextBold(46, wp.mag <= 3 && !wp.reloading ? UiKit.Blood : UiKit.Ink, TextAnchor.LowerRight);
            GUI.Label(new Rect(x, y + UiKit.Px(16), UiKit.Px(250), UiKit.Px(52)), mag, magStyle);
            GUI.Label(new Rect(x + UiKit.Px(254), y + UiKit.Px(16), UiKit.Px(86), UiKit.Px(52)),
                " / " + wp.reserve, UiKit.Text(20, Util.Hex(0x9aa096), TextAnchor.LowerLeft));

            string alt = "";
            if (wp.reloading) alt = "RELOADING";
            else if (wp.upgradeLevel > 0) alt = "REFIT " + new string('I', wp.upgradeLevel);
            else if (run.weapons.Other != null)
                alt = GameSettings.KeyLabel(GameSettings.Key(Bind.Swap)) + " - " + run.weapons.Other.Name;
            GUI.Label(new Rect(x, y + UiKit.Px(68), UiKit.Px(340), UiKit.Px(18)), alt,
                UiKit.Text(11, UiKit.Acid, TextAnchor.UpperRight));
        }

        // ------------------------------------------------------------ centre
        static void DrawCentre(GameRun run, float w, float h)
        {
            float cx = w * 0.5f, cy = h * 0.5f;

            float scopeT = run.viewModel != null ? run.viewModel.ScopeT : 0f;
            if (scopeT > 0.001f)
            {
                DrawScope(run, w, h, scopeT);
                return;
            }

            if (GameSettings.ShowCrosshair && !run.player.downed)
            {
                float gap = run.weapons.Ads ? UiKit.Px(4) : UiKit.Px(8);
                float len = run.weapons.Ads ? UiKit.Px(4) : UiKit.Px(8);
                float t = Mathf.Max(1f, UiKit.Px(2));
                var c = new Color(0.92f, 0.96f, 0.87f, run.weapons.Ads ? 0.5f : 0.95f);
                UiKit.Fill(new Rect(cx - t * 0.5f, cy - gap - len, t, len), c);
                UiKit.Fill(new Rect(cx - t * 0.5f, cy + gap, t, len), c);
                UiKit.Fill(new Rect(cx - gap - len, cy - t * 0.5f, len, t), c);
                UiKit.Fill(new Rect(cx + gap, cy - t * 0.5f, len, t), c);
                UiKit.Fill(new Rect(cx - t * 0.5f, cy - t * 0.5f, t, t), c);
            }

            if (run.hitmarkerT > 0f)
            {
                float a = Mathf.Clamp01(run.hitmarkerT / 0.22f);
                var col = run.hitmarkerKill > 0f ? UiKit.Acid
                    : (run.hitmarkerCrit > 0f ? Util.Hex(0xff5a45) : Color.white);
                col.a = a;
                float d = UiKit.Px(10) + (1f - a) * UiKit.Px(6);
                float t = Mathf.Max(1f, UiKit.Px(2));
                for (int i = 0; i < 4; i++)
                {
                    float sx = (i % 2 == 0) ? -1f : 1f;
                    float sy = (i < 2) ? -1f : 1f;
                    UiKit.Fill(new Rect(cx + sx * d - t, cy + sy * d - t, t * 3f, t), col);
                }
            }

            if (run.prompt.valid) DrawPrompt(run.prompt, cx, cy);
        }

        // ------------------------------------------------------------ scope
        static Texture2D _scopeMask;

        /// <summary>
        /// Sight picture for a magnified optic: an opaque surround with a round
        /// hole punched in it, an etched mil-dot reticle, and a tube that irises
        /// in as the shooter settles behind the glass.
        /// </summary>
        static void DrawScope(GameRun run, float w, float h, float t)
        {
            float cx = w * 0.5f, cy = h * 0.5f;
            float shortSide = Mathf.Min(w, h);
            float radius = shortSide * (0.62f - 0.20f * Mathf.Clamp01(t));
            float alpha = Mathf.Clamp01((t - 0.35f) / 0.5f);
            if (alpha <= 0.001f) return;

            var sway = run.viewModel.ScopeSway;
            float ox = sway.x * shortSide * 0.035f;
            float oy = -sway.y * shortSide * 0.035f;
            float l = cx + ox - radius, rgt = cx + ox + radius;
            float top = cy + oy - radius, bot = cy + oy + radius;
            var black = new Color(0f, 0f, 0f, alpha);

            // Everything outside the tube's bounding square is solid.
            UiKit.Fill(new Rect(0, 0, w, Mathf.Max(0, top)), black);
            UiKit.Fill(new Rect(0, Mathf.Min(h, bot), w, Mathf.Max(0, h - bot)), black);
            UiKit.Fill(new Rect(0, Mathf.Max(0, top), Mathf.Max(0, l), Mathf.Min(h, bot) - Mathf.Max(0, top)), black);
            UiKit.Fill(new Rect(Mathf.Min(w, rgt), Mathf.Max(0, top), Mathf.Max(0, w - rgt),
                Mathf.Min(h, bot) - Mathf.Max(0, top)), black);
            // ...and the corners of that square are masked into a circle.
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(l, top, radius * 2f, radius * 2f), ScopeMask());
            GUI.color = Color.white;

            // Reticle: heavy posts, fine centre cross, mil dots down and across.
            var etch = new Color(0.04f, 0.06f, 0.04f, alpha);
            float post = Mathf.Max(2f, radius * 0.013f);
            float gap = radius * 0.18f;
            UiKit.Fill(new Rect(l, cy + oy - post * 0.5f, radius - gap, post), etch);
            UiKit.Fill(new Rect(cx + ox + gap, cy + oy - post * 0.5f, radius - gap, post), etch);
            UiKit.Fill(new Rect(cx + ox - post * 0.5f, top, post, radius - gap), etch);
            UiKit.Fill(new Rect(cx + ox - post * 0.5f, cy + oy + gap, post, radius - gap), etch);
            float fine = Mathf.Max(1f, post * 0.35f);
            UiKit.Fill(new Rect(cx + ox - gap, cy + oy - fine * 0.5f, gap * 2f, fine), etch);
            UiKit.Fill(new Rect(cx + ox - fine * 0.5f, cy + oy - gap, fine, gap * 2f), etch);
            float dot = Mathf.Max(2f, radius * 0.008f);
            for (int i = 1; i <= 4; i++)
            {
                float d = gap + i * radius * 0.16f;
                UiKit.Fill(new Rect(cx + ox - dot, cy + oy + d - dot, dot * 2f, dot * 2f), etch);
                UiKit.Fill(new Rect(cx + ox - d - dot, cy + oy - dot, dot * 2f, dot * 2f), etch);
                UiKit.Fill(new Rect(cx + ox + d - dot, cy + oy - dot, dot * 2f, dot * 2f), etch);
            }
        }

        /// <summary>Square texture: transparent inside a circle, opaque outside.</summary>
        static Texture2D ScopeMask()
        {
            if (_scopeMask != null) return _scopeMask;
            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    // A pixel of feather so the rim does not stair-step.
                    float a = Mathf.Clamp01((d - 1f) * N * 0.5f + 0.5f);
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _scopeMask = tex;
            return tex;
        }

        static void DrawPrompt(Prompt p, float cx, float cy)
        {
            bool hasSub = !string.IsNullOrEmpty(p.sub);
            float w = UiKit.Px(420);
            float h = UiKit.Px(56 + (p.hasCost ? 22 : 0) + (hasSub ? 24 : 0));
            var r = new Rect(cx - w * 0.5f, cy + UiKit.Px(62), w, h);
            UiKit.Fill(r, new Color(0.02f, 0.035f, 0.03f, 0.82f));
            UiKit.Frame(r, p.blocked ? UiKit.Blood : new Color(0.62f, 0.85f, 0.23f, 0.45f), UiKit.Px(1));

            string key = GameSettings.KeyLabel(GameSettings.Key(Bind.Interact));
            float x = r.x + UiKit.Px(14);
            if (!p.blocked)
            {
                var kr = new Rect(x, r.y + UiKit.Px(10), UiKit.Px(30), UiKit.Px(22));
                UiKit.Fill(kr, UiKit.Acid);
                GUI.Label(kr, key, UiKit.TextBold(12, Util.Hex(0x0a1108), TextAnchor.MiddleCenter));
                x += UiKit.Px(40);
            }
            GUI.Label(new Rect(x, r.y + UiKit.Px(8), r.width - UiKit.Px(50), UiKit.Px(26)), p.text,
                UiKit.TextBold(15, UiKit.Ink, TextAnchor.MiddleLeft));

            // Price and description both show — you should not have to buy a perk
            // to find out what it does.
            float sy = r.y + UiKit.Px(34);
            if (p.hasCost)
            {
                GUI.Label(new Rect(r.x, sy, r.width, UiKit.Px(22)), Util.FormatNumber(p.cost) + " POINTS",
                    UiKit.Text(12, p.blocked ? UiKit.Blood : UiKit.Acid, TextAnchor.MiddleCenter));
                sy += UiKit.Px(20);
            }
            if (hasSub)
                GUI.Label(new Rect(r.x + UiKit.Px(14), sy, r.width - UiKit.Px(28), UiKit.Px(24)), p.sub,
                    UiKit.Text(11, new Color(0.88f, 0.92f, 0.84f, 0.72f), TextAnchor.MiddleCenter));

            if (p.hasHold)
            {
                var hr = new Rect(r.x + UiKit.Px(14), r.yMax - UiKit.Px(10), r.width - UiKit.Px(28), UiKit.Px(4));
                UiKit.Bar(hr, p.hold, UiKit.Acid, new Color(1, 1, 1, 0.14f));
            }
        }

        // ------------------------------------------------------------ overlays
        static void DrawDamage(GameRun run, float w, float h)
        {
            var p = run.player;
            float hp = Mathf.Clamp01(p.health / p.maxHealth);
            float dmgT = Mathf.Clamp01(1f - p.sinceDamage / 1.6f);
            float vig = Mathf.Max(dmgT * 0.85f, (1f - hp) * 0.35f);
            if (p.downed) vig = Mathf.Max(vig, 0.5f);
            if (vig > 0.01f)
            {
                // Cheap vignette: four edge bands rather than a full-screen shader.
                var c = new Color(0.58f, 0.04f, 0.04f, vig * 0.55f);
                float band = h * 0.18f;
                UiKit.Fill(new Rect(0, 0, w, band), c);
                UiKit.Fill(new Rect(0, h - band, w, band), c);
                UiKit.Fill(new Rect(0, 0, band, h), c);
                UiKit.Fill(new Rect(w - band, 0, band, h), c);
            }

            // Directional damage arrows.
            foreach (var f in p.damageFlashes)
            {
                float rel = f.angle - p.yaw;
                float dist = UiKit.Px(120);
                float x = w * 0.5f + Mathf.Sin(rel) * dist;
                float y = h * 0.5f - Mathf.Cos(rel) * dist;
                var col = new Color(0.9f, 0.2f, 0.16f, Mathf.Clamp01(f.t));
                UiKit.Fill(new Rect(x - UiKit.Px(11), y - UiKit.Px(4), UiKit.Px(22), UiKit.Px(8)), col);
            }
        }

        static void DrawToasts(GameRun run, float w, float h)
        {
            float y = h * 0.5f - UiKit.Px(170);
            for (int i = 0; i < run.toasts.Count; i++)
            {
                var t = run.toasts[i];
                Color c = t.kind == ToastKind.Bad ? UiKit.Blood
                    : (t.kind == ToastKind.Warn ? UiKit.Amber : UiKit.Acid);
                c.a = Mathf.Clamp01(t.t / 0.5f);
                GUI.Label(new Rect(w * 0.5f - UiKit.Px(300), y, UiKit.Px(600), UiKit.Px(28)), t.text,
                    UiKit.TextBold(t.big ? 22 : 16, c, TextAnchor.MiddleCenter));
                y += UiKit.Px(t.big ? 30 : 24);
            }
        }

        static void DrawPowerUps(GameRun run, float w, float h)
        {
            var entries = run.drops.HudEntries();
            float x = w * 0.5f - entries.Count * UiKit.Px(90) * 0.5f;
            foreach (var e in entries)
            {
                var r = new Rect(x, h - UiKit.Px(110), UiKit.Px(178), UiKit.Px(26));
                UiKit.Fill(r, new Color(0.02f, 0.035f, 0.03f, 0.75f));
                UiKit.Frame(r, e.color, 1f);
                GUI.Label(new Rect(r.x + UiKit.Px(8), r.y, r.width - UiKit.Px(16), r.height),
                    e.label, UiKit.TextBold(11, e.color, TextAnchor.MiddleLeft));
                GUI.Label(new Rect(r.x, r.y, r.width - UiKit.Px(8), r.height),
                    Mathf.Ceil(e.t) + "s", UiKit.Text(12, e.color, TextAnchor.MiddleRight));
                x += UiKit.Px(186);
            }
        }

        static void DrawDowned(GameRun run, float w, float h)
        {
            var p = run.player;
            GUI.Label(new Rect(0, h * 0.66f, w, UiKit.Px(56)), "YOU ARE DOWN",
                UiKit.TextBold(44, Util.Hex(0xd34a3c), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(0, h * 0.66f + UiKit.Px(52), w, UiKit.Px(24)), "BLEEDING OUT",
                UiKit.Text(12, UiKit.Dim, TextAnchor.MiddleCenter));
            UiKit.Bar(new Rect(w * 0.5f - UiKit.Px(170), h * 0.66f + UiKit.Px(80), UiKit.Px(340), UiKit.Px(5)),
                p.bleedOut / p.bleedOutMax, UiKit.Blood, new Color(1, 1, 1, 0.12f));
        }

        static void DrawBanner(GameRun run, float w, float h)
        {
            float t = 1f - run.bannerT / 2.7f;
            float a = t < 0.15f ? t / 0.15f : (t > 0.8f ? Mathf.Max(0f, (1f - t) / 0.2f) : 1f);
            var col = Util.Hex(0xe2ded2);
            col.a = a;
            GUI.Label(new Rect(0, h * 0.36f, w, UiKit.Px(120)), run.bannerText,
                UiKit.TextBold(84, col, TextAnchor.MiddleCenter));
            float barW = UiKit.Px(420) * Mathf.Clamp01(t < 0.2f ? t / 0.2f : (t > 0.8f ? (1f - t) / 0.2f : 1f));
            UiKit.Fill(new Rect(w * 0.5f - barW * 0.5f, h * 0.36f + UiKit.Px(112), barW, UiKit.Px(3)),
                new Color(UiKit.Blood.r, UiKit.Blood.g, UiKit.Blood.b, a));
        }
    }
}
