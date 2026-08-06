using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public struct WorldHit
    {
        public bool hit;
        public float dist;
        public Vector3 point;
        public Vector3 normal;
    }

    /// <summary>
    /// Builds the map geometry, owns the collider list the characters move
    /// against, and holds zone/door/power state. Movement uses hand-rolled AABB
    /// collision rather than Unity physics so it behaves identically to the
    /// browser build and needs no rigidbody tuning.
    /// </summary>
    public class WorldBuilder
    {
        const float WallT = 0.5f;
        const float SillH = 1.05f;

        public readonly GameObject root;
        public readonly List<Aabb> colliders = new List<Aabb>();
        public readonly List<DoorObj> doors = new List<DoorObj>();
        public readonly List<Barricade> barricades = new List<Barricade>();
        public NavGrid nav;
        public bool Powered { get; private set; }

        readonly Dictionary<string, bool> _zoneActive = new Dictionary<string, bool>();
        readonly List<LightRig> _lights = new List<LightRig>();
        readonly List<MeshRenderer> _emergency = new List<MeshRenderer>();
        readonly List<Light> _emergencyLights = new List<Light>();
        Light _fill;

        class LightRig
        {
            public Light light;
            public MeshRenderer bulb;
            public float baseIntensity;
            public string zone;
            public float flickerSeed;
        }

        public WorldBuilder()
        {
            root = new GameObject("World");
            foreach (var z in MapData.Zones) _zoneActive[z.id] = z.unlocked;

            float x0, x1, z0, z1;
            MapData.Bounds(out x0, out x1, out z0, out z1);
            nav = new NavGrid(x0, x1, z0, z1, 1f);

            foreach (var z in MapData.Zones) BuildZone(z);
            foreach (var w in MapData.Windows) BuildWindow(w);
            foreach (var d in MapData.Doors) BuildDoor(d);
            foreach (var c in MapData.Clutter) BuildClutter(c);
            BuildLights();
            BuildDressing();

            for (int i = 0; i < MapData.Zones.Count; i++)
                nav.SetZoneActive(i, _zoneActive[MapData.Zones[i].id]);
        }

        // ================================================================ build
        static Material WallMat(string style)
        {
            switch (style)
            {
                case "metal": return Art.Lit("w_metal", Color.white, 0.45f, 0.55f, Art.MetalPanel(), 3f);
                case "tile": return Art.Lit("w_tile", Color.white, 0.6f, 0f, Art.Tile(), 5f);
                case "dirt": return Art.Lit("w_dirt", Color.white, 0f, 0f, Art.Dirt(), 4f);
                default: return Art.Lit("w_concrete", Color.white, 0.05f, 0f, Art.Concrete(), 3f);
            }
        }

        static Material FloorMat(string style)
        {
            switch (style)
            {
                case "metal": return Art.Lit("f_metal", Color.white, 0.4f, 0.45f, Art.MetalPanel(), 8f);
                case "tile": return Art.Lit("f_tile", Color.white, 0.65f, 0f, Art.Tile(), 9f);
                case "dirt": return Art.Lit("f_dirt", Color.white, 0f, 0f, Art.Dirt(), 10f);
                default: return Art.Lit("f_concrete", Color.white, 0.06f, 0f, Art.ConcreteFloor(), 10f);
            }
        }

        static Material CeilMat(string style)
        {
            switch (style)
            {
                case "metal": return Art.Lit("c_metal", Util.Hex(0x22262a), 0.2f, 0.4f);
                case "tile": return Art.Lit("c_tile", Util.Hex(0x30343a), 0.1f);
                case "dirt": return Art.Lit("c_dirt", Util.Hex(0x1a1712), 0f);
                default: return Art.Lit("c_concrete", Util.Hex(0x2a2e30), 0f);
            }
        }

        void BuildZone(ZoneDef z)
        {
            int zi = MapData.ZoneIndex(z.id);
            var floorMat = FloorMat(z.style);

            if (z.hasSlope)
            {
                float dy = z.slopeYTo - z.slopeYFrom;
                float run = Mathf.Abs(z.slopeTo - z.slopeFrom);
                float len = Mathf.Sqrt(run * run + dy * dy);
                var f = Props.Box(len, 0.4f, z.Depth, floorMat,
                    new Vector3(z.CenterX, (z.slopeYFrom + z.slopeYTo) * 0.5f - 0.2f, z.CenterZ), root.transform);
                f.transform.rotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, z.slopeTo - z.slopeFrom) * Mathf.Rad2Deg);
            }
            else
            {
                Props.Box(z.Width, 0.4f, z.Depth, floorMat, new Vector3(z.CenterX, z.y - 0.2f, z.CenterZ), root.transform);
                Props.Box(z.Width, 0.3f, z.Depth, CeilMat(z.style),
                    new Vector3(z.CenterX, z.y + z.h + 0.15f, z.CenterZ), root.transform);
            }

            nav.AddWalkableRect(z.x0 + 0.35f, z.x1 - 0.35f, z.z0 + 0.35f, z.z1 - 0.35f, z.y, zi, z);

            var mat = WallMat(z.style);
            BuildEdge(z, mat, true, z.z0, z.x0, z.x1, -1f);
            BuildEdge(z, mat, true, z.z1, z.x0, z.x1, 1f);
            BuildEdge(z, mat, false, z.x0, z.z0, z.z1, -1f);
            BuildEdge(z, mat, false, z.x1, z.z0, z.z1, 1f);
        }

        /// <summary>Builds one wall run, subtracting openings and window apertures.</summary>
        void BuildEdge(ZoneDef z, Material mat, bool alongX, float at, float from, float to, float normal)
        {
            var spans = new List<Vector2> { new Vector2(from, to) };

            System.Action<float, float> cut = (a, b) =>
            {
                var outSpans = new List<Vector2>();
                foreach (var s in spans)
                {
                    if (b <= s.x || a >= s.y) { outSpans.Add(s); continue; }
                    if (a > s.x) outSpans.Add(new Vector2(s.x, a));
                    if (b < s.y) outSpans.Add(new Vector2(b, s.y));
                }
                spans = outSpans;
            };

            foreach (var op in MapData.Openings)
            {
                if (alongX) { if (at >= op.z0 - 0.01f && at <= op.z1 + 0.01f) cut(op.x0, op.x1); }
                else if (at >= op.x0 - 0.01f && at <= op.x1 + 0.01f) cut(op.z0, op.z1);
            }
            foreach (var wd in MapData.Windows)
            {
                if (alongX) { if (Mathf.Abs(at - wd.z) < 0.6f && wd.nz != 0f) cut(wd.x - 1.6f, wd.x + 1.6f); }
                else if (Mathf.Abs(at - wd.x) < 0.6f && wd.nx != 0f) cut(wd.z - 1.6f, wd.z + 1.6f);
            }

            foreach (var s in spans)
            {
                if (s.y - s.x < 0.08f) continue;
                int pieces = z.hasSlope ? 6 : 1;
                for (int i = 0; i < pieces; i++)
                {
                    float a = Mathf.Lerp(s.x, s.y, (float)i / pieces);
                    float b = Mathf.Lerp(s.x, s.y, (float)(i + 1) / pieces);
                    float mid = (a + b) * 0.5f;
                    float len = b - a;
                    float yBase = z.hasSlope ? z.HeightAt(alongX ? mid : at, alongX ? at : mid) : z.y;
                    float wallCenter = at + normal * WallT * 0.5f;

                    if (alongX)
                    {
                        Props.Box(len, z.h + 0.6f, WallT, mat,
                            new Vector3(mid, yBase + (z.h + 0.6f) * 0.5f - 0.3f, wallCenter), root.transform);
                        colliders.Add(new Aabb(mid - len / 2, mid + len / 2,
                            wallCenter - WallT / 2, wallCenter + WallT / 2, yBase - 0.3f, yBase + z.h, false));
                    }
                    else
                    {
                        Props.Box(WallT, z.h + 0.6f, len, mat,
                            new Vector3(wallCenter, yBase + (z.h + 0.6f) * 0.5f - 0.3f, mid), root.transform);
                        colliders.Add(new Aabb(wallCenter - WallT / 2, wallCenter + WallT / 2,
                            mid - len / 2, mid + len / 2, yBase - 0.3f, yBase + z.h, false));
                    }
                }
            }
        }

        void BuildWindow(WindowDef def)
        {
            var zone = MapData.Zone(def.zone);
            var b = new Barricade(def, zone.y, root.transform, zone.h);
            barricades.Add(b);

            float hw = def.nz != 0f ? 1.7f : WallT;
            float hd = def.nz != 0f ? WallT : 1.7f;
            // Sill blocks the player; zombies vault it.
            colliders.Add(new Aabb(def.x - hw, def.x + hw, def.z - hd, def.z + hd, zone.y, zone.y + SillH, false));
            // Header above the aperture.
            colliders.Add(new Aabb(def.x - hw, def.x + hw, def.z - hd, def.z + hd, zone.y + 2.6f, zone.y + zone.h, false));

            // ---- alcove shell
            // Light enough that ambient fill gives the alcove some form — zombies
            // queueing at the boards need something to read against.
            var dark = Art.Lit("m_alcove", Util.Hex(0x1c242b), 0f);
            float cx = (def.ax0 + def.ax1) * 0.5f, cz = (def.az0 + def.az1) * 0.5f;
            float aw = def.ax1 - def.ax0, ad = def.az1 - def.az0;
            Props.Box(aw, 0.3f, ad, dark, new Vector3(cx, zone.y - 0.15f, cz), root.transform);
            Props.Box(aw, 0.3f, ad, dark, new Vector3(cx, zone.y + 3.6f, cz), root.transform);
            if (def.nz != 0f)
            {
                Props.Box(0.5f, 3.8f, ad, dark, new Vector3(def.ax0 - 0.25f, zone.y + 1.9f, cz), root.transform);
                Props.Box(0.5f, 3.8f, ad, dark, new Vector3(def.ax1 + 0.25f, zone.y + 1.9f, cz), root.transform);
                Props.Box(aw + 1f, 3.8f, 0.5f, dark,
                    new Vector3(cx, zone.y + 1.9f, def.nz > 0 ? def.az1 + 0.25f : def.az0 - 0.25f), root.transform);
            }
            else
            {
                Props.Box(aw, 3.8f, 0.5f, dark, new Vector3(cx, zone.y + 1.9f, def.az0 - 0.25f), root.transform);
                Props.Box(aw, 3.8f, 0.5f, dark, new Vector3(cx, zone.y + 1.9f, def.az1 + 0.25f), root.transform);
                Props.Box(0.5f, 3.8f, ad + 1f, dark,
                    new Vector3(def.nx > 0 ? def.ax1 + 0.25f : def.ax0 - 0.25f, zone.y + 1.9f, cz), root.transform);
            }

            int zi = MapData.ZoneIndex(def.zone);
            nav.AddWalkableRect(def.ax0 + 0.3f, def.ax1 - 0.3f, def.az0 + 0.3f, def.az1 - 0.3f, zone.y, zi, null);
            // Bridge the aperture itself so pathing can reach through the window.
            nav.AddWalkableRect(
                def.x - (def.nz != 0f ? 1.2f : 1.4f), def.x + (def.nz != 0f ? 1.2f : 1.4f),
                def.z - (def.nz != 0f ? 1.4f : 1.2f), def.z + (def.nz != 0f ? 1.4f : 1.2f),
                zone.y, zi, null);
        }

        void BuildDoor(DoorDef def)
        {
            var zone = MapData.Zone(def.zone);
            var d = new DoorObj(def, zone.y, zone.h, root.transform);
            doors.Add(d);
            colliders.Add(d.collider);
            nav.Block(def.x0, def.x1, def.z0, def.z1, true, 0.2f);
        }

        void BuildClutter(ClutterDef c)
        {
            var z = MapData.Zone(c.zone);
            var rng = new Util.Rng(c.seed);
            var placed = new List<Vector2>();
            string[] kinds = { "crates", "barrels", "shelves", "tables", "generators", "debris" };
            int[] counts = { c.crates, c.barrels, c.shelves, c.tables, c.generators, c.debris };

            for (int k = 0; k < kinds.Length; k++)
            {
                for (int n = 0; n < counts[k]; n++)
                {
                    for (int attempt = 0; attempt < 24; attempt++)
                    {
                        const float margin = 2.0f;
                        float px = z.x0 + margin + rng.Next() * Mathf.Max(0.1f, z.Width - margin * 2);
                        float pz = z.z0 + margin + rng.Next() * Mathf.Max(0.1f, z.Depth - margin * 2);
                        if (NearReserved(px, pz, 2.6f)) continue;
                        bool clash = false;
                        foreach (var p in placed)
                        {
                            if ((p - new Vector2(px, pz)).sqrMagnitude < 2.4f * 2.4f) { clash = true; break; }
                        }
                        if (clash) continue;

                        var prop = Props.RandomProp(kinds[k], Mathf.FloorToInt(rng.Next() * 1000), root.transform);
                        float y = z.HeightAt(px, pz);
                        prop.transform.localPosition = new Vector3(px, y, pz);
                        prop.transform.localRotation = Quaternion.Euler(0, rng.Next() * 360f, 0);
                        placed.Add(new Vector2(px, pz));

                        var info = prop.GetComponent<PropInfo>();
                        if (info != null)
                        {
                            float hw = Mathf.Max(info.width, info.depth) * 0.5f;
                            colliders.Add(new Aabb(px - hw, px + hw, pz - hw, pz + hw, y, y + info.height, info.platform));
                            nav.Block(px - hw, px + hw, pz - hw, pz + hw, true, 0.15f);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>Keeps clutter clear of doors, windows, machines and the spawn.</summary>
        static bool NearReserved(float x, float z, float r)
        {
            foreach (var w in MapData.Windows)
                if (Dist(w.x, w.z, x, z) < r + 2.2f) return true;
            foreach (var d in MapData.Doors)
                if (Dist((d.x0 + d.x1) * 0.5f, (d.z0 + d.z1) * 0.5f, x, z) < r + 3.0f) return true;
            foreach (var p in MapData.PerkSpots)
                if (Dist(p.x, p.z, x, z) < r + 1.6f) return true;
            foreach (var b in MapData.BoxSpots)
                if (Dist(b.x, b.z, x, z) < r + 2.0f) return true;
            foreach (var t in MapData.Traps)
                if (Dist(t.x, t.z, x, z) < r + 1.8f) return true;
            foreach (var wb in MapData.WallBuys)
                if (Dist(wb.x, wb.z, x, z) < r + 1.4f) return true;
            foreach (var a in MapData.ArmorStations)
                if (Dist(a.x, a.z, x, z) < r + 1.4f) return true;
            if (Dist(MapData.UpgradeMachinePos.x, MapData.UpgradeMachinePos.z, x, z) < r + 3.0f) return true;
            if (Dist(MapData.PowerSwitchPos.x, MapData.PowerSwitchPos.z, x, z) < r + 2.0f) return true;
            if (Dist(MapData.SpawnPos.x, MapData.SpawnPos.z, x, z) < r + 2.5f) return true;
            foreach (var op in MapData.Openings)
                if (x > op.x0 - 1.5f && x < op.x1 + 1.5f && z > op.z0 - 1.5f && z < op.z1 + 1.5f) return true;
            return false;
        }

        static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void BuildLights()
        {
            var fillGo = new GameObject("fill");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.rotation = Quaternion.Euler(52f, 30f, 0f);
            _fill = fillGo.AddComponent<Light>();
            _fill.type = LightType.Directional;
            _fill.color = Util.Hex(0x38465a);
            _fill.intensity = 0.28f;
            _fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Util.Hex(0x2c3846);
            RenderSettings.ambientEquatorColor = Util.Hex(0x1c2228);
            RenderSettings.ambientGroundColor = Util.Hex(0x0a0c0e);

            var q = GameSettings.Preset;
            int budget = q.dynamicLights;
            int shadowBudget = q.shadows ? 2 : 0;

            foreach (var l in MapData.Lights)
            {
                if (budget-- <= 0) break;
                var go = new GameObject("light");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(l.x, l.y, l.z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Util.Hex(l.color);
                light.range = l.d;
                light.intensity = 0f;
                if (shadowBudget > 0 && l.i >= 0.85f) { light.shadows = LightShadows.Soft; shadowBudget--; }
                else light.shadows = LightShadows.None;

                Props.Box(0.7f, 0.12f, 0.3f, Props.DarkMetal, new Vector3(l.x, l.y + 0.25f, l.z), root.transform);
                var bulb = Props.Box(0.55f, 0.06f, 0.22f,
                    Art.Emissive("em_bulb" + l.color, Util.Hex(l.color), 0.05f),
                    new Vector3(l.x, l.y + 0.16f, l.z), root.transform);

                _lights.Add(new LightRig
                {
                    light = light,
                    bulb = bulb.GetComponent<MeshRenderer>(),
                    baseIntensity = l.i,
                    zone = l.zone,
                    flickerSeed = Util.Rand(0f, 10f),
                });
            }

            // Red emergency strips glow while the grid is down.
            int emCount = 0;
            foreach (var l in MapData.Lights)
            {
                var em = Props.Box(0.3f, 0.08f, 0.08f, Art.Emissive("em_emergency", Util.Hex(0xff2b1e), 1.4f),
                    new Vector3(l.x + 0.6f, l.y + 0.1f, l.z), root.transform);
                _emergency.Add(em.GetComponent<MeshRenderer>());
                if (emCount++ < 6)
                {
                    var go = new GameObject("emLight");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(l.x + 0.6f, l.y + 0.1f, l.z);
                    var rl = go.AddComponent<Light>();
                    rl.type = LightType.Point;
                    rl.color = Util.Hex(0xff3b24);
                    rl.range = 14f;
                    rl.intensity = 1.6f;
                    rl.shadows = LightShadows.None;
                    _emergencyLights.Add(rl);
                }
            }
        }

        /// <summary>Pipes and zone signage that sell the facility.</summary>
        void BuildDressing()
        {
            foreach (var z in MapData.Zones)
            {
                if (z.hasSlope) continue;
                var rng = new Util.Rng(Mathf.RoundToInt(z.x0 * 31 + z.z0 * 17));
                int runs = Mathf.Max(1, Mathf.RoundToInt(z.Width / 12f));
                for (int i = 0; i < runs; i++)
                {
                    var p = Props.PipeRun(z.Width * 0.8f, i, root.transform);
                    p.transform.localPosition = new Vector3(z.CenterX, z.y + z.h - 0.45f,
                        z.z0 + 1.2f + rng.Next() * Mathf.Max(0.1f, z.Depth - 2.4f));
                }
                string word = z.name.Split(' ')[0];
                var sign = Props.Quad(2.6f, 0.7f, Art.Emissive("sign_zone_" + z.id, Color.white, 0.6f,
                    PixelFont.Sign(word, "", Util.Hex(0x8d948c), Util.Hex(0x101312), 256, 64)),
                    new Vector3(z.CenterX, z.y + z.h - 1.1f, z.z0 + 0.28f), root.transform);
                sign.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            }
        }

        // ================================================================ query
        public void AddObstacle(float x, float z, float w, float d, float y, float h, bool platform)
        {
            colliders.Add(new Aabb(x - w / 2, x + w / 2, z - d / 2, z + d / 2, y, y + h, platform));
            nav.Block(x - w / 2, x + w / 2, z - d / 2, z + d / 2, true, 0.1f);
        }

        /// <summary>Highest walkable surface under a point, honouring step-up height.</summary>
        public float GroundHeightAt(float x, float z, float feetY, float step)
        {
            float best = nav.HeightAt(x, z);
            for (int i = 0; i < colliders.Count; i++)
            {
                var c = colliders[i];
                if (!c.platform) continue;
                if (x < c.x0 || x > c.x1 || z < c.z0 || z > c.z1) continue;
                if (c.y1 > best && c.y1 <= feetY + step + 0.02f) best = c.y1;
            }
            return best;
        }

        /// <summary>Height of a mantle-able ledge in front of the player, or -1.</summary>
        public float LedgeAt(float x, float z, float feetY, float maxHeight)
        {
            float best = -1f;
            for (int i = 0; i < colliders.Count; i++)
            {
                var c = colliders[i];
                if (!c.platform) continue;
                if (x < c.x0 - 0.1f || x > c.x1 + 0.1f || z < c.z0 - 0.1f || z > c.z1 + 0.1f) continue;
                if (c.y1 <= feetY + 0.2f || c.y1 > feetY + maxHeight) continue;
                if (c.y1 > best) best = c.y1;
            }
            return best;
        }

        /// <summary>Slide-along-wall circle collision. Returns true when something was hit.</summary>
        public bool Collide(ref float px, ref float pz, float radius, float feetY, float headY)
        {
            bool hit = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool any = false;
                for (int i = 0; i < colliders.Count; i++)
                {
                    var c = colliders[i];
                    if (c.y1 <= feetY + 0.06f || c.y0 >= headY) continue;
                    float nx = Mathf.Clamp(px, c.x0, c.x1);
                    float nz = Mathf.Clamp(pz, c.z0, c.z1);
                    float dx = px - nx, dz = pz - nz;
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= radius * radius) continue;
                    any = true; hit = true;
                    if (d2 > 1e-6f)
                    {
                        float d = Mathf.Sqrt(d2);
                        px += (dx / d) * (radius - d);
                        pz += (dz / d) * (radius - d);
                    }
                    else
                    {
                        // Centre inside the box — push out along the shallowest axis.
                        float toL = px - c.x0, toR = c.x1 - px, toB = pz - c.z0, toT = c.z1 - pz;
                        float m = Mathf.Min(Mathf.Min(toL, toR), Mathf.Min(toB, toT));
                        if (m == toL) px = c.x0 - radius;
                        else if (m == toR) px = c.x1 + radius;
                        else if (m == toB) pz = c.z0 - radius;
                        else pz = c.z1 + radius;
                    }
                }
                if (!any) break;
            }
            return hit;
        }

        /// <summary>Ray against static geometry (slab test over every collider box).</summary>
        public WorldHit Raycast(Vector3 origin, Vector3 dir, float maxDist)
        {
            var result = new WorldHit { hit = false, dist = maxDist };
            float bestT = maxDist;
            for (int i = 0; i < colliders.Count; i++)
            {
                var c = colliders[i];
                float tmin = 0f, tmax = bestT;
                int nAxis = 0, nSign = 0;
                bool ok = true;
                for (int a = 0; a < 3; a++)
                {
                    float o = a == 0 ? origin.x : (a == 1 ? origin.y : origin.z);
                    float d = a == 0 ? dir.x : (a == 1 ? dir.y : dir.z);
                    float lo = a == 0 ? c.x0 : (a == 1 ? c.y0 : c.z0);
                    float hi = a == 0 ? c.x1 : (a == 1 ? c.y1 : c.z1);
                    if (Mathf.Abs(d) < 1e-8f)
                    {
                        if (o < lo || o > hi) { ok = false; break; }
                        continue;
                    }
                    float inv = 1f / d;
                    float t1 = (lo - o) * inv, t2 = (hi - o) * inv;
                    int sign = -1;
                    if (t1 > t2) { float tt = t1; t1 = t2; t2 = tt; sign = 1; }
                    if (t1 > tmin) { tmin = t1; nAxis = a; nSign = sign; }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) { ok = false; break; }
                }
                if (!ok || tmin <= 0.01f || tmin >= bestT) continue;
                bestT = tmin;
                result.hit = true;
                result.dist = tmin;
                result.point = origin + dir * tmin;
                result.normal = new Vector3(nAxis == 0 ? nSign : 0, nAxis == 1 ? nSign : 0, nAxis == 2 ? nSign : 0);
            }
            return result;
        }

        // ================================================================ state
        public bool IsZoneActive(string id)
        {
            bool v;
            return _zoneActive.TryGetValue(id, out v) && v;
        }

        public void UnlockZone(string id)
        {
            if (IsZoneActive(id)) return;
            _zoneActive[id] = true;
            nav.SetZoneActive(MapData.ZoneIndex(id), true);
        }

        public void OpenDoor(DoorObj door)
        {
            door.Purchase();
            colliders.Remove(door.collider);
            nav.Block(door.def.x0, door.def.x1, door.def.z0, door.def.z1, false, 0.2f);
            foreach (var zid in door.def.opens) UnlockZone(zid);
        }

        public List<Barricade> ActiveBarricades()
        {
            var list = new List<Barricade>();
            foreach (var b in barricades) if (IsZoneActive(b.zone)) list.Add(b);
            return list;
        }

        public void SetPowered(bool on)
        {
            Powered = on;
            foreach (var em in _emergency) Art.SetEmission(em.material, Util.Hex(0xff2b1e) * (on ? 0.05f : 1.4f));
            foreach (var l in _emergencyLights) l.intensity = on ? 0f : 2.6f;
        }

        public void Update(float dt, float time)
        {
            foreach (var d in doors) d.Update(dt);

            foreach (var l in _lights)
            {
                bool active = Powered && IsZoneActive(l.zone);
                float flick = active
                    ? 0.86f + Mathf.Sin(time * 9.3f + l.flickerSeed) * 0.05f + Mathf.Sin(time * 31f + l.flickerSeed * 3f) * 0.03f
                    : 0f;
                float target = active ? l.baseIntensity * flick * 2.6f : l.baseIntensity * 0.5f;
                l.light.intensity = Mathf.Lerp(l.light.intensity, target, Mathf.Clamp01(dt * 6f));
                if (l.bulb != null)
                    Art.SetEmission(l.bulb.material, l.light.color * (active ? 1.8f * flick : 0.05f));
            }
            if (_fill != null)
                _fill.intensity = Mathf.Lerp(_fill.intensity, Powered ? 0.75f : 0.5f, Mathf.Clamp01(dt * 2f));
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root);
            colliders.Clear();
        }
    }
}
