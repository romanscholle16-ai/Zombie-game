import * as THREE from 'three';
import { World } from './world/World.js';
import { applyEnvironment } from './world/Environment.js';
import { MAP } from './world/MapData.js';
import { Player } from './entities/Player.js';
import { ZombieManager } from './entities/ZombieManager.js';
import { ViewModel } from './weapons/ViewModel.js';
import { WeaponSystem } from './weapons/WeaponSystem.js';
import { Effects } from './systems/Effects.js';
import { InteractionSystem, Interactable } from './systems/Interaction.js';
import { PowerSystem } from './systems/PowerSystem.js';
import { PerkSystem } from './systems/Perks.js';
import { ShopSystem } from './systems/Shop.js';
import { MysteryBox } from './systems/MysteryBox.js';
import { UpgradeMachine } from './systems/PackAPunch.js';
import { TrapSystem } from './systems/Traps.js';
import { PowerUpSystem } from './systems/PowerUps.js';
import { RoundManager, POINTS, PHASE } from './systems/RoundManager.js';
import { HUD } from './ui/HUD.js';
import { Input } from './core/Input.js';
import { Audio } from './core/AudioEngine.js';
import { Settings } from './core/Settings.js';
import { SaveData } from './core/SaveData.js';
import { clamp } from './core/Util.js';

/** One run of the survival mode. */
export class Game {
  constructor(app) {
    this.app = app;
    this.renderer = app.renderer;
    this.hud = new HUD();
    this.over = false;
    this.paused = false;
    this.elapsed = 0;
    this.time = 0;
    this.roundsSurvived = 0;

    // ---- scene
    this.scene = new THREE.Scene();
    const q = Settings.quality;
    this.scene.fog = new THREE.FogExp2(MAP.fogColor, q.fogDensity);
    this.scene.background = new THREE.Color(0x05070a);

    this.camera = new THREE.PerspectiveCamera(Settings.get('fov'), 16 / 9, 0.08, 260);
    applyEnvironment(this.scene, this.renderer, 0.4);

    // ---- world + entities
    this.world = new World(this.scene);
    this.fx = new Effects(this.scene);
    this.player = new Player(this.world, this.camera);
    this.player.spawnAt(MAP.spawn);
    this.zombies = new ZombieManager(this.scene, this.world);
    this.viewModel = new ViewModel(this.renderer);
    this.interaction = new InteractionSystem();

    // ---- systems
    this.power = new PowerSystem(this.world, this.scene, this.interaction);
    this.perks = new PerkSystem(this.world, this.scene, this.interaction, this.power);
    this.shop = new ShopSystem(this.world, this.scene, this.interaction);
    this.box = new MysteryBox(this.world, this.scene, this.interaction, () => this.hooks);
    this.pap = new UpgradeMachine(this.world, this.scene, this.interaction, this.power, () => this.hooks);
    this.traps = new TrapSystem(this.world, this.scene, this.interaction, this.power);
    this.drops = new PowerUpSystem(this.scene, () => this.hooks);
    this.rounds = new RoundManager(this.world, this.zombies);

    this.hooks = this._makeHooks();
    this.weapons = new WeaponSystem({
      player: this.player,
      world: this.world,
      zombies: this.zombies,
      fx: this.fx,
      viewModel: this.viewModel,
      camera: this.camera,
      hooks: this.hooks,
    });

    this._registerDoors();
    this._registerBarricades();
    this._wireEvents();

    this._buildFlashlight();

    this.hud.reset();
    this.hud.show();
    this.rounds.start();
    Audio.startAmbient();
    this.heartbeatT = 0;
  }

  // ================================================================= setup
  /** Helmet lamp — the annex is unlit until the grid comes back up. */
  _buildFlashlight() {
    this.flashlightOn = true;
    this.flashlight = new THREE.SpotLight(0xfff0d8, 240, 46, 0.8, 0.55, 1.0);
    this.flashlight.position.set(0, 0, 0);
    this.flashlight.target.position.set(0, 0, -1);
    this.scene.add(this.flashlight);
    this.scene.add(this.flashlight.target);
  }

  _updateFlashlight(dt) {
    if (Input.wasPressed('flashlight')) {
      this.flashlightOn = !this.flashlightOn;
      Audio.play('ui', { kind: 'tick' });
    }
    const cam = this.camera;
    const dir = new THREE.Vector3();
    cam.getWorldDirection(dir);
    this.flashlight.position.copy(cam.position).addScaledVector(dir, 0.15);
    this.flashlight.target.position.copy(cam.position).addScaledVector(dir, 12);
    // Brighter while the grid is down; still a useful cone afterwards.
    const want = this.flashlightOn ? (this.power.on ? 120 : 260) : 0;
    this.flashlight.intensity += (want - this.flashlight.intensity) * clamp(dt * 8, 0, 1);
  }

