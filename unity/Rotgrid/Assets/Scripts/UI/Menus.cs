using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum Screen2 { None, Main, Solo, Multiplayer, Loadout, Stats, Settings, Credits, Pause, GameOver }

    /// <summary>The whole front end: main menu, panels, pause and death screen.</summary>
    public class Menus
    {
        public Screen2 current = Screen2.Main;
        readonly Stack<Screen2> _stack = new Stack<Screen2>();
        readonly Game _app;

        int _selected;
        Vector2 _scroll;
        Bind? _rebinding;
        int _loadoutIndex;
        int _perkIndex = -1;   // -1 while a weapon is selected instead
        readonly List<WeaponDef> _loadoutList = new List<WeaponDef>();

        public Menus(Game app)
        {
            _app = app;
            foreach (WeaponClass c in System.Enum.GetValues(typeof(WeaponClass)))
            {
                if (c == WeaponClass.Melee) continue;
                _loadoutList.AddRange(Weapons.ByClass(c));
            }
        }

        public bool IsOpen { get { return current != Screen2.None; } }

        public void Show(Screen2 s)
        {
            current = s;
            _selected = 0;
            _scroll = Vector2.zero;
        }

        public void Push(Screen2 s)
        {
            _stack.Push(current);
            Show(s);
        }

        public void Back()
        {
            Game.Audio.Play(Sfx.UiBack, 1f);
            Show(_stack.Count > 0 ? _stack.Pop() : Screen2.Main);
        }

        public void Hide()
        {
            current = Screen2.None;
            _stack.Clear();
        }

        // ================================================================ draw
        public void Draw()
        {
            if (current == Screen2.None) return;
            UiKit.Begin();
            HandleKeys();

            switch (current)
            {
                case Screen2.Main: DrawMain(); break;
                case Screen2.Solo: DrawSolo(); break;
                case Screen2.Multiplayer: DrawMultiplayer(); break;
                case Screen2.Loadout: DrawLoadout(); break;
                case Screen2.Stats: DrawStats(); break;
                case Screen2.Settings: DrawSettings(); break;
                case Screen2.Credits: DrawCredits(); break;
                case Screen2.Pause: DrawPause(); break;
                case Screen2.GameOver: DrawGameOver(); break;
            }
        }

        void HandleKeys()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (_rebinding.HasValue)
            {
                if (e.keyCode != KeyCode.None)
                {
                    if (e.keyCode != KeyCode.Escape) GameSettings.SetKey(_rebinding.Value, e.keyCode);
                    _rebinding = null;
                    InputMap.Capturing = false;
                    e.Use();
                }
                return;
            }
            if (e.keyCode == KeyCode.Escape && current != Screen2.Main && current != Screen2.GameOver)
            {
                if (current == Screen2.Pause) _app.Resume();
                else Back();
                e.Use();
            }
        }

        // ---------------------------------------------------------------- shell
        void Backdrop(bool dim)
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            UiKit.Fill(new Rect(0, 0, w, h), dim
                ? new Color(0.012f, 0.02f, 0.02f, 0.9f)
                : new Color(0.012f, 0.02f, 0.02f, 0.55f));
        }

        Rect PanelHead(string title, string accent, string hint)
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            float pad = UiKit.Px(70);
            var head = new Rect(pad, UiKit.Px(46), w - pad * 2f, UiKit.Px(56));
            GUI.Label(head, title, UiKit.TextBold(38, UiKit.Ink, TextAnchor.MiddleLeft));
            float titleW = UiKit.TextBold(38, UiKit.Ink, TextAnchor.MiddleLeft).CalcSize(new GUIContent(title)).x;
            GUI.Label(new Rect(head.x + titleW + UiKit.Px(6), head.y, head.width, head.height), accent,
                UiKit.TextBold(38, UiKit.Acid, TextAnchor.MiddleLeft));
            if (!string.IsNullOrEmpty(hint))
                GUI.Label(head, hint, UiKit.Text(12, UiKit.Dim, TextAnchor.MiddleRight));
            UiKit.Fill(new Rect(pad, head.yMax, w - pad * 2f, 1f), new Color(1, 1, 1, 0.12f));
            return new Rect(pad, head.yMax + UiKit.Px(22), w - pad * 2f, h - head.yMax - UiKit.Px(120));
        }

        // ---------------------------------------------------------------- main
        void DrawMain()
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            // Left-side scrim only, so the corridor stays visible on the right.
            UiKit.Fill(new Rect(0, 0, w * 0.62f, h), new Color(0.012f, 0.02f, 0.02f, 0.82f));

            float pad = UiKit.Px(70);
            GUI.Label(new Rect(pad, UiKit.Px(60), w, UiKit.Px(110)), "ROT",
                UiKit.TextBold(96, Util.Hex(0xf2f0e8), TextAnchor.UpperLeft));
            float rotW = UiKit.TextBold(96, UiKit.Ink, TextAnchor.UpperLeft).CalcSize(new GUIContent("ROT")).x;
            GUI.Label(new Rect(pad + rotW, UiKit.Px(60), w, UiKit.Px(110)), "GRID",
                UiKit.TextBold(96, UiKit.Acid, TextAnchor.UpperLeft));
            GUI.Label(new Rect(pad, UiKit.Px(168), w, UiKit.Px(24)),
                "ROUND-BASED SURVIVAL   ·   BLACKSITE 7",
                UiKit.Text(14, UiKit.Dim, TextAnchor.UpperLeft));
            UiKit.Fill(new Rect(pad, UiKit.Px(198), UiKit.Px(420), UiKit.Px(2)), UiKit.Acid);

            string[] labels = { "PLAY", "SOLO MATCH", "MULTIPLAYER", "LOADOUT VIEWER", "STATISTICS", "SETTINGS", "CREDITS", "QUIT" };
            float y = h * 0.36f;
            float itemH = UiKit.Px(44);
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(pad, y + i * (itemH + UiKit.Px(2)), UiKit.Px(430), itemH);
                if (UiKit.MenuItem(r, "0" + (i + 1), labels[i], _selected == i))
                {
                    Game.Audio.Play(Sfx.UiSelect, 1f);
                    Activate(i);
                }
                if (r.Contains(Event.current.mousePosition)) _selected = i;
            }

            var save = SaveData.Data;
            GUI.Label(new Rect(pad, h - UiKit.Px(58), w, UiKit.Px(24)),
                "HIGHEST ROUND  " + save.highestRound
                + "      LIFETIME KILLS  " + Util.FormatNumber(save.lifetimeKills)
                + "      RUNS  " + save.gamesPlayed,
                UiKit.Text(12, UiKit.Dim, TextAnchor.UpperLeft));
            GUI.Label(new Rect(pad, h - UiKit.Px(34), w, UiKit.Px(24)),
                "MOVE WASD   ·   LOOK MOUSE   ·   FIRE LMB   ·   USE F",
                UiKit.Text(12, UiKit.Dim, TextAnchor.UpperLeft));
        }

        void Activate(int index)
        {
            switch (index)
            {
                case 0: _app.StartRun(); break;
                case 1: Push(Screen2.Solo); break;
                case 2: Push(Screen2.Multiplayer); break;
                case 3: Push(Screen2.Loadout); break;
                case 4: Push(Screen2.Stats); break;
                case 5: Push(Screen2.Settings); break;
                case 6: Push(Screen2.Credits); break;
                case 7: _app.Quit(); break;
            }
        }

        // ---------------------------------------------------------------- solo
        void DrawSolo()
        {
            Backdrop(true);
            var body = PanelHead("SOLO ", "MATCH", "ESC TO GO BACK");
            float rowH = UiKit.Px(34);
            float y = body.y;

            GUI.Label(new Rect(body.x, y, body.width, rowH), "OPERATOR", UiKit.H2);
            y += rowH;
            GUI.Label(new Rect(body.x, y, UiKit.Px(200), rowH), "Callsign", UiKit.Label);
            GameSettings.PlayerName = GUI.TextField(new Rect(body.x + UiKit.Px(220), y, UiKit.Px(320), rowH),
                GameSettings.PlayerName, 18).ToUpperInvariant();
            y += rowH + UiKit.Px(24);

            GUI.Label(new Rect(body.x, y, body.width, rowH), "DEPLOYMENT", UiKit.H2);
            y += rowH;
            string[,] cards =
            {
                { "MAP", MapData.Name }, { "ZONES", MapData.Zones.Count.ToString() },
                { "STARTING POINTS", "500" }, { "STARTING HEALTH", "100" },
                { "BLEED-OUT", "30s" }, { "PLAYERS", "1 / 4" },
                { "PRIMARY", Weapons.All["sidearm"].name }, { "MELEE", Weapons.Knife.name },
            };
            float cw = body.width / 4f - UiKit.Px(10);
            for (int i = 0; i < cards.GetLength(0); i++)
            {
                var r = new Rect(body.x + (i % 4) * (cw + UiKit.Px(12)), y + (i / 4) * UiKit.Px(76), cw, UiKit.Px(66));
                UiKit.PanelBox(r);
                GUI.Label(new Rect(r.x + UiKit.Px(12), r.y + UiKit.Px(10), r.width, UiKit.Px(16)), cards[i, 0], UiKit.StatKey);
                GUI.Label(new Rect(r.x + UiKit.Px(12), r.y + UiKit.Px(26), r.width, UiKit.Px(34)), cards[i, 1],
                    UiKit.TextBold(20, i == 0 ? UiKit.Acid : UiKit.Ink, TextAnchor.UpperLeft));
            }
            y += UiKit.Px(170);

            GUI.Label(new Rect(body.x, y, body.width, rowH), "OBJECTIVE", UiKit.H2);
            var wrap = UiKit.Text(14, Util.Hex(0xc0c5bd), TextAnchor.UpperLeft);
            wrap.wordWrap = true;
            GUI.Label(new Rect(body.x, y + rowH, UiKit.Px(760), UiKit.Px(70)),
                "Hold the annex. Buy your way deeper, restore the grid, and survive as long as the horde lets you. There is no extraction.",
                wrap);

            DrawFooter(new[] { "DEPLOY", "BACK" }, i =>
            {
                if (i == 0) _app.StartRun();
                else Back();
            });
        }

        // ---------------------------------------------------------------- MP
        void DrawMultiplayer()
        {
            Backdrop(true);
            var body = PanelHead("CO-OP ", "LOBBY", "PLACEHOLDER — NOT YET ONLINE");
            var wrap = UiKit.Text(14, Util.Hex(0xc0c5bd), TextAnchor.UpperLeft);
            wrap.wordWrap = true;
            GUI.Label(new Rect(body.x, body.y, UiKit.Px(820), UiKit.Px(90)),
                "Networked co-op is not enabled in this build. The systems underneath it already assume more than one "
                + "survivor: the round manager scales spawn counts by player count, the downed state has a revive path, "
                + "and every entity update is driven from a single authoritative tick.", wrap);

            float y = body.y + UiKit.Px(110);
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(body.x + i * (UiKit.Px(230) + UiKit.Px(12)), y, UiKit.Px(230), UiKit.Px(88));
                UiKit.Fill(r, UiKit.PanelSoft);
                UiKit.Frame(r, i == 0 ? UiKit.Acid : new Color(1, 1, 1, 0.18f), 1f);
                GUI.Label(new Rect(r.x, r.y + UiKit.Px(16), r.width, UiKit.Px(20)), "SLOT " + (i + 1),
                    UiKit.Text(11, UiKit.Dim, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(r.x, r.y + UiKit.Px(40), r.width, UiKit.Px(28)),
                    i == 0 ? GameSettings.PlayerName : "OPEN",
                    UiKit.TextBold(18, i == 0 ? UiKit.Ink : UiKit.Dim, TextAnchor.MiddleCenter));
            }

            y += UiKit.Px(120);
            GUI.Label(new Rect(body.x, y, body.width, UiKit.Px(28)), "WHAT IS ALREADY WIRED FOR IT", UiKit.H2);
            GUI.Label(new Rect(body.x, y + UiKit.Px(30), UiKit.Px(820), UiKit.Px(80)),
                "RoundManager takes a player-count multiplier · PlayerController.downed supports external revives · "
                + "ZombieManager targets the nearest survivor through a shared flow field. Missing: transport, state "
                + "replication and lobby matchmaking. Everything else is local-authoritative already.", wrap);

            DrawFooter(new[] { "BACK" }, i => Back());
        }

        // ---------------------------------------------------------------- loadout
        void DrawLoadout()
        {
            Backdrop(true);
            var body = PanelHead("LOAD", "OUT", "EVERY WEAPON IN THE POOL");

            float listW = UiKit.Px(300);
            var listRect = new Rect(body.x, body.y, listW, body.height);
            float rowH = UiKit.Px(26);
            _scroll = GUI.BeginScrollView(listRect, _scroll,
                new Rect(0, 0, listW - UiKit.Px(16), _loadoutList.Count * rowH + UiKit.Px(120)));
            WeaponClass lastClass = WeaponClass.Melee;
            float ly = 0f;
            for (int i = 0; i < _loadoutList.Count; i++)
            {
                var w = _loadoutList[i];
                if (w.cls != lastClass)
                {
                    lastClass = w.cls;
                    GUI.Label(new Rect(0, ly, listW, rowH), w.ClassLabel + "S", UiKit.H2);
                    ly += rowH;
                }
                var r = new Rect(0, ly, listW - UiKit.Px(20), rowH);
                bool sel = i == _loadoutIndex;
                if (sel) UiKit.Fill(r, new Color(0.62f, 0.85f, 0.23f, 0.18f));
                UiKit.Fill(new Rect(r.x + UiKit.Px(4), r.y + rowH * 0.5f - UiKit.Px(4), UiKit.Px(8), UiKit.Px(8)), w.RarityColor);
                GUI.Label(new Rect(r.x + UiKit.Px(20), r.y, r.width, rowH), w.name,
                    UiKit.Text(14, sel ? UiKit.Ink : Util.Hex(0xc2c7c0), TextAnchor.MiddleLeft));
                if (r.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _loadoutIndex = i;
                    _perkIndex = -1;
                    Game.Audio.Play(Sfx.UiMove, 1f);
                }
                ly += rowH;
            }

            // Perk reference lives here too, so the screen is a real reference sheet.
            GUI.Label(new Rect(0, ly, listW, rowH), "PERKS", UiKit.H2);
            ly += rowH;
            for (int i = 0; i < Perks.All.Count; i++)
            {
                var pk = Perks.All[i];
                var r = new Rect(0, ly, listW - UiKit.Px(20), rowH);
                bool sel = _perkIndex == i;
                if (sel) UiKit.Fill(r, new Color(0.62f, 0.85f, 0.23f, 0.18f));
                UiKit.Fill(new Rect(r.x + UiKit.Px(4), r.y + rowH * 0.5f - UiKit.Px(4), UiKit.Px(8), UiKit.Px(8)), pk.Color);
                GUI.Label(new Rect(r.x + UiKit.Px(20), r.y, r.width, rowH),
                    pk.name + "  -  " + Util.FormatNumber(pk.cost),
                    UiKit.Text(14, sel ? UiKit.Ink : Util.Hex(0xc2c7c0), TextAnchor.MiddleLeft));
                if (r.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _perkIndex = i;
                    Game.Audio.Play(Sfx.UiMove, 1f);
                }
                ly += rowH;
            }
            GUI.EndScrollView();

            if (_perkIndex >= 0)
            {
                DrawPerkDetail(body, listW, Perks.All[Mathf.Clamp(_perkIndex, 0, Perks.All.Count - 1)]);
                DrawFooter(new[] { "BACK" }, i => Back());
                return;
            }

            var def = _loadoutList[Mathf.Clamp(_loadoutIndex, 0, _loadoutList.Count - 1)];
            float dx = body.x + listW + UiKit.Px(24);
            float dw = body.width - listW - UiKit.Px(24);

            GUI.Label(new Rect(dx, body.y, dw, UiKit.Px(40)), def.name, UiKit.TextBold(30, UiKit.Ink, TextAnchor.UpperLeft));
            GUI.Label(new Rect(dx, body.y + UiKit.Px(38), dw, UiKit.Px(22)),
                def.ClassLabel + "   ·   " + def.RarityLabel,
                UiKit.Text(12, def.RarityColor, TextAnchor.UpperLeft));
            var wrap = UiKit.Text(14, Util.Hex(0xc0c5bd), TextAnchor.UpperLeft);
            wrap.wordWrap = true;
            GUI.Label(new Rect(dx, body.y + UiKit.Px(66), Mathf.Min(dw, UiKit.Px(520)), UiKit.Px(60)), def.blurb, wrap);

            string[] keys = { "DAMAGE", "FIRE RATE", "MAGAZINE", "RESERVE", "RECOIL", "ADS SPEED", "RELOAD", "MOBILITY" };
            float[] fracs =
            {
                Mathf.Clamp01(def.damage / 900f), Mathf.Clamp01(def.rpm / 1000f),
                Mathf.Clamp01(def.mag / 125f), Mathf.Clamp01(def.reserve / 500f),
                Mathf.Clamp01(def.recoilV / 7f), Mathf.Clamp01(1f - def.adsTime / 0.5f),
                Mathf.Clamp01(1f - def.reloadTime / 5f), Mathf.Clamp01(def.moveScale),
            };
            string[] vals =
            {
                def.damage.ToString("0"), def.rpm + " rpm", def.mag.ToString(), def.reserve.ToString(),
                def.recoilV.ToString("0.0"), def.adsTime.ToString("0.00") + "s",
                def.reloadTime.ToString("0.0") + "s", Mathf.RoundToInt(def.moveScale * 100f) + "%",
            };
            float by = body.y + UiKit.Px(140);
            for (int i = 0; i < keys.Length; i++)
            {
                float ry = by + i * UiKit.Px(28);
                GUI.Label(new Rect(dx, ry, UiKit.Px(120), UiKit.Px(22)), keys[i], UiKit.StatKey);
                UiKit.Bar(new Rect(dx + UiKit.Px(130), ry + UiKit.Px(8), UiKit.Px(300), UiKit.Px(6)),
                    fracs[i], UiKit.Acid, new Color(1, 1, 1, 0.1f));
                GUI.Label(new Rect(dx + UiKit.Px(440), ry, UiKit.Px(120), UiKit.Px(22)), vals[i],
                    UiKit.Text(12, Util.Hex(0xcfd3cc), TextAnchor.MiddleLeft));
            }

            float pyy = by + UiKit.Px(240);
            GUI.Label(new Rect(dx, pyy, dw, UiKit.Px(24)),
                def.wallCost > 0
                    ? "WALL PURCHASE " + Util.FormatNumber(def.wallCost) + "   ·   AMMO " + Util.FormatNumber(def.wallAmmoCost)
                    : (def.boxWeight > 0 ? "MYSTERY CRATE ONLY" : "STANDARD ISSUE"),
                UiKit.Text(12, UiKit.Dim, TextAnchor.UpperLeft));

            DrawFooter(new[] { "BACK" }, i => Back());
        }

        /// <summary>Full description of a perk: what it costs, and what it buys.</summary>
        void DrawPerkDetail(Rect body, float listW, PerkDef pk)
        {
            float dx = body.x + listW + UiKit.Px(24);
            float dw = body.width - listW - UiKit.Px(24);

            // Badge with the perk's letter, the way it reads on the HUD.
            var badge = new Rect(dx, body.y + UiKit.Px(2), UiKit.Px(52), UiKit.Px(52));
            UiKit.Fill(badge, new Color(0f, 0f, 0f, 0.45f));
            UiKit.Frame(badge, pk.Color, UiKit.Px(2));
            GUI.Label(badge, pk.letter, UiKit.TextBold(24, pk.Color, TextAnchor.MiddleCenter));

            float tx = dx + UiKit.Px(68);
            GUI.Label(new Rect(tx, body.y, dw, UiKit.Px(40)), pk.name,
                UiKit.TextBold(28, pk.Color, TextAnchor.UpperLeft));
            GUI.Label(new Rect(tx, body.y + UiKit.Px(36), dw, UiKit.Px(22)),
                "PERK   ·   " + Util.FormatNumber(pk.cost) + " POINTS",
                UiKit.Text(12, UiKit.Dim, TextAnchor.UpperLeft));

            var wrap = UiKit.Text(15, UiKit.Ink, TextAnchor.UpperLeft);
            wrap.wordWrap = true;
            GUI.Label(new Rect(dx, body.y + UiKit.Px(74), Mathf.Min(dw, UiKit.Px(560)), UiKit.Px(26)),
                pk.tagline, wrap);
            var wrapDim = UiKit.Text(13, Util.Hex(0xc0c5bd), TextAnchor.UpperLeft);
            wrapDim.wordWrap = true;
            GUI.Label(new Rect(dx, body.y + UiKit.Px(104), Mathf.Min(dw, UiKit.Px(560)), UiKit.Px(26)),
                pk.effect, wrapDim);

            float py = body.y + UiKit.Px(146);
            if (pk.detail != null)
            {
                for (int i = 0; i < pk.detail.Length; i++)
                {
                    UiKit.Fill(new Rect(dx, py + UiKit.Px(7), UiKit.Px(6), UiKit.Px(6)), UiKit.Acid);
                    GUI.Label(new Rect(dx + UiKit.Px(16), py, Mathf.Min(dw, UiKit.Px(540)), UiKit.Px(40)),
                        pk.detail[i], wrapDim);
                    py += UiKit.Px(34);
                }
            }

            GUI.Label(new Rect(dx, py + UiKit.Px(14), Mathf.Min(dw, UiKit.Px(560)), UiKit.Px(46)),
                "Perk machines need the power on. Every perk is lost when you are downed - "
                + "buy them back before the next horde arrives.",
                UiKit.Text(12, UiKit.Dim, TextAnchor.UpperLeft));
        }

        // ---------------------------------------------------------------- stats
        void DrawStats()
        {
            Backdrop(true);
            var body = PanelHead("CAREER ", "RECORD", "STORED LOCALLY ON THIS DEVICE");
            var d = SaveData.Data;

            string[,] cards =
            {
                { "HIGHEST ROUND", d.highestRound.ToString() },
                { "LIFETIME KILLS", Util.FormatNumber(d.lifetimeKills) },
                { "HEADSHOTS", Util.FormatNumber(d.lifetimeHeadshots) },
                { "RUNS PLAYED", Util.FormatNumber(d.gamesPlayed) },
                { "TIME SURVIVED", Util.FormatTime(d.totalTimeSurvived) },
                { "POINTS EARNED", Util.FormatNumber(d.totalPointsEarned) },
                { "BARRIERS FIXED", Util.FormatNumber(d.barriersRepaired) },
                { "CRATE SPINS", Util.FormatNumber(d.boxSpins) },
                { "TIMES DOWNED", Util.FormatNumber(d.totalDowns) },
            };
            float cw = body.width / 5f - UiKit.Px(10);
            for (int i = 0; i < cards.GetLength(0); i++)
            {
                var r = new Rect(body.x + (i % 5) * (cw + UiKit.Px(12)), body.y + (i / 5) * UiKit.Px(84), cw, UiKit.Px(72));
                UiKit.PanelBox(r);
                GUI.Label(new Rect(r.x + UiKit.Px(12), r.y + UiKit.Px(10), r.width, UiKit.Px(16)), cards[i, 0], UiKit.StatKey);
                GUI.Label(new Rect(r.x + UiKit.Px(12), r.y + UiKit.Px(28), r.width, UiKit.Px(36)), cards[i, 1],
                    UiKit.TextBold(24, i == 0 ? UiKit.Acid : UiKit.Ink, TextAnchor.UpperLeft));
            }

            float y = body.y + UiKit.Px(190);
            if (d.weapons.Count > 0)
            {
                GUI.Label(new Rect(body.x, y, body.width, UiKit.Px(24)), "WEAPON PERFORMANCE", UiKit.H2);
                y += UiKit.Px(28);
                d.weapons.Sort((a, b) => b.kills.CompareTo(a.kills));
                int shown = Mathf.Min(8, d.weapons.Count);
                for (int i = 0; i < shown; i++)
                {
                    var wr = d.weapons[i];
                    string nm = Weapons.All.ContainsKey(wr.id) ? Weapons.All[wr.id].name : wr.id.ToUpperInvariant();
                    string acc = wr.shotsFired > 0 ? (100f * wr.shotsHit / wr.shotsFired).ToString("0.0") + "%" : "—";
                    GUI.Label(new Rect(body.x, y, UiKit.Px(260), UiKit.Px(22)), nm, UiKit.Text(13, UiKit.Ink, TextAnchor.MiddleLeft));
                    GUI.Label(new Rect(body.x + UiKit.Px(270), y, UiKit.Px(120), UiKit.Px(22)), "KILLS " + wr.kills, UiKit.Small);
                    GUI.Label(new Rect(body.x + UiKit.Px(400), y, UiKit.Px(140), UiKit.Px(22)), "HS " + wr.headshots, UiKit.Small);
                    GUI.Label(new Rect(body.x + UiKit.Px(530), y, UiKit.Px(140), UiKit.Px(22)), "ACC " + acc, UiKit.Small);
                    y += UiKit.Px(22);
                }
            }

            if (d.history.Count > 0)
            {
                y += UiKit.Px(16);
                GUI.Label(new Rect(body.x, y, body.width, UiKit.Px(24)), "RECENT RUNS", UiKit.H2);
                y += UiKit.Px(28);
                for (int i = 0; i < Mathf.Min(5, d.history.Count); i++)
                {
                    var r = d.history[i];
                    GUI.Label(new Rect(body.x, y, UiKit.Px(700), UiKit.Px(22)),
                        r.date + "     ROUND " + r.round + "     " + Util.FormatNumber(r.kills) + " KILLS     "
                        + Util.FormatTime(r.time) + "     " + Util.FormatNumber(r.points) + " PTS",
                        UiKit.Small);
                    y += UiKit.Px(22);
                }
            }

            DrawFooter(new[] { "BACK", "WIPE RECORD" }, i =>
            {
                if (i == 0) Back();
                else SaveData.Wipe();
            });
        }

        // ---------------------------------------------------------------- settings
        void DrawSettings()
        {
            Backdrop(true);
            var body = PanelHead("SET", "TINGS", "SAVED AUTOMATICALLY");
            float colW = body.width * 0.46f;
            float rowH = UiKit.Px(30);
            float y = body.y;

            GUI.Label(new Rect(body.x, y, colW, rowH), "AUDIO", UiKit.H2); y += rowH;
            GameSettings.MasterVolume = UiKit.SliderRow(new Rect(body.x, y, colW, rowH), "Master Volume",
                GameSettings.MasterVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%"); y += rowH;
            GameSettings.MusicVolume = UiKit.SliderRow(new Rect(body.x, y, colW, rowH), "Music Volume",
                GameSettings.MusicVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%"); y += rowH;
            GameSettings.SfxVolume = UiKit.SliderRow(new Rect(body.x, y, colW, rowH), "SFX Volume",
                GameSettings.SfxVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%"); y += rowH + UiKit.Px(14);

            GUI.Label(new Rect(body.x, y, colW, rowH), "CONTROLS", UiKit.H2); y += rowH;
            GameSettings.MouseSensitivity = UiKit.SliderRow(new Rect(body.x, y, colW, rowH), "Mouse Sensitivity",
                GameSettings.MouseSensitivity, 0.02f, 1f, GameSettings.MouseSensitivity.ToString("0.00")); y += rowH;
            GameSettings.ControllerSensitivity = UiKit.SliderRow(new Rect(body.x, y, colW, rowH), "Controller Sensitivity",
                GameSettings.ControllerSensitivity, 0.4f, 8f, GameSettings.ControllerSensitivity.ToString("0.0")); y += rowH;
            GameSettings.InvertY = UiKit.ToggleRow(new Rect(body.x, y, colW, rowH), "Invert Vertical Look",
                GameSettings.InvertY); y += rowH + UiKit.Px(14);

            GUI.Label(new Rect(body.x, y, colW, rowH), "VIDEO", UiKit.H2); y += rowH;
            GameSettings.Fov = UiKit.SliderRow(new Rect(body.x, y, colW, rowH), "Field of View",
                GameSettings.Fov, 60f, 120f, Mathf.RoundToInt(GameSettings.Fov) + "°"); y += rowH;

            GUI.Label(new Rect(body.x, y, colW * 0.34f, rowH), "Graphics Quality", UiKit.Label);
            string[] qualities = { "low", "medium", "high", "ultra" };
            float qx = body.x + colW * 0.36f;
            foreach (var q in qualities)
            {
                var r = new Rect(qx, y + UiKit.Px(4), UiKit.Px(80), UiKit.Px(22));
                bool sel = GameSettings.Quality == q;
                UiKit.Fill(r, sel ? UiKit.Acid : new Color(1, 1, 1, 0.06f));
                GUI.Label(r, GameSettings.Presets[q].label,
                    UiKit.TextBold(12, sel ? Util.Hex(0x08110a) : UiKit.Ink, TextAnchor.MiddleCenter));
                if (r.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    GameSettings.Quality = q;
                    GameSettings.ApplyQuality();
                }
                qx += UiKit.Px(86);
            }
            y += rowH;

            GameSettings.MotionBlur = UiKit.ToggleRow(new Rect(body.x, y, colW, rowH), "Motion Blur", GameSettings.MotionBlur); y += rowH;
            GameSettings.ScreenShake = UiKit.ToggleRow(new Rect(body.x, y, colW, rowH), "Screen Shake", GameSettings.ScreenShake); y += rowH;
            GameSettings.ShowCrosshair = UiKit.ToggleRow(new Rect(body.x, y, colW, rowH), "Show Crosshair", GameSettings.ShowCrosshair); y += rowH;
            bool fs = UiKit.ToggleRow(new Rect(body.x, y, colW, rowH), "Fullscreen", GameSettings.Fullscreen);
            if (fs != GameSettings.Fullscreen) { GameSettings.Fullscreen = fs; GameSettings.ApplyQuality(); }

            // ---- keybinds in the right column
            float kx = body.x + colW + UiKit.Px(50);
            float ky = body.y;
            GUI.Label(new Rect(kx, ky, colW, rowH), "KEY BINDINGS", UiKit.H2);
            ky += rowH;
            foreach (Bind b in System.Enum.GetValues(typeof(Bind)))
            {
                GUI.Label(new Rect(kx, ky, UiKit.Px(220), rowH), GameSettings.Label(b), UiKit.Label);
                var r = new Rect(kx + UiKit.Px(230), ky + UiKit.Px(3), UiKit.Px(150), UiKit.Px(24));
                bool listening = _rebinding.HasValue && _rebinding.Value == b;
                UiKit.Fill(r, listening ? UiKit.Acid : new Color(1, 1, 1, 0.06f));
                UiKit.Frame(r, listening ? UiKit.Acid : new Color(1, 1, 1, 0.16f), 1f);
                GUI.Label(r, listening ? "PRESS A KEY" : GameSettings.KeyLabel(GameSettings.Key(b)),
                    UiKit.TextBold(12, listening ? Util.Hex(0x08110a) : UiKit.Ink, TextAnchor.MiddleCenter));
                if (r.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _rebinding = b;
                    InputMap.Capturing = true;
                }
                ky += UiKit.Px(27);
            }

            DrawFooter(new[] { "BACK", "RESET BINDS", "RESET ALL" }, i =>
            {
                if (i == 0) { GameSettings.Save(); Back(); }
                else if (i == 1) GameSettings.ResetBinds();
                else { GameSettings.ResetAll(); GameSettings.ApplyQuality(); }
            });
        }

        // ---------------------------------------------------------------- credits
        void DrawCredits()
        {
            Backdrop(true);
            var body = PanelHead("CRE", "DITS", "");
            var wrap = UiKit.Text(14, Util.Hex(0xc6cac2), TextAnchor.UpperLeft);
            wrap.wordWrap = true;
            float y = body.y;

            string[,] blocks =
            {
                { "ROTGRID", "An original round-based survival shooter." },
                { "DESIGN & CODE", "Built from scratch for this project." },
                { "ART", "Every texture, model, animation and sound in this build is generated procedurally at runtime — "
                         + "canvas-drawn materials, primitive-built meshes and synthesised audio. No third-party art, audio "
                         + "or trademarked content is used anywhere." },
                { "TECHNOLOGY", "Unity, the built-in render pipeline, the legacy input manager and IMGUI. No packages "
                                + "and no imported assets." },
                { "INSPIRATION", "The wave-survival loop — buy doors, restore power, upgrade weapons, chase a higher round "
                                 + "— is a genre convention with a long lineage. Nothing here is copied from any specific "
                                 + "title: names, map, weapons, perks and audio are original to this project." },
                { "THANKS", "To everyone who ever said \"one more round\"." },
            };
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                GUI.Label(new Rect(body.x, y, body.width, UiKit.Px(24)), blocks[i, 0], UiKit.H2);
                y += UiKit.Px(26);
                GUI.Label(new Rect(body.x, y, UiKit.Px(820), UiKit.Px(70)), blocks[i, 1], wrap);
                y += UiKit.Px(blocks[i, 1].Length > 120 ? 74 : 30);
            }

            DrawFooter(new[] { "BACK" }, i => Back());
        }

        // ---------------------------------------------------------------- pause
        void DrawPause()
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            UiKit.Fill(new Rect(0, 0, w, h), new Color(0.012f, 0.02f, 0.02f, 0.82f));
            GUI.Label(new Rect(0, h * 0.28f, w, UiKit.Px(70)), "PAUSED",
                UiKit.TextBold(50, UiKit.Acid, TextAnchor.MiddleCenter));

            string[] labels = { "RESUME", "SETTINGS", "RESTART RUN", "ABANDON RUN" };
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(w * 0.5f - UiKit.Px(150), h * 0.42f + i * UiKit.Px(52), UiKit.Px(300), UiKit.Px(42));
                if (UiKit.Button(r, labels[i], i == 0))
                {
                    Game.Audio.Play(Sfx.UiSelect, 1f);
                    if (i == 0) _app.Resume();
                    else if (i == 1) Push(Screen2.Settings);
                    else if (i == 2) _app.StartRun();
                    else _app.EndToMenu();
                }
            }
        }

        // ---------------------------------------------------------------- game over
        void DrawGameOver()
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            UiKit.Fill(new Rect(0, 0, w, h), new Color(0.02f, 0.012f, 0.012f, 0.92f));
            var s = _app.LastSummary;
            if (s == null) return;

            GUI.Label(new Rect(0, h * 0.1f, w, UiKit.Px(130)), "GAME OVER",
                UiKit.TextBold(96, Util.Hex(0xd34a3c), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(0, h * 0.1f + UiKit.Px(120), w, UiKit.Px(24)),
                MapData.Name + "   ·   " + GameSettings.PlayerName,
                UiKit.Text(12, UiKit.Dim, TextAnchor.MiddleCenter));
            if (s.newBest)
                GUI.Label(new Rect(0, h * 0.1f + UiKit.Px(146), w, UiKit.Px(24)), "NEW PERSONAL BEST",
                    UiKit.TextBold(13, UiKit.Amber, TextAnchor.MiddleCenter));

            string[,] cards =
            {
                { "ROUNDS SURVIVED", s.roundsSurvived.ToString() },
                { "HIGHEST ROUND", s.round.ToString() },
                { "ZOMBIES KILLED", Util.FormatNumber(s.kills) },
                { "HEADSHOTS", Util.FormatNumber(s.headshots) },
                { "SURVIVAL TIME", Util.FormatTime(s.time) },
                { "POINTS EARNED", Util.FormatNumber(s.pointsEarned) },
                { "ACCURACY", s.shotsFired > 0 ? (100f * s.shotsHit / s.shotsFired).ToString("0.0") + "%" : "—" },
                { "MELEE KILLS", Util.FormatNumber(s.meleeKills) },
                { "BARRIERS FIXED", Util.FormatNumber(s.repairs) },
            };
            float cw = UiKit.Px(190), ch = UiKit.Px(72);
            float gridW = 3 * cw + 2 * UiKit.Px(12);
            for (int i = 0; i < cards.GetLength(0); i++)
            {
                var r = new Rect(w * 0.5f - gridW * 0.5f + (i % 3) * (cw + UiKit.Px(12)),
                    h * 0.34f + (i / 3) * (ch + UiKit.Px(12)), cw, ch);
                UiKit.Fill(r, new Color(1, 1, 1, 0.04f));
                UiKit.Frame(r, new Color(1, 1, 1, 0.1f), 1f);
                GUI.Label(new Rect(r.x, r.y + UiKit.Px(12), r.width, UiKit.Px(18)), cards[i, 0],
                    UiKit.Text(10, UiKit.Dim, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(r.x, r.y + UiKit.Px(30), r.width, UiKit.Px(34)), cards[i, 1],
                    UiKit.TextBold(24, UiKit.Ink, TextAnchor.MiddleCenter));
            }

            float by = h * 0.34f + 3 * (ch + UiKit.Px(12)) + UiKit.Px(20);
            if (UiKit.Button(new Rect(w * 0.5f - UiKit.Px(220), by, UiKit.Px(200), UiKit.Px(44)), "RESTART", true))
                _app.StartRun();
            if (UiKit.Button(new Rect(w * 0.5f + UiKit.Px(20), by, UiKit.Px(200), UiKit.Px(44)), "RETURN TO MENU", false))
                _app.EndToMenu();
        }

        // ---------------------------------------------------------------- footer
        void DrawFooter(string[] labels, System.Action<int> onClick)
        {
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            float pad = UiKit.Px(70);
            UiKit.Fill(new Rect(pad, h - UiKit.Px(84), w - pad * 2f, 1f), new Color(1, 1, 1, 0.12f));
            float x = pad;
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(x, h - UiKit.Px(70), UiKit.Px(190), UiKit.Px(42));
                if (UiKit.Button(r, labels[i], i == 0))
                {
                    Game.Audio.Play(Sfx.UiSelect, 1f);
                    onClick(i);
                }
                x += UiKit.Px(202);
            }
        }
    }
}
