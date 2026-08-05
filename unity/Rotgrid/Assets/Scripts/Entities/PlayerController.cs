using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// First-person controller: walk, sprint, crouch, slide, jump, mantle and
    /// ADS, plus the survival state (health, armour, perks, points, downed and
    /// bleed-out). Movement is hand-rolled against the world's AABB list.
    /// </summary>
    public class PlayerController
    {
        public const float StandHeight = 1.72f;
        public const float CrouchHeight = 1.06f;
        public const float Radius = 0.42f;
        const float Gravity = 19.5f;

        const float WalkSpeed = 4.3f;
        const float SprintSpeed = 6.9f;
        const float CrouchSpeed = 2.3f;
        const float AdsSpeed = 2.5f;
        const float SlideBurst = 9.4f;
        const float SlideTime = 0.72f;
        const float JumpVel = 5.6f;

        readonly WorldBuilder _world;
        public readonly Transform camera;

        public Vector3 pos;
        public Vector3 vel;
        public float yaw, pitch;
        public float eyeHeight = StandHeight;
        float _targetEye = StandHeight;

        public bool grounded = true, crouching, sprinting, sliding;
        bool _wasGrounded = true;
        float _slideT;

        // Mantle
        bool _mantling;
        float _mantleT, _mantleDur;
        Vector3 _mantleFrom, _mantleTo;

        public float stamina = 1f;
        float _staminaDrain = 0.24f, _staminaRegen = 0.32f;

        public float maxHealth = 100f, health = 100f;
        public float armor, maxArmor;
        public float sinceDamage = 99f;
        const float RegenDelay = 4.2f, RegenRate = 26f;

        public bool downed, dead;
        public float bleedOut, bleedOutMax = 30f;
        public int selfRevives;

        public int points = 500;
        public float pointMultiplier = 1f;
        public bool instantKill;

        readonly HashSet<string> _perks = new HashSet<string>();
        public IEnumerable<string> Perks { get { return _perks; } }

        public float shake;
        float _bob, _bobAmount, _viewRoll, _stepDist;
        Vector2 _recoil;
        public Vector2 lastLook;

        public readonly RunSummary stats = new RunSummary();
        public readonly List<DamageFlash> damageFlashes = new List<DamageFlash>();

        public struct DamageFlash { public float angle, t; }

        public PlayerController(WorldBuilder world, Transform cameraTransform)
        {
            _world = world;
            camera = cameraTransform;
        }

        public void SpawnAt(Vector3 p, float yawRadians)
        {
            pos = p;
            yaw = yawRadians;
            pitch = 0f;
            vel = Vector3.zero;
        }

        public Vector3 EyePos { get { return new Vector3(pos.x, pos.y + eyeHeight, pos.z); } }
        /// <summary>Unity convention: yaw 0 faces +Z, and matches the camera's euler Y.</summary>
        public Vector3 Forward { get { return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)); } }
        public bool HasPerk(string id) { return _perks.Contains(id); }
        public int PerkCount { get { return _perks.Count; } }

        public float SpeedScale
        {
            get
            {
                float s = HasPerk("swift") ? 1.13f : 1f;
                if (downed) s = 0.42f;
                return s;
            }
        }

        // ============================================================== combat
        public void TakeDamage(float amount, Vector3 fromPos)
        {
            if (dead || amount <= 0f) return;
            if (downed)
            {
                // Attacks while downed accelerate the bleed-out.
                bleedOut = Mathf.Max(0f, bleedOut - amount * 0.12f);
                return;
            }
            sinceDamage = 0f;
            if (armor > 0f)
            {
                float absorbed = Mathf.Min(armor, amount * 0.65f);
                armor -= absorbed;
                amount -= absorbed;
            }
            health -= amount;
            shake = Mathf.Min(1.2f, shake + amount * 0.02f);
            damageFlashes.Add(new DamageFlash
            {
                angle = Mathf.Atan2(fromPos.x - pos.x, fromPos.z - pos.z),
                t = 1.1f,
            });
            Game.Audio.Play(Sfx.Hurt, Mathf.Clamp(amount / 30f, 0.4f, 1f));
            if (health <= 0f) GoDown();
        }

        void GoDown()
        {
            health = 0f;
            downed = true;
            stats.downs++;
            bleedOutMax = HasPerk("medic") ? 45f : 30f;
            bleedOut = bleedOutMax;
            _targetEye = 0.6f;
            sprinting = false;
            sliding = false;
            ClearPerks();
            OnDowned();
        }

        public System.Action OnDowned = delegate { };
        public System.Action OnDied = delegate { };
        public System.Action OnRevived = delegate { };

        public void ReviveSelf()
        {
            downed = false;
            health = maxHealth * 0.5f;
            armor = 0f;
            sinceDamage = 0f;
            stats.revives++;
            OnRevived();
        }

        public void Die()
        {
            dead = true;
            downed = false;
            OnDied();
        }

        public void AddArmor(float amount)
        {
            maxArmor = Mathf.Max(maxArmor, 100f);
            armor = Mathf.Clamp(armor + amount, 0f, maxArmor);
        }

        public void AddShake(float amount)
        {
            if (!GameSettings.ScreenShake) return;
            shake = Mathf.Min(1.6f, shake + amount);
        }

        public void AddRecoil(float x, float y)
        {
            _recoil.x += x;
            _recoil.y += y;
        }

        // ============================================================== economy
        public int AddPoints(int n)
        {
            if (n <= 0) return 0;
            int total = Mathf.RoundToInt(n * pointMultiplier);
            points += total;
            stats.pointsEarned += total;
            return total;
        }

        public bool Spend(int n)
        {
            if (points < n) return false;
            points -= n;
            return true;
        }

        public bool GivePerk(PerkDef p)
        {
            if (_perks.Contains(p.id)) return false;
            _perks.Add(p.id);
            stats.perksBought.Add(p.id);
            if (p.id == "vital") { maxHealth = 175f; health = maxHealth; }
            if (p.id == "medic") selfRevives = 1;
            if (p.id == "endure") { _staminaDrain = 0.13f; _staminaRegen = 0.5f; }
            return true;
        }

        public void ClearPerks()
        {
            _perks.Clear();
            maxHealth = 100f;
            health = Mathf.Min(health, maxHealth);
            selfRevives = 0;
            _staminaDrain = 0.24f;
            _staminaRegen = 0.32f;
        }

        // ============================================================== update
        public void Update(float dt, bool canMove, bool adsing)
        {
            Look(dt, canMove);
            if (dead) return;

            if (_mantling) UpdateMantle(dt);
            else Move(dt, canMove, adsing);

            Vitals(dt);
            UpdateCamera(dt, adsing);
        }

        void Look(float dt, bool canMove)
        {
            if (!canMove) { lastLook = Vector2.zero; return; }
            var d = InputMap.LookDelta(dt);
            lastLook = d;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch + d.y, -Mathf.PI / 2f + 0.03f, Mathf.PI / 2f - 0.03f);
        }

        void Move(float dt, bool canMove, bool adsing)
        {
            Vector2 axis = canMove ? InputMap.MoveAxis() : Vector2.zero;
            bool wantSprint = canMove && InputMap.Down(Bind.Sprint) && axis.y > 0.3f && !adsing && !downed;
            bool wantCrouch = canMove && InputMap.Down(Bind.Crouch);

            // ---- slide
            if (sliding)
            {
                _slideT -= dt;
                if (_slideT <= 0f || !grounded) { sliding = false; crouching = wantCrouch; }
            }
            else if (wantCrouch && sprinting && grounded && !downed && stamina > 0.12f)
            {
                sliding = true;
                _slideT = SlideTime;
                stamina = Mathf.Max(0f, stamina - 0.16f);
                var f = Forward;
                vel.x = f.x * SlideBurst * SpeedScale;
                vel.z = f.z * SlideBurst * SpeedScale;
                Game.Audio.Play(Sfx.Step, 0.9f);
            }
            crouching = sliding || (wantCrouch && grounded);

            // ---- stamina
            sprinting = wantSprint && stamina > 0.02f && !crouching;
            if (sprinting) stamina = Mathf.Clamp01(stamina - _staminaDrain * dt);
            else stamina = Mathf.Clamp01(stamina + _staminaRegen * dt * (vel.sqrMagnitude > 0.4f ? 0.6f : 1f));

            // ---- desired speed
            float speed = WalkSpeed;
            if (downed) speed = CrouchSpeed * 0.72f;
            else if (sliding) speed = SlideBurst;
            else if (crouching) speed = CrouchSpeed;
            else if (sprinting) speed = SprintSpeed;
            else if (adsing) speed = AdsSpeed;
            speed *= SpeedScale;

            // forward = (sin, cos), right = (cos, -sin)
            float sin = Mathf.Sin(yaw), cos = Mathf.Cos(yaw);
            float wx = sin * axis.y + cos * axis.x;
            float wz = cos * axis.y - sin * axis.x;
            float wl = Mathf.Sqrt(wx * wx + wz * wz);
            if (wl > 1f) { wx /= wl; wz /= wl; }

            if (!sliding)
            {
                float lambda = grounded ? 8.7f : 1.7f;
                vel.x = Util.Damp(vel.x, wx * speed, lambda, dt);
                vel.z = Util.Damp(vel.z, wz * speed, lambda, dt);
                if (grounded && wl < 0.05f)
                {
                    float f = Mathf.Exp(-11f * dt);
                    vel.x *= f;
                    vel.z *= f;
                }
            }
            else
            {
                float f = Mathf.Exp(-3.4f * dt);
                vel.x *= f;
                vel.z *= f;
            }

            // ---- jump / mantle
            if (canMove && !downed && InputMap.Pressed(Bind.Jump))
            {
                var fwd = Forward;
                float ledge = _world.LedgeAt(pos.x + fwd.x * 0.7f, pos.z + fwd.z * 0.7f, pos.y, 1.75f);
                if (ledge >= 0f && ledge > pos.y + 0.3f) StartMantle(ledge);
                else if (grounded) { vel.y = JumpVel; grounded = false; sliding = false; }
            }

            // ---- integrate
            vel.y -= Gravity * dt;
            float nx = pos.x + vel.x * dt;
            float nz = pos.z + vel.z * dt;
            float height = (crouching || downed) ? CrouchHeight : StandHeight;
            _world.Collide(ref nx, ref nz, Radius, pos.y, pos.y + height);
            pos.x = nx;
            pos.z = nz;

            pos.y += vel.y * dt;
            float ground = _world.GroundHeightAt(pos.x, pos.z, pos.y, grounded ? 0.66f : 0.12f);
            if (pos.y <= ground + 0.001f)
            {
                if (!_wasGrounded && vel.y < -6f)
                {
                    AddShake(Mathf.Clamp(-vel.y * 0.02f, 0f, 0.35f));
                    Game.Audio.Play(Sfx.Step, 0.8f);
                }
                pos.y = ground;
                vel.y = 0f;
                grounded = true;
            }
            else grounded = false;
            _wasGrounded = grounded;

            if (pos.y < -40f) { pos.y = 0f; vel = Vector3.zero; }

            // ---- footsteps
            if (grounded)
            {
                _stepDist += new Vector2(vel.x, vel.z).magnitude * dt;
                float stride = sprinting ? 2.1f : (crouching ? 2.6f : 1.7f);
                if (_stepDist > stride)
                {
                    _stepDist = 0f;
                    Game.Audio.Play(Sfx.Step, sprinting ? 0.5f : 0.32f);
                }
            }

            _targetEye = downed ? 0.55f : (sliding ? 0.72f : (crouching ? CrouchHeight : StandHeight));
        }

        void StartMantle(float targetY)
        {
            var f = Forward;
            _mantling = true;
            _mantleT = 0f;
            _mantleDur = 0.42f;
            _mantleFrom = pos;
            _mantleTo = new Vector3(pos.x + f.x * 1.05f, targetY + 0.02f, pos.z + f.z * 1.05f);
            vel = Vector3.zero;
            Game.Audio.Play(Sfx.Step, 0.6f);
        }

        void UpdateMantle(float dt)
        {
            _mantleT += dt;
            float t = Mathf.Clamp01(_mantleT / _mantleDur);
            // Up first, then forward — reads like a real pull-up.
            float up = Mathf.Clamp01(t * 1.55f);
            float fwd = Mathf.Clamp01((t - 0.42f) / 0.58f);
            pos.y = Mathf.Lerp(_mantleFrom.y, _mantleTo.y, up * up * (3f - 2f * up));
            pos.x = Mathf.Lerp(_mantleFrom.x, _mantleTo.x, fwd);
            pos.z = Mathf.Lerp(_mantleFrom.z, _mantleTo.z, fwd);
            _viewRoll = Mathf.Sin(t * Mathf.PI) * 0.06f;
            if (t >= 1f) { _mantling = false; grounded = true; }
        }

        void Vitals(float dt)
        {
            if (downed)
            {
                bleedOut -= dt;
                if (bleedOut <= 0f)
                {
                    if (selfRevives > 0)
                    {
                        selfRevives--;
                        ReviveSelf();
                        Game.Audio.Play(Sfx.Perk, 0.8f);
                    }
                    else Die();
                }
                return;
            }
            sinceDamage += dt;
            if (sinceDamage > RegenDelay && health < maxHealth)
                health = Mathf.Min(maxHealth, health + RegenRate * dt);

            for (int i = damageFlashes.Count - 1; i >= 0; i--)
            {
                var f = damageFlashes[i];
                f.t -= dt;
                if (f.t <= 0f) damageFlashes.RemoveAt(i);
                else damageFlashes[i] = f;
            }
        }

        void UpdateCamera(float dt, bool adsing)
        {
            eyeHeight = Util.Damp(eyeHeight, _targetEye, 12f, dt);

            _recoil.x = Util.Damp(_recoil.x, 0f, 9f, dt);
            _recoil.y = Util.Damp(_recoil.y, 0f, 9f, dt);

            float speed = new Vector2(vel.x, vel.z).magnitude;
            float target = grounded ? Mathf.Clamp01(speed / SprintSpeed) * (adsing ? 0.35f : 1f) : 0f;
            _bobAmount = Util.Damp(_bobAmount, target, 8f, dt);
            _bob += dt * (sprinting ? 13.5f : 9.5f) * Mathf.Clamp(speed / 3f, 0f, 1.5f);

            float bobY = Mathf.Sin(_bob * 2f) * 0.035f * _bobAmount;
            float bobX = Mathf.Cos(_bob) * 0.05f * _bobAmount;
            float roll = Mathf.Cos(_bob) * 0.012f * _bobAmount;

            shake = Mathf.Max(0f, shake - 5.5f * dt * (0.5f + shake));
            float sh = shake * shake;
            float shx = (Random.value - 0.5f) * sh * 0.12f;
            float shy = (Random.value - 0.5f) * sh * 0.12f;
            float shr = (Random.value - 0.5f) * sh * 0.06f;

            _viewRoll = Util.Damp(_viewRoll, sliding ? -0.09f : 0f, 8f, dt);

            camera.position = new Vector3(
                pos.x + bobX * 0.4f + shx,
                pos.y + eyeHeight + bobY + shy,
                pos.z + shx * 0.4f);

            float pitchDeg = -(pitch + _recoil.y) * Mathf.Rad2Deg;
            float yawDeg = (yaw + _recoil.x) * Mathf.Rad2Deg;
            float rollDeg = -(roll + _viewRoll + shr + (downed ? 0.22f : 0f)) * Mathf.Rad2Deg;
            camera.rotation = Quaternion.Euler(pitchDeg, yawDeg, rollDeg);
        }

        public float TargetFov(bool adsing)
        {
            float b = GameSettings.Fov;
            return adsing ? b * 0.72f : (sprinting ? b * 1.06f : b);
        }
    }
}
