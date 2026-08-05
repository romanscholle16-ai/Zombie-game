import * as THREE from 'three';
import { makePowerupIcon } from '../world/Props.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp, weightedPick, rand } from '../core/Util.js';

const LIFETIME = 26;
const BLINK_AT = 7;

export const DROPS = {
  maxAmmo: { id: 'maxAmmo', label: 'MAX AMMO', glyph: 'AMMO', color: 0x4aa3ff, weight: 24, duration: 0 },
  instantKill: { id: 'instantKill', label: 'INSTANT KILL', glyph: 'KILL', color: 0xc0392b, weight: 16, duration: 30 },
  doublePoints: { id: 'doublePoints', label: 'DOUBLE POINTS', glyph: 'X2', color: 0x9fd93a, weight: 18, duration: 30 },
  nuke: { id: 'nuke', label: 'PURGE', glyph: 'PURGE', color: 0xe8a33d, weight: 12, duration: 0 },
  fireSale: { id: 'fireSale', label: 'FIRE SALE', glyph: 'SALE', color: 0xb479ff, weight: 8, duration: 35 },
  bonusPoints: { id: 'bonusPoints', label: 'BONUS POINTS', glyph: '+', color: 0xffd24a, weight: 22, duration: 0 },
};

const DROP_LIST = Object.values(DROPS);

/**
 * Special drops. A running kill counter guarantees one every so often on top
 * of the flat chance, which keeps the pacing readable instead of streaky.
 */
export class PowerUpSystem {
  constructor(scene, ctxRef) {
    this.scene = scene;
    this.ctxRef = ctxRef;
    this.items = [];
    this.pool = [];
    this.killsSinceDrop = 0;
    this.dropThreshold = 55;
    this.active = new Map();  // id -> remaining seconds
  }

  _acquire(def) {
    // Pooled per drop type so icons keep their own colours and glyphs.
    let item = this.pool.find((p) => !p.alive && p.def === def);
    if (!item) {
      const group = makePowerupIcon(def.color, def.glyph);
      this.scene.add(group);
      item = { group, alive: false, def };
      this.pool.push(item);
    }
    item.alive = true;
    item.t = LIFETIME;
    item.group.visible = true;
    if (!this.items.includes(item)) this.items.push(item);
    return item;
  }

  /** Called on every zombie death. */
  onKill(pos, groundY, round) {
    this.killsSinceDrop++;
    const guaranteed = this.killsSinceDrop >= this.dropThreshold;
    if (!guaranteed && Math.random() > 0.022) return null;
    this.killsSinceDrop = 0;
    this.dropThreshold = 45 + Math.floor(Math.random() * 30);
    return this.spawn(pos, groundY, round);
  }

  spawn(pos, groundY, round) {
    const entries = DROP_LIST.map((d) => {
      let w = d.weight;
      if (d.id === 'fireSale' && round < 5) w = 0;
      if (d.id === 'nuke' && round < 3) w *= 0.4;
      return { weight: w, d };
    });
    const def = weightedPick(entries).d;
    const item = this._acquire(def);
    item.group.position.set(pos.x, (groundY ?? pos.y) + 0.85, pos.z);
    item.baseY = item.group.position.y;
    return item;
  }

  collect(item, ctx) {
    item.alive = false;
    item.group.visible = false;
    const def = item.def;
    Audio.play('powerup', { volume: 1 });
    ctx.toast(def.label, 'pts', true);

    switch (def.id) {
      case 'maxAmmo':
        ctx.refillAllAmmo();
        break;
      case 'nuke':
        ctx.purge();
        break;
      case 'bonusPoints':
        ctx.player.addPoints(500, 'bonus');
        break;
      case 'fireSale':
        this.active.set('fireSale', def.duration);
        ctx.startFireSale(def.duration);
        break;
      default:
        this.active.set(def.id, def.duration);
        break;
    }
    if (def.id === 'doublePoints') ctx.player.multiplier = 2;
    if (def.id === 'instantKill') ctx.player.instantKill = true;
  }

  isActive(id) { return (this.active.get(id) ?? 0) > 0; }

  update(dt, time, ctx) {
    // Timed effects.
    for (const [id, t] of Array.from(this.active)) {
      const nt = t - dt;
      if (nt <= 0) {
        this.active.delete(id);
        if (id === 'doublePoints') ctx.player.multiplier = 1;
        if (id === 'instantKill') ctx.player.instantKill = false;
        ctx.toast(`${DROPS[id]?.label ?? id} EXPIRED`, 'warn');
      } else {
        this.active.set(id, nt);
      }
    }

    for (let i = this.items.length - 1; i >= 0; i--) {
      const it = this.items[i];
      if (!it.alive) { this.items.splice(i, 1); continue; }
      it.t -= dt;
      if (it.t <= 0) {
        it.alive = false;
        it.group.visible = false;
        this.items.splice(i, 1);
        continue;
      }
      it.group.rotation.y = time * 1.7;
      it.group.position.y = it.baseY + Math.sin(time * 2.6) * 0.12;
      const blink = it.t < BLINK_AT ? (Math.sin(time * 16) > 0 ? 1 : 0.15) : 1;
      it.group.userData.light.intensity = 18 * blink;
      it.group.userData.shell.material.emissiveIntensity = 1.6 * blink;
      it.group.visible = it.t >= BLINK_AT || blink > 0.5;

      const p = ctx.player.pos;
      if (Math.hypot(p.x - it.group.position.x, p.z - it.group.position.z) < 2.2
        && Math.abs(p.y - (it.baseY - 0.85)) < 2.5) {
        this.collect(it, ctx);
        this.items.splice(i, 1);
      }
    }
  }

  /** Remaining seconds for the HUD tray. */
  hudEntries() {
    return Array.from(this.active.entries()).map(([id, t]) => ({
      id, label: DROPS[id]?.label ?? id, color: DROPS[id]?.color ?? 0xffffff, t,
    }));
  }

  reset() {
    for (const it of this.items) { it.alive = false; it.group.visible = false; }
    this.items.length = 0;
    this.active.clear();
    this.killsSinceDrop = 0;
  }
}

export { clamp, rand, THREE };
