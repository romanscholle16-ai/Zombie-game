import * as THREE from 'three';
import { Weapon } from './Weapon.js';
import { WEAPONS, KNIFE } from './WeaponDefs.js';
import { voiceFor } from './WeaponVoices.js';
import { Input } from '../core/Input.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp, rand } from '../core/Util.js';

const SWAP_TIME = 0.55;

/**
 * Owns the two carried weapons plus the knife, resolves shots against zombie
 * hitboxes and the world, and reports hits back to the game for scoring.
 */
export class WeaponSystem {
  constructor(ctx) {
    this.ctx = ctx;                 // { player, world, zombies, fx, viewModel, camera, hooks }
    this.slots = [new Weapon(WEAPONS.sidearm), null];
    this.index = 0;
    this.swapT = 0;
    this.pendingIndex = null;
    this.meleeCd = 0;
    this.meleeActive = 0;
    this.raycaster = new THREE.Raycaster();
    this.raycaster.far = 220;
    this.wasTriggerDown = false;
    this.adsHeld = false;
    this._tmpDir = new THREE.Vector3();
    this._tmpOrigin = new THREE.Vector3();
    this.ctx.viewModel.setWeapon(this.current.def);
  }

  get current() { return this.slots[this.index]; }
  get other() { return this.slots[this.index === 0 ? 1 : 0]; }
  get busy() { return this.swapT > 0 || this.meleeActive > 0; }
  get ads() {
    return this.adsHeld && !this.busy && !this.current.reloading && !this.ctx.player.downed;
  }

  reloadSpeedMult() { return this.ctx.player.perks.has('rapid') ? 2.0 : 1; }
  swapSpeedMult() { return this.ctx.player.perks.has('swift') ? 1.35 : 1; }

  // ------------------------------------------------------------- inventory
  ownsWeapon(id) { return this.slots.some((w) => w && w.id === id); }

  weaponAmmoFull(id) {
    const w = this.slots.find((s) => s && s.id === id);
    return w ? w.reserve >= w.def.reserve && w.mag >= w.def.mag : false;
  }

  refillWeapon(id) {
    const w = this.slots.find((s) => s && s.id === id);
    if (w) w.refillAmmo();
  }

  refillAll() {
    for (const w of this.slots) if (w) w.refillAmmo();
  }

  giveWeapon(def) {
    const existing = this.slots.findIndex((w) => w && w.id === def.id);
    if (existing >= 0) {
      this.slots[existing].refillAmmo();
      this.ctx.hooks.toast('AMMO REFILLED', 'pts');
      return this.slots[existing];
    }
    const empty = this.slots.indexOf(null);
    const target = empty >= 0 ? empty : this.index;
    const w = new Weapon(def);
    this.slots[target] = w;
    this._switchTo(target, true);
    this.ctx.hooks.toast(def.name, 'pts');
    return w;
  }

  /** Hands the active weapon over to the refit machine. */
  takeWeaponForUpgrade() {
    const w = this.current;
    this.slots[this.index] = new Weapon(WEAPONS.sidearm);
    this.slots[this.index].mag = 0;
    this.slots[this.index].reserve = 0;
    this.ctx.viewModel.setWeapon(this.current.def);
    return w;
  }

  returnUpgraded(weapon) {
    if (!weapon) return;
    this.slots[this.index] = weapon;
    this.ctx.viewModel.setWeapon(weapon.def);
    this.swapT = 0.3;
    this.ctx.hooks.toast(weapon.name, 'pts', true);
  }

  _switchTo(i, immediate = false) {
    if (i === this.index && !immediate) return;
    if (!this.slots[i]) return;
    this.current?.cancelReload();
    this.index = i;
    this.swapT = SWAP_TIME / this.swapSpeedMult();
    this.ctx.viewModel.setWeapon(this.slots[i].def);
  }

  swap() {
    const next = this.index === 0 ? 1 : 0;
    if (!this.slots[next]) return;
    this._switchTo(next);
    Audio.play('reload', { stage: 2, volume: 0.5 });
  }