  _makeHooks() {
    const self = this;
    return {
      player: this.player,
      get currentWeapon() { return self.weapons.current; },
      toast: (t, kind, big) => this.hud.toast(t, kind, big),
      deny: () => { Audio.play('ui', { kind: 'deny' }); this.hud.denyFlash(); },
      hitmarker: (crit, kill) => {
        this.hud.hitmarker(crit, kill);
        Audio.play('hitmarker', { crit, volume: 0.9 });
      },
      weaponStat: (id) => {
        const s = this.player.stats.weaponStats;
        if (!s[id]) s[id] = { kills: 0, headshots: 0, shotsFired: 0, shotsHit: 0, damage: 0 };
        return s[id];
      },
      onHit: (z, res) => this._onHit(z, res),
      onKill: (z, info) => this._onKill(z, info),
      giveWeapon: (def) => this.weapons.giveWeapon(def),
      ownsWeapon: (id) => this.weapons.ownsWeapon(id),
      weaponAmmoFull: (id) => this.weapons.weaponAmmoFull(id),
      refillWeapon: (id) => this.weapons.refillWeapon(id),
      refillAllAmmo: () => this.weapons.refillAll(),
      takeWeaponForUpgrade: () => this.weapons.takeWeaponForUpgrade(),
      returnUpgraded: (w) => this.weapons.returnUpgraded(w),
      startFireSale: (s) => this.box.startFireSale(s),
      purge: () => this._purge(),
      zombies: this.zombies,
      fx: this.fx,
      onTrapKill: (z, trap) => this._onKill(z, { headshot: false, weapon: null, melee: false, trap }),
    };
  }

  _registerDoors() {
    const self = this;
    for (const door of this.world.doors) {
      const it = new (class extends Interactable {
        prompt() {
          if (door.open) return null;
          if (door.requiresPower) {
            return self.power.on
              ? { text: `OPEN ${door.name}`, sub: 'GRID ONLINE' }
              : { text: door.name, sub: 'REQUIRES POWER', blocked: true };
          }
          return { text: `OPEN ${door.name}`, cost: door.cost };
        }
        activate(ctx) {
          if (door.open) return false;
          if (door.requiresPower) {
            if (!self.power.on) { ctx.deny(); return false; }
          } else if (!ctx.player.spend(door.cost)) {
            ctx.deny();
            return false;
          }
          self.world.openDoor(door);
          this.enabled = false;
          ctx.toast(`${door.name} OPEN`, 'pts');
          return true;
        }
      })({ id: door.id, x: door.cx, y: door.y + 1.2, z: door.cz, radius: 3.0, priority: 3 });
      this.interaction.add(it);
    }
  }

  _registerBarricades() {
    const self = this;
    this.repairItems = [];
    for (const b of this.world.barricades) {
      const it = new (class extends Interactable {
        prompt(ctx) {
          if (b.isOpen === false && b.boardCount >= 6) return null;
          if (b.boardCount >= 6) return null;
          if (!self.world.isZoneActive(b.zone)) return null;
          if (!b.isInside(ctx.player.pos.x, ctx.player.pos.z)) return null;
          return { text: 'REPAIR BARRIER', sub: `${b.boardCount}/6 PLANKS`, hold: this.holdProgress ?? 0 };
        }
        activate() { return false; }
      })({ id: `rep_${b.id}`, x: b.x, y: b.y + 1.2, z: b.z, radius: 2.6, priority: 4 });
      it.repairTarget = b;
      it.holdRepeat = true;
      it.holdProgress = 0;
      this.interaction.add(it);
      this.repairItems.push(it);
    }
  }

