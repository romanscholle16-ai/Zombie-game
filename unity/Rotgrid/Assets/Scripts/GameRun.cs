using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>A purchasable door, wired into the interaction system.</summary>
    public class DoorInteractable : Interactable
    {
        readonly DoorObj _door;
        readonly WorldBuilder _world;
        readonly PowerSystem _power;

        public DoorInteractable(DoorObj door, WorldBuilder world, PowerSystem power)
            : base(door.id, new Vector3(door.cx, door.y + 1.2f, door.cz), 3f, 3)
        {
            _door = door;
            _world = world;
            _power = power;
        }

        public override Prompt GetPrompt(GameRun run)
        {
            if (_door.open) return Prompt.None;
            if (_door.requiresPower)
            {
                return _power.On
                    ? Prompt.Plain("OPEN " + _door.name, "GRID ONLINE")
                    : Prompt.Blocked(_door.name, "REQUIRES POWER");
            }
            return Prompt.Buy("OPEN " + _door.name, _door.cost, "");
        }

        public override bool Activate(GameRun run)
        {
            if (_door.open) return false;
            if (_door.requiresPower)
            {
                if (!_power.On) { run.Deny(); return false; }
            }
            else if (!run.player.Spend(_door.cost)) { run.Deny(); return false; }

            _world.OpenDoor(_door);
            enabled = false;
            run.Toast(_door.name + " OPEN", ToastKind.Good, false);
            return true;
        }
    }

    /// <summary>Hold-to-repair on a damaged window.</summary>
    public class RepairInteractable : Interactable
    {
        public readonly Barricade barricade;
        public float holdProgress;

        public RepairInteractable(Barricade b)
            : base("rep_" + b.id, new Vector3(b.x, b.y + 1.2f, b.z), 2.6f, 4)
        {
            barricade = b;
        }

        public override Prompt GetPrompt(GameRun run)
        {
            return Prompt.Hold("REPAIR BARRIER", barricade.BoardCount + "/6 PLANKS", holdProgress);
        }

        public override bool Activate(GameRun run) { return false; }
    }

    public struct ToastMsg
    {
        public string text;
        public ToastKind kind;
        public bool big;
        public float t;
    }

    /// <summary>
    /// One run of the survival mode: owns the world, the player and every
    /// system, and drives them all from a single tick so ordering is explicit.
    /// </summary>
    public class GameRun
    {
        public readonly Game app;
        public readonly WorldBuilder world;
        public readonly PlayerController player;
        public readonly ZombieManager zombies;
        public readonly WeaponSystem weapons;
        public readonly Effects fx;
        public readonly InteractionSystem interaction;
        public readonly PowerSystem power;
        public readonly PerkSystem perks;
        public readonly ShopSystem shop;
        public readonly MysteryBox box;
        public readonly UpgradeMachine pap;
        public readonly TrapSystem traps;
        public readonly PowerUpSystem drops;
        public readonly RoundManager rounds;
        public readonly ViewModel viewModel;
        public readonly Camera camera;

        public bool over, paused;
        public float elapsed, time;
        public int roundsSurvived;

        public readonly List<ToastMsg> toasts = new List<ToastMsg>();
        public float hitmarkerT, hitmarkerCrit, hitmarkerKill;
        public float bannerT;
        public string bannerText = "";
        public Prompt prompt;

        readonly List<RepairInteractable> _repairItems = new List<RepairInteractable>();
        readonly ZombieContext _ctx = new ZombieContext();
        readonly GameObject _root;
        Light _flashlight;
        bool _flashlightOn = true;
        float _heartbeatT;
        public RunSummary summary;

        public GameRun(Game application, Camera worldCamera, int vmLayer)
        {
            app = application;
            camera = worldCamera;
            _root = new GameObject("Run");

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = MapData.FogColor;
            RenderSettings.fogDensity = GameSettings.Preset.fogDensity;

            world = new WorldBuilder();
            fx = new Effects(worldCamera.transform);
            player = new PlayerController(world, worldCamera.transform);
            player.SpawnAt(MapData.SpawnPos, MapData.SpawnYaw);
            zombies = new ZombieManager(world);
            interaction = new InteractionSystem();
            viewModel = new ViewModel(worldCamera, vmLayer);

            power = new PowerSystem(world, interaction, _root.transform);
            perks = new PerkSystem(world, power, interaction, _root.transform);
            shop = new ShopSystem(interaction, _root.transform);
            box = new MysteryBox(world, interaction, _root.transform);
            pap = new UpgradeMachine(world, power, interaction, _root.transform);
            traps = new TrapSystem(world, power, interaction, _root.transform);
            drops = new PowerUpSystem(_root.transform);
            rounds = new RoundManager(world, zombies);
            weapons = new WeaponSystem(this, viewModel, vmLayer);

            foreach (var d in world.doors) interaction.Add(new DoorInteractable(d, world, power));
            foreach (var b in world.barricades)
            {
                var r = new RepairInteractable(b);
                interaction.Add(r);
                _repairItems.Add(r);
            }

            BuildFlashlight();
            WireEvents();

            rounds.StartRun();
            Game.Audio.StartAmbient();
        }

        // ================================================================ setup
        /// <summary>Helmet lamp — the annex is unlit until the grid comes back up.</summary>
        void BuildFlashlight()
        {
            var go = new GameObject("flashlight");
            go.transform.SetParent(camera.transform, false);
            _flashlight = go.AddComponent<Light>();
            _flashlight.type = LightType.Spot;
            _flashlight.color = Util.Hex(0xfff0d8);
            _flashlight.range = 40f;
            _flashlight.spotAngle = 70f;
            _flashlight.intensity = 4f;
            _flashlight.shadows = LightShadows.None;
        }

        void WireEvents()
        {
            rounds.OnRoundStart = (r, special) =>
            {
                bannerText = special == "boss" ? "ROUND " + r + " - ABOMINATION"
                    : special == "horde" ? "ROUND " + r + " - HORDE"
                    : "ROUND " + r;
                bannerT = 2.7f;
                player.AddShake(0.2f);
                if (special == "boss") Toast("SOMETHING LARGE IS COMING", ToastKind.Bad, true);
                if (special == "horde") Toast("THEY ARE RUNNING", ToastKind.Bad, true);
            };

            rounds.OnRoundEnd = r =>
            {
                roundsSurvived = r;
                int award = Points.RoundBase + r * Points.RoundPerLevel;
                player.AddPoints(award);
                Toast("ROUND " + r + " CLEARED  +" + award, ToastKind.Good, false);
                // Give the player a beat: rebuild any barrier nobody is at.
                foreach (var b in world.ActiveBarricades())
                    if (b.attackers.Count == 0) b.RestoreAll();
            };

            player.OnDowned = () =>
            {
                Toast("YOU ARE DOWN", ToastKind.Bad, true);
                Game.Audio.Play(Sfx.Heartbeat, 1f);
            };
            player.OnDied = GameOver;
            player.OnRevived = () => Toast("BACK ON YOUR FEET", ToastKind.Good, true);

            _ctx.OnAttack = ZombieAttack;
            _ctx.OnRemoved = z => zombies.Remove(z);
            _ctx.OnBurnKill = z => OnKill(z, false, null, false, true);
            _ctx.fx = fx;
        }

        // ================================================================ hooks
        public void Toast(string text, ToastKind kind, bool big)
        {
            toasts.Add(new ToastMsg { text = text, kind = kind, big = big, t = 1.9f });
            while (toasts.Count > 6) toasts.RemoveAt(0);
        }

        public void Deny()
        {
            Game.Audio.Play(Sfx.UiDeny, 1f);
        }

        public void Hitmarker(bool crit, bool kill)
        {
            hitmarkerT = 0.22f;
            hitmarkerCrit = crit ? 1f : 0f;
            hitmarkerKill = kill ? 1f : 0f;
            Game.Audio.Play(crit ? Sfx.HitmarkerCrit : Sfx.Hitmarker, 0.9f);
        }

        public void OnHit(Zombie z, DamageResult res)
        {
            if (res.damage <= 0f) return;
            player.AddPoints(Points.Hit);
        }

        public void OnTrapKill(Zombie z) { OnKill(z, false, null, false, true); }

        public void OnKill(Zombie z, bool headshot, Weapon weapon, bool melee, bool trap)
        {
            int b = melee ? Points.MeleeKill
                : trap ? Points.TrapKill
                : headshot ? Points.Kill + Points.HeadshotBonus
                : Points.Kill;
            player.AddPoints(Mathf.RoundToInt(b * z.pointsMult));

            player.stats.kills++;
            if (headshot) player.stats.headshots++;
            if (melee) player.stats.meleeKills++;
            if (weapon != null)
            {
                var ws = player.stats.Weapon(weapon.Id);
                ws.kills++;
                if (headshot) ws.headshots++;
            }

            rounds.OnKill();
            float groundY = world.nav.HeightAt(z.pos.x, z.pos.z);
            fx.BloodPool(z.pos, groundY);
            drops.OnKill(z.pos, groundY, rounds.round);
        }

        public void Purge()
        {
            int n = 0;
            var list = new List<Zombie>(zombies.alive);
            foreach (var z in list)
            {
                if (!z.active || z.state == ZState.Dying) continue;
                n++;
                fx.Explosion(z.pos + Vector3.up, 1.6f, Util.Hex(0xe8a33d));
                z.TakeDamage(z.health * 10f + 1e6f, BodyPart.Torso, null, fx, Vector3.zero);
                player.AddPoints(50);
                player.stats.kills++;
                rounds.OnKill();
            }
            player.AddShake(0.7f);
            Game.Audio.Play(Sfx.Explosion, 1f);
            if (n > 0) rounds.ForceClear();
        }

        void ZombieAttack(Zombie z, float damage)
        {
            if (player.dead) return;
            float dx = player.pos.x - z.pos.x, dz = player.pos.z - z.pos.z;
            if (Mathf.Sqrt(dx * dx + dz * dz) > z.radius + 2.1f) return;
            player.TakeDamage(damage, z.pos);
            player.AddShake(0.22f);
        }

        // ================================================================ loop
        public void Update(float dt)
        {
            if (over)
            {
                fx.Update(dt);
                return;
            }
            time += dt;
            if (!paused) elapsed += dt;

            bool canAct = !paused && !app.MenuOpen;

            weapons.Update(dt, canAct && !player.dead);
            player.Update(dt, canAct, weapons.Ads);

            float zoom = viewModel != null ? viewModel.AdsZoom : 1.39f;
            float fov = player.TargetFov(weapons.Ads, zoom);
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, fov, Mathf.Clamp01(dt * (zoom > 3f ? 15f : 11f)));

            world.Update(dt, time);
            perks.Update(dt, time);
            shop.Update(time);
            power.Update(dt);
            box.Update(dt, time, rounds.round, this);
            pap.Update(dt, time, this);
            traps.Update(dt, time, this);
            drops.Update(dt, time, this);

            if (!player.dead) rounds.Update(dt, player.pos);

            _ctx.playerPos = player.pos;
            _ctx.playerDead = player.dead;
            _ctx.time = time;
            zombies.Update(dt, _ctx);

            for (int i = 0; i < _repairItems.Count; i++)
            {
                var it = _repairItems[i];
                it.enabled = it.barricade.BoardCount < 6 && world.IsZoneActive(it.barricade.zone);
            }
            if (canAct) UpdateInteraction(dt);
            else prompt = Prompt.None;

            viewModel.Update(dt, weapons.Ads,
                new Vector2(player.vel.x, player.vel.z).magnitude / 5f,
                player.sprinting, weapons.Current.reloading, player.lastLook,
                player.downed, weapons.Current);

            UpdateFlashlight(dt);
            fx.Update(dt);
            UpdateAmbience(dt);
            UpdateHudTimers(dt);
        }

        void UpdateFlashlight(float dt)
        {
            if (InputMap.Pressed(Bind.Flashlight))
            {
                _flashlightOn = !_flashlightOn;
                Game.Audio.Play(Sfx.UiTick, 1f);
            }
            float want = _flashlightOn ? (power.On ? 2.4f : 5f) : 0f;
            _flashlight.intensity = Mathf.Lerp(_flashlight.intensity, want, Mathf.Clamp01(dt * 8f));
        }

        void UpdateAmbience(float dt)
        {
            float hp = player.health / player.maxHealth;
            if ((hp < 0.32f && !player.dead) || player.downed)
            {
                _heartbeatT -= dt;
                if (_heartbeatT <= 0f)
                {
                    _heartbeatT = player.downed ? 1f : 0.6f + hp * 1.6f;
                    Game.Audio.Play(Sfx.Heartbeat, player.downed ? 0.8f : 0.55f);
                }
            }
        }

        void UpdateHudTimers(float dt)
        {
            if (hitmarkerT > 0f) hitmarkerT -= dt;
            if (bannerT > 0f) bannerT -= dt;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                t.t -= dt;
                if (t.t <= 0f) toasts.RemoveAt(i);
                else toasts[i] = t;
            }
        }

        void UpdateInteraction(float dt)
        {
            var best = interaction.FindBest(player.pos, player.Forward);
            if (best == null) { prompt = Prompt.None; return; }

            var repair = best as RepairInteractable;
            if (repair != null)
            {
                var b = repair.barricade;
                bool inside = b.IsInside(player.pos.x, player.pos.z);
                if (b.BoardCount >= 6 || !inside || !world.IsZoneActive(b.zone))
                {
                    repair.holdProgress = 0f;
                    prompt = Prompt.None;
                    return;
                }
                if (InputMap.Down(Bind.Interact) && !player.downed)
                {
                    repair.holdProgress += dt / 0.55f;
                    if (repair.holdProgress >= 1f)
                    {
                        repair.holdProgress = 0f;
                        int pts = b.Repair(fx);
                        if (pts > 0)
                        {
                            player.AddPoints(pts);
                            player.stats.repairs++;
                        }
                    }
                }
                else repair.holdProgress = Mathf.Max(0f, repair.holdProgress - dt * 1.6f);

                prompt = Prompt.Hold("REPAIR BARRIER", b.BoardCount + "/6 PLANKS", repair.holdProgress);
                return;
            }

            var p = best.GetPrompt(this);
            if (!p.valid) { prompt = Prompt.None; return; }
            if (p.hasCost && player.points < p.cost) p.blocked = true;
            prompt = p;

            if (InputMap.Pressed(Bind.Interact) && !player.downed) best.Activate(this);
        }

        // ================================================================ end
        void GameOver()
        {
            if (over) return;
            over = true;
            Game.Audio.StopAmbient();
            rounds.Stop();
            InputMap.SetCursorLocked(false);

            var s = player.stats;
            s.round = rounds.round;
            s.roundsSurvived = Mathf.Max(0, rounds.round - 1);
            s.time = elapsed;
            s.newBest = SaveData.RecordRun(s);
            summary = s;
            app.OnGameOver(s);
        }

        public void Dispose()
        {
            zombies.Clear();
            fx.Dispose();
            world.Dispose();
            interaction.Clear();
            Game.Audio.StopAmbient();
            if (_root != null) Object.Destroy(_root);
        }
    }
}