  // ------------------------------------------------------------- update
  update(dt, canAct) {
    this.swapT = Math.max(0, this.swapT - dt);
    this.meleeCd = Math.max(0, this.meleeCd - dt);
    if (this.meleeActive > 0) {
      this.meleeActive -= dt;
      if (this.meleeActive <= 0) this.ctx.viewModel.showKnife(false);
    }

    const w = this.current;
    const wasReloading = w.reloading;
    w.update(dt, this.reloadSpeedMult());
    if (wasReloading && !w.reloading) this.ctx.viewModel.endReload();

    if (!canAct) {
      this.adsHeld = false;
      this.wasTriggerDown = false;
      return;
    }

    this.adsHeld = Input.isDown('ads') && !this.ctx.player.sprinting;

    if (Input.wasPressed('swap')) this.swap();
    if (Input.wasPressed('slot1')) this._switchTo(0);
    if (Input.wasPressed('slot2')) this._switchTo(1);
    if (Input.wasPressed('reload')) {
      if (w.startReload(this.reloadSpeedMult())) this.ctx.viewModel.startReload();
    }
    if (Input.wasPressed('melee')) this.melee();

    const held = Input.isDown('fire');
    const pressed = held && !this.wasTriggerDown;
    if (!held) w.shotsThisTrigger = 0;

    if (this.busy || this.ctx.player.downed) {
      this.wasTriggerDown = held;
      return;
    }

    // Firing a shotgun mid-reload interrupts it, like the real thing.
    if (w.reloading && w.def.shellReload && held && w.mag > 0) {
      w.cancelReload();
      this.ctx.viewModel.endReload();
    }

    if (w.wantsToFire(held, pressed)) {
      if (w.mag > 0) {
        this._fireShot(w);
        if (w.def.fireMode === 'burst') this._queueBurst(w);
      } else if (pressed) {
        Audio.play('dryfire', { volume: 0.8 });
        if (w.canReload && w.startReload(this.reloadSpeedMult())) this.ctx.viewModel.startReload();
      }
    }

    // Auto-reload when the magazine runs dry mid-burst.
    if (w.mag === 0 && !w.reloading && w.canReload && !held) {
      if (w.startReload(this.reloadSpeedMult())) this.ctx.viewModel.startReload();
    }

    this.wasTriggerDown = held;
  }

  _queueBurst(w) {
    const n = (w.def.burst ?? 3) - 1;
    for (let i = 1; i <= n; i++) {
      setTimeout(() => {
        if (this.current !== w || w.mag <= 0 || w.reloading || this.busy) return;
        this._fireShot(w);
      }, i * w.fireInterval * 1000);
    }
    w.burstCooldown = w.fireInterval * n + (w.def.burstDelay ?? 0.2);
  }

  // ------------------------------------------------------------- firing
  _fireShot(w) {
    if (!w.consume()) return;
    const { player, viewModel, fx, hooks } = this.ctx;

    player.stats.shotsFired++;
    hooks.weaponStat(w.id).shotsFired++;

    const def = w.def;
    Audio.play('shoot', { weight: def.sound, voice: voiceFor(def), volume: 1 });
    viewModel.fireKick(def);
    player.addShake(clamp(def.recoil.v * 0.012, 0.02, 0.14));

    // Recoil pattern: vertical climb plus alternating horizontal drift.
    const heat = 0.55 + w.heat * 0.9;
    const adsFactor = this.ads ? 0.62 : 1;
    player.addRecoil(
      rand(-def.recoil.h, def.recoil.h) * 0.006 * heat * adsFactor,
      def.recoil.v * 0.007 * heat * adsFactor,
    );

    const spread = this._currentSpread(w);
    const origin = this.ctx.camera.getWorldPosition(this._tmpOrigin).clone();
    const baseDir = this.ctx.camera.getWorldDirection(this._tmpDir).clone();
    const muzzle = viewModel.muzzleWorld(this.ctx.camera).clone();

    const pellets = def.pellets ?? 1;
    const hitZombies = new Set();
    let anyHit = false;
    let anyCrit = false;
    let killed = 0;

    for (let p = 0; p < pellets; p++) {
      const dir = baseDir.clone();
      if (spread > 0) {
        dir.x += rand(-spread, spread);
        dir.y += rand(-spread, spread);
        dir.z += rand(-spread, spread);
        dir.normalize();
      }
      const r = this._resolvePellet(origin, dir, w, muzzle, hitZombies);
      anyHit = anyHit || r.hit;
      anyCrit = anyCrit || r.crit;
      killed += r.killed;
    }

    if (anyHit) {
      player.stats.shotsHit++;
      hooks.weaponStat(w.id).shotsHit++;
      hooks.hitmarker(anyCrit, killed > 0);
    }
  }

