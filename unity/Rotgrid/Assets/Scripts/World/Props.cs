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

        /// <summary>
        /// Perk dispenser: a battered vending cabinet with a lit marquee, a rack
        /// of bottles behind scratched glass, a dispense tray at waist height and
        /// a kick plate that has seen better decades.
        /// </summary>
        public static GameObject PerkMachine(PerkDef perk, Transform parent)
        {
            var g = new GameObject("perk_" + perk.id);
            g.transform.SetParent(parent, false);
            var T = g.transform;
            var shell = Art.Lit("m_perkShell" + perk.id, perk.Color, 0.48f, 0.42f, Art.MetalPanel());
            var dark = Art.Lit("m_perkDark", Util.Hex(0x181d20), 0.3f, 0.45f);
            var glowMat = Art.Emissive("em_perk" + perk.id, perk.Color, 1.8f);

            const float W = 1.18f, H = 2.24f, D = 0.86f;

            // ---- carcass: back, sides and a chamfered top
            Box(W, H, 0.1f, shell, new Vector3(0, H * 0.5f, -D * 0.5f + 0.05f), T);
            Box(0.11f, H, D, shell, new Vector3(-(W * 0.5f - 0.055f), H * 0.5f, 0), T);
            Box(0.11f, H, D, shell, new Vector3(W * 0.5f - 0.055f, H * 0.5f, 0), T);
            Box(W, 0.12f, D, dark, new Vector3(0, H - 0.06f, 0), T);
            Box(W * 0.99f, 0.5f, D * 0.62f, shell, new Vector3(0, H - 0.34f, -D * 0.16f), T);

            // ---- plinth and kick plate
            Box(W + 0.08f, 0.16f, D + 0.06f, dark, new Vector3(0, 0.08f, 0), T);
            Box(W * 0.9f, 0.2f, 0.04f, Rust, new Vector3(0, 0.26f, D * 0.5f - 0.01f), T);

            var info = g.AddComponent<MachineInfo>();

            // ---- lit marquee across the crown
            var marquee = Quad(W * 0.86f, 0.34f, Art.Emissive("sign_perk" + perk.id, Color.white, 1.4f,
                PixelFont.Sign(perk.shortName, perk.cost + " PTS", perk.Color, Util.Hex(0x0a0c0a), 256, 128)),
                new Vector3(0, H - 0.3f, D * 0.5f - 0.02f), T);
            marquee.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            var mf1 = Box(W * 0.92f, 0.05f, 0.05f, glowMat, new Vector3(0, H - 0.13f, D * 0.5f - 0.02f), T);
            var mf2 = Box(W * 0.92f, 0.05f, 0.05f, glowMat, new Vector3(0, H - 0.47f, D * 0.5f - 0.02f), T);

            // ---- display window: bottle rack behind glass. The backing panel
            // sits behind the bottles, not in front of them.
            Box(W * 0.82f, 0.94f, 0.06f, Art.Lit("m_perkBay", Util.Hex(0x0b0e10), 0f),
                new Vector3(0, 1.34f, D * 0.5f - 0.36f), T);
            var bottleMat = Art.Emissive("em_perkRack" + perk.id, perk.Color, 0.9f);
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Cyl(0.058f, 0.24f, bottleMat, new Vector3(-0.3f + i * 0.2f, 1.06f + row * 0.44f, D * 0.5f - 0.19f), T);
                    Cyl(0.026f, 0.06f, dark, new Vector3(-0.3f + i * 0.2f, 1.21f + row * 0.44f, D * 0.5f - 0.19f), T);
                }
                Box(W * 0.8f, 0.02f, 0.14f, dark, new Vector3(0, 0.93f + row * 0.44f, D * 0.5f - 0.19f), T);
            }
            Quad(W * 0.82f, 0.96f, Art.Lit("m_perkGlass", new Color(0.74f, 0.85f, 0.9f, 0.19f), 0.9f, 0.2f),
                new Vector3(0, 1.34f, D * 0.5f - 0.008f), T)
                .transform.localRotation = Quaternion.Euler(0, 180f, 0);
            Box(W * 0.86f, 0.05f, 0.06f, dark, new Vector3(0, 1.84f, D * 0.5f - 0.01f), T);
            Box(W * 0.86f, 0.05f, 0.06f, dark, new Vector3(0, 0.84f, D * 0.5f - 0.01f), T);
            Box(0.05f, 1.04f, 0.06f, dark, new Vector3(-W * 0.42f, 1.34f, D * 0.5f - 0.01f), T);
            Box(0.05f, 1.04f, 0.06f, dark, new Vector3(W * 0.42f, 1.34f, D * 0.5f - 0.01f), T);

            // ---- selection buttons down the side of the glass
            for (int i = 0; i < 3; i++)
                Box(0.07f, 0.05f, 0.03f, i == 1 ? glowMat : dark,
                    new Vector3(W * 0.33f, 0.72f - i * 0.09f, D * 0.5f - 0.01f), T);

            // ---- dispense tray, and the bottle that drops into it
            Box(W * 0.56f, 0.3f, 0.16f, Art.Lit("m_perkslot", Util.Hex(0x07090a), 0f),
                new Vector3(0, 0.58f, D * 0.5f - 0.06f), T);
            Box(W * 0.6f, 0.04f, 0.2f, dark, new Vector3(0, 0.42f, D * 0.5f - 0.04f), T);
            Box(W * 0.52f, 0.24f, 0.02f, dark, new Vector3(0, 0.6f, D * 0.5f + 0.02f), T);
            var bottle = Cyl(0.062f, 0.26f, Art.Emissive("em_bottle" + perk.id, perk.Color, 1.4f),
                new Vector3(0, 0.62f, D * 0.5f - 0.04f), T);
            bottle.SetActive(false);

            // ---- accent light bars up the corners
            var barL = Box(0.05f, 1.5f, 0.05f, glowMat, new Vector3(-W * 0.5f + 0.03f, 1.2f, D * 0.5f - 0.06f), T);
            var barR = Box(0.05f, 1.5f, 0.05f, glowMat, new Vector3(W * 0.5f - 0.03f, 1.2f, D * 0.5f - 0.06f), T);

            info.glow = new[]
            {
                mf1.GetComponent<MeshRenderer>(), mf2.GetComponent<MeshRenderer>(),
                barL.GetComponent<MeshRenderer>(), barR.GetComponent<MeshRenderer>(),
                marquee.GetComponent<MeshRenderer>(),
            };
            info.bottle = bottle;
            info.colliderSize = new Vector3(W + 0.1f, H, D + 0.06f);
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

        /// <summary>
        /// Weapon upgrade machine. Reads as a repurposed industrial press: a
        /// heavy armoured cabinet, a hooded intake you feed a weapon into,
        /// hydraulic rams either side, a hazard-striped apron and extraction
        /// ducting stacked over the top.
        /// </summary>
        public static GameObject UpgradeMachine(Transform parent)
        {
            var g = new GameObject("refit");
            g.transform.SetParent(parent, false);
            var T = g.transform;
            var shell = Art.Lit("m_papShell", Util.Hex(0x6b6f74), 0.45f, 0.7f, Art.MetalPanel());
            var hot = Art.Emissive("em_pap", Util.Hex(0xff8a3d), 2.4f);
            const float W = 2.7f, H = 2.75f, D = 1.7f;

            // ---- carcass with a chamfered hood
            Box(W, H * 0.78f, D, shell, new Vector3(0, H * 0.39f + 0.16f, 0), T);
            Box(W + 0.16f, 0.22f, D + 0.16f, DarkMetal, new Vector3(0, 0.11f, 0), T);
            Box(W + 0.1f, 0.2f, D + 0.1f, DarkMetal, new Vector3(0, H * 0.78f + 0.26f, 0), T);
            var hood = Box(W * 0.94f, 0.46f, D * 0.7f, shell, new Vector3(0, H - 0.2f, D * 0.12f), T);
            hood.transform.localRotation = Quaternion.Euler(-12.6f, 0, 0);

            // ---- hazard apron along the base, where you stand
            Box(W * 0.98f, 0.26f, 0.06f, HazardMat, new Vector3(0, 0.35f, D * 0.5f + 0.02f), T);

            // ---- intake throat: recessed slot, glowing interior, iris ring
            Box(1.62f, 0.62f, 0.34f, Art.Lit("m_papslot", Util.Hex(0x060807), 0f),
                new Vector3(0, 1.38f, D * 0.5f - 0.06f), T);
            var throat = Box(1.46f, 0.46f, 0.06f, hot, new Vector3(0, 1.38f, D * 0.5f + 0.03f), T);
            var iris = Cyl(0.66f, 0.06f, hot, new Vector3(0, 1.38f, D * 0.5f + 0.07f), T);
            iris.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                var roller = Cyl(0.05f, 1.3f, DarkMetal, new Vector3(0, 1.38f + s2 * 0.26f, D * 0.5f - 0.02f), T);
                roller.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }

            // ---- hydraulic rams either side of the intake
            var rod = Art.Lit("m_papRod", Util.Hex(0xd2d8dc), 0.8f, 0.95f);
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                Cyl(0.13f, 1.2f, DarkMetal, new Vector3(s2 * 1.12f, 1.5f, D * 0.2f), T);
                Cyl(0.08f, 0.5f, rod, new Vector3(s2 * 1.12f, 2.2f, D * 0.2f), T);
                Box(0.34f, 0.12f, 0.34f, DarkMetal, new Vector3(s2 * 1.12f, 2.44f, D * 0.2f), T);
            }

            // ---- gauges and a control box on the cheeks
            var gaugeMat = Art.Lit("m_papGauge", Util.Hex(0x11161a), 0.5f, 0.6f);
            for (int i = 0; i < 3; i++)
            {
                var gauge = Cyl(0.09f, 0.05f, gaugeMat, new Vector3(-0.95f + i * 0.3f, 0.82f, D * 0.5f + 0.01f), T);
                gauge.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                Box(0.02f, 0.06f, 0.02f, hot, new Vector3(-0.95f + i * 0.3f, 0.84f, D * 0.5f + 0.04f), T);
            }
            Box(0.44f, 0.5f, 0.14f, DarkMetal, new Vector3(1.0f, 0.85f, D * 0.5f - 0.02f), T);
            Box(0.3f, 0.16f, 0.04f, hot, new Vector3(1.0f, 0.98f, D * 0.5f + 0.05f), T);

            // ---- extraction ducting off the back and over the top
            var pipes = PipeRun(2.6f, 5, T);
            pipes.transform.localPosition = new Vector3(0, H + 0.12f, -D * 0.28f);
            for (int s2 = -1; s2 <= 1; s2 += 2)
                Cyl(0.16f, 0.8f, Rust, new Vector3(s2 * 0.9f, H + 0.5f, -D * 0.28f), T);

            // ---- signage
            var sign = Quad(1.9f, 0.56f, Art.Emissive("sign_pap", Color.white, 1.6f,
                PixelFont.Sign("REFIT", "INSERT WEAPON", Util.Hex(0xff8a3d), Util.Hex(0x0d0a08), 256, 96)),
                new Vector3(0, 2.16f, D * 0.5f + 0.06f), T);
            sign.transform.localRotation = Quaternion.Euler(-12.6f, 180f, 0);

            var lightGo = new GameObject("papLight");
            lightGo.transform.SetParent(T, false);
            lightGo.transform.localPosition = new Vector3(0, 1.8f, D * 0.5f + 0.7f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Util.Hex(0xff8a3d);
            l.range = 12f;
            l.intensity = 0f;

            var info = g.AddComponent<MachineInfo>();
            info.glow = new[] { throat.GetComponent<MeshRenderer>(), iris.GetComponent<MeshRenderer>() };
            info.pivot = iris.transform;
            info.light = l;
            info.colliderSize = new Vector3(W + 0.2f, H, D + 0.2f);
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
        /// <summary>Cylinder lying along +z, which is how most gun parts run.</summary>
        static GameObject CylZ(float r, float len, Material mat, Vector3 pos, Transform parent)
        {
            var go = Cyl(r, len, mat, pos, parent);
            go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            return go;
        }

        /// <summary>Ring of short bars — stands in for a torus without a mesh.</summary>
        static GameObject Ring(float radius, float tube, Material mat, Vector3 pos, Transform parent, int seg = 14)
        {
            var g = new GameObject("ring");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            float arc = Mathf.PI * 2f / seg;
            float bar = radius * arc * 1.15f;
            for (int i = 0; i < seg; i++)
            {
                float a = i * arc;
                var b = Box(tube * 2f, bar, tube * 2f, mat,
                    new Vector3(Mathf.Sin(a) * radius, Mathf.Cos(a) * radius, 0), g.transform);
                b.transform.localRotation = Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg);
            }
            return g;
        }

        /// <summary>A length of accessory rail: base strip plus recoil lugs.</summary>
        static void RailSection(float len, Material mat, Vector3 pos, Transform parent)
        {
            var g = new GameObject("rail");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            Box(0.019f, 0.007f, len, mat, Vector3.zero, g.transform);
            int n = Mathf.Clamp(Mathf.RoundToInt(len / 0.024f), 2, 14);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : (float)i / (n - 1);
                Box(0.023f, 0.006f, 0.008f, mat,
                    new Vector3(0, 0.006f, -len / 2 + 0.009f + t * (len - 0.018f)), g.transform);
            }
        }

        /// <summary>Box magazine that curves forward as it drops, following its cartridges.</summary>
        static GameObject CurvedMag(float len, float w, float d, Material mat, Vector3 pos, Transform parent, float curve)
        {
            var g = new GameObject("mag");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            const int n = 6;
            float seg = len / n;
            float y = 0f, z = 0f;
            for (int i = 0; i < n; i++)
            {
                float a = curve * ((float)i / n);
                var s = Box(w * (1f - i * 0.015f), seg * 1.1f, d, mat,
                    new Vector3(0, y - Mathf.Cos(a) * seg / 2f, z + Mathf.Sin(a) * seg / 2f), g.transform);
                s.transform.localRotation = Quaternion.Euler(-a * Mathf.Rad2Deg, 0, 0);
                y -= Mathf.Cos(a) * seg;
                z += Mathf.Sin(a) * seg;
            }
            var fp = Box(w * 1.12f, 0.012f, d * 1.1f, mat, new Vector3(0, y + 0.004f, z), g.transform);
            fp.transform.localRotation = Quaternion.Euler(-curve * Mathf.Rad2Deg, 0, 0);
            return g;
        }

        /// <summary>Birdcage flash hider, ported brake, can, or a plain crown.</summary>
        static void MuzzleDevice(string kind, float bore, Material mat, float z, Transform parent)
        {
            float r = Mathf.Max(0.011f, bore * 1.5f);
            if (kind == "brake")
            {
                CylZ(r * 1.25f, 0.062f, mat, new Vector3(0, 0, z + 0.031f), parent);
                for (int i = 0; i < 3; i++)
                    for (int s = -1; s <= 1; s += 2)
                        Box(0.006f, r * 2.2f, 0.007f, mat, new Vector3(s * r * 1.1f, 0, z + 0.014f + i * 0.016f), parent);
            }
            else if (kind == "suppressor")
            {
                CylZ(r * 2f, 0.16f, mat, new Vector3(0, 0, z + 0.08f), parent);
                for (int i = 0; i < 4; i++)
                    Ring(r * 2f, 0.004f, mat, new Vector3(0, 0, z + 0.03f + i * 0.036f), parent, 10);
            }
            else if (kind == "none")
            {
                CylZ(r * 1.05f, 0.02f, mat, new Vector3(0, 0, z + 0.01f), parent);
            }
            else
            {
                var slot = Art.Lit("m_gnslot", Util.Hex(0x0b0e10), 0.02f);
                CylZ(r * 1.1f, 0.014f, mat, new Vector3(0, 0, z + 0.007f), parent);
                CylZ(r * 1.35f, 0.048f, mat, new Vector3(0, 0, z + 0.038f), parent);
                for (int i = 0; i < 5; i++)
                {
                    float a = -Mathf.PI * 0.35f + (i / 4f) * Mathf.PI * 0.7f;
                    Box(0.005f, 0.005f, 0.03f, slot,
                        new Vector3(Mathf.Sin(a) * r * 1.3f, Mathf.Cos(a) * r * 1.3f, z + 0.04f), parent);
                }
                Ring(r * 1.35f, 0.005f, mat, new Vector3(0, 0, z + 0.062f), parent, 12);
            }
        }

        /// <summary>Swept pistol grip with a backstrap, plus trigger guard and trigger.</summary>
        static void GripAssembly(Material mat, Material metal, Vector3 pos, Transform parent, float rake, float len, bool guard)
        {
            var g = new GameObject("grip");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            const int n = 4;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float off = len * (t + 0.5f / n);
                var seg = Box(0.032f, len / n * 1.12f, 0.05f - t * 0.012f, mat,
                    new Vector3(0, -off, -Mathf.Sin(rake) * off), g.transform);
                seg.transform.localRotation = Quaternion.Euler(rake * 0.55f * Mathf.Rad2Deg, 0, 0);
            }
            Box(0.03f, 0.03f, 0.045f, mat, new Vector3(0, -0.012f, -0.018f), g.transform);
            Box(0.034f, 0.012f, 0.05f, metal, new Vector3(0, -len - 0.005f, -Mathf.Sin(rake) * len), g.transform);
            if (!guard) return;
            // Trigger guard: a bowed row of links, plus the trigger inside it.
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.PI * (0.1f + 0.8f * i / 6f);
                Box(0.007f, 0.009f, 0.009f, metal,
                    new Vector3(0, -0.028f - Mathf.Sin(a) * 0.026f, 0.031f - Mathf.Cos(a) * 0.026f), g.transform);
            }
            var tr = Box(0.008f, 0.026f, 0.007f, metal, new Vector3(0, -0.024f, 0.026f), g.transform);
            tr.transform.localRotation = Quaternion.Euler(11f, 0, 0);
        }

        /// <summary>Telescoping stock on a buffer tube, with cheek weld and rubber pad.</summary>
        static void CollapsibleStock(float len, Material mat, Material metal, Material rubber, float z, Transform parent)
        {
            CylZ(0.019f, len * 0.96f, metal, new Vector3(0, 0.004f, z - len * 0.48f), parent);
            Box(0.044f, 0.062f, len * 0.46f, mat, new Vector3(0, -0.004f, z - len * 0.6f), parent);
            Box(0.05f, 0.016f, len * 0.5f, mat, new Vector3(0, 0.032f, z - len * 0.58f), parent);
            Box(0.03f, 0.03f, 0.012f, metal, new Vector3(0, -0.03f, z - len * 0.42f), parent);
            Box(0.052f, 0.086f, 0.016f, rubber, new Vector3(0, -0.004f, z - len * 0.94f), parent);
        }

        /// <summary>One-piece stock with a wrist and comb — shotguns and bolt guns.</summary>
        static void FixedStock(float len, Material mat, Material rubber, float z, float drop, Transform parent)
        {
            var wrist = Box(0.038f, 0.062f, len * 0.42f, mat, new Vector3(0, -0.018f, z - len * 0.2f), parent);
            wrist.transform.localRotation = Quaternion.Euler(-5f, 0, 0);
            var comb = Box(0.046f, 0.075f, len * 0.52f, mat, new Vector3(0, -drop, z - len * 0.66f), parent);
            comb.transform.localRotation = Quaternion.Euler(-2f, 0, 0);
            Box(0.05f, 0.026f, len * 0.4f, mat, new Vector3(0, -drop + 0.05f, z - len * 0.7f), parent);
            Box(0.05f, 0.096f, 0.018f, rubber, new Vector3(0, -drop - 0.006f, z - len * 0.95f), parent);
        }

        /// <summary>
        /// Builds a weapon from the parts a real firearm has — receiver, magwell,
        /// ejection port, charging handle, grip, handguard, gas block, muzzle
        /// device, stock and sights. Forms are generic to their class; nothing
        /// here reproduces any manufacturer's product, marking or trade dress.
        ///
        /// The bore runs along +z, up is +y, origin at the receiver centre.
        /// </summary>
        public static GameObject WeaponModel(WeaponDef def, float scale, Transform parent)
        {
            var g = new GameObject("weapon_" + def.id);
            g.transform.SetParent(parent, false);
            g.transform.localScale = Vector3.one * scale;
            var T = g.transform;

            Color bodyCol = Util.Lighten(def.Tint, 3.6f);
            Color accentCol = Util.Lighten(def.Accent, 2.3f);
            var steel = Art.Lit("gnS" + def.tint, bodyCol, 0.6f, 0.8f);
            var poly = Art.Lit("gnP" + def.tint, Util.Lighten(bodyCol, 0.72f), 0.22f, 0.05f);
            var accent = Art.Lit("gnA" + def.accent, accentCol, 0.7f, 0.78f);
            var grip = Art.Lit("gnGrip", Util.Hex(0x555c62), 0.05f, 0.03f);
            var rubber = Art.Lit("gnRubber", Util.Hex(0x1d2124), 0.02f, 0f);
            var wood = Art.Lit("gnWood", Util.Hex(0x7a5333), 0.4f, 0.02f);

            WeaponClass fam = def.cls;
            bool revolver = def.revolver;
            bool energy = fam == WeaponClass.Wonder;
            bool handgun = fam == WeaponClass.Pistol || revolver;
            var furniture = (fam == WeaponClass.Shotgun || def.wood) ? wood : poly;

            // Real receivers are far narrower than they are tall; the old models
            // were square in section, which is most of why they read as toys.
            float W = Mathf.Min(def.recvW, 0.058f);
            float H = def.recvH, D = def.recvL;
            float bl = def.barrelLen, bore = def.bore;
            float boreY = H * 0.16f;
            float front = D / 2f;

            var info = g.AddComponent<WeaponModelInfo>();

            // ---------------------------------------------------------- receiver
            if (handgun)
            {
                Box(W, H * 0.5f, D, steel, new Vector3(0, boreY + H * 0.16f, 0), T);
                Box(W * 0.72f, H * 0.34f, D * 0.86f, steel, new Vector3(0, boreY - H * 0.1f, -D * 0.02f), T);
                for (int i = 0; i < 6; i++)
                    Box(W * 1.02f, H * 0.34f, 0.005f, accent, new Vector3(0, boreY + H * 0.16f, -D * 0.42f + i * 0.011f), T);
                Box(W * 1.04f, H * 0.16f, D * 0.2f, steel, new Vector3(0, boreY + H * 0.2f, D * 0.02f), T);
            }
            else
            {
                Box(W, H * 0.46f, D, steel, new Vector3(0, boreY + H * 0.14f, 0), T);
                Box(W * 0.98f, H * 0.06f, D * 0.98f, accent, new Vector3(0, boreY + H * 0.37f, 0), T);
                Box(W * 0.94f, H * 0.4f, D * 0.82f, furniture, new Vector3(0, boreY - H * 0.16f, -D * 0.02f), T);
                Box(W * 1.03f, H * 0.16f, D * 0.22f, Art.Lit("m_gnport", Util.Hex(0x0c0f11), 0.05f),
                    new Vector3(W * 0.02f, boreY + H * 0.14f, D * 0.1f), T);
                var defl = Box(W * 0.34f, H * 0.13f, 0.03f, steel, new Vector3(W * 0.5f, boreY + H * 0.16f, D * 0.2f), T);
                defl.transform.localRotation = Quaternion.Euler(0, 0, 29f);
                Box(W * 1.5f, H * 0.08f, 0.02f, accent, new Vector3(0, boreY + H * 0.26f, -D * 0.48f), T);
                Box(W * 0.3f, H * 0.06f, 0.05f, accent, new Vector3(0, boreY + H * 0.26f, -D * 0.42f), T);
                var sel = CylZ(0.008f, W * 1.3f, accent, new Vector3(0, boreY - H * 0.1f, -D * 0.24f), T);
                sel.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                Box(W * 1.2f, 0.014f, 0.014f, accent, new Vector3(0, boreY - H * 0.06f, -D * 0.06f), T);
            }

            // ---------------------------------------------------------- barrel
            if (revolver)
            {
                Box(W * 0.7f, H * 0.3f, bl, accent, new Vector3(0, boreY + H * 0.12f, front + bl / 2f), T);
                CylZ(bore * 1.7f, bl, accent, new Vector3(0, boreY + H * 0.12f, front + bl / 2f), T);
                Box(W * 0.5f, H * 0.16f, bl * 0.8f, steel, new Vector3(0, boreY - H * 0.02f, front + bl * 0.42f), T);
                float cr = H * 0.3f;
                CylZ(cr, 0.09f, steel, new Vector3(0, boreY + H * 0.1f, -D * 0.02f), T);
                var hole = Art.Lit("m_gnbore", Util.Hex(0x0a0c0d), 0.02f);
                for (int i = 0; i < 6; i++)
                {
                    float a = (i / 6f) * Mathf.PI * 2f;
                    CylZ(bore * 1.15f, 0.095f, hole,
                        new Vector3(Mathf.Sin(a) * cr * 0.62f, boreY + H * 0.1f + Mathf.Cos(a) * cr * 0.62f, -D * 0.02f), T);
                }
            }
            else if (fam == WeaponClass.Shotgun)
            {
                CylZ(bore * 1.28f, bl, accent, new Vector3(0, boreY + H * 0.12f, front + bl / 2f), T);
                CylZ(bore * 0.95f, bl * 0.86f, steel,
                    new Vector3(0, boreY + H * 0.12f - bore * 2.1f, front + bl * 0.43f), T);
                if (!def.hasDrum)
                {
                    Box(W * 1.15f, H * 0.3f, bl * 0.34f, furniture, new Vector3(0, boreY + H * 0.02f, front + bl * 0.36f), T);
                    for (int i = 0; i < 5; i++)
                        Box(W * 1.18f, 0.008f, 0.008f, steel, new Vector3(0, boreY + H * 0.02f, front + bl * 0.22f + i * 0.018f), T);
                }
                Ring(bore * 1.3f, 0.004f, steel, new Vector3(0, boreY + H * 0.12f, front + bl), T, 12);
            }
            else if (fam == WeaponClass.Pistol)
            {
                CylZ(bore * 1.25f, D * 0.92f, accent, new Vector3(0, boreY + H * 0.16f, D * 0.08f), T);
                if (def.compensator)
                    Box(W * 0.9f, H * 0.2f, 0.05f, accent, new Vector3(0, boreY + H * 0.22f, front + 0.02f), T);
            }
            else
            {
                float gasZ = front + bl * 0.52f;
                CylZ(bore * 2.1f, bl * 0.5f, accent, new Vector3(0, boreY, front + bl * 0.25f), T);
                CylZ(bore * 1.55f, bl * 0.52f, accent, new Vector3(0, boreY, front + bl * 0.75f), T);
                Box(W * 0.52f, H * 0.24f, 0.036f, steel, new Vector3(0, boreY + H * 0.02f, gasZ), T);
                CylZ(0.006f, bl * 0.5f, steel, new Vector3(0, boreY + H * 0.12f, front + bl * 0.28f), T);
                if (fam == WeaponClass.Lmg)
                {
                    Box(0.016f, 0.05f, 0.11f, steel, new Vector3(W * 0.4f, boreY + H * 0.42f, front + bl * 0.1f), T);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var leg = CylZ(0.008f, 0.2f, steel, new Vector3(s * 0.02f, boreY - H * 0.5f, gasZ + 0.02f), T);
                        leg.transform.localRotation = Quaternion.Euler(-66f, 0, s * 16f);
                    }
                }
            }

            // ---------------------------------------------------------- handguard
            if (def.hasGuard && !handgun && fam != WeaponClass.Shotgun)
            {
                float hgLen = bl * (fam == WeaponClass.Sniper ? 0.6f : 0.72f);
                float hgZ = front + hgLen / 2f + 0.005f;
                if (fam == WeaponClass.Sniper || def.wood)
                {
                    Box(W * 1.25f, H * 0.42f, hgLen, furniture, new Vector3(0, boreY - H * 0.12f, hgZ), T);
                }
                else
                {
                    var vent = Art.Lit("m_gnvent", Util.Hex(0x0d1012), 0.02f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i / 6f) * Mathf.PI * 2f + Mathf.PI / 6f;
                        var pnl = Box(W * 0.44f, H * 0.12f, hgLen, poly,
                            new Vector3(Mathf.Sin(a) * W * 0.44f, boreY + Mathf.Cos(a) * W * 0.44f, hgZ), T);
                        pnl.transform.localRotation = Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg);
                    }
                    for (int i = 0; i < 4; i++)
                        for (int s = -1; s <= 1; s += 2)
                            Box(0.004f, 0.016f, 0.028f, vent,
                                new Vector3(s * W * 0.5f, boreY, hgZ - hgLen * 0.3f + i * hgLen * 0.2f), T);
                    RailSection(hgLen * 0.9f, accent, new Vector3(0, boreY + W * 0.56f, hgZ), T);
                    if (fam != WeaponClass.Sniper)
                    {
                        var fg = Box(0.028f, 0.075f, 0.05f, grip, new Vector3(0, boreY - W * 0.75f, hgZ + hgLen * 0.1f), T);
                        fg.transform.localRotation = Quaternion.Euler(-24f, 0, 0);
                    }
                }
            }

            // ---------------------------------------------------------- grip
            float gripZ = -D * (handgun ? 0.22f : 0.2f);
            float gripY = boreY - H * (handgun ? 0.24f : 0.3f);
            GripAssembly(furniture == wood && fam == WeaponClass.Shotgun ? wood : grip, steel,
                new Vector3(0, gripY, gripZ), T, handgun ? 0.3f : 0.36f, handgun ? 0.115f : 0.13f, true);

            // ---------------------------------------------------------- feed
            if (def.hasDrum)
            {
                float dr = fam == WeaponClass.Lmg ? 0.1f : 0.085f;
                float dy = boreY - H * 0.42f - dr * 0.5f;
                var drum = CylZ(dr, 0.062f, furniture, new Vector3(0, dy, D * 0.04f), T);
                Ring(dr * 0.96f, 0.006f, steel, new Vector3(0, dy, D * 0.04f + 0.032f), T, 14);
                Box(W * 0.7f, H * 0.3f, 0.05f, furniture, new Vector3(0, boreY - H * 0.32f, D * 0.04f), T);
                info.magazine = drum.transform;
                info.magHome = drum.transform.localPosition;
            }
            else if (!def.hasTube && def.hasMag)
            {
                GameObject m;
                if (fam == WeaponClass.Pistol)
                {
                    m = Box(0.028f, def.magLen, 0.042f, furniture,
                        new Vector3(0, gripY - def.magLen * 0.5f + 0.02f, gripZ + 0.006f), T);
                    m.transform.localRotation = Quaternion.Euler(17f, 0, 0);
                }
                else
                {
                    m = CurvedMag(def.magLen, 0.03f, 0.05f, furniture,
                        new Vector3(0, boreY - H * 0.36f, D * 0.04f), T, def.magCurve);
                }
                info.magazine = m.transform;
                info.magHome = m.transform.localPosition;
            }

            // ---------------------------------------------------------- stock
            if (def.hasStock && !handgun)
            {
                float backZ = -D / 2f;
                if (fam == WeaponClass.Shotgun || fam == WeaponClass.Sniper || def.wood)
                    FixedStock(def.stockLen, furniture, rubber, backZ, H * 0.22f, T);
                else
                    CollapsibleStock(def.stockLen, furniture, steel, rubber, backZ, T);
            }

            // ---------------------------------------------------------- sights
            float topY = boreY + H * 0.4f;
            float sightY;
            if (def.hasOptic)
            {
                float mag = def.opticMag;
                bool big = mag >= 4f;
                float tubeR = big ? 0.019f : 0.015f;
                float oy = topY + tubeR + 0.019f;
                float oz = -D * 0.06f;
                float oLen = big ? 0.26f : 0.14f;
                var optBody = Art.Lit("m_gnoptic", Util.Hex(0x1a1e21), 0.58f, 0.6f);
                CylZ(tubeR, oLen, optBody, new Vector3(0, oy, oz), T);
                if (big)
                {
                    CylZ(tubeR * 1.6f, 0.08f, optBody, new Vector3(0, oy, oz + oLen / 2f + 0.04f), T);
                    Cyl(0.011f, 0.018f, optBody, new Vector3(0, oy + tubeR + 0.009f, oz - 0.01f), T);
                    var wind = CylZ(0.01f, 0.016f, optBody, new Vector3(tubeR + 0.008f, oy, oz - 0.01f), T);
                    wind.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                }
                CylZ(tubeR * 1.25f, 0.034f, optBody, new Vector3(0, oy, oz - oLen / 2f - 0.017f), T);
                var lensMat = Art.Emissive("m_gnlens", Util.Hex(0x2b6a7d), 0.35f);
                CylZ(big ? tubeR * 1.5f : tubeR * 0.9f, 0.004f, lensMat,
                    new Vector3(0, oy, oz + (big ? oLen / 2f + 0.076f : oLen / 2f + 0.002f)), T);
                for (int k = 0; k < 2; k++)
                {
                    float dz = (k == 0 ? -1f : 1f) * oLen * 0.3f;
                    Ring(tubeR + 0.004f, 0.005f, accent, new Vector3(0, oy, oz + dz), T, 12);
                    Box(0.022f, 0.018f, 0.016f, accent, new Vector3(0, oy - tubeR - 0.012f, oz + dz), T);
                }
                sightY = oy;
                info.opticMagnification = mag;
                info.scoped = big;
            }
            else
            {
                float fz = front + bl * 0.86f;
                Box(0.006f, 0.026f, 0.008f, accent, new Vector3(0, topY + 0.012f, fz), T);
                Ring(0.016f, 0.003f, accent, new Vector3(0, topY + 0.012f, fz), T, 10);
                Box(0.026f, 0.01f, 0.022f, accent, new Vector3(0, topY - 0.002f, fz), T);
                Ring(0.011f, 0.004f, accent, new Vector3(0, topY + 0.014f, -D * 0.4f), T, 10);
                Box(0.03f, 0.016f, 0.018f, accent, new Vector3(0, topY + 0.001f, -D * 0.4f), T);
                if (!handgun) RailSection(D * 0.7f, accent, new Vector3(0, topY + 0.004f, -D * 0.02f), T);
                sightY = topY + 0.014f;
            }
            info.sightHeight = sightY;

            // ---------------------------------------------------------- energy
            if (def.hasCell || def.upgradeLevel > 0)
            {
                var em = Art.Emissive("em_cell" + def.accent, accentCol, 2.4f);
                var cell = CylZ(0.022f, 0.13f, em, new Vector3(0, boreY + H * 0.02f, -D * 0.12f), T);
                cell.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                var coil = Ring(bore * 2.4f, 0.006f, em, new Vector3(0, boreY, front + bl * 0.72f), T, 12);
                var coil2 = Ring(bore * 2.8f, 0.005f, em, new Vector3(0, boreY, front + bl * 0.92f), T, 12);
                for (int i = 0; i < 4; i++)
                    Box(W * 1.3f, 0.006f, 0.01f, accent, new Vector3(0, boreY + H * 0.24f, front + bl * 0.2f + i * 0.03f), T);
                info.energy = new[] { cell.GetComponent<MeshRenderer>() };
                info.energyGroups = new[] { coil.transform, coil2.transform };
            }

            // Muzzle device, and the exact bore exit for tracers and flash.
            float muzzleZ = handgun ? (revolver ? front + bl : D * 0.54f) : front + bl;
            if (fam != WeaponClass.Shotgun)
                MuzzleDevice(def.muzzle, bore, accent, muzzleZ, T);

            info.muzzle = new Vector3(0, boreY + (fam == WeaponClass.Pistol ? H * 0.16f : 0f), muzzleZ + 0.03f);
            // Where the firing hand grips the weapon. The view model hangs
            // everything off this, so a long rifle and a pistol both sit
            // naturally in frame instead of being centred on their receivers.
            info.gripAnchor = new Vector3(0, gripY - 0.035f, gripZ + 0.02f);
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
        public Transform[] energyGroups;
        public Vector3 muzzle;
        public Vector3 gripAnchor;
        public float sightHeight = 0.09f;
        public float opticMagnification = 1f;
        public bool scoped;
    }
}
