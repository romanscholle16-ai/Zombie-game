using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Weapon refit station — the Pack-a-Punch equivalent, three tiers deep.
    /// Insert a weapon, wait for the machine, collect the improved version.
    /// </summary>
    public class UpgradeMachine : Interactable
    {
        enum State { Idle, Working, Ready }

        const float WorkTime = 3.6f;
        const float HoldTime = 12f;

        readonly PowerSystem _power;
        readonly GameObject _root;
        readonly MachineInfo _info;
        readonly GameObject _displayRoot;
        GameObject _displayModel;

        State _state = State.Idle;
        float _t;
        Weapon _working;

        public UpgradeMachine(WorldBuilder world, PowerSystem power, InteractionSystem interaction, Transform parent)
            : base("pap", MapData.UpgradeMachinePos + new Vector3(0, 1.2f, 1.6f), 3f, 2)
        {
            _power = power;
            _root = Props.UpgradeMachine(parent);
            _root.transform.position = MapData.UpgradeMachinePos;
            _root.transform.rotation = Quaternion.Euler(0, MapData.UpgradeMachineRot, 0);
            _info = _root.GetComponent<MachineInfo>();

            var c = _info.colliderSize;
            world.AddObstacle(MapData.UpgradeMachinePos.x, MapData.UpgradeMachinePos.z, c.x, c.z,
                MapData.UpgradeMachinePos.y, c.y, false);

            _displayRoot = new GameObject("papDisplay");
            _displayRoot.transform.SetParent(_root.transform, false);
            _displayRoot.transform.localPosition = new Vector3(0, 1.35f, 1.5f);
            _displayRoot.SetActive(false);

            interaction.Add(this);
        }

        public override Prompt GetPrompt(GameRun run)
        {
            if (!_power.On) return Prompt.Blocked("REFIT STATION", "NO POWER");
            if (_state == State.Working) return Prompt.Blocked("REFITTING", "");
            if (_state == State.Ready) return Prompt.Plain("COLLECT " + _working.Name, "");
            var w = run.weapons.Current;
            if (w == null) return Prompt.Blocked("REFIT STATION", "");
            if (w.upgradeLevel >= Weapons.Tiers.Length) return Prompt.Blocked("REFIT STATION", "WEAPON AT MAXIMUM");
            return Prompt.Buy("REFIT " + w.Name, w.NextUpgradeCost,
                "TIER " + (w.upgradeLevel + 1) + " OF " + Weapons.Tiers.Length);
        }

        public override bool Activate(GameRun run)
        {
            if (!_power.On) return false;
            if (_state == State.Working) return false;
            if (_state == State.Ready)
            {
                run.weapons.ReturnUpgraded(_working);
                _working = null;
                _state = State.Idle;
                _displayRoot.SetActive(false);
                return true;
            }
            var w = run.weapons.Current;
            if (w == null || w.upgradeLevel >= Weapons.Tiers.Length) return false;
            if (!run.player.Spend(w.NextUpgradeCost)) { run.Deny(); return false; }
            _working = run.weapons.TakeForUpgrade();
            _working.Upgrade();
            _state = State.Working;
            _t = WorkTime;
            Game.Audio.PlayAt(Sfx.Pap, _root.transform.position, 1f);
            run.Toast("REFIT IN PROGRESS", ToastKind.Warn, false);
            return true;
        }

        void SetDisplay(WeaponDef def)
        {
            if (_displayModel != null) Object.Destroy(_displayModel);
            _displayModel = Props.WeaponModel(def, 1.2f, _displayRoot.transform);
            _displayRoot.SetActive(true);
        }

        public void Update(float dt, float time, GameRun run)
        {
            bool on = _power.On;
            if (_info.glow != null)
            {
                foreach (var g in _info.glow)
                {
                    if (g == null) continue;
                    Art.SetEmission(g.material, Util.Hex(0xff8a3d) * (on ? 1.8f + Mathf.Sin(time * 4f) * 0.7f : 0.05f));
                }
            }
            if (_info.light != null) _info.light.intensity = on ? 46f + Mathf.Sin(time * 5f) * 14f : 0f;
            if (_info.pivot != null) _info.pivot.Rotate(0, 0, dt * 80f);

            if (_state == State.Working)
            {
                _t -= dt;
                if (_info.light != null) _info.light.intensity = 70f + Mathf.Sin(time * 30f) * 45f;
                if (_t <= 0f)
                {
                    _state = State.Ready;
                    _t = HoldTime;
                    SetDisplay(_working.def);
                    Game.Audio.PlayAt(Sfx.BoxStopRare, _root.transform.position, 1f);
                    if (run != null) run.Toast("REFIT COMPLETE", ToastKind.Good, false);
                }
            }
            else if (_state == State.Ready)
            {
                _t -= dt;
                if (_displayModel != null)
                {
                    _displayModel.transform.localRotation = Quaternion.Euler(0, time * 100f, 0);
                    _displayModel.transform.localPosition = new Vector3(0, Mathf.Sin(time * 3f) * 0.06f, 0);
                }
                if (_t <= 0f)
                {
                    // Nobody collected it — hand it back rather than deleting a weapon.
                    if (run != null) run.weapons.ReturnUpgraded(_working);
                    _working = null;
                    _state = State.Idle;
                    _displayRoot.SetActive(false);
                }
            }
        }
    }
}