  _currentSpread(w) {
    const def = w.def;
    const player = this.ctx.player;
    let s = this.ads ? def.spread.ads : def.spread.hip;
    const moving = Math.hypot(player.vel.x, player.vel.z);
    s += (def.spread.move ?? 0.03) * clamp(moving / 6, 0, 1) * (this.ads ? 0.35 : 1);
    if (!player.grounded) s *= 1.6;
    if (player.crouching) s *= 0.7;
    s *= 1 + w.heat * 0.55;
    return s;
  }

  _resolvePellet(origin, dir, w, muzzle, hitZombies) {
    const { world, zombies, fx, player, hooks } = this.ctx;
    const def = w.def;
    const wallHit = world.raycastWorld(origin, dir, def.range);
    const wallDist = wallHit ? wallHit.dist : def.range;

    this.raycaster.set(origin, dir);
    this.raycaster.far = Math.min(def.range, wallDist);
    const targets = zombies.raycastTargets();
    const hits = targets.length ? this.raycaster.intersectObjects(targets, false) : [];

    let budget = def.penetration ?? 1;
    let hitAny = false;
    let crit = false;
    let killed = 0;
    let lastPoint = null;

    for (const h of hits) {
      if (budget <= 0) break;
      const z = h.object.userData.zombie;
      if (!z || !z.active) continue;
      if (hitZombies.has(z)) continue;
      hitZombies.add(z);
      budget--;
      hitAny = true;
      lastPoint = h.point;

      const dmg = player.instantKill ? z.health * 4 + 10000 : this._damageAt(def, h.distance);
      const res = z.takeDamage(dmg, h.object.userData.part, def, fx, dir);
      hooks.weaponStat(w.id).damage += Math.round(res.damage);
      Audio.playAt('hit', h.point, { crit: res.headshot, volume: 0.8 });
      if (res.crit) crit = true;
      if (res.killed) {
        killed++;
        hooks.onKill(z, { headshot: res.headshot, weapon: w, melee: false });
      } else {
        hooks.onHit(z, res);
      }

      if (def.special === 'chain') this._chain(z, w, dmg);
      if (def.special === 'burn') z.applyBurn(def.damage * 0.12, 3);
    }

    if (def.special === 'blast') {
      const p = lastPoint ?? (wallHit ? wallHit.point : {
        x: origin.x + dir.x * def.range, y: origin.y + dir.y * def.range, z: origin.z + dir.z * def.range,
      });
      this._blast(p, def, w);
    }

    if (!hitAny && wallHit) {
      fx.impactPuff(wallHit.point, wallHit.normal);
      Audio.playAt('ricochet', wallHit.point, { volume: 0.5 });
    }

    const end = lastPoint ?? (wallHit ? wallHit.point : {
      x: origin.x + dir.x * def.range, y: origin.y + dir.y * def.range, z: origin.z + dir.z * def.range,
    });
    if (Math.random() < 0.55 || def.pellets > 1) {
      fx.tracer(muzzle, end, def.accent === 0x66d9ff ? 0x9fe6ff : 0xffd9a0);
    }

    return { hit: hitAny, crit, killed };
  }

  _damageAt(def, dist) {
    const t = clamp(dist / def.range, 0, 1);
    return def.damage * (1 - t * (def.falloff ?? 0.5));
  }

