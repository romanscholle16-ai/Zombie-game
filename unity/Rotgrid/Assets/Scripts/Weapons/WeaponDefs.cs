using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum Rarity { Common, Uncommon, Rare, Epic, Wonder }
    public enum FireMode { Auto, Semi, Burst }
    public enum WeaponClass { Pistol, Smg, Ar, Shotgun, Lmg, Sniper, Wonder, Melee }
    public enum SoundWeight { Light, Medium, Heavy, Shotgun, Energy }
    public enum SpecialEffect { None, Chain, Blast, Burn }

    /// <summary>
    /// One weapon's stats and silhouette. All names and numbers are original.
    /// `boxWeight` of 0 keeps a weapon out of the mystery crate.
    /// </summary>
    public class WeaponDef
    {
        public string id, name, blurb;
        public WeaponClass cls;
        public Rarity rarity;
        public FireMode fireMode = FireMode.Auto;

        public float damage;
        public int rpm;
        public int mag, reserve;
        public int pellets = 1;
        public int burst = 3;
        public float burstDelay = 0.2f;
        public float headMult = 2.6f;
        public float limbMult = 0.72f;
        public float torsoMult = 1f;
        public float range = 90f;
        public float falloff = 0.55f;
        public int penetration = 1;
        public float recoilV = 0.9f, recoilH = 0.4f;
        public float spreadHip = 0.045f, spreadAds = 0.008f, spreadMove = 0.03f;
        public float adsTime = 0.24f;
        public float reloadTime = 2f, emptyReloadTime = 2.6f;
        public bool shellReload;
        public float moveScale = 1f;
        public int boxWeight = 10;
        public int wallCost, wallAmmoCost;
        public SoundWeight sound = SoundWeight.Medium;
        public int tint = 0x2e3337, accent = 0x53595e;
        public SpecialEffect special = SpecialEffect.None;
        public int chainTargets = 5;
        public float chainRadius = 6.5f, chainFalloff = 0.7f;
        public float blastRadius = 3.5f, blastDamage;
        public int upgradeLevel;

        // Silhouette
        public float recvW = 0.085f, recvH = 0.135f, recvL = 0.4f;
        public float barrelLen = 0.3f, bore = 0.028f, magLen = 0.2f, stockLen = 0.24f;
        public bool hasGuard = true, hasMag = true, hasStock = true;
        public bool hasOptic, hasDrum, hasTube, hasCell;
        public float magCurve = 0.4f;      // how far the magazine sweeps forward
        public float opticMag = 2f;        // true magnification; >= 4 gets a scope
        public string muzzle = "cage";     // cage | brake | suppressor | none
        public bool wood;                  // walnut furniture instead of polymer
        public bool compensator;           // ported slide on a machine pistol
        public bool revolver;              // cylinder and underlug, no magazine

        public Color Tint { get { return Util.Hex(tint); } }
        public Color Accent { get { return Util.Hex(accent); } }
        public float FireInterval { get { return 60f / Mathf.Max(1, rpm); } }

        public Color RarityColor
        {
            get
            {
                switch (rarity)
                {
                    case Rarity.Uncommon: return Util.Hex(0x5ad07a);
                    case Rarity.Rare: return Util.Hex(0x4aa3ff);
                    case Rarity.Epic: return Util.Hex(0xb479ff);
                    case Rarity.Wonder: return Util.Hex(0x9fd93a);
                    default: return Util.Hex(0x8d948c);
                }
            }
        }

        public string RarityLabel { get { return rarity.ToString().ToUpperInvariant(); } }

        public string ClassLabel
        {
            get
            {
                switch (cls)
                {
                    case WeaponClass.Pistol: return "PISTOL";
                    case WeaponClass.Smg: return "SMG";
                    case WeaponClass.Ar: return "ASSAULT RIFLE";
                    case WeaponClass.Shotgun: return "SHOTGUN";
                    case WeaponClass.Lmg: return "LMG";
                    case WeaponClass.Sniper: return "SNIPER";
                    case WeaponClass.Wonder: return "WONDER";
                    default: return "MELEE";
                }
            }
        }

        public WeaponDef Clone()
        {
            return (WeaponDef)MemberwiseClone();
        }
    }

    public static class Weapons
    {
        public static readonly Dictionary<string, WeaponDef> All = new Dictionary<string, WeaponDef>();
        public static readonly List<WeaponDef> List = new List<WeaponDef>();

        public static WeaponDef Knife = new WeaponDef
        {
            id = "knife", name = "COMBAT KNIFE", cls = WeaponClass.Melee, rarity = Rarity.Common,
            damage = 150f, range = 2.5f, rpm = 110, headMult = 1f, limbMult = 1f,
            blurb = "Silent, free, and always in reach.",
        };

        static void Add(WeaponDef d)
        {
            All[d.id] = d;
            List.Add(d);
        }

        static Weapons()
        {
            // ------------------------------------------------------- pistols
            Add(new WeaponDef
            {
                id = "sidearm", name = "SIDEARM MK1", cls = WeaponClass.Pistol, rarity = Rarity.Common,
                damage = 55, rpm = 400, fireMode = FireMode.Semi, mag = 8, reserve = 80,
                reloadTime = 1.5f, emptyReloadTime = 1.9f, adsTime = 0.18f,
                recoilV = 1.5f, recoilH = 0.5f, spreadHip = 0.038f, spreadAds = 0.004f, spreadMove = 0.026f,
                wallAmmoCost = 250, boxWeight = 0, sound = SoundWeight.Light,
                recvW = 0.05f, recvH = 0.1f, recvL = 0.2f, barrelLen = 0.12f, hasStock = false, hasGuard = false,
                magLen = 0.15f, bore = 0.011f, muzzle = "none",
                blurb = "Standard-issue service pistol. Reliable, unremarkable, and free.",
            });
            Add(new WeaponDef
            {
                id = "magnus", name = "MAGNUS 44", cls = WeaponClass.Pistol, rarity = Rarity.Uncommon,
                damage = 210, rpm = 190, fireMode = FireMode.Semi, mag = 6, reserve = 48,
                reloadTime = 2.4f, emptyReloadTime = 2.9f, adsTime = 0.22f,
                recoilV = 4.2f, recoilH = 1.4f, spreadHip = 0.055f, spreadAds = 0.003f, spreadMove = 0.04f,
                headMult = 3f, boxWeight = 12, sound = SoundWeight.Heavy, tint = 0x3b3f42, accent = 0x8d7a4a,
                recvW = 0.052f, recvH = 0.11f, recvL = 0.2f, barrelLen = 0.19f, revolver = true, hasGuard = false,
                bore = 0.0125f, muzzle = "none",
                hasStock = false, hasMag = false,
                blurb = "Hand cannon. Six chances to remove a head from its shoulders.",
            });
            Add(new WeaponDef
            {
                id = "hornet", name = "HORNET AP", cls = WeaponClass.Pistol, rarity = Rarity.Uncommon,
                damage = 62, rpm = 900, mag = 30, reserve = 210,
                reloadTime = 1.8f, emptyReloadTime = 2.2f, adsTime = 0.2f,
                recoilV = 1.5f, recoilH = 1.1f, spreadHip = 0.07f, spreadAds = 0.02f, spreadMove = 0.05f,
                wallCost = 900, wallAmmoCost = 450, sound = SoundWeight.Light, tint = 0x33383c,
                recvW = 0.05f, recvH = 0.1f, recvL = 0.24f, barrelLen = 0.15f, hasStock = false, hasGuard = false,
                magLen = 0.26f, bore = 0.0105f, compensator = true, muzzle = "brake",
                blurb = "Full-auto machine pistol. Empties fast, hits like a swarm.",
            });

            // ------------------------------------------------------- SMGs
            Add(new WeaponDef
            {
                id = "wasp9", name = "WASP-9", cls = WeaponClass.Smg, rarity = Rarity.Common,
                damage = 78, rpm = 820, mag = 32, reserve = 256,
                reloadTime = 1.9f, emptyReloadTime = 2.5f, adsTime = 0.2f,
                recoilV = 1.1f, recoilH = 0.6f, spreadHip = 0.05f, spreadAds = 0.012f, spreadMove = 0.035f,
                wallCost = 1000, wallAmmoCost = 500, boxWeight = 12, sound = SoundWeight.Light, tint = 0x2b3033,
                recvW = 0.052f, recvH = 0.115f, recvL = 0.3f, barrelLen = 0.19f, stockLen = 0.17f, magLen = 0.24f,
                magCurve = 0.22f, bore = 0.0105f, muzzle = "cage",
                blurb = "Compact 9mm. The dependable answer to the first ten rounds.",
            });
            Add(new WeaponDef
            {
                id = "ripcord", name = "RIPCORD", cls = WeaponClass.Smg, rarity = Rarity.Rare,
                damage = 92, rpm = 1000, mag = 40, reserve = 320,
                reloadTime = 2.1f, emptyReloadTime = 2.7f, adsTime = 0.22f,
                recoilV = 1.3f, recoilH = 0.9f, spreadHip = 0.055f, spreadAds = 0.014f, spreadMove = 0.04f,
                wallCost = 1300, wallAmmoCost = 600, boxWeight = 11, sound = SoundWeight.Light, accent = 0x9a6b3a,
                recvW = 0.054f, recvH = 0.12f, recvL = 0.32f, barrelLen = 0.21f, stockLen = 0.2f, magLen = 0.27f,
                magCurve = 0.26f, bore = 0.0105f, hasOptic = true, opticMag = 1.4f, muzzle = "brake",
                blurb = "Overclocked bolt group. Absurd rate of fire, appetite to match.",
            });
            Add(new WeaponDef
            {
                id = "sleet", name = "SLEET SMG", cls = WeaponClass.Smg, rarity = Rarity.Epic,
                damage = 105, rpm = 900, mag = 45, reserve = 315, penetration = 2,
                reloadTime = 2f, emptyReloadTime = 2.6f, adsTime = 0.19f,
                recoilV = 0.9f, recoilH = 0.5f, spreadHip = 0.042f, spreadAds = 0.008f, spreadMove = 0.03f,
                boxWeight = 8, tint = 0x2a3440, accent = 0x6fa8c9,
                recvW = 0.054f, recvH = 0.122f, recvL = 0.34f, barrelLen = 0.23f, stockLen = 0.2f, magLen = 0.29f,
                magCurve = 0.3f, bore = 0.011f, hasOptic = true, opticMag = 2f, muzzle = "suppressor",
                blurb = "Cryo-treated barrel shroud. Stays accurate long past reason.",
            });

            // ------------------------------------------------------- rifles
            Add(new WeaponDef
            {
                id = "kestrel", name = "KESTREL AR", cls = WeaponClass.Ar, rarity = Rarity.Uncommon,
                damage = 130, rpm = 700, mag = 30, reserve = 270, penetration = 2,
                reloadTime = 2.2f, emptyReloadTime = 2.9f, adsTime = 0.26f,
                recoilV = 1.6f, recoilH = 0.6f, spreadHip = 0.06f, spreadAds = 0.005f, spreadMove = 0.04f,
                wallCost = 1200, wallAmmoCost = 600, boxWeight = 12, tint = 0x33393c,
                recvW = 0.056f, recvH = 0.13f, recvL = 0.4f, barrelLen = 0.3f, stockLen = 0.24f,
                magLen = 0.25f, magCurve = 0.55f, bore = 0.0115f, muzzle = "cage",
                blurb = "Service rifle. Punches through a shambling queue without complaint.",
            });
            Add(new WeaponDef
            {
                id = "vulture", name = "VULTURE 7", cls = WeaponClass.Ar, rarity = Rarity.Rare,
                damage = 155, rpm = 620, mag = 35, reserve = 280, penetration = 3,
                reloadTime = 2.4f, emptyReloadTime = 3f, adsTime = 0.28f,
                recoilV = 2f, recoilH = 0.7f, spreadHip = 0.062f, spreadAds = 0.004f, spreadMove = 0.045f,
                wallCost = 1400, wallAmmoCost = 700, tint = 0x3a3a32, accent = 0x7a6f4a,
                recvW = 0.058f, recvH = 0.134f, recvL = 0.42f, barrelLen = 0.34f, stockLen = 0.24f, magLen = 0.27f,
                magCurve = 0.5f, bore = 0.0135f, hasOptic = true, opticMag = 2.5f, muzzle = "brake",
                blurb = "Heavy-calibre battle rifle. Slower, meaner, worth the recoil.",
            });
            Add(new WeaponDef
            {
                id = "talon", name = "TALON BURST", cls = WeaponClass.Ar, rarity = Rarity.Epic,
                damage = 175, rpm = 900, fireMode = FireMode.Burst, burst = 3, burstDelay = 0.24f,
                mag = 36, reserve = 288, penetration = 3,
                reloadTime = 2.3f, emptyReloadTime = 2.9f, adsTime = 0.25f,
                recoilV = 1.8f, recoilH = 0.5f, spreadHip = 0.05f, spreadAds = 0.003f, spreadMove = 0.04f,
                boxWeight = 8, tint = 0x2d3540, accent = 0x9fb0c4,
                recvW = 0.058f, recvH = 0.134f, recvL = 0.42f, barrelLen = 0.34f, stockLen = 0.26f, magLen = 0.27f,
                magCurve = 0.5f, bore = 0.012f, hasOptic = true, opticMag = 4f, muzzle = "cage",
                blurb = "Three-round burst with a locked trigger group. Surgical at range.",
            });

            // ------------------------------------------------------- shotguns
            Add(new WeaponDef
            {
                id = "breaker", name = "BREAKER 12", cls = WeaponClass.Shotgun, rarity = Rarity.Uncommon,
                damage = 60, pellets = 8, rpm = 90, fireMode = FireMode.Semi, mag = 6, reserve = 54,
                reloadTime = 0.55f, emptyReloadTime = 0.55f, shellReload = true, adsTime = 0.3f,
                range = 22f, falloff = 0.85f, recoilV = 4.5f, recoilH = 1.2f, penetration = 2,
                spreadHip = 0.11f, spreadAds = 0.075f, spreadMove = 0.13f,
                wallCost = 1200, wallAmmoCost = 600, boxWeight = 11, sound = SoundWeight.Shotgun,
                tint = 0x3a2f26, accent = 0x6a5236,
                recvW = 0.058f, recvH = 0.14f, recvL = 0.34f, barrelLen = 0.4f, stockLen = 0.28f, wood = true, bore = 0.0185f,
                hasMag = false, hasTube = true,
                blurb = "Pump-action crowd editor. Devastating inside four metres.",
            });
            Add(new WeaponDef
            {
                id = "sawtooth", name = "SAWTOOTH AA", cls = WeaponClass.Shotgun, rarity = Rarity.Epic,
                damage = 52, pellets = 10, rpm = 240, mag = 12, reserve = 84, penetration = 3,
                reloadTime = 3f, emptyReloadTime = 3.6f, adsTime = 0.32f,
                range = 26f, falloff = 0.8f, recoilV = 3.2f, recoilH = 1.4f,
                spreadHip = 0.1f, spreadAds = 0.07f, spreadMove = 0.12f,
                boxWeight = 8, sound = SoundWeight.Shotgun, tint = 0x30363a, accent = 0x8a3f2a,
                recvW = 0.058f, recvH = 0.145f, recvL = 0.38f, barrelLen = 0.34f, stockLen = 0.24f, bore = 0.0185f,
                hasDrum = true,
                blurb = "Fully automatic scattergun. Feed it points, it feeds you space.",
            });

            // ------------------------------------------------------- LMGs
            Add(new WeaponDef
            {
                id = "anvil", name = "ANVIL LMG", cls = WeaponClass.Lmg, rarity = Rarity.Rare,
                damage = 145, rpm = 640, mag = 100, reserve = 400, penetration = 4,
                reloadTime = 4.2f, emptyReloadTime = 5f, adsTime = 0.42f,
                recoilV = 2.2f, recoilH = 1f, spreadHip = 0.09f, spreadAds = 0.008f, spreadMove = 0.07f,
                moveScale = 0.88f, wallCost = 1800, wallAmmoCost = 800, boxWeight = 9, sound = SoundWeight.Heavy,
                tint = 0x2f3336, accent = 0x555b5e,
                recvW = 0.058f, recvH = 0.155f, recvL = 0.48f, barrelLen = 0.42f, stockLen = 0.26f, hasDrum = true,
                bore = 0.0145f, muzzle = "cage",
                blurb = "Hundred-round belt. The reload is a commitment; the burst is worth it.",
            });
            Add(new WeaponDef
            {
                id = "grinder", name = "GRINDER", cls = WeaponClass.Lmg, rarity = Rarity.Epic,
                damage = 165, rpm = 780, mag = 125, reserve = 500, penetration = 5,
                reloadTime = 4.6f, emptyReloadTime = 5.4f, adsTime = 0.46f,
                recoilV = 2.4f, recoilH = 1.2f, spreadHip = 0.095f, spreadAds = 0.01f, spreadMove = 0.075f,
                moveScale = 0.84f, boxWeight = 7, sound = SoundWeight.Heavy, tint = 0x35302c, accent = 0x8a6a3a,
                recvW = 0.058f, recvH = 0.16f, recvL = 0.5f, barrelLen = 0.44f, stockLen = 0.26f, hasDrum = true,
                bore = 0.0155f, muzzle = "brake",
                blurb = "Rotary-fed monster. Holds a corridor by itself.",
            });

            // ------------------------------------------------------- snipers
            Add(new WeaponDef
            {
                id = "longshot", name = "LONGSHOT", cls = WeaponClass.Sniper, rarity = Rarity.Rare,
                damage = 620, rpm = 55, fireMode = FireMode.Semi, mag = 5, reserve = 45, penetration = 6,
                reloadTime = 2.9f, emptyReloadTime = 3.5f, adsTime = 0.44f,
                headMult = 3.2f, range = 200f, falloff = 0.1f,
                recoilV = 6f, recoilH = 1f, spreadHip = 0.12f, spreadAds = 0.0008f, spreadMove = 0.1f,
                moveScale = 0.92f, wallCost = 1600, wallAmmoCost = 700, boxWeight = 9, sound = SoundWeight.Heavy,
                tint = 0x2c3130, accent = 0x6a7a68,
                recvW = 0.056f, recvH = 0.135f, recvL = 0.5f, barrelLen = 0.52f, stockLen = 0.32f, magCurve = 0.14f,
                wood = true, opticMag = 6f, muzzle = "brake",
                magLen = 0.15f, bore = 0.0125f, hasOptic = true,
                blurb = "Bolt-action anti-materiel rifle. One round, one corridor.",
            });
            Add(new WeaponDef
            {
                id = "spire", name = "SPIRE RAILGUN", cls = WeaponClass.Sniper, rarity = Rarity.Epic,
                damage = 900, rpm = 40, fireMode = FireMode.Semi, mag = 4, reserve = 32, penetration = 10,
                reloadTime = 3.2f, emptyReloadTime = 3.8f, adsTime = 0.5f,
                headMult = 3.4f, range = 240f, falloff = 0.05f,
                recoilV = 7f, recoilH = 0.6f, spreadHip = 0.14f, spreadAds = 0.0004f, spreadMove = 0.11f,
                moveScale = 0.9f, boxWeight = 6, sound = SoundWeight.Energy, tint = 0x24303a, accent = 0x66d9ff,
                recvW = 0.058f, recvH = 0.142f, recvL = 0.52f, barrelLen = 0.54f, stockLen = 0.3f, magCurve = 0.12f,
                opticMag = 8f, muzzle = "brake",
                magLen = 0.16f, bore = 0.0125f, hasOptic = true, hasCell = true,
                blurb = "Magnetically accelerated slug. Passes through everything in the hall.",
            });

            // ------------------------------------------------------- wonder
            Add(new WeaponDef
            {
                id = "arclance", name = "ARC LANCE", cls = WeaponClass.Wonder, rarity = Rarity.Wonder,
                damage = 420, rpm = 160, fireMode = FireMode.Semi, mag = 12, reserve = 60,
                reloadTime = 3.4f, emptyReloadTime = 3.9f, adsTime = 0.3f,
                headMult = 2f, range = 60f, falloff = 0.2f,
                recoilV = 2.2f, recoilH = 0.4f, spreadHip = 0.03f, spreadAds = 0.002f, spreadMove = 0.02f,
                special = SpecialEffect.Chain, chainTargets = 5, chainRadius = 6.5f, chainFalloff = 0.7f,
                boxWeight = 3, sound = SoundWeight.Energy, tint = 0x1e2a30, accent = 0x66d9ff,
                recvW = 0.058f, recvH = 0.155f, recvL = 0.44f, barrelLen = 0.36f, stockLen = 0.22f, opticMag = 1.5f,
                muzzle = "none",
                hasMag = false, hasOptic = true, hasCell = true, bore = 0.024f,
                blurb = "Prototype capacitor lance. Arcs between everything with a pulse.",
            });
            Add(new WeaponDef
            {
                id = "rotcannon", name = "BLIGHT CANNON", cls = WeaponClass.Wonder, rarity = Rarity.Wonder,
                damage = 260, rpm = 90, fireMode = FireMode.Semi, mag = 8, reserve = 40,
                reloadTime = 3.6f, emptyReloadTime = 4.2f, adsTime = 0.34f,
                headMult = 1.6f, range = 45f, falloff = 0.3f,
                recoilV = 3.4f, recoilH = 0.5f, spreadHip = 0.04f, spreadAds = 0.006f, spreadMove = 0.03f,
                special = SpecialEffect.Blast, blastRadius = 4.4f, blastDamage = 380f,
                boxWeight = 3, sound = SoundWeight.Energy, tint = 0x232a1e, accent = 0x9fd93a,
                recvW = 0.058f, recvH = 0.165f, recvL = 0.42f, barrelLen = 0.3f, stockLen = 0.22f, magCurve = 0.2f,
                muzzle = "none",
                magLen = 0.24f, hasCell = true, bore = 0.032f,
                blurb = "Fires an unstable spore charge. Do not discharge at arm's length.",
            });
        }

        public static List<WeaponDef> BoxPool()
        {
            var pool = new List<WeaponDef>();
            foreach (var w in List) if (w.boxWeight > 0) pool.Add(w);
            return pool;
        }

        public static List<WeaponDef> ByClass(WeaponClass c)
        {
            var list = new List<WeaponDef>();
            foreach (var w in List) if (w.cls == c) list.Add(w);
            list.Sort((a, b) => a.rarity == b.rarity
                ? string.Compare(a.name, b.name, System.StringComparison.Ordinal)
                : a.rarity.CompareTo(b.rarity));
            return list;
        }

        // ------------------------------------------------------------ upgrades
        public struct UpgradeTier
        {
            public int cost;
            public float damage, mag, reserve;
            public int accent;
            public UpgradeTier(int c, float d, float m, float r, int a) { cost = c; damage = d; mag = m; reserve = r; accent = a; }
        }

        public static readonly UpgradeTier[] Tiers =
        {
            new UpgradeTier(5000, 2.4f, 1.5f, 1.6f, 0x9fd93a),
            new UpgradeTier(3500, 1.6f, 1.2f, 1.25f, 0x66d9ff),
            new UpgradeTier(4500, 1.5f, 1.15f, 1.2f, 0xff8a3d),
        };

        static readonly string[] Prefix = { "", "RECLAIMED ", "ASCENDANT ", "APEX " };

        public static int UpgradeCost(int level)
        {
            return Tiers[Mathf.Clamp(level, 0, Tiers.Length - 1)].cost;
        }

        /// <summary>Derives an upgraded definition for a weapon at the given tier.</summary>
        public static WeaponDef Upgraded(WeaponDef def, int level)
        {
            if (level <= 0) return def;
            var o = def.Clone();
            float dmg = 1f, mag = 1f, res = 1f;
            int accent = def.accent;
            for (int i = 0; i < level && i < Tiers.Length; i++)
            {
                dmg *= Tiers[i].damage;
                mag *= Tiers[i].mag;
                res *= Tiers[i].reserve;
                accent = Tiers[i].accent;
            }
            o.damage = Mathf.Round(def.damage * dmg);
            o.mag = Mathf.RoundToInt(def.mag * mag);
            o.reserve = Mathf.RoundToInt(def.reserve * res);
            o.recoilV = def.recoilV * 0.82f;
            o.recoilH = def.recoilH * 0.82f;
            o.reloadTime = def.reloadTime * 0.88f;
            o.emptyReloadTime = def.emptyReloadTime * 0.88f;
            o.penetration = def.penetration + level;
            o.accent = accent;
            o.upgradeLevel = level;
            o.name = Prefix[Mathf.Min(level, 3)] + def.name;
            o.hasCell = true;
            // Refits add an area effect once the weapon is properly worked over.
            if (level >= 2 && def.special == SpecialEffect.None) o.special = SpecialEffect.Burn;
            if (level >= 3 && o.special == SpecialEffect.Burn)
            {
                o.special = SpecialEffect.Blast;
                o.blastRadius = 3.0f;
                o.blastDamage = o.damage * 0.6f;
            }
            return o;
        }
    }
}
