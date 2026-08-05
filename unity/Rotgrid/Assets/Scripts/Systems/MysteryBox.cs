using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// The mystery crate. Spins for a rarity-weighted weapon, then occasionally
    /// packs up and relocates so its position is never a given.
    /// </summary>
    public class MysteryBox : Interactable
    {
        enum State { Idle, Spin, Reveal, Leaving, Hidden }

        const float SpinTime = 2.9f;
        const float GrabTime = 9f;
        const int BaseCost = 950;

        readonly WorldBuilder _world;
        readonly Transform _parent;
        readonly GameObject _root;
        readonly MachineInfo _info;
        readonly List<WeaponDef> _pool;
        readonly GameObject _displayRoot;
        GameObject _displayModel;

        State _state = State.Idle;
        float _t, _cycleT, _fireSaleT;
        int _usesLeft;
        int _spotIndex;
        public WeaponDef pending;

        public MysteryBox(WorldBuilder world, InteractionSystem interaction, Transform parent)
            : base("box", Vector3.zero, 2.6f, 2)
        {
            _world = world;
            _parent = parent;
            _pool = Weapons.BoxPool();
            _usesLeft = Util.RandInt(4, 8);

            _root = Props.MysteryBox(parent);
            _info = _root.GetComponent<MachineInfo>();
            _displayRoot = new GameObject("display");
            _displayRoot.transform.SetParent(_root.transform, false);
            _displayRoot.transform.localPosition = new Vector3(0, 1.55f, 0);
            _displayRoot.SetActive(false);

            interaction.Add(this);
            MoveTo(0, true, null);
        }

        public int Cost { get { return _fireSaleT > 0f ? 10 : BaseCost; } }
        BoxSpotDef Spot { get { return MapData.BoxSpots[_spotIndex]; } }

        public void StartFireSale(float seconds) { _fireSaleT = seconds; }

        void MoveTo(int index, bool immediate, GameRun run)
        {
            _spotIndex = index;
            var s = Spot;
            _root.transform.position = new Vector3(s.x, s.y, s.z);
            _root.transform.rotation = Quaternion.Euler(0, s.rot, 0);
            _root.transform.localScale = Vector3.one;
            _root.SetActive(true);
            pos = new Vector3(s.x, s.y + 0.6f, s.z);
            _usesLeft = Util.RandInt(4, 8);
            _state = State.Idle;
            if (!immediate)
            {
                Game.Audio.PlayAt(Sfx.BoxStop, new Vector3(s.x, s.y + 1f, s.z), 1f);
                if (run != null) run.Toast("THE CRATE HAS MOVED", ToastKind.Warn, false);
            }
        }

        void Relocate(GameRun run)
        {
            var options = new List<int>();
            for (int i = 0; i < MapData.BoxSpots.Count; i++)
                if (i != _spotIndex && _world.IsZoneActive(MapData.BoxSpots[i].zone)) options.Add(i);
            MoveTo(options.Count > 0 ? options[Random.Range(0, options.Count)] : _spotIndex, false, run);
        }

        public override Prompt GetPrompt(GameRun run)
        {
            if (_state == State.Spin) return Prompt.Blocked("CRATE CYCLING", "");
            if (_state == State.Reveal) return Prompt.Plain("TAKE " + pending.name, pending.RarityLabel);
            if (_state != State.Idle) return Prompt.Blocked("CRATE SEALED", "");
            return Prompt.Buy("MYSTERY CRATE", Cost,
                _fireSaleT > 0f ? "FIRE SALE" : _usesLeft + " CYCLES LEFT");
        }

        public override bool Activate(GameRun run)
        {
            if (_state == State.Reveal)
            {
                run.weapons.GiveWeapon(pending);
                pending = null;
                _displayRoot.SetActive(false);
                if (_usesLeft <= 0) { _state = State.Leaving; _t = 2f; }
                else { _state = State.Idle; _t = 0f; }
                return true;
            }
            if (_state != State.Idle) return false;
            if (!run.player.Spend(Cost)) { run.Deny(); return false; }
            run.player.stats.boxSpins++;
            _state = State.Spin;
            _t = SpinTime;
            _cycleT = 0f;
            _usesLeft--;
            _displayRoot.SetActive(true);
            Game.Audio.PlayAt(Sfx.BoxSpin, _root.transform.position, 1f);
            return true;
        }

        WeaponDef Roll(int round)
        {
            // Higher rounds tilt the pool toward better hardware.
            float bias = Mathf.Clamp01((round - 4) / 22f);
            var weights = new float[_pool.Count];
            for (int i = 0; i < _pool.Count; i++)
            {
                int order = (int)_pool[i].rarity;
                weights[i] = _pool[i].boxWeight * (1f + order * bias * 1.6f) * (order == 4 ? 0.55f + bias : 1f);
            }
            return _pool[Util.WeightedPick(weights)];
        }

        void SetDisplay(WeaponDef def)
        {
            if (_displayModel != null) Object.Destroy(_displayModel);
            _displayModel = Props.WeaponModel(def, 1.15f, _displayRoot.transform);
        }

        public void Update(float dt, float time, int round, GameRun run)
        {
            if (_fireSaleT > 0f) _fireSaleT -= dt;
            var lid = _info.pivot;
            var light = _info.light;

            switch (_state)
            {
                case State.Spin:
                    _t -= dt;
                    _cycleT -= dt;
                    if (lid != null) lid.localRotation = Quaternion.Euler(-Mathf.Clamp01((SpinTime - _t) * 3f) * 72f, 0, 0);
                    if (light != null) light.intensity = 48f + Mathf.Sin(time * 22f) * 22f;
                    if (_cycleT <= 0f)
                    {
                        // Slow the carousel down as it settles.
                        float p = 1f - _t / SpinTime;
                        _cycleT = 0.07f + p * p * 0.36f;
                        SetDisplay(Util.Pick(_pool));
                    }
                    SpinDisplay(time, 3.2f, 6f);
                    if (_t <= 0f)
                    {
                        pending = Roll(round);
                        SetDisplay(pending);
                        _state = State.Reveal;
                        _t = GrabTime;
                        bool rare = (int)pending.rarity >= 3;
                        Game.Audio.PlayAt(rare ? Sfx.BoxStopRare : Sfx.BoxStop, _root.transform.position, 1f);
                        if (run != null) run.Toast(pending.name, rare ? ToastKind.Warn : ToastKind.Good, false);
                    }
                    break;

                case State.Reveal:
                    _t -= dt;
                    if (light != null) light.intensity = 40f + Mathf.Sin(time * 6f) * 12f;
                    SpinDisplay(time, 1.6f, 3f);
                    if (_t <= 0f)
                    {
                        _displayRoot.SetActive(false);
                        pending = null;
                        if (_usesLeft <= 0) { _state = State.Leaving; _t = 2f; }
                        else { _state = State.Idle; _t = 0f; }
                        if (lid != null) lid.localRotation = Quaternion.identity;
                    }
                    break;

                case State.Leaving:
                {
                    _t -= dt;
                    float k = Mathf.Clamp01(1f - _t / 2f);
                    var s = Spot;
                    _root.transform.position = new Vector3(s.x, s.y + k * k * 9f, s.z);
                    _root.transform.Rotate(0, dt * 290f, 0);
                    _root.transform.localScale = Vector3.one * (1f - k * 0.5f);
                    if (_t <= 0f) { _root.SetActive(false); _state = State.Hidden; _t = 1.2f; }
                    break;
                }

                case State.Hidden:
                    _t -= dt;
                    if (_t <= 0f) Relocate(run);
                    break;

                default:
                    if (lid != null) lid.localRotation = Quaternion.Lerp(lid.localRotation, Quaternion.identity, Mathf.Clamp01(dt * 6f));
                    if (light != null)
                        light.intensity = _fireSaleT > 0f
                            ? 34f + Mathf.Sin(time * 12f) * 16f
                            : 9f + Mathf.Sin(time * 1.4f) * 4f;
                    break;
            }
        }

        void SpinDisplay(float time, float spin, float bobRate)
        {
            if (_displayModel == null) return;
            _displayModel.transform.localRotation = Quaternion.Euler(0, time * spin * Mathf.Rad2Deg * 0.1f, 0);
            _displayModel.transform.localPosition = new Vector3(0, Mathf.Sin(time * bobRate) * 0.05f, 0);
        }

        public void Reset()
        {
            _state = State.Idle;
            _fireSaleT = 0f;
            _displayRoot.SetActive(false);
            MoveTo(0, true, null);
        }
    }
}