  _chain(source, w, baseDamage) {
    const { zombies, fx, hooks, player } = this.ctx;
    const cfg = w.def.chain ?? { targets: 4, radius: 6, falloff: 0.7 };
    let current = source;
    let dmg = baseDamage * cfg.falloff;
    const visited = new Set([source]);
    for (let i = 0; i < cfg.targets; i++) {
      const near = zombies.near(current.pos.x, current.pos.z, cfg.radius)
        .filter((z) => !visited.has(z));
      if (!near.length) break;
      const next = near[0];
      visited.add(next);
      fx.arc(
        { x: current.pos.x, y: current.pos.y + 1.2, z: current.pos.z },
        { x: next.pos.x, y: next.pos.y + 1.2, z: next.pos.z },
        0x66d9ff,
      );
      const res = next.takeDamage(player.instantKill ? next.health * 4 + 10000 : dmg, 'torso', w.def, fx, null);
      if (res.killed) hooks.onKill(next, { headshot: false, weapon: w, melee: false });
      else hooks.onHit(next, res);
      dmg *= cfg.falloff;
      current = next;
    }
  }

  _blast(point, def, w) {
    const { zombies, fx, hooks, player } = this.ctx;
    const cfg = def.blast ?? { radius: 3.5, damage: def.damage };
    fx.explosion(point, cfg.radius, def.accent ?? 0xffa860);
    Audio.playAt('explosion', point, { volume: 1 });
    player.addShake(0.28);
    for (const z of zombies.near(point.x, point.z, cfg.radius)) {
      const d = Math.hypot(z.pos.x - point.x, z.pos.z - point.z);
      const falloff = clamp(1 - d / cfg.radius, 0, 1);
      const res = z.takeDamage(
        player.instantKill ? z.health * 4 + 10000 : cfg.damage * falloff,
        'torso', def, fx, null,
      );
      if (res.killed) hooks.onKill(z, { headshot: false, weapon: w, melee: false });
      else hooks.onHit(z, res);
    }
  }

  // ------------------------------------------------------------- melee
  melee() {
    if (this.meleeCd > 0 || this.ctx.player.downed) return;
    this.meleeCd = 0.62;
    this.meleeActive = 0.42;
    this.ctx.viewModel.showKnife(true);
    this.ctx.viewModel.startMelee();
    Audio.play('melee', { volume: 0.8 });

    const { camera, zombies, fx, hooks, player } = this.ctx;
    const origin = camera.getWorldPosition(new THREE.Vector3());
    const dir = camera.getWorldDirection(new THREE.Vector3());
    this.raycaster.set(origin, dir);
    this.raycaster.far = KNIFE.range;
    const hits = this.raycaster.intersectObjects(zombies.raycastTargets(), false);
    // Slight forgiveness: also sweep for anything very close in front.
    let target = hits.find((h) => h.object.userData.zombie?.active);
    if (!target) {
      const near = zombies.near(
        player.pos.x + dir.x * 1.2, player.pos.z + dir.z * 1.2, 1.5,
      );
      if (near.length) target = { object: { userData: { zombie: near[0], part: 'torso' } }, point: near[0].pos };
    }
    if (!target) return;

    const z = target.object.userData.zombie;
    const dmg = player.instantKill ? z.health * 4 + 10000 : KNIFE.damage * (1 + player.perks.size * 0.05);
    const res = z.takeDamage(dmg, target.object.userData.part ?? 'torso', KNIFE, fx, dir);
    Audio.playAt('hit', target.point, { volume: 0.9 });
    hooks.hitmarker(res.headshot, res.killed);
    if (res.killed) hooks.onKill(z, { headshot: res.headshot, weapon: null, melee: true });
    else hooks.onHit(z, res);
  }

  // ------------------------------------------------------------- HUD
  hudState() {
    const w = this.current;
    return {
      name: w.name,
      mag: w.mag,
      reserve: w.reserve,
      reloading: w.reloading,
      reloadProgress: w.reloadProgress,
      upgradeLevel: w.upgradeLevel,
      other: this.other ? this.other.name : null,
      ads: this.ads,
    };
  }

  reset() {
    this.slots = [new Weapon(WEAPONS.sidearm), null];
    this.index = 0;
    this.swapT = 0;
    this.ctx.viewModel.setWeapon(this.current.def);
  }
}
