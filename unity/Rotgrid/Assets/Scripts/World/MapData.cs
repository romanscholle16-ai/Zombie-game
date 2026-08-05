using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public class ZoneDef
    {
        public string id, name, style, needs;
        public float x0, x1, z0, z1, y, h;
        public bool unlocked;
        public bool hasSlope;
        public bool slopeAxisX;
        public float slopeFrom, slopeTo, slopeYFrom, slopeYTo;

        public float CenterX { get { return (x0 + x1) * 0.5f; } }
        public float CenterZ { get { return (z0 + z1) * 0.5f; } }
        public float Width { get { return x1 - x0; } }
        public float Depth { get { return z1 - z0; } }

        public float HeightAt(float x, float z)
        {
            if (!hasSlope) return y;
            float v = slopeAxisX ? x : z;
            float t = Mathf.Clamp01((v - slopeFrom) / (slopeTo - slopeFrom));
            return Mathf.Lerp(slopeYFrom, slopeYTo, t);
        }
    }

    public class RectDef
    {
        public float x0, x1, z0, z1;
        public RectDef(float a, float b, float c, float d) { x0 = a; x1 = b; z0 = c; z1 = d; }
        public bool Contains(float x, float z) { return x > x0 && x < x1 && z > z0 && z < z1; }
    }

    public class DoorDef
    {
        public string id, name, zone;
        public int cost;
        public bool requiresPower;
        public float x0, x1, z0, z1;
        public string[] opens;
    }

    public class WindowDef
    {
        public string id, zone;
        public float x, z, nx, nz;
        public float ax0, az0, ax1, az1;   // alcove
    }

    public class WallBuyDef { public string id, weapon, zone; public float x, z, rot, y; }
    public class ArmorDef { public string id, zone; public float x, z, rot, y; public int cost; }
    public class PerkSpotDef { public string perk, zone; public float x, z, rot, y; }
    public class BoxSpotDef { public string id, zone; public float x, z, y, rot; }
    public class TrapDef
    {
        public string id, kind, zone;
        public float x, z, y, rot;
        public int cost;
        public RectDef area;
        public float panelX, panelZ, panelRot;
    }
    public class LightDef { public float x, y, z; public string zone; public int color; public float i, d; }
    public class ClutterDef
    {
        public string zone;
        public int seed, crates, barrels, shelves, tables, generators, debris;
    }

    /// <summary>
    /// "BLACKSITE 7" — the same original map the browser build uses, described
    /// declaratively so the world builder, colliders and nav grid all come from
    /// one source. Adding a map means adding another file shaped like this.
    /// </summary>
    public static class MapData
    {
        public const string Name = "BLACKSITE 7";
        public const string Tagline = "Decommissioned research annex";
        public static readonly Color FogColor = Util.Hex(0x0a0f12);

        public static readonly Vector3 SpawnPos = new Vector3(0f, 0f, 4f);
        public const float SpawnYaw = Mathf.PI;

        public static readonly List<ZoneDef> Zones = new List<ZoneDef>
        {
            new ZoneDef { id="atrium", name="ARRIVALS ATRIUM", x0=-12, x1=12, z0=-12, z1=12, y=0, h=5.2f, style="concrete", unlocked=true },
            new ZoneDef { id="n_hall", name="NORTH CORRIDOR", x0=-4, x1=4, z0=-18, z1=-12, y=0, h=3.6f, style="metal", needs="doorA" },
            new ZoneDef { id="lab", name="BIOLOGY LAB", x0=-16, x1=16, z0=-44, z1=-18, y=0, h=5.6f, style="tile", needs="doorA" },
            new ZoneDef { id="e_hall", name="SERVICE CORRIDOR", x0=12, x1=20, z0=-4, z1=4, y=0, h=3.6f, style="metal", needs="doorB" },
            new ZoneDef { id="power", name="POWER STATION", x0=20, x1=44, z0=-14, z1=12, y=0, h=6.4f, style="metal", needs="doorB" },
            new ZoneDef { id="w_ramp", name="DESCENT RAMP", x0=-28, x1=-16, z0=-34, z1=-26, y=0, h=4.2f, style="concrete", needs="doorC",
                          hasSlope=true, slopeAxisX=true, slopeFrom=-16, slopeTo=-28, slopeYFrom=0, slopeYTo=-4 },
            new ZoneDef { id="under", name="UNDERGROUND FACILITY", x0=-48, x1=-28, z0=-40, z1=-14, y=-4, h=5.4f, style="dirt", needs="doorC" },
            new ZoneDef { id="pap_hall", name="SEALED PASSAGE", x0=-42, x1=-36, z0=-48, z1=-40, y=-4, h=3.4f, style="metal", needs="doorPaP" },
            new ZoneDef { id="pap", name="REFIT CHAMBER", x0=-52, x1=-32, z0=-64, z1=-48, y=-4, h=6.0f, style="metal", needs="doorPaP" },
        };

        /// <summary>Wall cut-outs: any wall segment overlapping one is not built.</summary>
        public static readonly List<RectDef> Openings = new List<RectDef>
        {
            new RectDef(-4, 4, -13, -11),      // atrium <-> north corridor
            new RectDef(-4, 4, -19, -17),      // north corridor <-> lab
            new RectDef(11, 13, -4, 4),        // atrium <-> service corridor
            new RectDef(19, 21, -4, 4),        // service corridor <-> power
            new RectDef(-17, -15, -34, -26),   // lab <-> ramp
            new RectDef(-29, -27, -34, -26),   // ramp <-> underground
            new RectDef(-42, -36, -41, -39),   // underground <-> sealed passage
            new RectDef(-42, -36, -49, -47),   // sealed passage <-> refit chamber
        };

        public static readonly List<DoorDef> Doors = new List<DoorDef>
        {
            new DoorDef { id="doorA", name="BLAST DOOR A", cost=750, zone="atrium",
                          x0=-4, x1=4, z0=-12.45f, z1=-11.55f, opens=new[]{"n_hall","lab"} },
            new DoorDef { id="doorB", name="BLAST DOOR B", cost=1000, zone="atrium",
                          x0=11.55f, x1=12.45f, z0=-4, z1=4, opens=new[]{"e_hall","power"} },
            new DoorDef { id="doorC", name="BLAST DOOR C", cost=1250, zone="lab",
                          x0=-16.45f, x1=-15.55f, z0=-34, z1=-26, opens=new[]{"w_ramp","under"} },
            new DoorDef { id="doorPaP", name="REFIT SEAL", cost=0, requiresPower=true, zone="under",
                          x0=-42, x1=-36, z0=-48.45f, z1=-47.55f, opens=new[]{"pap_hall","pap"} },
        };

        public static readonly List<WindowDef> Windows = new List<WindowDef>
        {
            new WindowDef { id="w_atr_s1", zone="atrium", x=-6, z=12, nx=0, nz=1, ax0=-8.6f, az0=12, ax1=-3.4f, az1=18.5f },
            new WindowDef { id="w_atr_s2", zone="atrium", x=6, z=12, nx=0, nz=1, ax0=3.4f, az0=12, ax1=8.6f, az1=18.5f },
            new WindowDef { id="w_atr_w1", zone="atrium", x=-12, z=-6, nx=-1, nz=0, ax0=-18.5f, az0=-8.6f, ax1=-12, az1=-3.4f },
            new WindowDef { id="w_atr_w2", zone="atrium", x=-12, z=6, nx=-1, nz=0, ax0=-18.5f, az0=3.4f, ax1=-12, az1=8.6f },
            new WindowDef { id="w_atr_e", zone="atrium", x=12, z=-9, nx=1, nz=0, ax0=12, az0=-11.4f, ax1=18.5f, az1=-6.6f },
            new WindowDef { id="w_lab_n1", zone="lab", x=-8, z=-44, nx=0, nz=-1, ax0=-10.6f, az0=-50.5f, ax1=-5.4f, az1=-44 },
            new WindowDef { id="w_lab_n2", zone="lab", x=8, z=-44, nx=0, nz=-1, ax0=5.4f, az0=-50.5f, ax1=10.6f, az1=-44 },
            new WindowDef { id="w_lab_w", zone="lab", x=-16, z=-23, nx=-1, nz=0, ax0=-22.5f, az0=-25.6f, ax1=-16, az1=-20.4f },
            new WindowDef { id="w_lab_e", zone="lab", x=16, z=-30, nx=1, nz=0, ax0=16, az0=-32.6f, ax1=22.5f, az1=-27.4f },
            new WindowDef { id="w_pow_n", zone="power", x=32, z=-14, nx=0, nz=-1, ax0=29.4f, az0=-20.5f, ax1=34.6f, az1=-14 },
            new WindowDef { id="w_pow_e", zone="power", x=44, z=0, nx=1, nz=0, ax0=44, az0=-2.6f, ax1=50.5f, az1=2.6f },
            new WindowDef { id="w_pow_s", zone="power", x=32, z=12, nx=0, nz=1, ax0=29.4f, az0=12, ax1=34.6f, az1=18.5f },
            new WindowDef { id="w_und_w", zone="under", x=-48, z=-28, nx=-1, nz=0, ax0=-54.5f, az0=-30.6f, ax1=-48, az1=-25.4f },
            new WindowDef { id="w_und_n", zone="under", x=-33, z=-40, nx=0, nz=-1, ax0=-35.6f, az0=-46.5f, ax1=-30.4f, az1=-40 },
            new WindowDef { id="w_und_s", zone="under", x=-38, z=-14, nx=0, nz=1, ax0=-40.6f, az0=-14, ax1=-35.4f, az1=-8.5f },
        };

        public static readonly List<WallBuyDef> WallBuys = new List<WallBuyDef>
        {
            new WallBuyDef { id="wb_pistol", weapon="sidearm", zone="atrium", x=0, z=11.7f, rot=180f, y=0 },
            new WallBuyDef { id="wb_smg", weapon="wasp9", zone="atrium", x=-11.7f, z=0, rot=90f, y=0 },
            new WallBuyDef { id="wb_shotgun", weapon="breaker", zone="lab", x=15.7f, z=-22, rot=-90f, y=0 },
            new WallBuyDef { id="wb_ar", weapon="kestrel", zone="lab", x=-15.7f, z=-38, rot=90f, y=0 },
            new WallBuyDef { id="wb_smg2", weapon="ripcord", zone="power", x=43.7f, z=-8, rot=-90f, y=0 },
            new WallBuyDef { id="wb_lmg", weapon="anvil", zone="power", x=26, z=11.7f, rot=180f, y=0 },
            new WallBuyDef { id="wb_sniper", weapon="longshot", zone="under", x=-47.7f, z=-20, rot=90f, y=-4 },
            new WallBuyDef { id="wb_ar2", weapon="vulture", zone="under", x=-32, z=-39.7f, rot=0f, y=-4 },
        };

        public static readonly List<ArmorDef> ArmorStations = new List<ArmorDef>
        {
            new ArmorDef { id="arm_atrium", zone="atrium", x=11.7f, z=6, rot=-90f, y=0, cost=500 },
            new ArmorDef { id="arm_power", zone="power", x=20.3f, z=-12, rot=90f, y=0, cost=500 },
        };

        public static readonly List<PerkSpotDef> PerkSpots = new List<PerkSpotDef>
        {
            new PerkSpotDef { perk="vital", zone="lab", x=-13, z=-21, rot=90f, y=0 },
            new PerkSpotDef { perk="rapid", zone="power", x=40, z=9, rot=180f, y=0 },
            new PerkSpotDef { perk="swift", zone="atrium", x=9.5f, z=9.5f, rot=-135f, y=0 },
            new PerkSpotDef { perk="medic", zone="under", x=-45, z=-17, rot=90f, y=-4 },
            new PerkSpotDef { perk="endure", zone="lab", x=13, z=-41, rot=-90f, y=0 },
        };

        public static readonly List<BoxSpotDef> BoxSpots = new List<BoxSpotDef>
        {
            new BoxSpotDef { id="bs_atrium", zone="atrium", x=-7, z=-7, y=0, rot=34f },
            new BoxSpotDef { id="bs_lab", zone="lab", x=9, z=-33, y=0, rot=-23f },
            new BoxSpotDef { id="bs_power", zone="power", x=25, z=5, y=0, rot=69f },
            new BoxSpotDef { id="bs_under", zone="under", x=-41, z=-22, y=-4, rot=-63f },
            new BoxSpotDef { id="bs_pap", zone="pap", x=-36, z=-60, y=-4, rot=137f },
        };

        public static readonly Vector3 UpgradeMachinePos = new Vector3(-42f, -4f, -57f);
        public const float UpgradeMachineRot = 0f;
        public const string UpgradeMachineZone = "pap";

        public static readonly Vector3 PowerSwitchPos = new Vector3(42.4f, 0f, -11f);
        public const float PowerSwitchRot = -90f;
        public const string PowerSwitchZone = "power";

        public static readonly List<TrapDef> Traps = new List<TrapDef>
        {
            new TrapDef { id="trap_e", kind="electric", zone="e_hall", x=16, z=0, y=0, cost=1000, rot=0,
                          area=new RectDef(12.5f, 19.5f, -3.5f, 3.5f), panelX=19.6f, panelZ=3.4f, panelRot=-90f },
            new TrapDef { id="trap_f", kind="fire", zone="lab", x=0, z=-31, y=0, cost=1250, rot=0,
                          area=new RectDef(-5, 5, -36, -26), panelX=-6.5f, panelZ=-26.5f, panelRot=0f },
            new TrapDef { id="trap_t", kind="turret", zone="under", x=-36, z=-24, y=-4, cost=1500, rot=180,
                          area=new RectDef(-47, -29, -34, -15), panelX=-34.5f, panelZ=-24, panelRot=-90f },
        };

        public static readonly List<LightDef> Lights = new List<LightDef>
        {
            new LightDef { x=0, y=3.9f, z=0, zone="atrium", color=0xffd9a8, i=1.0f, d=26 },
            new LightDef { x=-7, y=3.6f, z=7, zone="atrium", color=0xbfd8ff, i=0.55f, d=16 },
            new LightDef { x=7, y=3.6f, z=-7, zone="atrium", color=0xbfd8ff, i=0.55f, d=16 },
            new LightDef { x=0, y=2.8f, z=-15, zone="n_hall", color=0x9fd93a, i=0.5f, d=12 },
            new LightDef { x=-8, y=4.2f, z=-24, zone="lab", color=0xd8f0ff, i=0.9f, d=24 },
            new LightDef { x=8, y=4.2f, z=-36, zone="lab", color=0xd8f0ff, i=0.9f, d=24 },
            new LightDef { x=16, y=2.8f, z=0, zone="e_hall", color=0xffb066, i=0.5f, d=12 },
            new LightDef { x=30, y=4.8f, z=-4, zone="power", color=0xffc98a, i=1.0f, d=28 },
            new LightDef { x=38, y=4.8f, z=6, zone="power", color=0xffc98a, i=0.8f, d=22 },
            new LightDef { x=-22, y=-0.4f, z=-30, zone="w_ramp", color=0x88ff9c, i=0.5f, d=14 },
            new LightDef { x=-38, y=-0.6f, z=-22, zone="under", color=0x7fe0a0, i=0.85f, d=26 },
            new LightDef { x=-44, y=-0.6f, z=-34, zone="under", color=0x7fe0a0, i=0.7f, d=22 },
            new LightDef { x=-42, y=-0.8f, z=-56, zone="pap", color=0xff9c5a, i=1.1f, d=26 },
        };

        public static readonly List<ClutterDef> Clutter = new List<ClutterDef>
        {
            new ClutterDef { zone="atrium", seed=11, crates=7, barrels=4, shelves=2, debris=12 },
            new ClutterDef { zone="lab", seed=23, crates=4, barrels=3, tables=6, shelves=4, debris=14 },
            new ClutterDef { zone="power", seed=37, crates=6, barrels=8, generators=3, shelves=2, debris=10 },
            new ClutterDef { zone="under", seed=53, crates=8, barrels=6, shelves=3, debris=18 },
            new ClutterDef { zone="pap", seed=71, crates=3, barrels=4, debris=8 },
            new ClutterDef { zone="n_hall", seed=83, crates=1, debris=4 },
            new ClutterDef { zone="e_hall", seed=97, barrels=2, debris=4 },
            new ClutterDef { zone="w_ramp", seed=101, debris=6 },
        };

        public static ZoneDef Zone(string id)
        {
            for (int i = 0; i < Zones.Count; i++) if (Zones[i].id == id) return Zones[i];
            return Zones[0];
        }

        public static int ZoneIndex(string id)
        {
            for (int i = 0; i < Zones.Count; i++) if (Zones[i].id == id) return i;
            return -1;
        }

        /// <summary>Bounding box of everything, including the spawn alcoves.</summary>
        public static void Bounds(out float x0, out float x1, out float z0, out float z1)
        {
            x0 = float.MaxValue; x1 = float.MinValue;
            z0 = float.MaxValue; z1 = float.MinValue;
            foreach (var z in Zones)
            {
                x0 = Mathf.Min(x0, z.x0); x1 = Mathf.Max(x1, z.x1);
                z0 = Mathf.Min(z0, z.z0); z1 = Mathf.Max(z1, z.z1);
            }
            foreach (var w in Windows)
            {
                x0 = Mathf.Min(x0, w.ax0); x1 = Mathf.Max(x1, w.ax1);
                z0 = Mathf.Min(z0, w.az0); z1 = Mathf.Max(z1, w.az1);
            }
            x0 -= 4f; x1 += 4f; z0 -= 4f; z1 += 4f;
        }
    }
}
