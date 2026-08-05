using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// The facility starts dark. Throwing the breaker brings up the lights, the
    /// perk machines, the refit chamber and the traps.
    /// </summary>
    public class PowerSystem : Interactable
    {
        readonly WorldBuilder _world;
        readonly MachineInfo _info;
        public bool On { get; private set; }
        public float ActivatingT;

        public PowerSystem(WorldBuilder world, InteractionSystem interaction, Transform parent)
            : base("power", MapData.PowerSwitchPos + Vector3.up * 1.2f, 2.8f, 2)
        {
            _world = world;
            var go = Props.PowerSwitch(parent);
            go.transform.position = MapData.PowerSwitchPos;
            go.transform.rotation = Quaternion.Euler(0, MapData.PowerSwitchRot, 0);
            _info = go.GetComponent<MachineInfo>();
            world.AddObstacle(MapData.PowerSwitchPos.x, MapData.PowerSwitchPos.z, 1.2f, 0.7f,
                MapData.PowerSwitchPos.y, 2.3f, false);
            interaction.Add(this);
        }

        public override Prompt GetPrompt(GameRun run)
        {
            return On
                ? Prompt.Blocked("MAIN BREAKER", "GRID ONLINE")
                : Prompt.Plain("ACTIVATE POWER", "RESTORES THE FACILITY GRID");
        }

        public override bool Activate(GameRun run)
        {
            if (On) return false;
            On = true;
            ActivatingT = 2.6f;
            _world.SetPowered(true);
            if (_info.pivot != null) _info.pivot.localRotation = Quaternion.Euler(-52f, 0, 0);
            if (_info.glow != null && _info.glow.Length > 0 && _info.glow[0] != null)
                Art.SetEmission(_info.glow[0].material, Util.Hex(0x9fd93a) * 2.2f);
            Game.Audio.Play(Sfx.Power, 1f);
            if (run != null)
            {
                run.Toast("POWER RESTORED", ToastKind.Good, true);
                run.player.AddShake(0.5f);
            }
            return true;
        }

        public void Update(float dt)
        {
            if (ActivatingT > 0f) ActivatingT -= dt;
        }
    }
}
