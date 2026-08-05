using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>A wall-mounted weapon: buy it once, then buy ammo for it.</summary>
    public class WallBuy : Interactable
    {
        public readonly WeaponDef def;
        readonly MachineInfo _info;

        public WallBuy(WallBuyDef spot, Transform parent)
            : base(spot.id, new Vector3(spot.x, spot.y + 1.2f, spot.z), 2.5f, 1)
        {
            def = Weapons.All[spot.weapon];
            var go = Props.WallBuy(def, parent);
            go.transform.position = new Vector3(spot.x, spot.y + 1.5f, spot.z);
            go.transform.rotation = Quaternion.Euler(0, spot.rot, 0);
            _info = go.GetComponent<MachineInfo>();
        }

        public override Prompt GetPrompt(GameRun run)
        {
            if (run.weapons.Owns(def.id))
            {
                if (run.weapons.AmmoFull(def.id)) return Prompt.Blocked(def.name, "AMMO FULL");
                return Prompt.Buy(def.name + " AMMO", def.wallAmmoCost, "");
            }
            if (def.wallCost <= 0) return Prompt.Blocked(def.name, "STANDARD ISSUE");
            return Prompt.Buy("BUY " + def.name, def.wallCost, def.ClassLabel);
        }

        public override bool Activate(GameRun run)
        {
            if (run.weapons.Owns(def.id))
            {
                if (run.weapons.AmmoFull(def.id)) return false;
                if (!run.player.Spend(def.wallAmmoCost)) { run.Deny(); return false; }
                run.weapons.RefillWeapon(def.id);
                Game.Audio.Play(Sfx.UiSelect, 1f);
                run.Toast("AMMO PURCHASED", ToastKind.Good, false);
                return true;
            }
            if (def.wallCost <= 0) return false;
            if (!run.player.Spend(def.wallCost)) { run.Deny(); return false; }
            run.weapons.GiveWeapon(def);
            Game.Audio.Play(Sfx.UiSelect, 1f);
            return true;
        }

        public void Tick(float time)
        {
            if (_info != null && _info.spinTarget != null)
            {
                var p = _info.spinTarget.localPosition;
                _info.spinTarget.localPosition = new Vector3(p.x, 0.09f + Mathf.Sin(time * 1.6f + pos.x) * 0.02f, p.z);
            }
        }
    }

    /// <summary>Armour plating dispenser.</summary>
    public class ArmorStation : Interactable
    {
        readonly int _cost;

        public ArmorStation(ArmorDef spot, Transform parent)
            : base(spot.id, new Vector3(spot.x, spot.y + 1.1f, spot.z), 2.3f, 1)
        {
            _cost = spot.cost;
            var g = new GameObject("armor_" + spot.id);
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(spot.x, spot.y, spot.z);
            g.transform.rotation = Quaternion.Euler(0, spot.rot, 0);
            Props.Box(1.2f, 1.8f, 0.35f, Props.DarkMetal, new Vector3(0, 0.9f, 0), g.transform);
            Props.Box(1.0f, 0.28f, 0.16f, Art.Emissive("em_armor", Util.Hex(0x4aa3ff), 1.6f),
                new Vector3(0, 1.5f, 0.2f), g.transform);
            for (int i = 0; i < 3; i++)
                Props.Box(0.72f, 0.1f, 0.3f, Props.Metal, new Vector3(0, 0.45f + i * 0.32f, 0.14f), g.transform);
            var sign = Props.Quad(1.1f, 0.4f, Art.Emissive("sign_armor", Color.white, 1.2f,
                PixelFont.Sign("PLATING", spot.cost.ToString(), Util.Hex(0x4aa3ff), Util.Hex(0x080b0d), 256, 96)),
                new Vector3(0, 1.85f, 0.2f), g.transform);
            sign.transform.localRotation = Quaternion.Euler(0, 180f, 0);
        }

        public override Prompt GetPrompt(GameRun run)
        {
            if (run.player.armor >= 100f) return Prompt.Blocked("ARMOUR PLATING", "PLATES FULL");
            return Prompt.Buy("ARMOUR PLATING", _cost, "ABSORBS 65% OF DAMAGE");
        }

        public override bool Activate(GameRun run)
        {
            if (run.player.armor >= 100f) return false;
            if (!run.player.Spend(_cost)) { run.Deny(); return false; }
            run.player.AddArmor(50f);
            Game.Audio.Play(Sfx.UiSelect, 1f);
            run.Toast("PLATES APPLIED", ToastKind.Good, false);
            return true;
        }
    }

    public class ShopSystem
    {
        readonly List<WallBuy> _walls = new List<WallBuy>();

        public ShopSystem(InteractionSystem interaction, Transform parent)
        {
            foreach (var spot in MapData.WallBuys)
            {
                var w = new WallBuy(spot, parent);
                interaction.Add(w);
                _walls.Add(w);
            }
            foreach (var spot in MapData.ArmorStations)
                interaction.Add(new ArmorStation(spot, parent));
        }

        public void Update(float time)
        {
            foreach (var w in _walls) w.Tick(time);
        }
    }
}
