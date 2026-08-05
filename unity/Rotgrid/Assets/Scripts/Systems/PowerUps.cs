using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public class DropDef
    {
        public string id, label, glyph;
        public int color;
        public float weight, duration;
        public Color Color { get { return Util.Hex(color); } }
    }

    /// <summary>
    /// Special drops. A running kill counter guarantees one every so often on
    /// top of the flat chance, which keeps the pacing readable instead of
    /// streaky.
    /// </summary>
    public class PowerUpSystem
    {
        const float Lifetime = 26f;
        const float BlinkAt = 7f;

        public static readonly List<DropDef> Drops = new List<DropDef>
        {
            new DropDef { id="maxAmmo", label="MAX AMMO", glyph="AMMO", color=0x4aa3ff, weight=24, duration=0 },
            new DropDef { id="instantKill", label="INSTANT KILL", glyph="KILL", color=0xc0392b, weight=16, duration=30 },
            new DropDef { id="doublePoints", label="DOUBLE POINTS", glyph="X2", color=0x9fd93a, weight=18, duration=30 },
            new DropDef { id="nuke", label="PURGE", glyph="PURGE", color=0xe8a33d, weight=12, duration=0 },
            new DropDef { id="fireSale", label="FIRE SALE", glyph="SALE", color=0xb479ff, weight=8, duration=35 },
            new DropDef { id="bonusPoints", label="BONUS POINTS", glyph="+", color=0xffd24a, weight=22, duration=0 },
        };

        class Item
        {
            public GameObject go;
            public MachineInfo info;
            public DropDef def;
            public bool alive;
            public float t, baseY;
        }

        readonly List<Item> _pool = new List<Item>();
        readonly List<Item> _live = new List<Item>();
        readonly Dictionary<string, float> _active = new Dictionary<string, float>();
        readonly Transform _parent;
        int _killsSinceDrop;
        int _threshold = 55;

        public PowerUpSystem(Transform parent) { _parent = parent; }

        public bool IsActive(string id) { return _active.ContainsKey(id) && _active[id] > 0f; }

        Item Acquire(DropDef def)
        {
            foreach (var p in _pool) if (!p.alive && p.def == def) { p.alive = true; p.t = Lifetime; p.go.SetActive(true); _live.Add(p); return p; }
            var go = Props.PowerUpIcon(def.Color, def.glyph, _parent);
            var item = new Item { go = go, info = go.GetComponent<MachineInfo>(), def = def, alive = true, t = Lifetime };
            _pool.Add(item);
            _live.Add(item);
            return item;
        }

        /// <summary>Called on every zombie death.</summary>
        public void OnKill(Vector3 pos, float groundY, int round)
        {
            _killsSinceDrop++;
            bool guaranteed = _killsSinceDrop >= _threshold;
            if (!guaranteed && Random.value > 0.022f) return;
            _killsSinceDrop = 0;
            _threshold = 45 + Random.Range(0, 30);
            Spawn(pos, groundY, round);
        }

        public void Spawn(Vector3 pos, float groundY, int round)
        {
            var weights = new float[Drops.Count];
            for (int i = 0; i < Drops.Count; i++)
            {
                float w = Drops[i].weight;
                if (Drops[i].id == "fireSale" && round < 5) w = 0f;
                if (Drops[i].id == "nuke" && round < 3) w *= 0.4f;
                weights[i] = w;
            }
            var def = Drops[Util.WeightedPick(weights)];
            var item = Acquire(def);
            item.baseY = groundY + 0.85f;
            item.go.transform.position = new Vector3(pos.x, item.baseY, pos.z);
        }

        void Collect(Item item, GameRun run)
        {
            item.alive = false;
            item.go.SetActive(false);
            var def = item.def;
            Game.Audio.Play(Sfx.PowerUp, 1f);
            run.Toast(def.label, ToastKind.Good, true);

            switch (def.id)
            {
                case "maxAmmo": run.weapons.RefillAll(); break;
                case "nuke": run.Purge(); break;
                case "bonusPoints": run.player.AddPoints(500); break;
                case "fireSale":
                    _active["fireSale"] = def.duration;
                    run.box.StartFireSale(def.duration);
                    break;
                default:
                    _active[def.id] = def.duration;
                    break;
            }
            if (def.id == "doublePoints") run.player.pointMultiplier = 2f;
            if (def.id == "instantKill") run.player.instantKill = true;
        }

        public void Update(float dt, float time, GameRun run)
        {
            var expired = new List<string>();
            var keys = new List<string>(_active.Keys);
            foreach (var id in keys)
            {
                float t = _active[id] - dt;
                if (t <= 0f) expired.Add(id);
                else _active[id] = t;
            }
            foreach (var id in expired)
            {
                _active.Remove(id);
                if (id == "doublePoints") run.player.pointMultiplier = 1f;
                if (id == "instantKill") run.player.instantKill = false;
                run.Toast(LabelFor(id) + " EXPIRED", ToastKind.Warn, false);
            }

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var it = _live[i];
                if (!it.alive) { _live.RemoveAt(i); continue; }
                it.t -= dt;
                if (it.t <= 0f)
                {
                    it.alive = false;
                    it.go.SetActive(false);
                    _live.RemoveAt(i);
                    continue;
                }
                it.go.transform.rotation = Quaternion.Euler(0, time * 97f, 0);
                var p = it.go.transform.position;
                it.go.transform.position = new Vector3(p.x, it.baseY + Mathf.Sin(time * 2.6f) * 0.12f, p.z);

                float blink = it.t < BlinkAt ? (Mathf.Sin(time * 16f) > 0f ? 1f : 0.15f) : 1f;
                if (it.info != null && it.info.light != null) it.info.light.intensity = 18f * blink;
                if (it.info != null && it.info.glow != null && it.info.glow.Length > 0 && it.info.glow[0] != null)
                    Art.SetEmission(it.info.glow[0].material, it.def.Color * (1.6f * blink));

                var pp = run.player.pos;
                float dx = pp.x - p.x, dz = pp.z - p.z;
                if (dx * dx + dz * dz < 2.2f * 2.2f && Mathf.Abs(pp.y - (it.baseY - 0.85f)) < 2.5f)
                {
                    Collect(it, run);
                    _live.RemoveAt(i);
                }
            }
        }

        static string LabelFor(string id)
        {
            foreach (var d in Drops) if (d.id == id) return d.label;
            return id;
        }

        public struct HudEntry { public string label; public Color color; public float t; }

        public List<HudEntry> HudEntries()
        {
            var list = new List<HudEntry>();
            foreach (var kv in _active)
            {
                foreach (var d in Drops)
                {
                    if (d.id != kv.Key) continue;
                    list.Add(new HudEntry { label = d.label, color = d.Color, t = kv.Value });
                    break;
                }
            }
            return list;
        }

        public void Reset()
        {
            foreach (var it in _live) { it.alive = false; it.go.SetActive(false); }
            _live.Clear();
            _active.Clear();
            _killsSinceDrop = 0;
        }
    }
}