  _wireEvents() {
    this.rounds.on('roundStart', (r, special) => {
      this.hud.showRoundBanner(r, special);
      this.player.addShake(0.2);
      if (special === 'boss') this.hud.toast('SOMETHING LARGE IS COMING', 'bad', true);
      if (special === 'horde') this.hud.toast('THEY ARE RUNNING', 'bad', true);
    });

    this.rounds.on('roundEnd', (r) => {
      this.roundsSurvived = r;
      const award = POINTS.roundBase + r * POINTS.roundPerLevel;
      this.player.addPoints(award, 'round');
      this.hud.toast(`ROUND ${r} CLEARED  +${award}`, 'pts');
      // Give the player a beat: rebuild any barrier nobody is standing at.
      for (const b of this.world.activeBarricades()) {
        if (!b.attackers.size) b.restoreAll();
      }
    });

    this.player.on('downed', () => {
      this.hud.toast('YOU ARE DOWN', 'bad', true);
      Audio.play('heartbeat', { volume: 1 });
      this.player.clearPerks();
    });

    this.player.on('died', () => this._gameOver());
    this.player.on('revived', () => this.hud.toast('BACK ON YOUR FEET', 'pts', true));
  }

  // ================================================================= scoring
  _onHit(z, res) {
    if (res.damage <= 0) return;
    const pts = this.player.addPoints(POINTS.hit, 'hit');
    void pts; void z;
  }

  _onKill(z, info) {
    const base = info.melee ? POINTS.meleeKill
      : info.trap ? POINTS.trapKill
        : info.headshot ? POINTS.kill + POINTS.headshotBonus
          : POINTS.kill;
    const award = Math.round(base * (z.pointsMult ?? 1));
    this.player.addPoints(award, 'kill');

    const st = this.player.stats;
    st.kills++;
    if (info.headshot) st.headshots++;
    if (info.melee) st.meleeKills++;
    if (info.weapon) {
      const ws = this.hooks.weaponStat(info.weapon.id);
      ws.kills++;
      if (info.headshot) ws.headshots++;
    }

    this.rounds.onKill();
    const groundY = this.world.nav.heightAt(z.pos.x, z.pos.z);
    this.fx.bloodPool(z.pos, groundY);
    this.drops.onKill(z.pos, groundY, this.rounds.round);
    if (z.barricade) z.barricade.attackers.delete(z);
  }

  _purge() {
    const list = this.zombies.alive.slice();
    let n = 0;
    for (const z of list) {
      if (z.state === 6 || !z.active) continue;
      n++;
      const groundY = this.world.nav.heightAt(z.pos.x, z.pos.z);
      this.fx.explosion({ x: z.pos.x, y: z.pos.y + 1, z: z.pos.z }, 1.6, 0xe8a33d);
      z.takeDamage(z.health * 10 + 1e6, 'torso', null, this.fx, null);
      this.player.addPoints(50, 'purge');
      this.player.stats.kills++;
      this.rounds.onKill();
      void groundY;
    }
    this.player.addShake(0.7);
    Audio.play('explosion', { volume: 1 });
    if (n === 0) return;
    this.rounds.forceClear();
  }

  // ================================================================= loop
  update(dt) {
    if (this.over) {
      this.fx.update(dt);
      return;
    }
    this.time += dt;
    if (!this.paused) this.elapsed += dt;

    const canAct = !this.paused && !this.app.screens.active;

    // ---- player + weapons
    this.weapons.update(dt, canAct && !this.player.dead);
    this.player.update(dt, {
      canMove: canAct,
      adsing: this.weapons.ads,
      adsZoom: this.viewModel.adsZoom,
    });

    // ---- audio listener
    const f = this.player.forward;
    Audio.setListener(this.player.pos, f);

    // ---- world + systems
    this.world.update(dt, this.time);
    this.perks.update(dt, this.time);
    this.shop.update(dt, this.time);
    this.power.update(dt);
    this.box.update(dt, this.time, this.rounds.round);
    this.pap.update(dt, this.time);
    this.traps.update(dt, this.time, this.hooks);
    this.drops.update(dt, this.time, this.hooks);

    // ---- rounds + zombies
    if (!this.player.dead) this.rounds.update(dt, this.player.pos);
    this.zombies.update(dt, {
      player: this.player,
      time: this.time,
      fx: this.fx,
      onAttack: (z, dmg) => this._zombieAttack(z, dmg),
      onBoardBroken: () => {},
      onBurnKill: (z) => this._onKill(z, { headshot: false, weapon: null, melee: false, trap: true }),
    });

    // ---- interaction (repair prompts only matter on damaged barriers)
    for (const it of this.repairItems) {
      const b = it.repairTarget;
      it.enabled = b.boardCount < 6 && this.world.isZoneActive(b.zone);
    }
    if (canAct) this._updateInteraction(dt);
    else this.hud.setPrompt(null);

    // ---- view model
    this.viewModel.update(dt, {
      ads: this.weapons.ads,
      moving: Math.hypot(this.player.vel.x, this.player.vel.z) / 5,
      sprinting: this.player.sprinting,
      reloading: this.weapons.current.reloading,
      lookDX: this.player.lastLook?.yaw ?? 0,
      lookDY: this.player.lastLook?.pitch ?? 0,
      downed: this.player.downed,
      weapon: this.weapons.current,
    });

    this._updateFlashlight(dt);
    this.fx.update(dt);
    this._updateAmbience(dt);

    // ---- HUD
    this.hud.update({
      player: this.player,
      weapons: this.weapons.hudState(),
      rounds: this.rounds,
      elapsed: this.elapsed,
      powerups: this.drops,
      scope: this.viewModel.scopeState(),
    });
  }

