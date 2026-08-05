using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Perk definitions. Names and effects are original; the shape of the
    /// system — buy a bottle from a machine, keep it until you go down —
    /// follows the round-based survival template.
    /// </summary>
    public class PerkDef
    {
        public string id, name, shortName, letter, effect;
        public int cost;
        public int color;
        public Color Color { get { return Util.Hex(color); } }
    }

    public static class Perks
    {
        public static readonly List<PerkDef> All = new List<PerkDef>
        {
            new PerkDef { id="vital", name="VITAL SURGE", shortName="VITAL", letter="V", cost=2500, color=0xc0392b,
                          effect="Raises maximum health from 100 to 175." },
            new PerkDef { id="rapid", name="QUICK HANDS", shortName="HANDS", letter="Q", cost=3000, color=0xe8a33d,
                          effect="Reloads roughly twice as fast." },
            new PerkDef { id="swift", name="LIGHT STEP", shortName="STEP", letter="L", cost=2000, color=0x4aa3ff,
                          effect="Move 13% faster and swap weapons quicker." },
            new PerkDef { id="medic", name="SECOND WIND", shortName="WIND", letter="S", cost=1500, color=0x9fd93a,
                          effect="Longer bleed-out and one self-revive per life." },
            new PerkDef { id="endure", name="IRON LUNG", shortName="LUNG", letter="I", cost=2000, color=0xb479ff,
                          effect="Sprint roughly twice as long and recover faster." },
        };

        public static PerkDef Get(string id)
        {
            foreach (var p in All) if (p.id == id) return p;
            return All[0];
        }
    }

    /// <summary>A single perk dispenser in the world.</summary>
    public class PerkMachine : Interactable
    {
        public readonly PerkDef perk;
        readonly PerkSystem _system;
        readonly MachineInfo _info;
        float _dispenseT;

        public PerkMachine(PerkSpotDef spot, PerkSystem system, Transform parent)
            : base("perk_" + spot.perk, new Vector3(spot.x, spot.y + 1.1f, spot.z), 2.4f, 1)
        {
            perk = Perks.Get(spot.perk);
            _system = system;
            var go = Props.PerkMachine(perk, parent);
            go.transform.position = new Vector3(spot.x, spot.y, spot.z);
            go.transform.rotation = Quaternion.Euler(0, spot.rot, 0);
            _info = go.GetComponent<MachineInfo>();
        }

        public Vector3 ColliderSize { get { return _info.colliderSize; } }
        bool Powered { get { return _system.power.On; } }

        public override Prompt GetPrompt(GameRun run)
        {
            if (!Powered) return Prompt.Blocked(perk.name, "NO POWER");
            if (run.player.HasPerk(perk.id)) return Prompt.Blocked(perk.name, "ALREADY ACTIVE");
            return Prompt.Buy("BUY " + perk.name, perk.cost, perk.effect);
        }

        public override bool Activate(GameRun run)
        {
            if (!Powered || run.player.HasPerk(perk.id)) return false;
            if (!run.player.Spend(perk.cost)) { run.Deny(); return false; }
            run.player.GivePerk(perk);
            Game.Audio.Play(Sfx.Perk, 1f);
            _dispenseT = 1.6f;
            run.Toast(perk.name + " ACQUIRED", ToastKind.Good, false);
            return true;
        }

        public void Tick(float dt, float time)
        {
            bool on = Powered;
            if (_info.glow != null)
            {
                foreach (var g in _info.glow)
                {
                    if (g == null) continue;
                    float k = on ? 1.4f + Mathf.Sin(time * 3f + pos.x) * 0.5f : 0.04f;
                    Art.SetEmission(g.material, perk.Color * k);
                }
            }
            if (_dispenseT > 0f)
            {
                _dispenseT -= dt;
                if (_info.bottle != null)
                {
                    _info.bottle.SetActive(true);
                    var p = _info.bottle.transform.localPosition;
                    _info.bottle.transform.localPosition = new Vector3(p.x, 0.66f + Mathf.Sin(_dispenseT * 4f) * 0.03f, p.z);
                }
                if (_dispenseT <= 0f && _info.bottle != null) _info.bottle.SetActive(false);
            }
        }
    }

    public class PerkSystem
    {
        public readonly PowerSystem power;
        public readonly List<PerkMachine> machines = new List<PerkMachine>();

        public PerkSystem(WorldBuilder world, PowerSystem powerSystem, InteractionSystem interaction, Transform parent)
        {
            power = powerSystem;
            foreach (var spot in MapData.PerkSpots)
            {
                var m = new PerkMachine(spot, this, parent);
                interaction.Add(m);
                machines.Add(m);
                var c = m.ColliderSize;
                world.AddObstacle(spot.x, spot.z, c.x, c.z, spot.y, c.y, false);
            }
        }

        public void Update(float dt, float time)
        {
            foreach (var m in machines) m.Tick(dt, time);
        }
    }
}
