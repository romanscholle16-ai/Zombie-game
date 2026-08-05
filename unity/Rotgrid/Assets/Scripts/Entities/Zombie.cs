using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum BodyPart { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }

    /// <summary>Raycast target on a zombie limb; carries the damage multiplier key.</summary>
    public class ZombieHitbox : MonoBehaviour
    {
        public Zombie zombie;
        public BodyPart part;
    }

    public class ZombieType
    {
        public string id, label;
        public float healthMult, speedMin, speedMax, damage, scale, points, staggerResist;
        public bool tinted;
        public int color;
    }

    public enum ZState { Idle, Breach, Vault, Chase, Attack, Stagger, Dying, Dead }

    public struct DamageResult
    {
        public bool killed, headshot, crit;
        public float damage;
    }

    /// <summary>
    /// One zombie: a primitive-built body with an animated lurch, flow-field
    /// pathing, window breaching, staggering, limb loss and a crawler state.
    /// </summary>
    public class Zombie
    {
        const float PlayerClearance = 0.78f;

        public static readonly Dictionary<string, ZombieType> Types = new Dictionary<string, ZombieType>
        {
            { "normal", new ZombieType { id="normal", label="SHAMBLER", healthMult=1f, speedMin=1.35f, speedMax=3.5f,
                                         damage=26f, scale=1f, points=1f, staggerResist=1f } },
            { "runner", new ZombieType { id="runner", label="SPRINTER", healthMult=0.72f, speedMin=4.4f, speedMax=5.9f,
                                         damage=22f, scale=0.94f, points=1.15f, staggerResist=0.7f, tinted=true, color=0x7f5a3c } },
            { "heavy",  new ZombieType { id="heavy", label="BRUTE", healthMult=3.4f, speedMin=1.1f, speedMax=2.1f,
                                         damage=46f, scale=1.28f, points=1.6f, staggerResist=2.6f, tinted=true, color=0x4a5a4a } },
            { "boss",   new ZombieType { id="boss", label="ABOMINATION", healthMult=26f, speedMin=2f, speedMax=3f,
                                         damage=62f, scale=1.85f, points=6f, staggerResist=12f, tinted=true, color=0x6a2a2a } },
        };

        static int _uid;

        public readonly int id;
        public readonly GameObject root;
        public ZState state = ZState.Dead;
        public bool active;
        public ZombieType type;
        public Vector3 pos, vel;
        public float yaw;
        public float health, maxHealth;
        public float radius = 0.42f, height = 1.85f, scale = 1f;
        public float damage;
        public float pointsMult = 1f;
        public bool crawler;
        public Barricade barricade;

        public readonly List<ZombieHitbox> hitboxes = new List<ZombieHitbox>();

        // Body rig
        Transform _body, _torso, _head, _jaw;
        Transform _lShoulder, _rShoulder, _lElbow, _rElbow;
        Transform _lHip, _rHip, _lKnee, _rKnee;
        readonly MeshRenderer[] _clothParts = new MeshRenderer[8];
        Material _baseCloth;

        float _speed, _baseSpeed;
        float _attackCd, _staggerT, _deathT, _breachT, _groanT, _pathT, _phase, _attackAnim;
        float _burnT, _burnDps, _slowT, _slowFactor = 1f;
        Vector3 _lastFlow;
        readonly Dictionary<BodyPart, float> _limb = new Dictionary<BodyPart, float>();

        // Vault
        Vector3 _vaultFrom, _vaultTo;
        float _vaultT, _vaultDur, _vaultPeak;

        public Zombie(Transform parent)
        {
            id = ++_uid;
            root = new GameObject("zombie" + id);
            root.transform.SetParent(parent, false);
            Build();
            root.SetActive(false);
        }

        // ================================================================ model
        void Build()
        {
            int variant = id % 5;
            var skin = Art.Lit("zskin" + variant, Color.white, 0.05f, 0f, Art.ZombieSkin(variant), 1f);
            _baseCloth = Art.Lit("zcloth" + (id % 4), Color.white, 0.03f, 0f, Art.Cloth(id % 4), 1f);

            var bodyGo = new GameObject("body");
            bodyGo.transform.SetParent(root.transform, false);
            _body = bodyGo.transform;

            var torsoGo = new GameObject("torso");
            torsoGo.transform.SetParent(_body, false);
            torsoGo.transform.localPosition = new Vector3(0, 1.02f, 0);
            _torso = torsoGo.transform;

            var torsoMesh = Props.Box(0.52f, 0.66f, 0.3f, _baseCloth, Vector3.zero, _torso, true);
            var pelvis = Props.Box(0.44f, 0.28f, 0.28f, _baseCloth, new Vector3(0, 0.72f - 0.46f, 0), _body, true);
            Props.Box(0.14f, 0.12f, 0.14f, skin, new Vector3(0, 0.4f, 0), _torso);

            var headGo = new GameObject("head");
            headGo.transform.SetParent(_torso, false);
            headGo.transform.localPosition = new Vector3(0, 0.53f, 0);
            _head = headGo.transform;
            var headMesh = Props.Box(0.26f, 0.3f, 0.26f, skin, Vector3.zero, _head, true);
            var jawGo = Props.Box(0.2f, 0.09f, 0.2f, skin, new Vector3(0, -0.16f, 0.02f), _head, true);
            _jaw = jawGo.transform;
            var eye = Art.Emissive("zeye", Util.Hex(0xb8d84a), 0.6f);
            Props.Box(0.05f, 0.04f, 0.03f, eye, new Vector3(-0.07f, 0.03f, 0.13f), _head);
            Props.Box(0.05f, 0.04f, 0.03f, eye, new Vector3(0.07f, 0.03f, 0.13f), _head);

            // ---- arms
            GameObject lUpper, rUpper, lFore, rFore;
            _lShoulder = MakeArm(-1f, skin, out lUpper, out lFore, out _lElbow);
            _rShoulder = MakeArm(1f, skin, out rUpper, out rFore, out _rElbow);

            // ---- legs
            GameObject lThigh, rThigh, lShin, rShin;
            _lHip = MakeLeg(-1f, out lThigh, out lShin, out _lKnee);
            _rHip = MakeLeg(1f, out rThigh, out rShin, out _rKnee);

            Register(headMesh, BodyPart.Head);
            Register(jawGo, BodyPart.Head);
            Register(torsoMesh, BodyPart.Torso);
            Register(pelvis, BodyPart.Torso);
            Register(lUpper, BodyPart.LeftArm);
            Register(lFore, BodyPart.LeftArm);
            Register(rUpper, BodyPart.RightArm);
            Register(rFore, BodyPart.RightArm);
            Register(lThigh, BodyPart.LeftLeg);
            Register(lShin, BodyPart.LeftLeg);
            Register(rThigh, BodyPart.RightLeg);
            Register(rShin, BodyPart.RightLeg);

            _clothParts[0] = torsoMesh.GetComponent<MeshRenderer>();
            _clothParts[1] = pelvis.GetComponent<MeshRenderer>();
            _clothParts[2] = lUpper.GetComponent<MeshRenderer>();
            _clothParts[3] = rUpper.GetComponent<MeshRenderer>();
            _clothParts[4] = lThigh.GetComponent<MeshRenderer>();
            _clothParts[5] = rThigh.GetComponent<MeshRenderer>();
            _clothParts[6] = lShin.GetComponent<MeshRenderer>();
            _clothParts[7] = rShin.GetComponent<MeshRenderer>();
        }

        Transform MakeArm(float side, Material skin, out GameObject upper, out GameObject fore, out Transform elbow)
        {
            var shoulderGo = new GameObject(side < 0 ? "lShoulder" : "rShoulder");
            shoulderGo.transform.SetParent(_torso, false);
            shoulderGo.transform.localPosition = new Vector3(side * 0.33f, 0.24f, 0);
            upper = Props.Box(0.14f, 0.36f, 0.14f, _baseCloth, new Vector3(0, -0.18f, 0), shoulderGo.transform, true);

            var elbowGo = new GameObject("elbow");
            elbowGo.transform.SetParent(shoulderGo.transform, false);
            elbowGo.transform.localPosition = new Vector3(0, -0.36f, 0);
            elbow = elbowGo.transform;
            fore = Props.Box(0.12f, 0.34f, 0.12f, skin, new Vector3(0, -0.17f, 0), elbow, true);
            Props.Box(0.13f, 0.13f, 0.15f, skin, new Vector3(0, -0.36f, 0.02f), elbow);
            return shoulderGo.transform;
        }

        Transform MakeLeg(float side, out GameObject thigh, out GameObject shin, out Transform knee)
        {
            var hipGo = new GameObject(side < 0 ? "lHip" : "rHip");
            hipGo.transform.SetParent(_body, false);
            hipGo.transform.localPosition = new Vector3(side * 0.14f, 0.72f, 0);
            thigh = Props.Box(0.18f, 0.4f, 0.18f, _baseCloth, new Vector3(0, -0.2f, 0), hipGo.transform, true);

            var kneeGo = new GameObject("knee");
            kneeGo.transform.SetParent(hipGo.transform, false);
            kneeGo.transform.localPosition = new Vector3(0, -0.4f, 0);
            knee = kneeGo.transform;
            shin = Props.Box(0.16f, 0.36f, 0.16f, _baseCloth, new Vector3(0, -0.18f, 0), knee, true);
            Props.Box(0.17f, 0.1f, 0.26f, Art.Lit("zboot", Util.Hex(0x22201c), 0f), new Vector3(0, -0.38f, 0.04f), knee);
            return hipGo.transform;
        }

        void Register(GameObject go, BodyPart part)
        {
            var hb = go.AddComponent<ZombieHitbox>();
            hb.zombie = this;
            hb.part = part;
            hitboxes.Add(hb);
        }

        // ================================================================ spawn
        public void Spawn(string typeId, Vector3 p, float hp, float aggression, float damageScale, Barricade window)
        {
            type = Types.ContainsKey(typeId) ? Types[typeId] : Types["normal"];
            maxHealth = Mathf.Round(hp * type.healthMult);
            health = maxHealth;
            _speed = Mathf.Lerp(type.speedMin, type.speedMax, Mathf.Clamp01(aggression)) * Util.Rand(0.9f, 1.1f);
            _baseSpeed = _speed;
            damage = type.damage * damageScale;
            pointsMult = type.points;
            barricade = window;
            state = window != null ? ZState.Breach : ZState.Chase;

            _attackCd = Util.Rand(0f, 0.5f);
            _staggerT = 0f; _deathT = 0f; _breachT = 0f;
            _groanT = Util.Rand(1f, 6f);
            _pathT = Util.Rand(0f, 0.3f);
            _phase = Util.Rand(0f, Mathf.PI * 2f);
            _burnT = 0f; _slowT = 0f; _slowFactor = 1f;
            _attackAnim = 0f;
            crawler = false;
            _lastFlow = Vector3.zero;
            _limb.Clear();

            pos = p;
            vel = Vector3.zero;
            yaw = Util.Rand(0f, 360f);

            scale = type.scale * Util.Rand(0.94f, 1.06f);
            radius = 0.42f * scale;
            height = 1.85f * scale;
            root.transform.localScale = Vector3.one * scale;
            root.transform.position = pos;
            root.SetActive(true);
            active = true;

            _lShoulder.gameObject.SetActive(true);
            _rShoulder.gameObject.SetActive(true);
            _lElbow.gameObject.SetActive(true);
            _rElbow.gameObject.SetActive(true);
            _lHip.gameObject.SetActive(true);
            _rHip.gameObject.SetActive(true);
            _body.localRotation = Quaternion.identity;
            _body.localPosition = Vector3.zero;
            _torso.localRotation = Quaternion.identity;
            _head.localRotation = Quaternion.identity;

            // Typed zombies get their own tinted cloth so nothing is shared.
            var cloth = type.tinted
                ? Art.Lit("zclothT" + type.id + "_" + (id % 4), Util.Hex(type.color), 0.03f, 0f, Art.Cloth(id % 4), 1f)
                : _baseCloth;
            foreach (var r in _clothParts) if (r != null) r.sharedMaterial = cloth;
        }

        public void Despawn()
        {
            active = false;
            state = ZState.Dead;
            root.SetActive(false);
            if (barricade != null) { barricade.attackers.Remove(this); barricade = null; }
        }

        // ================================================================ damage
        public DamageResult TakeDamage(float amount, BodyPart part, WeaponDef def, Effects fx, Vector3 dir)
        {
            var res = new DamageResult();
            if (!active || state == ZState.Dying) return res;

            float mult;
            if (part == BodyPart.Head) { mult = def != null ? def.headMult : 2.6f; res.headshot = true; }
            else if (part == BodyPart.Torso) mult = def != null ? def.torsoMult : 1f;
            else mult = def != null ? def.limbMult : 0.72f;

            float dmg = amount * mult;
            health -= dmg;
            res.damage = dmg;
            res.crit = res.headshot;

            if (part != BodyPart.Torso && part != BodyPart.Head)
            {
                float cur;
                if (!_limb.TryGetValue(part, out cur)) cur = 1f;
                cur -= dmg / Mathf.Max(60f, maxHealth * 0.45f);
                _limb[part] = cur;
                if (cur <= 0f) SeverLimb(part, fx);
            }

            // Stagger: big relative hits interrupt the walk cycle.
            float staggerPower = dmg / (maxHealth * 0.22f * type.staggerResist);
            if (staggerPower > 1f && state != ZState.Vault)
            {
                _staggerT = Mathf.Min(0.45f, 0.12f + staggerPower * 0.06f);
                if (state == ZState.Chase || state == ZState.Attack) state = ZState.Stagger;
            }

            if (fx != null)
            {
                var hp = HitPoint(part);
                fx.BloodBurst(hp, dir, res.headshot ? 1.7f : 1f);
                if (res.headshot) fx.BoneBurst(hp);
            }

            if (health <= 0f)
            {
                Die(res.headshot, fx, dir);
                res.killed = true;
            }
            return res;
        }

        void SeverLimb(BodyPart part, Effects fx)
        {
            Transform node = null;
            switch (part)
            {
                case BodyPart.LeftArm: node = _lElbow; break;
                case BodyPart.RightArm: node = _rElbow; break;
                case BodyPart.LeftLeg: node = _lHip; break;
                case BodyPart.RightLeg: node = _rHip; break;
            }
            if (node == null || !node.gameObject.activeSelf) return;
            node.gameObject.SetActive(false);
            if (fx != null) fx.BloodBurst(node.position, Vector3.zero, 2.2f);

            if (part == BodyPart.LeftLeg || part == BodyPart.RightLeg)
            {
                bool legsGone = !_lHip.gameObject.activeSelf || !_rHip.gameObject.activeSelf;
                if (legsGone && !crawler)
                {
                    crawler = true;
                    _speed = _baseSpeed * 0.42f;
                    height *= 0.45f;
                }
            }
        }

        void Die(bool headshot, Effects fx, Vector3 dir)
        {
            state = ZState.Dying;
            _deathT = 0f;
            if (fx != null) fx.BloodBurst(pos + Vector3.up, dir, headshot ? 2.6f : 1.8f);
            Game.Audio.PlayAt(Sfx.ZombieDeath, pos + Vector3.up, 0.9f);
            if (barricade != null) { barricade.attackers.Remove(this); barricade = null; }
        }

        public Vector3 HitPoint(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return _head.position;
                case BodyPart.LeftArm: return _lElbow.position;
                case BodyPart.RightArm: return _rElbow.position;
                case BodyPart.LeftLeg: return _lKnee.position;
                case BodyPart.RightLeg: return _rKnee.position;
                default: return _torso.position;
            }
        }

        public void ApplyBurn(float dps, float seconds)
        {
            _burnT = Mathf.Max(_burnT, seconds);
            _burnDps = Mathf.Max(_burnDps, dps);
        }

        public void ApplySlow(float factor, float seconds)
        {
            _slowT = Mathf.Max(_slowT, seconds);
            _slowFactor = Mathf.Min(_slowFactor, factor);
        }

        // ================================================================ update
        public void Tick(float dt, ZombieContext ctx)
        {
            if (!active) return;

            if (_burnT > 0f)
            {
                _burnT -= dt;
                health -= _burnDps * dt;
                if (Random.value < dt * 6f && ctx.fx != null) ctx.fx.EmberBurst(pos + Vector3.up);
                if (health <= 0f && state != ZState.Dying)
                {
                    Die(false, ctx.fx, Vector3.zero);
                    ctx.OnBurnKill(this);
                }
            }
            if (_slowT > 0f) { _slowT -= dt; if (_slowT <= 0f) _slowFactor = 1f; }

            switch (state)
            {
                case ZState.Breach: UpdateBreach(dt, ctx); break;
                case ZState.Vault: UpdateVault(dt); break;
                case ZState.Stagger:
                    _staggerT -= dt;
                    if (_staggerT <= 0f) state = ZState.Chase;
                    FaceTowards(ctx.playerPos, dt, 6f);
                    break;
                case ZState.Attack: UpdateAttack(dt, ctx); break;
                case ZState.Chase: UpdateChase(dt, ctx); break;
                case ZState.Dying: UpdateDying(dt, ctx); return;
            }

            _groanT -= dt;
            if (_groanT <= 0f)
            {
                _groanT = Util.Rand(3.5f, 11f);
                float pitch = type.id == "heavy" ? 0.7f : (type.id == "boss" ? 0.5f : 1f);
                Game.Audio.MaybeGroan(pos, ctx.time, pitch);
            }

            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Animate(dt, ctx.time);
        }

        float SpeedNow()
        {
            float s = _speed;
            if (_slowT > 0f) s *= _slowFactor;
            if (crawler) s *= 0.8f;
            return s;
        }

        void UpdateBreach(float dt, ZombieContext ctx)
        {
            var b = barricade;
            if (b == null) { state = ZState.Chase; return; }
            float dx = b.outside.x - pos.x, dz = b.outside.z - pos.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d > 0.55f)
            {
                Step(dx / d, dz / d, dt, SpeedNow() * 0.9f, ctx);
                FaceDir(dx / d, dz / d, dt, 6f);
                return;
            }
            b.attackers.Add(this);
            FaceDir(-b.nx, -b.nz, dt, 6f);
            if (b.IsOpen) { StartVault(b); return; }

            _breachT -= dt;
            if (_breachT <= 0f)
            {
                _breachT = 0.85f;
                b.Damage(0.55f, ctx.fx);
                _attackAnim = 0.4f;
            }
        }

        void StartVault(Barricade b)
        {
            state = ZState.Vault;
            b.attackers.Remove(this);
            _vaultT = 0f;
            _vaultDur = type.id == "runner" ? 0.55f : 0.85f;
            _vaultFrom = pos;
            _vaultTo = b.inside;
            _vaultPeak = b.y + 1.35f;
            yaw = Mathf.Atan2(-b.nx, -b.nz) * Mathf.Rad2Deg;
            barricade = null;
        }

        void UpdateVault(float dt)
        {
            _vaultT += dt;
            float t = Mathf.Clamp01(_vaultT / _vaultDur);
            pos = Vector3.Lerp(_vaultFrom, _vaultTo, t);
            pos.y = Mathf.Lerp(_vaultFrom.y, _vaultTo.y, t) + Mathf.Sin(t * Mathf.PI) * (_vaultPeak - _vaultFrom.y) * 0.8f;
            // Hands forward, body pitched over the sill.
            _body.localRotation = Quaternion.Euler(-Mathf.Sin(t * Mathf.PI) * 40f, 0, 0);
            if (t >= 1f)
            {
                _body.localRotation = Quaternion.identity;
                state = ZState.Chase;
            }
        }

        void UpdateChase(float dt, ZombieContext ctx)
        {
            float dx = ctx.playerPos.x - pos.x;
            float dz = ctx.playerPos.z - pos.z;
            float distSq = dx * dx + dz * dz;
            float reach = radius + 0.95f + (crawler ? -0.1f : 0f);

            if (distSq < reach * reach * 2.4f && Mathf.Abs(ctx.playerPos.y - pos.y) < 2.2f)
            {
                state = ZState.Attack;
                return;
            }

            _pathT -= dt;
            if (_pathT <= 0f)
            {
                _pathT = 0.12f + Random.value * 0.1f;
                Vector3 flow;
                bool arrived;
                if (ctx.nav.FlowAt(pos.x, pos.z, out flow, out arrived) && !arrived) _lastFlow = flow;
                else
                {
                    float d = Mathf.Max(0.001f, Mathf.Sqrt(distSq));
                    _lastFlow = new Vector3(dx / d, 0f, dz / d);
                }
            }

            // Separation so the horde spreads across a doorway instead of stacking.
            Vector3 sep = ctx.Separation(this);
            float mx = _lastFlow.x + sep.x;
            float mz = _lastFlow.z + sep.z;
            float len = Mathf.Max(0.001f, Mathf.Sqrt(mx * mx + mz * mz));
            mx /= len; mz /= len;

            Step(mx, mz, dt, SpeedNow(), ctx);
            FaceDir(mx, mz, dt, 6f);
        }

        void UpdateAttack(float dt, ZombieContext ctx)
        {
            float dx = ctx.playerPos.x - pos.x;
            float dz = ctx.playerPos.z - pos.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float reach = radius + PlayerClearance + 0.75f;
            if (d > reach + 0.6f || ctx.playerDead) { state = ZState.Chase; return; }

            float inv = Mathf.Max(0.001f, d);
            FaceDir(dx / inv, dz / inv, dt, 10f);
            if (d > reach * 0.75f) Step(dx / inv, dz / inv, dt, SpeedNow() * 0.5f, ctx);

            _attackCd -= dt;
            if (_attackCd <= 0f)
            {
                _attackCd = type.id == "runner" ? 0.85f : 1.15f;
                _attackAnim = 0.45f;
                Game.Audio.PlayAt(Sfx.ZombieAttack, pos, 0.8f);
                ctx.OnAttack(this, damage);
            }
        }

        void UpdateDying(float dt, ZombieContext ctx)
        {
            _deathT += dt;
            float t = Mathf.Clamp01(_deathT / 0.9f);
            _body.localRotation = Quaternion.Euler(-t * 86f, 0, 0);
            _body.localPosition = new Vector3(0, -t * 0.35f, 0);
            root.transform.position = pos;
            root.transform.localScale = Vector3.one * (scale * (1f - t * 0.08f));
            if (_deathT > 1.5f)
            {
                ctx.OnRemoved(this);
                Despawn();
            }
        }

        void Step(float dx, float dz, float dt, float speed, ZombieContext ctx)
        {
            float nx = pos.x + dx * speed * dt;
            float nz = pos.z + dz * speed * dt;
            ctx.world.Collide(ref nx, ref nz, radius, pos.y, pos.y + height);

            // Never walk inside the player — their arms reach, their heads do not.
            float minD = radius + PlayerClearance;
            float px = nx - ctx.playerPos.x, pz = nz - ctx.playerPos.z;
            float pd2 = px * px + pz * pz;
            if (pd2 < minD * minD && Mathf.Abs(ctx.playerPos.y - pos.y) < 2f)
            {
                float pd = Mathf.Max(0.0001f, Mathf.Sqrt(pd2));
                nx = ctx.playerPos.x + (px / pd) * minD;
                nz = ctx.playerPos.z + (pz / pd) * minD;
            }

            pos.x = nx;
            pos.z = nz;
            float ground = ctx.nav.HeightAt(pos.x, pos.z);
            pos.y = Mathf.Lerp(pos.y, ground, Mathf.Clamp01(dt * 9f));
            vel = new Vector3(dx * speed, 0f, dz * speed);
        }

        void FaceTowards(Vector3 target, float dt, float rate)
        {
            float dx = target.x - pos.x, dz = target.z - pos.z;
            float d = Mathf.Max(0.001f, Mathf.Sqrt(dx * dx + dz * dz));
            FaceDir(dx / d, dz / d, dt, rate);
        }

        void FaceDir(float dx, float dz, float dt, float rate)
        {
            float target = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(yaw, target);
            yaw += delta * Mathf.Clamp01(dt * rate);
        }

        // ================================================================ anim
        /// <summary>Drives the walk cycle. Public so the menu backdrop can pose idlers.</summary>
        public void Animate(float dt, float time)
        {
            float moving = new Vector2(vel.x, vel.z).magnitude;
            float cadence = crawler ? 6.5f : 2.2f + moving * 1.5f;
            float t = time * cadence + _phase;
            float amp = Mathf.Clamp(moving / 3f, 0.2f, 1.2f);

            if (crawler)
            {
                _body.localRotation = Quaternion.Euler(-72f, 0, 0);
                _body.localPosition = new Vector3(0, -0.62f * scale, 0);
                _lShoulder.localRotation = Quaternion.Euler((Mathf.Sin(t) * 1.1f - 0.6f) * Mathf.Rad2Deg, 0, 12f);
                _rShoulder.localRotation = Quaternion.Euler((-Mathf.Sin(t) * 1.1f - 0.6f) * Mathf.Rad2Deg, 0, -12f);
            }
            else
            {
                // Classic lurch: heavy forward lean with an uneven gait.
                _body.localRotation = Quaternion.Euler(
                    (-0.12f - Mathf.Sin(t * 2f) * 0.03f * amp) * Mathf.Rad2Deg, 0,
                    Mathf.Sin(t) * 0.07f * amp * Mathf.Rad2Deg);
                _body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t)) * 0.045f * amp, 0);

                float swing = Mathf.Sin(t) * 0.75f * amp * Mathf.Rad2Deg;
                _lHip.localRotation = Quaternion.Euler(swing, 0, 0);
                _rHip.localRotation = Quaternion.Euler(-swing, 0, 0);
                _lKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, -swing) * 0.9f, 0, 0);
                _rKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, swing) * 0.9f, 0, 0);

                float reach = (state == ZState.Chase || state == ZState.Attack) ? -1.25f : -0.5f;
                float armSwing = Mathf.Sin(t + 0.6f) * 0.16f * amp;
                float extra = _attackAnim > 0f ? -Mathf.Sin((1f - _attackAnim / 0.45f) * Mathf.PI) * 1.1f : 0f;
                _lShoulder.localRotation = Quaternion.Euler((reach + armSwing + extra) * Mathf.Rad2Deg, 0, 12.6f);
                _rShoulder.localRotation = Quaternion.Euler((reach - armSwing + extra) * Mathf.Rad2Deg, 0, -12.6f);
                _lElbow.localRotation = Quaternion.Euler(-23f, 0, 0);
                _rElbow.localRotation = Quaternion.Euler(-23f, 0, 0);
            }

            if (_attackAnim > 0f)
            {
                _attackAnim = Mathf.Max(0f, _attackAnim - dt);
                float a = Mathf.Sin((1f - _attackAnim / 0.45f) * Mathf.PI);
                _torso.localRotation = Quaternion.Euler(-a * 14f, 0, 0);
            }
            else _torso.localRotation = Quaternion.Lerp(_torso.localRotation, Quaternion.identity, Mathf.Clamp01(dt * 8f));

            _head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.7f) * 7f);
            if (_jaw != null) _jaw.localRotation = Quaternion.Euler(11f + Mathf.Sin(t * 1.6f) * 10f, 0, 0);
        }

        /// <summary>Used by the menu backdrop: walk forward on rails, no AI.</summary>
        public void MenuWalk(float dt, float time, float speed, float yawDegrees)
        {
            vel = new Vector3(0f, 0f, speed);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0, yawDegrees, 0);
            Animate(dt, time);
        }

        public static string RandomTypeForRound(int round)
        {
            if (round >= 8 && Random.value < Mathf.Clamp((round - 6) * 0.028f, 0f, 0.32f)) return "runner";
            if (round >= 12 && Random.value < Mathf.Clamp((round - 10) * 0.014f, 0f, 0.16f)) return "heavy";
            return "normal";
        }
    }

    /// <summary>Everything a zombie needs from the outside world each tick.</summary>
    public class ZombieContext
    {
        public WorldBuilder world;
        public NavGrid nav;
        public Vector3 playerPos;
        public bool playerDead;
        public float time;
        public Effects fx;
        public System.Func<Zombie, Vector3> SeparationFn;
        public System.Action<Zombie, float> OnAttack = delegate { };
        public System.Action<Zombie> OnRemoved = delegate { };
        public System.Action<Zombie> OnBurnKill = delegate { };

        public Vector3 Separation(Zombie z) { return SeparationFn != null ? SeparationFn(z) : Vector3.zero; }
    }
}
