using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// An armable hazard: arc pylons, flame vents or a sentry gun. Costs points,
    /// runs on a timer, then cools down.
    /// </summary>
    public class Trap : Interactable
    {
        const float ActiveTime = 22f;
        const float Cooldown = 40f;

        public readonly TrapDef def;
        readonly TrapSystem _system;
        readonly MachineInfo _panel;
        readonly MachineInfo _emitter;
        readonly Light _light;
        readonly string _label;
        readonly float _dps;
        readonly Color _color;

        string _state = "ready";
        float _t, _shotT;

        public Trap(TrapDef d, TrapSystem system, Transform parent)
            : base(d.id, new Vector3(d.panelX, d.y + 1.2f, d.panelZ), 2.4f, 1)
        {
            def = d;
            _system = system;
            _color = Props.TrapColor(d.kind);
            if (d.kind == "electric") { _label = "ARC PYLONS"; _dps = 300f; }
            else if (d.kind == "fire") { _label = "FLAME VENTS"; _dps = 240f; }
            else { _label = "SENTRY GUN"; _dps = 0f; }

            var panelGo = Props.TrapPanel(d.kind, parent);
            panelGo.transform.position = new Vector3(d.panelX, d.y + 1.35f, d.panelZ);
            panelGo.transform.rotation = Quaternion.Euler(0, d.panelRot, 0);
            _panel = panelGo.GetComponent<MachineInfo>();

            var emitterGo = Props.TrapEmitter(d.kind, parent);
            emitterGo.transform.position = new Vector3(d.x, d.y, d.z);
            emitterGo.transform.rotation = Quaternion.Euler(0, d.rot, 0);
            _emitter = emitterGo.GetComponent<MachineInfo>();

            var lightGo = new GameObject("trapLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = new Vector3(d.x, d.y + 2.2f, d.z);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _color;
            _light.range = 20f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
        }

        bool Powered { get { return _system.power.On; } }

        public override Prompt GetPrompt(GameRun run)
        {
            if (!Powered) return Prompt.Blocked(_label, "NO POWER");
            if (_state == "active") return Prompt.Blocked(_label, "ACTIVE");
            if (_state == "cooldown") return Prompt.Blocked(_label, "RECHARGING " + Mathf.Ceil(_t) + "s");
            return Prompt.Buy("ARM " + _label, def.cost, ActiveTime + "s OF CARNAGE");
        }

        public override bool Activate(GameRun run)
        {
            if (!Powered || _state != "ready") return false;
            if (!run.player.Spend(def.cost)) { run.Deny(); return false; }
            _state = "active";
            _t = ActiveTime;
            Game.Audio.PlayAt(def.kind == "fire" ? Sfx.TrapFire : Sfx.TrapElectric,
                new Vector3(def.x, def.y + 1f, def.z), 1f);
            run.Toast(_label + " ARMED", ToastKind.Warn, false);
            _system.world.nav.SetDanger(def.area, 3f);
            return true;
        }

        public void Update(float dt, float time, GameRun run)
        {
            var face = (_panel.glow != null && _panel.glow.Length > 0) ? _panel.glow[0] : null;

            if (_state == "active")
            {
                _t -= dt;
                _light.intensity = 90f + Mathf.Sin(time * 30f) * 40f;
                if (face != null) Art.SetEmission(face.material, _color * 2.4f);
                RunEffect(dt, time, run);
                if (_t <= 0f)
                {
                    _state = "cooldown";
                    _t = Cooldown;
                    _system.world.nav.SetDanger(def.area, 0f);
                    if (def.kind == "fire" && _emitter.extras != null)
                        foreach (var j in _emitter.extras) if (j != null) j.SetActive(false);
                }
            }
            else
            {
                _light.intensity *= Mathf.Exp(-6f * dt);
                if (face != null)
                {
                    float k = Powered ? (_state == "ready" ? 1.4f + Mathf.Sin(time * 2f) * 0.4f : 0.35f) : 0.05f;
                    Art.SetEmission(face.material, _color * k);
                }
                if (_state == "cooldown")
                {
                    _t -= dt;
                    if (_t <= 0f) _state = "ready";
                }
                if (def.kind == "turret" && _emitter.pivot != null)
                    _emitter.pivot.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 1.2f) * 34f, 0);
            }
        }

        bool InArea(Zombie z)
        {
            return z.pos.x > def.area.x0 && z.pos.x < def.area.x1
                && z.pos.z > def.area.z0 && z.pos.z < def.area.z1;
        }

        void RunEffect(float dt, float time, GameRun run)
        {
            var targets = new List<Zombie>();
            foreach (var z in run.zombies.alive)
                if (z.active && z.state != ZState.Dying && InArea(z)) targets.Add(z);

            if (def.kind == "electric")
            {
                if (_emitter.glow != null)
                    foreach (var c in _emitter.glow) if (c != null) Art.SetEmission(c.material, _color * 3.5f);
                if (Random.value < dt * 26f)
                    run.fx.Arc(new Vector3(def.x - 2.6f, def.y + 2.9f, def.z),
                               new Vector3(def.x + 2.6f, def.y + 2.9f, def.z), _color);
                foreach (var z in targets)
                {
                    var res = z.TakeDamage(_dps * dt, BodyPart.Torso, null, null, Vector3.zero);
                    z.ApplySlow(0.45f, 0.4f);
                    if (Random.value < dt * 4f)
                        run.fx.Arc(new Vector3(def.x, def.y + 2.6f, def.z), z.pos + Vector3.up * 1.2f, _color);
                    if (res.killed) run.OnTrapKill(z);
                }
            }
            else if (def.kind == "fire")
            {
                if (_emitter.extras != null)
                {
                    foreach (var j in _emitter.extras)
                    {
                        if (j == null) continue;
                        j.SetActive(true);
                        var s = j.transform.localScale;
                        j.transform.localScale = new Vector3(s.x, 0.3f * (1f + Mathf.Sin(time * 24f + j.transform.localPosition.x) * 0.35f), s.z);
                    }
                }
                if (Random.value < dt * 30f)
                    run.fx.EmberBurst(new Vector3(def.x + Util.Rand(-3.5f, 3.5f), def.y + Util.Rand(0.3f, 1.6f),
                                                  def.z + Util.Rand(-0.6f, 0.6f)));
                foreach (var z in targets)
                {
                    var res = z.TakeDamage(_dps * dt, BodyPart.Torso, null, null, Vector3.zero);
                    z.ApplyBurn(90f, 3f);
                    if (res.killed) run.OnTrapKill(z);
                }
            }
            else
            {
                // Sentry: pick the closest target and put rounds into it.
                if (targets.Count > 0)
                {
                    Zombie best = targets[0];
                    float bestD = float.MaxValue;
                    foreach (var z in targets)
                    {
                        float d = (z.pos.x - def.x) * (z.pos.x - def.x) + (z.pos.z - def.z) * (z.pos.z - def.z);
                        if (d < bestD) { bestD = d; best = z; }
                    }
                    if (_emitter.pivot != null)
                    {
                        float ang = Mathf.Atan2(best.pos.x - def.x, best.pos.z - def.z) * Mathf.Rad2Deg - def.rot;
                        _emitter.pivot.localRotation = Quaternion.Euler(0, ang, 0);
                    }
                    _shotT -= dt;
                    if (_shotT <= 0f)
                    {
                        _shotT = 0.14f;
                        var from = new Vector3(def.x, def.y + 0.85f, def.z);
                        run.fx.Tracer(from, best.pos + Vector3.up * 1.1f, Util.Hex(0xffd24a));
                        Game.Audio.PlayAt(Sfx.ShootLight, from, 0.5f);
                        var part = Random.value < 0.34f ? BodyPart.Head : BodyPart.Torso;
                        var res = best.TakeDamage(320f, part, null, run.fx, Vector3.zero);
                        if (res.killed) run.OnTrapKill(best);
                    }
                }
                else if (_emitter.pivot != null)
                    _emitter.pivot.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 1.2f) * 34f, 0);
            }
        }

        public void ResetState()
        {
            _state = "ready";
            _t = 0f;
            _system.world.nav.SetDanger(def.area, 0f);
        }
    }

    public class TrapSystem
    {
        public readonly WorldBuilder world;
        public readonly PowerSystem power;
        public readonly List<Trap> traps = new List<Trap>();

        public TrapSystem(WorldBuilder w, PowerSystem p, InteractionSystem interaction, Transform parent)
        {
            world = w;
            power = p;
            foreach (var d in MapData.Traps)
            {
                var t = new Trap(d, this, parent);
                interaction.Add(t);
                traps.Add(t);
            }
        }

        public void Update(float dt, float time, GameRun run)
        {
            foreach (var t in traps) t.Update(dt, time, run);
        }
    }
}
