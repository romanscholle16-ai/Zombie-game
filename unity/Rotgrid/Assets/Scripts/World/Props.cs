using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Procedural prop, machine and weapon models. Everything is assembled from
    /// primitives and canvas-drawn textures at runtime — no imported meshes.
    /// </summary>
    public static class Props
    {
        // ------------------------------------------------------------ primitives
        public static GameObject Box(float w, float h, float d, Material mat, Vector3 pos, Transform parent, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "box";
            var col = go.GetComponent<Collider>();
            if (col != null && !collider) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(w, h, d);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        public static GameObject Cyl(float radius, float height, Material mat, Vector3 pos, Transform parent, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "cyl";
            var col = go.GetComponent<Collider>();
            if (col != null && !collider) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            // Unity cylinders are 2 units tall by default.
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        public static GameObject Quad(float w, float h, Material mat, Vector3 pos, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "quad";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(w, h, 1f);
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------ materials
        public static Material Wood { get { return Art.Lit("m_wood", Color.white, 0.08f, 0f, Art.Wood(), 1f); } }
        public static Material Plank { get { return Art.Lit("m_plank", Util.Hex(0xcaa06e), 0.05f, 0f, Art.Wood(), 1f); } }
        public static Material Metal { get { return Art.Lit("m_metal", Color.white, 0.48f, 0.7f, Art.MetalPanel(), 1f); } }
        public static Material Rust { get { return Art.Lit("m_rust", Color.white, 0.15f, 0.35f, Art.RustMetal(), 1f); } }
        public static Material DarkMetal { get { return Art.Lit("m_dark", Util.Hex(0x2b3033), 0.55f, 0.8f); } }
        public static Material Concrete { get { return Art.Lit("m_concrete", Color.white, 0.04f, 0f, Art.Concrete(), 1f); } }
        public static Material HazardMat { get { return Art.Lit("m_hazard", Color.white, 0.2f, 0.1f, Art.Hazard(), 1f); } }
        public static Material Glass { get { return Art.Lit("m_glass", new Color(0.62f, 0.83f, 0.81f, 0.4f), 0.92f, 0.1f); } }

        // ------------------------------------------------------------ clutter
        public static GameObject Crate(float size, int seed, Transform parent)
        {
            var g = new GameObject("crate");
            g.transform.SetParent(parent, false);
            float s = size * (0.8f + (seed % 5) * 0.08f);
            Box(s, s * 0.86f, s, Wood, new Vector3(0, s * 0.43f, 0), g.transform);
            var frame = Art.Lit("m_crateframe", Util.Hex(0x5a4529), 0.05f);
            float t = 0.06f * s;
            for (int i = 0; i < 4; i++)
            {
                float fx = (i % 2 == 0 ? -1 : 1) * s * 0.47f;
                float fz = (i < 2 ? -1 : 1) * s * 0.47f;
                Box(t, s * 0.86f, t, frame, new Vector3(fx, s * 0.43f, fz), g.transform);
            }
            Box(s * 1.01f, t, s * 1.01f, frame, new Vector3(0, s * 0.86f - t * 0.5f, 0), g.transform);
            g.AddComponent<PropInfo>().Set(s, s * 0.86f, s, true);
            return g;
        }

        public static GameObject Barrel(int seed, Transform parent)
        {
            var g = new GameObject("barrel");
            g.transform.SetParent(parent, false);
            var body = seed % 3 == 0 ? HazardMat : Rust;
            Cyl(0.36f, 1.1f, body, new Vector3(0, 0.55f, 0), g.transform);
            var rim = Art.Lit("m_barrelrim", Util.Hex(0x3a3f42), 0.4f, 0.6f);
            Cyl(0.38f, 0.06f, rim, new Vector3(0, 0.22f, 0), g.transform);
            Cyl(0.38f, 0.06f, rim, new Vector3(0, 0.88f, 0), g.transform);
            g.AddComponent<PropInfo>().Set(0.76f, 1.1f, 0.76f, true);
            return g;
        }

        public static GameObject Shelf(int seed, Transform parent)
        {
            var g = new GameObject("shelf");
            g.transform.SetParent(parent, false);
            float w = 2.2f, h = 2.0f, d = 0.6f;
            var post = Art.Lit("m_shelfpost", Util.Hex(0x4a5053), 0.4f, 0.55f);
            for (int i = 0; i < 4; i++)
            {
                float px = (i % 2 == 0 ? -1 : 1) * (w / 2 - 0.06f);
                float pz = (i < 2 ? -1 : 1) * (d / 2 - 0.06f);
                Box(0.08f, h, 0.08f, post, new Vector3(px, h / 2, pz), g.transform);
            }
            var rng = new Util.Rng(seed + 7);
            for (int i = 0; i < 3; i++)
            {
                Box(w, 0.06f, d, Metal, new Vector3(0, 0.45f + i * 0.66f, 0), g.transform);
                int n = 1 + Mathf.FloorToInt(rng.Next() * 3);
                for (int k = 0; k < n; k++)
                {
                    float bw = rng.Range(0.28f, 0.58f);
                    Box(bw, 0.26f, 0.36f, Wood,
                        new Vector3(-w / 2 + 0.3f + rng.Next() * (w - 0.6f), 0.61f + i * 0.66f, (rng.Next() - 0.5f) * 0.2f),
                        g.transform);
                }
            }
            g.AddComponent<PropInfo>().Set(w, h, d, false);
            return g;
        }

        public static GameObject Table(int seed, Transform parent)
        {
            var g = new GameObject("table");
            g.transform.SetParent(parent, false);
            float w = 2.0f, h = 0.9f, d = 0.9f;
            Box(w, 0.08f, d, Metal, new Vector3(0, h, 0), g.transform);
            var leg = Art.Lit("m_tableleg", Util.Hex(0x555b5e), 0.5f, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                float px = (i % 2 == 0 ? -1 : 1) * (w / 2 - 0.14f);
                float pz = (i < 2 ? -1 : 1) * (d / 2 - 0.12f);
                Box(0.07f, h, 0.07f, leg, new Vector3(px, h / 2, pz), g.transform);
            }
            var rng = new Util.Rng(seed + 3);
            for (int i = 0; i < 3; i++)
            {
                float r = rng.Range(0.07f, 0.12f);
                Cyl(r, rng.Range(0.2f, 0.36f), Glass,
                    new Vector3((rng.Next() - 0.5f) * (w - 0.5f), h + 0.14f, (rng.Next() - 0.5f) * 0.4f), g.transform);
            }
            g.AddComponent<PropInfo>().Set(w, h, d, true);
            return g;
        }

        public static GameObject Generator(int seed, Transform parent)
        {
            var g = new GameObject("generator");
            g.transform.SetParent(parent, false);
            Box(2.4f, 1.5f, 1.3f, Metal, new Vector3(0, 0.75f, 0), g.transform);
            Cyl(0.42f, 2.0f, Rust, new Vector3(-0.7f, 1.9f, 0), g.transform);
            Box(0.9f, 0.5f, 1.0f, DarkMetal, new Vector3(0.8f, 1.7f, 0), g.transform);
            Box(1.6f, 0.16f, 0.06f, Art.Emissive("em_gen", Util.Hex(0x7fe0a0), 1.4f), new Vector3(0, 1.1f, 0.68f), g.transform);
            g.AddComponent<PropInfo>().Set(2.4f, 2.9f, 1.3f, false);
            return g;
        }

        public static GameObject Debris(int seed, Transform parent)
        {
            var g = new GameObject("debris");
            g.transform.SetParent(parent, false);
            var rng = new Util.Rng(seed + 91);
            int n = 2 + Mathf.FloorToInt(rng.Next() * 4);
            for (int i = 0; i < n; i++)
            {
                var m = rng.Next() > 0.5f ? Concrete : Wood;
                float w = rng.Range(0.12f, 0.62f), h = rng.Range(0.06f, 0.24f), d = rng.Range(0.12f, 0.62f);
                var b = Box(w, h, d, m, new Vector3((rng.Next() - 0.5f) * 1.6f, h / 2, (rng.Next() - 0.5f) * 1.6f), g.transform);
                b.transform.localRotation = Quaternion.Euler(rng.Range(0, 18f), rng.Range(0, 360f), rng.Range(0, 18f));
            }
            return g;
        }

        public static GameObject PipeRun(float length, int seed, Transform parent)
        {
            var g = new GameObject("pipes");
            g.transform.SetParent(parent, false);
            var rng = new Util.Rng(seed + 17);
            for (int i = 0; i < 3; i++)
            {
                float r = rng.Range(0.07f, 0.15f);
                var p = Cyl(r, length, Rust, new Vector3((i - 1) * 0.28f, 0, 0), g.transform);
                p.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            return g;
        }

        public static GameObject RandomProp(string kind, int seed, Transform parent)
        {
            switch (kind)
            {
                case "crates": return Crate(1f + Util.Rand(-0.15f, 0.35f), seed, parent);
                case "barrels": return Barrel(seed, parent);
                case "shelves": return Shelf(seed, parent);
                case "tables": return Table(seed, parent);
                case "generators": return Generator(seed, parent);
                default: return Debris(seed, parent);
            }
        }

        // ------------------------------------------------------------ machines
        public static GameObject WallBuy(WeaponDef def, Transform parent)
        {
            var g = new GameObject("wallbuy_" + def.id);
            g.transform.SetParent(parent, false);
            Box(1.9f, 1.1f, 0.09f, Art.Lit("m_wbback", Util.Hex(0x14181a), 0.05f), Vector3.zero, g.transform);
            var frame = Art.Emissive("em_wbframe", Util.Hex(0x9fd93a), 1.1f);
            Box(1.96f, 0.05f, 0.1f, frame, new Vector3(0, 0.55f, 0.01f), g.transform);
            Box(1.96f, 0.05f, 0.1f, frame, new Vector3(0, -0.55f, 0.01f), g.transform);

            var gun = WeaponModel(def, 0.55f, g.transform);
            gun.transform.localPosition = new Vector3(0, 0.09f, 0.16f);
            gun.transform.localRotation = Quaternion.Euler(0, 90f, 4f);

            var sign = Quad(1.7f, 0.42f, Art.Emissive("sign_" + def.id,
                Color.white, 1.1f, PixelFont.Sign(def.name, def.wallCost + " PTS", Util.Hex(0x9fd93a), Util.Hex(0x0d100c), 256, 64)),
                new Vector3(0, -0.34f, 0.07f), g.transform);
            sign.transform.localRotation = Quaternion.Euler(0, 180f, 0);

            var info = g.AddComponent<MachineInfo>();
            info.spinTarget = gun.transform;
            return g;
        }

        public static GameObject PerkMachine(PerkDef perk, Transform parent)
        {
            var g = new GameObject("perk_" + perk.id);
            g.transform.SetParent(parent, false);
            var body = Art.Lit("m_perk" + perk.id, perk.Color, 0.35f, 0.3f);
            Box(1.15f, 2.1f, 0.9f, body, new Vector3(0, 1.05f, 0), g.transform);
            Box(1.25f, 0.14f, 1.0f, DarkMetal, new Vector3(0, 2.12f, 0), g.transform);
            Box(1.25f, 0.12f, 1.0f, DarkMetal, new Vector3(0, 0.06f, 0), g.transform);

            var glowMat = Art.Emissive("em_perk" + perk.id, perk.Color, 1.8f);
            var info = g.AddComponent<MachineInfo>();
            info.glow = new[]
            {
                Box(0.9f, 0.08f, 0.04f, glowMat, new Vector3(0, 0.95f, 0.47f), g.transform).GetComponent<MeshRenderer>(),
                Box(0.06f, 1.5f, 0.04f, glowMat, new Vector3(-0.6f, 1.2f, 0.3f), g.transform).GetComponent<MeshRenderer>(),
                Box(0.06f, 1.5f, 0.04f, glowMat, new Vector3(0.6f, 1.2f, 0.3f), g.transform).GetComponent<MeshRenderer>(),
            };

            var panel = Quad(0.85f, 0.55f, Art.Emissive("sign_perk" + perk.id, Color.white, 1.4f,
                PixelFont.Sign(perk.shortName, perk.cost.ToString(), perk.Color, Util.Hex(0x0a0c0a), 256, 128)),
                new Vector3(0, 1.5f, 0.47f), g.transform);
            panel.transform.localRotation = Quaternion.Euler(0, 180f, 0);

            Box(0.5f, 0.28f, 0.14f, Art.Lit("m_perkslot", Util.Hex(0x0a0c0a), 0f), new Vector3(0, 0.62f, 0.45f), g.transform);
            var bottle = Cyl(0.1f, 0.3f, Art.Emissive("em_bottle" + perk.id, perk.Color, 1.4f),
                new Vector3(0, 0.66f, 0.45f), g.transform);
            bottle.SetActive(false);
            info.bottle = bottle;
            info.colliderSize = new Vector3(1.25f, 2.2f, 1.0f);
            return g;
        }

        public static GameObject MysteryBox(Transform parent)
        {
            var g = new GameObject("mysterybox");
            g.transform.SetParent(parent, false);
            Box(1.5f, 0.95f, 1.1f, Wood, new Vector3(0, 0.47f, 0), g.transform);
            var trim = Art.Lit("m_boxtrim", Util.Hex(0x6b5a34), 0.3f, 0.25f);
            Box(0.08f, 0.95f, 1.14f, trim, new Vector3(-0.72f, 0.47f, 0), g.transform);
            Box(0.08f, 0.95f, 1.14f, trim, new Vector3(0.72f, 0.47f, 0), g.transform);

            var lidPivot = new GameObject("lid");
            lidPivot.transform.SetParent(g.transform, false);
            lidPivot.transform.localPosition = new Vector3(0, 0.95f, -0.55f);
            Box(1.55f, 0.16f, 1.15f, Wood, new Vector3(0, 0.08f, 0.55f), lidPivot.transform);
            Box(1.58f, 0.05f, 1.18f, trim, new Vector3(0, 0.17f, 0.55f), lidPivot.transform);

            var q = Quad(0.8f, 0.6f, Art.Emissive("sign_box", Color.white, 1.2f,
                PixelFont.Sign("?", "", Util.Hex(0xe8c14a), Util.Hex(0x0d100c), 128, 128)),
                new Vector3(0, 0.55f, 0.58f), g.transform);
            q.transform.localRotation = Quaternion.Euler(0, 180f, 0);

            var lightGo = new GameObject("glow");
            lightGo.transform.SetParent(g.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.2f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Util.Hex(0x9fd93a);
            l.range = 9f;
            l.intensity = 0f;

            var info = g.AddComponent<MachineInfo>();
            info.pivot = lidPivot.transform;
            info.light = l;
            info.colliderSize = new Vector3(1.6f, 1.0f, 1.2f);
            return g;
        }

        public static GameObject UpgradeMachine(Transform parent)
        {
            var g = new GameObject("refit");
            g.transform.SetParent(parent, false);
            Box(2.6f, 2.6f, 1.6f, Metal, new Vector3(0, 1.3f, 0), g.transform);
            Box(2.8f, 0.2f, 1.8f, DarkMetal, new Vector3(0, 2.62f, 0), g.transform);
            Box(2.8f, 0.18f, 1.8f, DarkMetal, new Vector3(0, 0.09f, 0), g.transform);
            Box(1.5f, 0.5f, 0.3f, Art.Lit("m_papslot", Util.Hex(0x070907), 0f), new Vector3(0, 1.35f, 0.78f), g.transform);

            var glowMat = Art.Emissive("em_pap", Util.Hex(0xff8a3d), 2.4f);
            var throat = Box(1.4f, 0.42f, 0.06f, glowMat, new Vector3(0, 1.35f, 0.87f), g.transform);
            var ring = Cyl(0.62f, 0.06f, glowMat, new Vector3(0, 1.35f, 0.92f), g.transform);
            ring.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            var pipes = PipeRun(2.4f, 5, g.transform);
            pipes.transform.localPosition = new Vector3(0, 2.35f, -0.5f);

            var sign = Quad(2.0f, 0.6f, Art.Emissive("sign_pap", Color.white, 1.6f,
                PixelFont.Sign("REFIT", "INSERT WEAPON", Util.Hex(0xff8a3d), Util.Hex(0x0d0a08), 256, 96)),
                new Vector3(0, 2.15f, 0.83f), g.transform);
            sign.transform.localRotation = Quaternion.Euler(0, 180f, 0);

            var lightGo = new GameObject("papLight");
            lightGo.transform.SetParent(g.transform, false);
            lightGo.transform.localPosition = new Vector3(0, 1.8f, 1.2f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Util.Hex(0xff8a3d);
            l.range = 12f;
            l.intensity = 0f;

            var info = g.AddComponent<MachineInfo>();
            info.glow = new[] { throat.GetComponent<MeshRenderer>(), ring.GetComponent<MeshRenderer>() };
            info.pivot = ring.transform;
            info.light = l;
            info.colliderSize = new Vector3(2.8f, 2.7f, 1.8f);
            return g;
        }

        public static GameObject PowerSwitch(Transform parent)
        {
            var g = new GameObject("breaker");
            g.transform.SetParent(parent, false);
            Box(1.6f, 2.2f, 0.5f, Metal, new Vector3(0, 1.1f, 0), g.transform);
            Box(1.7f, 0.1f, 0.6f, DarkMetal, new Vector3(0, 2.2f, 0), g.transform);
            var dead = Art.Lit("m_breakerdead", Util.Hex(0x2a1010), 0.3f);
            for (int i = 0; i < 3; i++) Box(0.34f, 0.34f, 0.08f, dead, new Vector3((i - 1) * 0.42f, 1.6f, 0.28f), g.transform);

            var lever = Box(0.16f, 0.7f, 0.16f, HazardMat, new Vector3(0, 0.85f, 0.3f), g.transform);
            lever.transform.localRotation = Quaternion.Euler(52f, 0, 0);

            var lamp = Box(0.2f, 0.2f, 0.06f, Art.Emissive("em_breakerlamp", Util.Hex(0xff2b1e), 2f),
                new Vector3(0.55f, 1.95f, 0.28f), g.transform);

            var sign = Quad(1.3f, 0.4f, Art.Emissive("sign_breaker", Color.white, 1.2f,
                PixelFont.Sign("MAIN BREAKER", "", Util.Hex(0xe8a33d), Util.Hex(0x0d0b08), 256, 64)),
                new Vector3(0, 0.35f, 0.27f), g.transform);
            sign.transform.localRotation = Quaternion.Euler(0, 180f, 0);

            var info = g.AddComponent<MachineInfo>();
            info.pivot = lever.transform;
            info.glow = new[] { lamp.GetComponent<MeshRenderer>() };
            info.colliderSize = new Vector3(1.7f, 2.3f, 0.6f);
            return g;
        }

        public static GameObject TrapPanel(string kind, Transform parent)
        {
            var g = new GameObject("trappanel_" + kind);
            g.transform.SetParent(parent, false);
            Box(0.7f, 0.9f, 0.16f, DarkMetal, Vector3.zero, g.transform);
            var face = Box(0.56f, 0.72f, 0.05f, Art.Emissive("em_trap" + kind, TrapColor(kind), 1.6f),
                new Vector3(0, 0, 0.1f), g.transform);
            var info = g.AddComponent<MachineInfo>();
            info.glow = new[] { face.GetComponent<MeshRenderer>() };
            return g;
        }

        public static Color TrapColor(string kind)
        {
            if (kind == "electric") return Util.Hex(0x66d9ff);
            if (kind == "fire") return Util.Hex(0xff7b2e);
            return Util.Hex(0xffd24a);
        }

        public static GameObject TrapEmitter(string kind, Transform parent)
        {
            var g = new GameObject("trap_" + kind);
            g.transform.SetParent(parent, false);
            var info = g.AddComponent<MachineInfo>();
            var glowMat = Art.Emissive("em_trapemit" + kind, TrapColor(kind), 2.2f);

            if (kind == "electric")
            {
                var coils = new MeshRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    float s = i == 0 ? -1f : 1f;
                    Cyl(0.14f, 3.2f, DarkMetal, new Vector3(s * 2.6f, 1.6f, 0), g.transform);
                    var coil = Cyl(0.3f, 0.12f, glowMat, new Vector3(s * 2.6f, 2.9f, 0), g.transform);
                    coils[i] = coil.GetComponent<MeshRenderer>();
                }
                info.glow = coils;
            }
            else if (kind == "fire")
            {
                var jets = new GameObject[4];
                for (int i = 0; i < 4; i++)
                {
                    Box(0.3f, 0.3f, 0.5f, Rust, new Vector3((i - 1.5f) * 2.2f, 0.3f, 0), g.transform);
                    jets[i] = Cyl(0.1f, 0.6f, glowMat, new Vector3((i - 1.5f) * 2.2f, 0.55f, 0.2f), g.transform);
                    jets[i].SetActive(false);
                }
                info.extras = jets;
            }
            else
            {
                Cyl(0.5f, 0.5f, DarkMetal, new Vector3(0, 0.25f, 0), g.transform);
                var head = new GameObject("head");
                head.transform.SetParent(g.transform, false);
                head.transform.localPosition = new Vector3(0, 0.8f, 0);
                Box(0.7f, 0.55f, 0.9f, Metal, Vector3.zero, head.transform);
                var barrel = Cyl(0.09f, 1.0f, DarkMetal, new Vector3(0, 0.05f, 0.6f), head.transform);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                var eye = Box(0.14f, 0.14f, 0.05f, glowMat, new Vector3(0, 0.2f, 0.46f), head.transform);
                info.pivot = head.transform;
                info.glow = new[] { eye.GetComponent<MeshRenderer>() };
            }
            return g;
        }

        public static GameObject PowerUpIcon(Color color, string glyph, Transform parent)
        {
            var g = new GameObject("powerup");
            g.transform.SetParent(parent, false);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sc = shell.GetComponent<Collider>();
            if (sc != null) Object.Destroy(sc);
            shell.transform.SetParent(g.transform, false);
            shell.transform.localScale = Vector3.one * 0.62f;
            var mat = Art.Emissive("em_pu" + glyph, color, 1.8f);
            shell.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var faceTex = PixelFont.Sign(glyph, "", Color.white, new Color(0, 0, 0, 0), 128, 128);
            for (int i = 0; i < 2; i++)
            {
                var q = Quad(0.5f, 0.5f, Art.Emissive("sign_pu" + glyph, Color.white, 1.4f, faceTex),
                    new Vector3(0, 0, i == 0 ? 0.34f : -0.34f), g.transform);
                q.transform.localRotation = Quaternion.Euler(0, i == 0 ? 180f : 0f, 0);
            }

            var lightGo = new GameObject("puLight");
            lightGo.transform.SetParent(g.transform, false);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = 7f;
            l.intensity = 16f;

            var info = g.AddComponent<MachineInfo>();
            info.light = l;
            info.glow = new[] { shell.GetComponent<MeshRenderer>() };
            return g;
        }

        // ------------------------------------------------------------ weapons
        /// <summary>
        /// Builds a chunky, readable weapon silhouette from its definition.
        /// Used for the view model, wall buys, the crate reveal and the loadout.
        /// </summary>
        public static GameObject WeaponModel(WeaponDef def, float scale, Transform parent)
        {
            var g = new GameObject("weapon_" + def.id);
            g.transform.SetParent(parent, false);
            g.transform.localScale = Vector3.one * scale;

            // Lifted from the world tints so the gun still reads in a dark room.
            Color bodyCol = Util.Lighten(def.Tint, 4.2f);
            Color accentCol = Util.Lighten(def.Accent, 2.6f);
            var body = Art.Lit("gunbody" + def.tint, bodyCol, 0.5f, 0.5f);
            var grip = Art.Lit("gungrip", Util.Hex(0x4a5054), 0.1f, 0.05f);
            var accent = Art.Lit("gunaccent" + def.accent, accentCol, 0.62f, 0.68f);

            float recW = def.recvW, recH = def.recvH, recL = def.recvL;
            float barrel = def.barrelLen;

            Box(recW, recH, recL, body, Vector3.zero, g.transform);
            var b = Cyl(def.bore, barrel, accent, new Vector3(0, 0.012f, recL / 2 + barrel / 2), g.transform);
            b.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            if (def.hasGuard)
                Box(recW * 0.86f, recH * 0.66f, barrel * 0.72f, body, new Vector3(0, -0.008f, recL / 2 + barrel * 0.34f), g.transform);

            var gp = Box(0.072f, 0.16f, 0.09f, grip, new Vector3(0, -0.12f, -recL * 0.18f), g.transform);
            gp.transform.localRotation = Quaternion.Euler(-16f, 0, 0);

            var info = g.AddComponent<WeaponModelInfo>();
            if (def.hasMag)
            {
                var mg = Box(0.07f, def.magLen, 0.075f, grip, new Vector3(0, -def.magLen / 2 - 0.05f, recL * 0.06f), g.transform);
                mg.transform.localRotation = Quaternion.Euler(6f, 0, 0);
                info.magazine = mg.transform;
                info.magHome = mg.transform.localPosition;
            }
            if (def.hasStock)
            {
                Box(0.07f, 0.11f, def.stockLen, body, new Vector3(0, -0.03f, -recL / 2 - def.stockLen / 2), g.transform);
                Box(0.075f, 0.15f, 0.05f, grip, new Vector3(0, -0.03f, -recL / 2 - def.stockLen - 0.02f), g.transform);
            }

            Box(0.05f, 0.045f, 0.05f, accent, new Vector3(0, recH / 2 + 0.02f, -recL * 0.26f), g.transform);
            Box(0.032f, 0.05f, 0.03f, accent, new Vector3(0, recH / 2 + 0.022f, recL / 2 + barrel * 0.82f), g.transform);
            info.sightHeight = recH / 2 + 0.045f;

            if (def.hasOptic)
            {
                var o = Cyl(0.045f, 0.16f, Art.Lit("m_optic", Util.Hex(0x2a2f33), 0.5f, 0.5f),
                    new Vector3(0, recH / 2 + 0.06f, -recL * 0.1f), g.transform);
                o.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                info.sightHeight = recH / 2 + 0.06f;
            }
            if (def.hasDrum)
            {
                var d = Cyl(0.13f, 0.09f, grip, new Vector3(0, -0.14f, recL * 0.05f), g.transform);
                d.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            }
            if (def.hasTube)
                Cyl(0.032f, barrel * 0.9f, accent, new Vector3(0, -0.05f, recL / 2 + barrel * 0.45f), g.transform)
                    .transform.localRotation = Quaternion.Euler(90f, 0, 0);

            if (def.hasCell || def.upgradeLevel > 0)
            {
                var cellMat = Art.Emissive("em_cell" + def.accent, accentCol, 2.4f);
                var cell = Cyl(0.06f, 0.22f, cellMat, new Vector3(0, 0.07f, -recL * 0.05f), g.transform);
                cell.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                var coil = Cyl(0.075f, 0.03f, cellMat, new Vector3(0, 0.012f, recL / 2 + barrel * 0.7f), g.transform);
                coil.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                info.energy = new[] { cell.GetComponent<MeshRenderer>(), coil.GetComponent<MeshRenderer>() };
            }

            info.muzzle = new Vector3(0, 0.012f, recL / 2 + barrel);
            return g;
        }

        public static GameObject KnifeModel(float scale, Transform parent)
        {
            var g = new GameObject("knife");
            g.transform.SetParent(parent, false);
            g.transform.localScale = Vector3.one * scale;
            var blade = Art.Lit("m_blade", Util.Hex(0xd6dde1), 0.74f, 0.7f);
            Box(0.03f, 0.09f, 0.32f, blade, new Vector3(0, 0, 0.2f), g.transform);
            Box(0.05f, 0.11f, 0.05f, Art.Lit("m_guard", Util.Hex(0x30353a), 0.5f, 0.6f), new Vector3(0, 0, 0.02f), g.transform);
            Box(0.042f, 0.05f, 0.18f, Art.Lit("m_handle", Util.Hex(0x33383c), 0.05f), new Vector3(0, 0, -0.1f), g.transform);
            return g;
        }
    }

    /// <summary>Footprint of a prop, so the world can build its collider and nav block.</summary>
    public class PropInfo : MonoBehaviour
    {
        public float width, height, depth;
        public bool platform;
        public void Set(float w, float h, float d, bool p) { width = w; height = h; depth = d; platform = p; }
    }

    /// <summary>Animated parts of a machine, looked up by the owning system.</summary>
    public class MachineInfo : MonoBehaviour
    {
        public MeshRenderer[] glow;
        public GameObject[] extras;
        public Transform pivot;
        public Transform spinTarget;
        public GameObject bottle;
        public Light light;
        public Vector3 colliderSize;
    }

    /// <summary>Parts of a weapon model the view model animates.</summary>
    public class WeaponModelInfo : MonoBehaviour
    {
        public Transform magazine;
        public Vector3 magHome;
        public MeshRenderer[] energy;
        public Vector3 muzzle;
        public float sightHeight = 0.09f;
    }
}
