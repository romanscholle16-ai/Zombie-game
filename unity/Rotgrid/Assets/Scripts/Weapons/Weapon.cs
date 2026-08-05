using UnityEngine;

namespace Rotgrid
{
    /// <summary>Runtime state for one carried weapon.</summary>
    public class Weapon
    {
        public readonly WeaponDef baseDef;
        public WeaponDef def;
        public int upgradeLevel;

        public int mag, reserve;
        public float cooldown;
        public bool reloading;
        public float reloadT, reloadTotal;
        public float burstCooldown;
        public float heat;
        bool _stage1, _stage2;

        public Weapon(WeaponDef d, int level = 0)
        {
            baseDef = d;
            upgradeLevel = level;
            def = Weapons.Upgraded(d, level);
            mag = def.mag;
            reserve = def.reserve;
        }

        public string Id { get { return baseDef.id; } }
        public string Name { get { return def.name; } }
        public bool IsEmpty { get { return mag <= 0; } }
        public bool CanReload { get { return !reloading && mag < def.mag && reserve > 0; } }
        public float FireInterval { get { return def.FireInterval; } }
        public int NextUpgradeCost { get { return Weapons.UpgradeCost(upgradeLevel); } }
        public float ReloadProgress { get { return reloading ? Mathf.Clamp01(1f - reloadT / Mathf.Max(0.01f, reloadTotal)) : 1f; } }

        public bool Upgrade()
        {
            if (upgradeLevel >= Weapons.Tiers.Length) return false;
            upgradeLevel++;
            def = Weapons.Upgraded(baseDef, upgradeLevel);
            mag = def.mag;
            reserve = def.reserve;
            reloading = false;
            return true;
        }

        public void RefillAmmo()
        {
            reserve = def.reserve;
            mag = def.mag;
            reloading = false;
        }

        /// <summary>True when the trigger state allows a shot this frame.</summary>
        public bool WantsToFire(bool held, bool pressed)
        {
            if (reloading || cooldown > 0f) return false;
            if (def.fireMode == FireMode.Auto) return held;
            if (def.fireMode == FireMode.Burst) return pressed && burstCooldown <= 0f;
            return pressed;
        }

        /// <summary>Consumes a round. Returns false when the magazine is dry.</summary>
        public bool Consume()
        {
            if (mag <= 0) return false;
            mag--;
            cooldown = FireInterval;
            heat = Mathf.Min(1f, heat + 0.16f);
            return true;
        }

        public bool StartReload(float speedMult)
        {
            if (!CanReload) return false;
            reloading = true;
            bool empty = mag <= 0;
            reloadTotal = (empty ? def.emptyReloadTime : def.reloadTime) / speedMult;
            reloadT = reloadTotal;
            _stage1 = _stage2 = false;
            Game.Audio.Play(Sfx.Reload1, 0.9f);
            return true;
        }

        public void CancelReload()
        {
            reloading = false;
            reloadT = 0f;
        }

        public void Update(float dt, float speedMult)
        {
            cooldown = Mathf.Max(0f, cooldown - dt);
            burstCooldown = Mathf.Max(0f, burstCooldown - dt);
            heat = Mathf.Max(0f, heat - dt * 1.6f);

            if (!reloading) return;
            reloadT -= dt;

            if (def.shellReload)
            {
                // Shotgun-style: one shell per cycle, interruptible.
                if (reloadT <= 0f)
                {
                    mag++;
                    reserve--;
                    Game.Audio.Play(Sfx.Reload2, 0.8f);
                    if (mag >= def.mag || reserve <= 0) reloading = false;
                    else
                    {
                        reloadTotal = def.reloadTime / speedMult;
                        reloadT = reloadTotal;
                    }
                }
                return;
            }

            // Magazine reload: two audible stages then the round transfer.
            float p = ReloadProgress;
            if (p > 0.45f && !_stage1) { _stage1 = true; Game.Audio.Play(Sfx.Reload2, 0.85f); }
            if (p > 0.85f && !_stage2) { _stage2 = true; Game.Audio.Play(Sfx.Reload3, 0.7f); }
            if (reloadT <= 0f)
            {
                int need = def.mag - mag;
                int take = Mathf.Min(need, reserve);
                mag += take;
                reserve -= take;
                reloading = false;
                _stage1 = _stage2 = false;
            }
        }
    }
}
