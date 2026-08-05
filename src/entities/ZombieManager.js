import { Zombie, ZSTATE } from './Zombie.js';
import { Settings } from '../core/Settings.js';
import { Emitter } from '../core/Util.js';

const CELL = 3.0;

/**
 * Owns the zombie pool, spatial hashing for separation, and the flow-field
 * refresh cadence.
 */
export class ZombieManager extends Emitter {
  constructor(scene, world) {
    super();
    this.scene = scene;
    this.world = world;
    this.pool = [];
    this.alive = [];
    this.grid = new Map();
    this.flowT = 0;
    this.hitboxes = [];
    this._hitboxDirty = true;
    this._sep = { x: 0, z: 0 };
  }

  get count() { return this.alive.length; }
  get livingCount() { return this.alive.filter((z) => z.state !== ZSTATE.DYING).length; }
  get maxConcurrent() { return Settings.quality.maxZombies; }

  spawn(opts) {
    let z = this.pool.find((p) => !p.active);
    if (!z) {
      z = new Zombie(this.scene);
      this.pool.push(z);
    }
    z.spawn(opts);
    this.alive.push(z);
    this._hitboxDirty = true;
    return z;
  }

  remove(z) {
    const i = this.alive.indexOf(z);
    if (i >= 0) this.alive.splice(i, 1);
    this._hitboxDirty = true;
  }

  clear() {
    for (const z of this.alive.slice()) {
      z.despawn();
    }
    this.alive.length = 0;
    this._hitboxDirty = true;
  }

  /** Flattened hitbox list for weapon raycasts. */
  raycastTargets() {
    if (this._hitboxDirty) {
      this.hitboxes = [];
      for (const z of this.alive) {
        if (z.state === ZSTATE.DYING) continue;
        // Raycasting reads matrixWorld, which the renderer would not refresh
        // until after this frame — force it so hits register where the zombie
        // actually is right now, not where it was drawn last frame.
        z.group.updateMatrixWorld(true);
        for (const h of z.hitboxes) if (h.visible !== false) this.hitboxes.push(h);
      }
      this._hitboxDirty = false;
    }
    return this.hitboxes;
  }

  /** Zombies within `r` of a point (used by explosions and traps). */
  near(x, z, r) {
    const out = [];
    const r2 = r * r;
    for (const zz of this.alive) {
      if (zz.state === ZSTATE.DYING) continue;
      const dx = zz.pos.x - x, dz = zz.pos.z - z;
      if (dx * dx + dz * dz <= r2) out.push(zz);
    }
    return out;
  }

  _rebuildGrid() {
    this.grid.clear();
    for (const z of this.alive) {
      const key = `${Math.floor(z.pos.x / CELL)},${Math.floor(z.pos.z / CELL)}`;
      let arr = this.grid.get(key);
      if (!arr) { arr = []; this.grid.set(key, arr); }
      arr.push(z);
    }
  }

  separation(z) {
    const out = this._sep;
    out.x = 0; out.z = 0;
    const cx = Math.floor(z.pos.x / CELL), cz = Math.floor(z.pos.z / CELL);
    for (let i = -1; i <= 1; i++) {
      for (let j = -1; j <= 1; j++) {
        const arr = this.grid.get(`${cx + i},${cz + j}`);
        if (!arr) continue;
        for (const o of arr) {
          if (o === z || o.state === ZSTATE.DYING) continue;
          const dx = z.pos.x - o.pos.x, dz = z.pos.z - o.pos.z;
          const d2 = dx * dx + dz * dz;
          const minD = z.radius + o.radius + 0.16;
          if (d2 > minD * minD || d2 < 1e-5) continue;
          const d = Math.sqrt(d2);
          const push = (minD - d) / minD;
          out.x += (dx / d) * push * 1.5;
          out.z += (dz / d) * push * 1.5;
        }
      }
    }
    return out;
  }

  update(dt, ctx) {
    this._rebuildGrid();
    this._hitboxDirty = true;

    // Refresh the flow field a few times a second from the player's cell.
    this.flowT -= dt;
    if (this.flowT <= 0) {
      this.flowT = 0.28;
      const nav = this.world.nav;
      const goal = nav.nearestPassable(ctx.player.pos.x, ctx.player.pos.z, 6);
      if (goal >= 0) nav.computeFlow([goal]);
    }

    const sub = {
      ...ctx,
      world: this.world,
      nav: this.world.nav,
      separation: (z) => this.separation(z),
      onRemoved: (z) => this.remove(z),
    };

    for (let i = this.alive.length - 1; i >= 0; i--) {
      this.alive[i].update(dt, sub);
    }
  }
}