  _zombieAttack(z, dmg) {
    if (this.player.dead) return;
    const d = Math.hypot(this.player.pos.x - z.pos.x, this.player.pos.z - z.pos.z);
    if (d > z.radius + 2.1) return;
    this.player.takeDamage(dmg, z.pos);
    this.player.addShake(0.22);
  }

  _updateAmbience(dt) {
    // Heartbeat when badly hurt, and a low-health rasp while downed.
    const hp = this.player.health / this.player.maxHealth;
    if ((hp < 0.32 && !this.player.dead) || this.player.downed) {
      this.heartbeatT -= dt;
      if (this.heartbeatT <= 0) {
        this.heartbeatT = this.player.downed ? 1.0 : 0.6 + hp * 1.6;
        Audio.play('heartbeat', { volume: this.player.downed ? 0.8 : 0.55 });
      }
    }
  }

  _updateInteraction(dt) {
    const forward = this.player.forward;
    const best = this.interaction.findBest(this.player, forward);
    if (!best) {
      this.hud.setPrompt(null);
      this._heldItem = null;
      return;
    }

    // Barricade repair: hold to renail planks one at a time.
    if (best.holdRepeat) {
      const b = best.repairTarget;
      const inside = b.isInside(this.player.pos.x, this.player.pos.z);
      if (b.boardCount >= 6 || !inside || !this.world.isZoneActive(b.zone)) {
        best.holdProgress = 0;
        this.hud.setPrompt(null);
        return;
      }
      if (Input.isDown('interact') && !this.player.downed) {
        best.holdProgress += dt / 0.55;
        if (best.holdProgress >= 1) {
          best.holdProgress = 0;
          const pts = b.repair(this.fx);
          if (pts) {
            this.player.addPoints(pts, 'repair');
            this.player.stats.repairs++;
          }
        }
      } else {
        best.holdProgress = Math.max(0, best.holdProgress - dt * 1.6);
      }
      this.hud.setPrompt({
        text: 'REPAIR BARRIER', sub: `${b.boardCount}/6 PLANKS`, hold: best.holdProgress,
      });
      return;
    }

    const p = best.prompt(this.hooks);
    if (!p) { this.hud.setPrompt(null); return; }
    if (p.cost !== undefined && p.cost !== null) {
      p.blocked = p.blocked || this.player.points < p.cost;
    }
    this.hud.setPrompt(p);

    if (Input.wasPressed('interact') && !this.player.downed) {
      best.activate(this.hooks);
    }
  }

  render() {
    this.renderer.render(this.scene, this.camera);
    this.viewModel.render(this.renderer);
  }

  resize(w, h) {
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.viewModel.resize(w / h);
  }

  // ================================================================= end
  _gameOver() {
    if (this.over) return;
    this.over = true;
    this.hud.hide();
    Audio.stopAmbient();
    Input.releaseLock();
    this.rounds.stop();

    const st = this.player.stats;
    const summary = {
      round: this.rounds.round,
      roundsSurvived: Math.max(0, this.rounds.round - 1),
      kills: st.kills,
      headshots: st.headshots,
      meleeKills: st.meleeKills,
      repairs: st.repairs,
      time: this.elapsed,
      pointsEarned: st.pointsEarned,
      shotsFired: st.shotsFired,
      shotsHit: st.shotsHit,
      downs: st.downs,
      revives: st.revives,
      boxSpins: st.boxSpins,
      perksBought: st.perksBought,
      weaponStats: st.weaponStats,
    };
    summary.newBest = SaveData.recordRun(summary);
    this.app.onGameOver(summary);
  }

  dispose() {
    this.hud.hide();
    this.zombies.clear();
    this.fx.reset();
    this.world.dispose();
    this.interaction.clear();
    Audio.stopAmbient();
  }
}

export { PHASE, clamp };
