using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Owns the two carried weapons plus the knife, resolves shots against
    /// zombie hitboxes and the world, and reports hits back for scoring.
    /// </summary>
    public class WeaponSystem
    {
        const float SwapTime = 0.55f;

        readonly GameRun _run;
        readonly ViewModel _vm;
        readonly int _vmLayer;
        readonly Weapon[] _slots = new Weapon[2];
        int _index;

        float _swapT, _meleeCd, _meleeActive;
        bool _wasTriggerDown;
        public bool adsHeld;
        readonly List<float> _burstQueue = new List<float>();

        public WeaponSystem(GameRun run, ViewModel vm, int vmLayer)
        {
            _run = run;
            _vm = vm;
            _vmLayer = vmLayer;
            _slots[0] = new Weapon(Weapons.All["sidearm"]);
            _vm.SetWeapon(Current.def, _vmLayer);
        }

        public Weapon Current { get { return _slots[_index]; } }
        public Weapon Other { get { return _slots[_index == 0 ? 1 : 0]; } }
        public bool Busy { get { return _swapT > 0f || _meleeActive > 0f; } }

        public bool Ads
        {
            get { return adsHeld && !Busy && !Current.reloading && !_run.player.downed; }
        }

        float ReloadSpeedMult() { return _run.player.HasPerk("rapid") ? 2f : 1f; }
        float SwapSpeedMult() { return _run.player.HasPerk("swift") ? 1.35f : 1f; }

        // ------------------------------------------------------------ inventory
        public bool Owns(string id)
        {
            foreach (var w in _slots) if (w != null && w.Id == id) return true;
            return false;
        }

        public bool AmmoFull(string id)
        {
            foreach (var w in _slots)
                if (w != null && w.Id == id) return w.reserve >= w.def.reserve && w.mag >= w.def.mag;
            return false;
        }

        public void RefillWeapon(string id)
        {
            foreach (var w in _slots) if (w != null && w.Id == id) w.RefillAmmo();
        }

        public void RefillAll()
        {
            foreach (var w in _slots) if (w != null) w.RefillAmmo();
        }

        public Weapon GiveWeapon(WeaponDef def)
        {
            for (int i = 0; i < 2; i++)
            {
                if (_slots[i] != null && _slots[i].Id == def.id)
                {
                    _slots[i].RefillAmmo();
                    _run.Toast("AMMO REFILLED", ToastKind.Good, false);
                    return _slots[i];
                }
            }
            int target = _slots[1] == null ? 1 : _index;
            var w = new Weapon(def);
            _slots[target] = w;
            SwitchTo(target, true);
            _run.Toast(def.name, ToastKind.Good, false);
            return w;
        }

        /// <summary>Hands the active weapon to the refit machine.</summary>
        public Weapon TakeForUpgrade()
        {
            var w = Current;
            var stub = new Weapon(Weapons.All["sidearm"]);
            stub.mag = 0;
            stub.reserve = 0;
            _slots[_index] = stub;
            _vm.SetWeapon(stub.def, _vmLayer);
            return w;
        }

        public void ReturnUpgraded(Weapon weapon)
        {
            if (weapon == null) return;
            _slots[_index] = weapon;
            _vm.SetWeapon(weapon.def, _vmLayer);
            _swapT = 0.3f;
            _run.Toast(weapon.Name, ToastKind.Good, true);
        }

        void SwitchTo(int i, bool immediate)
        {
            if (i < 0 || i > 1 || _slots[i] == null) return;
            if (i == _index && !immediate) return;
            if (Current != null) Current.CancelReload();
            _index = i;
            _swapT = SwapTime / SwapSpeedMult();
            _vm.SetWeapon(_slots[i].def, _vmLayer);
        }

        public void Swap()
        {
            int next = _index == 0 ? 1 : 0;
            if (_slots[next] == null) return;
            SwitchTo(next, false);
            Game.Audio.Play(Sfx.Reload3, 0.5f);
        }

        // ------------------------------------------------------------ update
        public void Update(float dt, bool canAct)
        {
            _swapT = Mathf.Max(0f, _swapT - dt);
            _meleeCd = Mathf.Max(0f, _meleeCd - dt);
            if (_meleeActive > 0f)
            {
                _meleeActive -= dt;
                if (_meleeActive <= 0f) _vm.ShowKnife(false);
            }

            var w = Current;
            w.Update(dt, ReloadSpeedMult());

            // Queued burst shots.
            for (int i = _burstQueue.Count - 1; i >= 0; i--)
            {
                float t = _burstQueue[i] - dt;
                if (t <= 0f)
                {
                    _burstQueue.RemoveAt(i);
                    if (!Busy && !w.reloading && w.mag > 0) FireShot(w);
                }
                else _burstQueue[i] = t;
            }

            if (!canAct)
            {
                adsHeld = false;
                _wasTriggerDown = false;
                return;
            }

            adsHeld = InputMap.Down(Bind.Ads) && !_run.player.sprinting;

            if (InputMap.Pressed(Bind.Swap)) Swap();
            if (InputMap.Pressed(Bind.Slot1)) SwitchTo(0, false);
            if (InputMap.Pressed(Bind.Slot2)) SwitchTo(1, false);
            if (InputMap.Pressed(Bind.Reload)) w.StartReload(ReloadSpeedMult());
            if (InputMap.Pressed(Bind.Melee)) Melee();

            bool held = InputMap.Down(Bind.Fire);
            bool pressed = held && !_wasTriggerDown;

            if (Busy || _run.player.downed) { _wasTriggerDown = held; return; }

            // Firing a shotgun mid-reload interrupts it, like the real thing.
            if (w.reloading && w.def.shellReload && held && w.mag > 0) w.CancelReload();

            if (w.WantsToFire(held, pressed))
            {
                if (w.mag > 0)
                {
                    FireShot(w);
                    if (w.def.fireMode == FireMode.Burst) QueueBurst(w);
                }
                else if (pressed)
                {
                    Game.Audio.Play(Sfx.DryFire, 0.8f);
                    w.StartReload(ReloadSpeedMult());
                }
            }

            // Auto-reload once the magazine runs dry and the trigger is released.
            if (w.mag == 0 && !w.reloading && w.CanReload && !held) w.StartReload(ReloadSpeedMult());

            _wasTriggerDown = held;
        }

        void QueueBurst(Weapon w)
        {
            int n = w.def.burst - 1;
            for (int i = 1; i <= n; i++) _burstQueue.Add(w.FireInterval * i);
            w.burstCooldown = w.FireInterval * n + w.def.burstDelay;
        }

        // ------------------------------------------------------------ firing
        public void FireShot(Weapon w)
        {
            if (!w.Consume()) return;
            var player = _run.player;
            var def = w.def;

            player.stats.shotsFired++;
            player.stats.Weapon(w.Id).shotsFired++;

            Game.Audio.Play(SoundFor(def.sound), 1f);
            _vm.FireKick(def);
            player.AddShake(Mathf.Clamp(def.recoilV * 0.012f, 0.02f, 0.14f));

            // Recoil pattern: vertical climb plus alternating horizontal drift.
            float heat = 0.55f + w.heat * 0.9f;
            float adsFactor = Ads ? 0.62f : 1f;
            player.AddRecoil(Util.Rand(-def.recoilH, def.recoilH) * 0.006f * heat * adsFactor,
                             def.recoilV * 0.007f * heat * adsFactor);

            float spread = CurrentSpread(w);
            Vector3 origin = player.camera.position;
            Vector3 baseDir = player.camera.forward;
            Vector3 muzzle = _vm.MuzzleWorld();

            var hitThisShot = new HashSet<Zombie>();
            bool anyHit = false, anyCrit = false;
            int killed = 0;

            for (int p = 0; p < Mathf.Max(1, def.pellets); p++)
            {
                Vector3 dir = baseDir;
                if (spread > 0f)
                {
                    dir += new Vector3(Util.Rand(-spread, spread), Util.Rand(-spread, spread), Util.Rand(-spread, spread));
                    dir = dir.normalized;
                }
                bool hit, crit;
                int k;
                ResolvePellet(origin, dir, w, muzzle, hitThisShot, out hit, out crit, out k);
                anyHit |= hit;
                anyCrit |= crit;
                killed += k;
            }

            if (anyHit)
            {
                player.stats.shotsHit++;
                player.stats.Weapon(w.Id).shotsHit++;
                _run.Hitmarker(anyCrit, killed > 0);
            }
        }

        static Sfx SoundFor(SoundWeight s)
        {
            switch (s)
            {
                case SoundWeight.Light: return Sfx.ShootLight;
                case SoundWeight.Heavy: return Sfx.ShootHeavy;
                case SoundWeight.Shotgun: return Sfx.ShootShotgun;
                case SoundWeight.Energy: return Sfx.ShootEnergy;
                default: return Sfx.ShootMedium;
            }
        }

        float CurrentSpread(Weapon w)
        {
            var def = w.def;
            var player = _run.player;
            float s = Ads ? def.spreadAds : def.spreadHip;
            float moving = new Vector2(player.vel.x, player.vel.z).magnitude;
            s += def.spreadMove * Mathf.Clamp01(moving / 6f) * (Ads ? 0.35f : 1f);
            if (!player.grounded) s *= 1.6f;
            if (player.crouching) s *= 0.7f;
            s *= 1f + w.heat * 0.55f;
            return s;
        }

        void ResolvePellet(Vector3 origin, Vector3 dir, Weapon w, Vector3 muzzle,
                           HashSet<Zombie> alreadyHit, out bool anyHit, out bool crit, out int killed)
        {
            anyHit = false;
            crit = false;
            killed = 0;

            var def = w.def;
            var wallHit = _run.world.Raycast(origin, dir, def.range);
            float maxDist = wallHit.hit ? wallHit.dist : def.range;

            var hits = Physics.RaycastAll(origin, dir, maxDist);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int budget = Mathf.Max(1, def.penetration);
            Vector3 lastPoint = Vector3.zero;
            bool hasLast = false;

            foreach (var h in hits)
            {
                if (budget <= 0) break;
                var hb = h.collider != null ? h.collider.GetComponent<ZombieHitbox>() : null;
                if (hb == null || hb.zombie == null || !hb.zombie.active) continue;
                var z = hb.zombie;
                if (alreadyHit.Contains(z)) continue;
                alreadyHit.Add(z);
                budget--;
                anyHit = true;
                lastPoint = h.point;
                hasLast = true;

                float dmg = _run.player.instantKill ? z.health * 4f + 10000f : DamageAt(def, h.distance);
                var res = z.TakeDamage(dmg, hb.part, def, _run.fx, dir);
                _run.player.stats.Weapon(w.Id).damage += Mathf.RoundToInt(res.damage);
                Game.Audio.PlayAt(res.headshot ? Sfx.HitCrit : Sfx.Hit, h.point, 0.8f);
                if (res.crit) crit = true;
                if (res.killed) { killed++; _run.OnKill(z, res.headshot, w, false, false); }
                else _run.OnHit(z, res);

                if (def.special == SpecialEffect.Chain) Chain(z, w, dmg);
                if (def.special == SpecialEffect.Burn) z.ApplyBurn(def.damage * 0.12f, 3f);
            }

            if (def.special == SpecialEffect.Blast)
            {
                Vector3 p = hasLast ? lastPoint : (wallHit.hit ? wallHit.point : origin + dir * def.range);
                Blast(p, def, w);
            }

            if (!anyHit && wallHit.hit)
            {
                _run.fx.ImpactPuff(wallHit.point, wallHit.normal);
                Game.Audio.PlayAt(Sfx.Ricochet, wallHit.point, 0.5f);
            }

            Vector3 end = hasLast ? lastPoint : (wallHit.hit ? wallHit.point : origin + dir * def.range);
            if (Random.value < 0.55f || def.pellets > 1)
                _run.fx.Tracer(muzzle, end, def.accent == 0x66d9ff ? Util.Hex(0x9fe6ff) : Util.Hex(0xffd9a0));
        }

        static float DamageAt(WeaponDef def, float dist)
        {
            float t = Mathf.Clamp01(dist / def.range);
            return def.damage * (1f - t * def.falloff);
        }

        void Chain(Zombie source, Weapon w, float baseDamage)
        {
            var def = w.def;
            var visited = new HashSet<Zombie> { source };
            var current = source;
            float dmg = baseDamage * def.chainFalloff;

            for (int i = 0; i < def.chainTargets; i++)
            {
                Zombie next = null;
                foreach (var z in _run.zombies.Near(current.pos.x, current.pos.z, def.chainRadius))
                {
                    if (visited.Contains(z)) continue;
                    next = z;
                    break;
                }
                if (next == null) break;
                visited.Add(next);
                _run.fx.Arc(current.pos + Vector3.up * 1.2f, next.pos + Vector3.up * 1.2f, Util.Hex(0x66d9ff));
                float d = _run.player.instantKill ? next.health * 4f + 10000f : dmg;
                var res = next.TakeDamage(d, BodyPart.Torso, def, _run.fx, Vector3.zero);
                if (res.killed) _run.OnKill(next, false, w, false, false);
                else _run.OnHit(next, res);
                dmg *= def.chainFalloff;
                current = next;
            }
        }

        void Blast(Vector3 point, WeaponDef def, Weapon w)
        {
            float radius = def.blastRadius;
            float damage = def.blastDamage > 0f ? def.blastDamage : def.damage;
            _run.fx.Explosion(point, radius, def.Accent);
            Game.Audio.PlayAt(Sfx.Explosion, point, 1f);
            _run.player.AddShake(0.28f);

            foreach (var z in _run.zombies.Near(point.x, point.z, radius))
            {
                float d = Mathf.Sqrt((z.pos.x - point.x) * (z.pos.x - point.x) + (z.pos.z - point.z) * (z.pos.z - point.z));
                float falloff = Mathf.Clamp01(1f - d / radius);
                float dmg = _run.player.instantKill ? z.health * 4f + 10000f : damage * falloff;
                var res = z.TakeDamage(dmg, BodyPart.Torso, def, _run.fx, Vector3.zero);
                if (res.killed) _run.OnKill(z, false, w, false, false);
                else _run.OnHit(z, res);
            }
        }

        // ------------------------------------------------------------ melee
        public void Melee()
        {
            if (_meleeCd > 0f || _run.player.downed) return;
            _meleeCd = 0.62f;
            _meleeActive = 0.42f;
            _vm.ShowKnife(true);
            _vm.StartMelee();
            Game.Audio.Play(Sfx.Melee, 0.8f);

            var cam = _run.player.camera;
            var hits = Physics.RaycastAll(cam.position, cam.forward, Weapons.Knife.range);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Zombie target = null;
            BodyPart part = BodyPart.Torso;
            Vector3 point = cam.position + cam.forward;
            foreach (var h in hits)
            {
                var hb = h.collider != null ? h.collider.GetComponent<ZombieHitbox>() : null;
                if (hb == null || hb.zombie == null || !hb.zombie.active) continue;
                target = hb.zombie;
                part = hb.part;
                point = h.point;
                break;
            }
            if (target == null)
            {
                // Slight forgiveness: sweep for anything very close in front.
                var near = _run.zombies.Near(_run.player.pos.x + cam.forward.x * 1.2f,
                                             _run.player.pos.z + cam.forward.z * 1.2f, 1.5f);
                if (near.Count > 0) { target = near[0]; point = target.pos + Vector3.up; }
            }
            if (target == null) return;

            float dmg = _run.player.instantKill
                ? target.health * 4f + 10000f
                : Weapons.Knife.damage * (1f + _run.player.PerkCount * 0.05f);
            var res = target.TakeDamage(dmg, part, Weapons.Knife, _run.fx, cam.forward);
            Game.Audio.PlayAt(Sfx.Hit, point, 0.9f);
            _run.Hitmarker(res.headshot, res.killed);
            if (res.killed) _run.OnKill(target, res.headshot, null, true, false);
            else _run.OnHit(target, res);
        }
    }
}
