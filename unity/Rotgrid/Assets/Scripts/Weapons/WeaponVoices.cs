using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Per-weapon gunshot voices.
    ///
    /// Every sound in this game is synthesised into a PCM buffer at runtime —
    /// nothing is sampled or imported. A shot is built from five layers, and
    /// giving each weapon its own numbers is what stops seventeen guns from
    /// sounding like one gun at three volumes:
    ///
    ///   mech   the action working — sear, bolt, slide. A short bright transient.
    ///   crack  the supersonic snap of the bullet. High, fast, sweeps downward.
    ///   body   the pressure pulse. A pitched thump that gives the shot weight.
    ///   ring   a resonant peak: barrel harmonic, receiver clang, capacitor whine.
    ///   tail   room reflections. Long in a concrete hall, short under a can.
    /// </summary>
    public struct WeaponVoice
    {
        public float mech, mechF, mechDur;
        public float crackHi, crackLo, crackDur, crackQ, crackLevel;
        public float body, bodyMul, bodyDur, punch;
        public int bodyShape;                 // 0 triangle, 1 sine, 2 saw, 3 square
        public float ring, ringF, ringQ, ringDur;
        public float tail, tailDur, tailF;
        public float chargeFrom, chargeTo, chargeDur, chargeLevel;

        public static WeaponVoice Default
        {
            get
            {
                return new WeaponVoice
                {
                    mech = 0.22f, mechF = 3200f, mechDur = 0.035f,
                    crackHi = 3000f, crackLo = 180f, crackDur = 0.2f, crackQ = 0.9f, crackLevel = 0.9f,
                    body = 110f, bodyMul = 2.2f, bodyDur = 0.18f, punch = 0.8f, bodyShape = 0,
                    ring = 0f, ringF = 900f, ringQ = 9f, ringDur = 0.14f,
                    tail = 0.45f, tailDur = 0.3f, tailF = 700f,
                };
            }
        }

        /// <summary>Longest layer, so the baked clip is exactly long enough.</summary>
        public float Length
        {
            get
            {
                float m = Mathf.Max(crackDur, bodyDur);
                m = Mathf.Max(m, ringDur);
                m = Mathf.Max(m, tailDur);
                return Mathf.Max(0.08f, m) + 0.02f;
            }
        }
    }

    public static class WeaponVoices
    {
        static Dictionary<string, WeaponVoice> _table;

        /// <summary>
        /// Voice for a weapon. Refitted weapons keep their identity but gain a
        /// brighter resonant edge, so an upgrade is audible as well as visible.
        /// </summary>
        public static WeaponVoice For(WeaponDef def)
        {
            if (_table == null) Build();
            WeaponVoice v;
            if (!_table.TryGetValue(def.id, out v)) v = FallbackFor(def.sound);
            int lvl = def.upgradeLevel;
            if (lvl > 0)
            {
                v.ring = Mathf.Min(0.7f, (v.ring > 0f ? v.ring : 0.15f) + 0.12f * lvl);
                v.ringF *= 1f + 0.12f * lvl;
                v.punch *= 1f + 0.1f * lvl;
                v.tail = Mathf.Min(1.2f, v.tail * (1f + 0.12f * lvl));
                v.tailDur *= 1f + 0.15f * lvl;
            }
            return v;
        }

        static WeaponVoice FallbackFor(SoundWeight w)
        {
            var v = WeaponVoice.Default;
            switch (w)
            {
                case SoundWeight.Light:
                    v.crackHi = 3600f; v.crackLo = 240f; v.crackDur = 0.14f;
                    v.body = 150f; v.punch = 0.55f; v.tailDur = 0.2f; break;
                case SoundWeight.Heavy:
                    v.crackHi = 2400f; v.crackLo = 120f; v.crackDur = 0.3f;
                    v.body = 78f; v.punch = 1.1f; v.tailDur = 0.5f; break;
                case SoundWeight.Shotgun:
                    v.crackHi = 2000f; v.crackLo = 90f; v.crackDur = 0.4f;
                    v.body = 62f; v.punch = 1.3f; v.tailDur = 0.6f; break;
                case SoundWeight.Energy:
                    v.crackHi = 5200f; v.crackLo = 400f; v.crackDur = 0.3f;
                    v.body = 220f; v.punch = 0.9f; v.tailDur = 0.5f; v.bodyShape = 2; break;
            }
            return v;
        }

        static void Add(string id, WeaponVoice v) { _table[id] = v; }

        static void Build()
        {
            _table = new Dictionary<string, WeaponVoice>();
            WeaponVoice v;

            // ------------------------------------------------------- pistols
            v = WeaponVoice.Default;
            v.mech = 0.34f; v.mechF = 4200f; v.crackHi = 4200f; v.crackLo = 300f;
            v.crackDur = 0.13f; v.crackQ = 1.2f; v.body = 165f; v.bodyDur = 0.1f;
            v.punch = 0.5f; v.ring = 0.16f; v.ringF = 1750f; v.tail = 0.32f; v.tailDur = 0.2f;
            Add("sidearm", v);

            // Big revolver: no slide noise, enormous body, long bare-muzzle tail.
            v = WeaponVoice.Default;
            v.mech = 0.1f; v.mechF = 2400f; v.crackHi = 3400f; v.crackLo = 130f;
            v.crackDur = 0.3f; v.crackQ = 0.7f; v.crackLevel = 1.15f;
            v.body = 74f; v.bodyMul = 3.0f; v.bodyDur = 0.34f; v.punch = 1.45f;
            v.ring = 0.22f; v.ringF = 520f; v.ringQ = 6f;
            v.tail = 0.85f; v.tailDur = 0.72f; v.tailF = 420f;
            Add("magnus", v);

            // Machine pistol: fast, tinny, almost no low end.
            v = WeaponVoice.Default;
            v.mech = 0.4f; v.mechF = 5200f; v.mechDur = 0.025f;
            v.crackHi = 5000f; v.crackLo = 420f; v.crackDur = 0.09f; v.crackQ = 1.6f;
            v.body = 210f; v.bodyDur = 0.07f; v.punch = 0.36f;
            v.ring = 0.24f; v.ringF = 2400f; v.tail = 0.24f; v.tailDur = 0.14f;
            Add("hornet", v);

            // ------------------------------------------------------- SMGs
            v = WeaponVoice.Default;
            v.mech = 0.38f; v.mechF = 4600f; v.mechDur = 0.028f;
            v.crackHi = 4400f; v.crackLo = 340f; v.crackDur = 0.1f; v.crackQ = 1.4f;
            v.body = 180f; v.bodyDur = 0.08f; v.punch = 0.44f;
            v.ring = 0.18f; v.ringF = 2050f; v.tail = 0.3f; v.tailDur = 0.17f;
            Add("wasp9", v);

            // Overclocked bolt: shorter, sharper, with a mechanical rattle on top.
            v = WeaponVoice.Default;
            v.mech = 0.52f; v.mechF = 5600f; v.mechDur = 0.022f;
            v.crackHi = 5200f; v.crackLo = 400f; v.crackDur = 0.075f; v.crackQ = 1.9f;
            v.body = 196f; v.bodyDur = 0.06f; v.punch = 0.4f;
            v.ring = 0.3f; v.ringF = 2750f; v.ringDur = 0.09f;
            v.tail = 0.26f; v.tailDur = 0.13f;
            Add("ripcord", v);

            // Suppressed: the crack is choked and the tail is nearly gone.
            v = WeaponVoice.Default;
            v.mech = 0.46f; v.mechF = 3400f;
            v.crackHi = 2600f; v.crackLo = 300f; v.crackDur = 0.11f; v.crackQ = 3.4f; v.crackLevel = 0.62f;
            v.body = 150f; v.bodyDur = 0.1f; v.punch = 0.5f;
            v.ring = 0.12f; v.ringF = 1200f; v.tail = 0.12f; v.tailDur = 0.1f; v.tailF = 400f;
            Add("sleet", v);

            // ------------------------------------------------------- rifles
            v = WeaponVoice.Default;
            v.mech = 0.28f; v.mechF = 3800f; v.crackHi = 3800f; v.crackLo = 220f;
            v.crackDur = 0.16f; v.crackQ = 1.1f; v.body = 128f; v.bodyDur = 0.14f;
            v.punch = 0.82f; v.ring = 0.2f; v.ringF = 1500f; v.tail = 0.5f; v.tailDur = 0.34f;
            Add("kestrel", v);

            // Heavier calibre: lower everything, more shove.
            v = WeaponVoice.Default;
            v.mech = 0.24f; v.mechF = 3200f; v.crackHi = 3200f; v.crackLo = 170f;
            v.crackDur = 0.21f; v.crackQ = 0.95f;
            v.body = 96f; v.bodyMul = 2.6f; v.bodyDur = 0.2f; v.punch = 1.1f;
            v.ring = 0.24f; v.ringF = 1050f; v.ringQ = 7f;
            v.tail = 0.62f; v.tailDur = 0.46f; v.tailF = 560f;
            Add("vulture", v);

            // Locked burst group: very tight, metallic, almost mechanical.
            v = WeaponVoice.Default;
            v.mech = 0.44f; v.mechF = 4800f; v.mechDur = 0.02f;
            v.crackHi = 4600f; v.crackLo = 260f; v.crackDur = 0.12f; v.crackQ = 1.8f;
            v.body = 140f; v.bodyDur = 0.1f; v.punch = 0.7f;
            v.ring = 0.34f; v.ringF = 2200f; v.ringQ = 12f; v.ringDur = 0.11f;
            v.tail = 0.42f; v.tailDur = 0.26f;
            Add("talon", v);

            // ------------------------------------------------------- shotguns
            // Pump gun: a wide, low boom with a wall of tail behind it.
            v = WeaponVoice.Default;
            v.mech = 0.3f; v.mechF = 2200f; v.mechDur = 0.06f;
            v.crackHi = 2400f; v.crackLo = 90f; v.crackDur = 0.36f; v.crackQ = 0.5f; v.crackLevel = 1.2f;
            v.body = 62f; v.bodyMul = 3.2f; v.bodyDur = 0.3f; v.punch = 1.35f; v.bodyShape = 1;
            v.ring = 0.1f; v.ringF = 400f; v.tail = 0.9f; v.tailDur = 0.68f; v.tailF = 320f;
            Add("breaker", v);

            v = WeaponVoice.Default;
            v.mech = 0.36f; v.mechF = 3000f;
            v.crackHi = 2800f; v.crackLo = 110f; v.crackDur = 0.26f; v.crackQ = 0.6f; v.crackLevel = 1.05f;
            v.body = 74f; v.bodyMul = 2.8f; v.bodyDur = 0.22f; v.punch = 1.1f;
            v.ring = 0.18f; v.ringF = 620f; v.tail = 0.7f; v.tailDur = 0.48f; v.tailF = 380f;
            Add("sawtooth", v);

            // ------------------------------------------------------- LMGs
            // Belt-fed: heavy body, audible link ejection, cavernous tail.
            v = WeaponVoice.Default;
            v.mech = 0.42f; v.mechF = 2800f; v.mechDur = 0.05f;
            v.crackHi = 3000f; v.crackLo = 150f; v.crackDur = 0.2f; v.crackQ = 0.85f;
            v.body = 88f; v.bodyMul = 2.7f; v.bodyDur = 0.19f; v.punch = 1.15f;
            v.ring = 0.26f; v.ringF = 860f; v.ringQ = 6f;
            v.tail = 0.7f; v.tailDur = 0.52f; v.tailF = 500f;
            Add("anvil", v);

            v = WeaponVoice.Default;
            v.mech = 0.5f; v.mechF = 3100f; v.mechDur = 0.045f;
            v.crackHi = 3300f; v.crackLo = 160f; v.crackDur = 0.17f; v.crackQ = 0.95f;
            v.body = 100f; v.bodyMul = 2.5f; v.bodyDur = 0.16f; v.punch = 1.05f;
            v.ring = 0.32f; v.ringF = 1150f; v.ringQ = 8f;
            v.tail = 0.66f; v.tailDur = 0.44f;
            Add("grinder", v);

            // ------------------------------------------------------- snipers
            // Anti-materiel rifle: brutal crack, brake slap, very long tail.
            v = WeaponVoice.Default;
            v.mech = 0.2f; v.mechF = 2600f; v.mechDur = 0.07f;
            v.crackHi = 5400f; v.crackLo = 120f; v.crackDur = 0.28f; v.crackQ = 0.6f; v.crackLevel = 1.3f;
            v.body = 66f; v.bodyMul = 3.4f; v.bodyDur = 0.3f; v.punch = 1.6f;
            v.ring = 0.3f; v.ringF = 700f; v.ringQ = 5f; v.ringDur = 0.24f;
            v.tail = 1.0f; v.tailDur = 0.95f; v.tailF = 460f;
            Add("longshot", v);

            // Railgun: a rising capacitor whine, then a hard electromagnetic snap.
            v = WeaponVoice.Default;
            v.mech = 0.16f; v.mechF = 6200f; v.mechDur = 0.09f;
            v.crackHi = 6400f; v.crackLo = 260f; v.crackDur = 0.22f; v.crackQ = 1.5f;
            v.body = 130f; v.bodyMul = 4.2f; v.bodyDur = 0.26f; v.bodyShape = 2; v.punch = 1.3f;
            v.ring = 0.5f; v.ringF = 3200f; v.ringQ = 16f; v.ringDur = 0.4f;
            v.tail = 0.8f; v.tailDur = 0.66f; v.tailF = 1400f;
            v.chargeFrom = 240f; v.chargeTo = 2600f; v.chargeDur = 0.1f; v.chargeLevel = 0.3f;
            Add("spire", v);

            // ------------------------------------------------------- wonder
            // Capacitor discharge: almost no crack, a fat buzzing body, singing tail.
            v = WeaponVoice.Default;
            v.mech = 0.12f; v.mechF = 5000f;
            v.crackHi = 5600f; v.crackLo = 900f; v.crackDur = 0.13f; v.crackQ = 2.6f; v.crackLevel = 0.5f;
            v.body = 190f; v.bodyMul = 3.6f; v.bodyDur = 0.3f; v.bodyShape = 2; v.punch = 1.0f;
            v.ring = 0.55f; v.ringF = 2400f; v.ringQ = 18f; v.ringDur = 0.5f;
            v.tail = 0.75f; v.tailDur = 0.6f; v.tailF = 1800f;
            v.chargeFrom = 400f; v.chargeTo = 1900f; v.chargeDur = 0.07f; v.chargeLevel = 0.26f;
            Add("arclance", v);

            // Spore charge: wet, low and unpleasant.
            v = WeaponVoice.Default;
            v.mech = 0.2f; v.mechF = 1800f; v.mechDur = 0.06f;
            v.crackHi = 1600f; v.crackLo = 70f; v.crackDur = 0.34f; v.crackQ = 0.45f; v.crackLevel = 0.95f;
            v.body = 54f; v.bodyMul = 3.8f; v.bodyDur = 0.38f; v.bodyShape = 3; v.punch = 1.3f;
            v.ring = 0.4f; v.ringF = 300f; v.ringQ = 4f; v.ringDur = 0.42f;
            v.tail = 0.85f; v.tailDur = 0.8f; v.tailF = 260f;
            Add("rotcannon", v);
        }
    }
}
