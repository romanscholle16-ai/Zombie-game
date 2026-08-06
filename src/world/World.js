import * as THREE from 'three';
import { MAP, mapBounds } from './MapData.js';
import { NavGrid } from './NavGrid.js';
import { Tex, mat, pbrMat } from './Textures.js';
import { box, randomProp, makePipeRun, Mats } from './Props.js';
import { Barricade } from './Barricade.js';
import { Door } from './Door.js';
import { Settings } from '../core/Settings.js';
import { seededRandom, clamp, Emitter } from '../core/Util.js';

const WALL_T = 0.5;
const SILL_H = 1.05;

/**
 * Surface sets per room style. Every wall and floor is a full PBR material —
 * albedo plus a normal and roughness map derived from the same canvas — so
 * concrete reads as pitted, tile as grouted and glazed, and metal as panelled
 * under a moving light. The derived maps cost one pass at boot and nothing at
 * runtime beyond two extra texture fetches per fragment.
 */
const STYLE_MATS = {
  concrete: () => ({
    wall: pbrMat('w_concrete', {
      tex: 'concrete', repeat: 3, roughness: 0.95, normalScale: 1.15,
      pbr: { normal: 2.0, rough: 0.92, roughRange: 0.16 },
    }),
    floor: pbrMat('f_concrete', {
      tex: 'concreteFloor', repeat: 10, roughness: 0.94, normalScale: 0.85,
      pbr: { normal: 1.5, rough: 0.9, roughRange: 0.2 },
    }),
    ceil: mat('c_concrete', { color: 0x2a2e30, roughness: 1 }),
  }),
  metal: () => ({
    wall: pbrMat('w_metal', {
      tex: 'metalPanel', repeat: 3, roughness: 0.55, metalness: 0.6, normalScale: 1.5,
      pbr: { normal: 2.6, rough: 0.55, roughRange: 0.34 },
    }),
    floor: pbrMat('f_metal', {
      tex: 'metalPanel', repeat: 8, roughness: 0.6, metalness: 0.5, normalScale: 1.2,
      pbr: { normal: 2.2, rough: 0.6, roughRange: 0.3 },
    }),
    ceil: mat('c_metal', { color: 0x22262a, roughness: 0.8, metalness: 0.4 }),
  }),
  tile: () => ({
    // Grout lines are darker than the glaze, so inverted roughness makes the
    // tiles shine and the joints stay matte.
    wall: pbrMat('w_tile', {
      tex: 'tile', repeat: 5, roughness: 0.4, normalScale: 1.6,
      pbr: { normal: 2.8, rough: 0.42, roughRange: 0.34, invertRough: true },
    }),
    floor: pbrMat('f_tile', {
      tex: 'tile', repeat: 9, roughness: 0.35, normalScale: 1.3,
      pbr: { normal: 2.4, rough: 0.38, roughRange: 0.3, invertRough: true },
    }),
    ceil: mat('c_tile', { color: 0x30343a, roughness: 0.9 }),
  }),
  dirt: () => ({
    wall: pbrMat('w_dirt', {
      tex: 'dirt', repeat: 4, roughness: 1, normalScale: 1.4,
      pbr: { normal: 2.2, rough: 0.96, roughRange: 0.12 },
    }),
    floor: pbrMat('f_dirt', {
      tex: 'dirt', repeat: 10, roughness: 1, normalScale: 1.1,
      pbr: { normal: 1.8, rough: 0.97, roughRange: 0.1 },
    }),
    ceil: mat('c_dirt', { color: 0x1a1712, roughness: 1 }),
  }),
};

export class World extends Emitter {
  constructor(scene) {
    super();
    this.scene = scene;
    this.root = new THREE.Group();
    scene.add(this.root);

    this.colliders = [];       // { x0,x1,z0,z1,y0,y1, platform }
    this.doors = [];
    this.barricades = [];
    this.lights = [];
    this.zoneActive = new Map();
    this.zoneIndex = new Map();
    this.powered = false;
    this.emissiveTargets = [];

    MAP.zones.forEach((z, i) => {
      this.zoneIndex.set(z.id, i);
      this.zoneActive.set(z.id, !!z.unlocked);
    });

    this.nav = new NavGrid(mapBounds(), 1.0);
    this._build();
  }

  // ================================================================= build
  _build() {
    const q = Settings.quality;
    for (const z of MAP.zones) this._buildZone(z, q);
    for (const w of MAP.windows) this._buildWindow(w);
    for (const d of MAP.doors) this._buildDoor(d);
    for (const c of MAP.clutter) this._buildClutter(c);
    this._buildLights(q);
    this._buildDressing();

    // Zones start locked except the spawn area.
    for (const z of MAP.zones) {
      this.nav.setZoneActive(this.zoneIndex.get(z.id), this.zoneActive.get(z.id) ? 1 : 0);
    }
  }

  _zoneY(z, x) {
    if (!z.slope) return z.y;
    const t = clamp((x - z.slope.from) / (z.slope.to - z.slope.from), 0, 1);
    return z.slope.yFrom + (z.slope.yTo - z.slope.yFrom) * t;
  }

  _buildZone(z, q) {
    const mats = (STYLE_MATS[z.style] || STYLE_MATS.concrete)();
    const w = z.x1 - z.x0, d = z.z1 - z.z0;
    const cx = (z.x0 + z.x1) / 2, cz = (z.z0 + z.z1) / 2;
    const zi = this.zoneIndex.get(z.id);

    // ---- floor
    if (z.slope) {
      const dy = z.slope.yTo - z.slope.yFrom;
      const run = Math.abs(z.slope.to - z.slope.from);
      const len = Math.hypot(run, dy);
      const floor = box(len, 0.4, d, mats.floor, cx, (z.slope.yFrom + z.slope.yTo) / 2 - 0.2, cz);
      floor.rotation.z = Math.atan2(dy, z.slope.to - z.slope.from);
      floor.receiveShadow = true;
      this.root.add(floor);
    } else {
      const floor = box(w, 0.4, d, mats.floor, cx, z.y - 0.2, cz);
      floor.receiveShadow = true;
      this.root.add(floor);
      const ceil = box(w, 0.3, d, mats.ceil, cx, z.y + z.h + 0.15, cz);
      ceil.castShadow = false;
      this.root.add(ceil);
    }

    // ---- nav
    this.nav.addWalkableRect(
      { x0: z.x0 + 0.35, x1: z.x1 - 0.35, z0: z.z0 + 0.35, z1: z.z1 - 0.35 },
      z.y, zi, z.slope || null,
    );

    // ---- walls (4 edges, minus openings and window apertures)
    const edges = [
      { axis: 'z', at: z.z0, from: z.x0, to: z.x1, n: -1 },
      { axis: 'z', at: z.z1, from: z.x0, to: z.x1, n: 1 },
      { axis: 'x', at: z.x0, from: z.z0, to: z.z1, n: -1 },
      { axis: 'x', at: z.x1, from: z.z0, to: z.z1, n: 1 },
    ];
    for (const e of edges) {
      for (const [s, t] of this._cutEdge(e)) {
        if (t - s < 0.08) continue;
        this._wallSegment(z, e, s, t, mats.wall);
      }
    }
    void q;
  }

  /** Subtract openings + window apertures from a wall edge. */
  _cutEdge(e) {
    let spans = [[e.from, e.to]];
    const cut = (a, b) => {
      const out = [];
      for (const [s, t] of spans) {
        if (b <= s || a >= t) { out.push([s, t]); continue; }
        if (a > s) out.push([s, a]);
        if (b < t) out.push([b, t]);
      }
      spans = out;
    };

    for (const op of MAP.openings) {
      if (e.axis === 'z') {
        if (e.at >= op.z0 - 0.01 && e.at <= op.z1 + 0.01) cut(op.x0, op.x1);
      } else if (e.at >= op.x0 - 0.01 && e.at <= op.x1 + 0.01) cut(op.z0, op.z1);
    }
    for (const wd of MAP.windows) {
      if (e.axis === 'z') {
        if (Math.abs(e.at - wd.z) < 0.6 && wd.nz !== 0) cut(wd.x - 1.6, wd.x + 1.6);
      } else if (Math.abs(e.at - wd.x) < 0.6 && wd.nx !== 0) cut(wd.z - 1.6, wd.z + 1.6);
    }
    return spans;
  }

  _wallSegment(z, e, s, t, material) {
    // Sloped rooms get their walls chopped into steps that follow the ramp.
    const pieces = z.slope ? 6 : 1;
    for (let i = 0; i < pieces; i++) {
      const a = s + (t - s) * (i / pieces);
      const b = s + (t - s) * ((i + 1) / pieces);
      const mid = (a + b) / 2;
      const yBase = z.slope
        ? this._zoneY(z, e.axis === 'z' ? mid : e.at)
        : z.y;
      const len = b - a;
      let m;
      if (e.axis === 'z') {
        m = box(len, z.h + 0.6, WALL_T, material, mid, yBase + (z.h + 0.6) / 2 - 0.3, e.at + e.n * WALL_T / 2);
        this.addCollider(mid - len / 2, mid + len / 2, e.at + e.n * WALL_T / 2 - WALL_T / 2,
          e.at + e.n * WALL_T / 2 + WALL_T / 2, yBase - 0.3, yBase + z.h);
      } else {
        m = box(WALL_T, z.h + 0.6, len, material, e.at + e.n * WALL_T / 2, yBase + (z.h + 0.6) / 2 - 0.3, mid);
        this.addCollider(e.at + e.n * WALL_T / 2 - WALL_T / 2, e.at + e.n * WALL_T / 2 + WALL_T / 2,
          mid - len / 2, mid + len / 2, yBase - 0.3, yBase + z.h);
      }
      m.receiveShadow = true;
      m.castShadow = false;
      this.root.add(m);
    }
  }

  _buildWindow(def) {
    const zone = MAP.zones.find((z) => z.id === def.zone);
    const b = new Barricade(def, zone.y, zone.h);
    this.root.add(b.group);
    this.barricades.push(b);

    // Sill blocks the player but zombies vault it.
    const hw = def.nz !== 0 ? 1.7 : WALL_T;
    const hd = def.nz !== 0 ? WALL_T : 1.7;
    this.addCollider(def.x - hw, def.x + hw, def.z - hd, def.z + hd, zone.y, zone.y + SILL_H, false, true);
    // Header above the aperture.
    this.addCollider(def.x - hw, def.x + hw, def.z - hd, def.z + hd, zone.y + 2.6, zone.y + zone.h);

    // ---- alcove shell
    const [ax0, az0, ax1, az1] = def.alcove;
    // Light enough that the hemisphere fill gives the alcove some form —
    // zombies queueing at the boards need something to read against.
    const dark = mat('alcove', { color: 0x1c242b, roughness: 1 });
    const cx = (ax0 + ax1) / 2, cz = (az0 + az1) / 2;
    const aw = ax1 - ax0, ad = az1 - az0;
    this.root.add(box(aw, 0.3, ad, dark, cx, zone.y - 0.15, cz));
    this.root.add(box(aw, 0.3, ad, dark, cx, zone.y + 3.6, cz));
    // Three enclosing walls (the fourth side is the window).
    const walls = def.nz !== 0
      ? [[ax0 - 0.25, cz, 0.5, ad], [ax1 + 0.25, cz, 0.5, ad], [cx, def.nz > 0 ? az1 + 0.25 : az0 - 0.25, aw + 1, 0.5]]
      : [[cx, az0 - 0.25, aw, 0.5], [cx, az1 + 0.25, aw, 0.5], [def.nx > 0 ? ax1 + 0.25 : ax0 - 0.25, cz, 0.5, ad + 1]];
    for (const [wx, wz, ww, wd] of walls) {
      this.root.add(box(ww, 3.8, wd, dark, wx, zone.y + 1.9, wz));
    }

    // ---- nav: alcove + the aperture bridge
    const zi = this.zoneIndex.get(def.zone);
    this.nav.addWalkableRect({ x0: ax0 + 0.3, x1: ax1 - 0.3, z0: az0 + 0.3, z1: az1 - 0.3 }, zone.y, zi);
    this.nav.addWalkableRect({
      x0: def.x - (def.nz !== 0 ? 1.2 : 1.4), x1: def.x + (def.nz !== 0 ? 1.2 : 1.4),
      z0: def.z - (def.nz !== 0 ? 1.4 : 1.2), z1: def.z + (def.nz !== 0 ? 1.4 : 1.2),
    }, zone.y, zi);
  }

  _buildDoor(def) {
    const zone = MAP.zones.find((z) => z.id === def.zone) || MAP.zones[0];
    const d = new Door(def, zone.y, zone.h);
    this.root.add(d.group);
    this.doors.push(d);
    this.colliders.push(d.collider);
    this.nav.block(d.rect, true, 0.2);
  }

  _buildClutter(c) {
    const z = MAP.zones.find((zz) => zz.id === c.zone);
    if (!z) return;
    const rng = seededRandom(c.seed);
    const kinds = ['crates', 'barrels', 'shelves', 'tables', 'generators', 'debris'];
    const placed = [];

    const tryPlace = (kind) => {
      for (let attempt = 0; attempt < 24; attempt++) {
        const margin = 2.0;
        const x = z.x0 + margin + rng() * Math.max(0.1, (z.x1 - z.x0) - margin * 2);
        const zz = z.z0 + margin + rng() * Math.max(0.1, (z.z1 - z.z0) - margin * 2);
        if (this._nearReserved(x, zz, 2.6)) continue;
        if (placed.some((p) => Math.hypot(p.x - x, p.z - zz) < 2.4)) continue;
        const prop = randomProp(kind, Math.floor(rng() * 1000));
        const y = this._zoneY(z, x);
        prop.position.set(x, y, zz);
        prop.rotation.y = rng() * Math.PI * 2;
        this.root.add(prop);
        placed.push({ x, z: zz });
        const col = prop.userData.collider;
        if (col) {
          const hw = Math.max(col.w, col.d) / 2;
          this.addCollider(x - hw, x + hw, zz - hw, zz + hw, y, y + col.h, !!col.platform);
          this.nav.block({ x0: x - hw, x1: x + hw, z0: zz - hw, z1: zz + hw }, true, 0.15);
        }
        return;
      }
    };

    for (const k of kinds) {
      const n = c[k] || 0;
      for (let i = 0; i < n; i++) tryPlace(k);
    }
  }

  /** Keeps clutter clear of doors, windows, machines and the player spawn. */
  _nearReserved(x, z, r) {
    for (const w of MAP.windows) if (Math.hypot(w.x - x, w.z - z) < r + 2.2) return true;
    for (const d of MAP.doors) {
      const cx = (d.x0 + d.x1) / 2, cz = (d.z0 + d.z1) / 2;
      if (Math.hypot(cx - x, cz - z) < r + 3.0) return true;
    }
    for (const p of MAP.perkSpots) if (Math.hypot(p.x - x, p.z - z) < r + 1.6) return true;
    for (const b of MAP.boxSpots) if (Math.hypot(b.x - x, b.z - z) < r + 2.0) return true;
    for (const t of MAP.traps) if (Math.hypot(t.x - x, t.z - z) < r + 1.8) return true;
    for (const wb of MAP.wallBuys) if (Math.hypot(wb.x - x, wb.z - z) < r + 1.4) return true;
    const u = MAP.upgradeMachine;
    if (Math.hypot(u.x - x, u.z - z) < r + 3.0) return true;
    const ps = MAP.powerSwitch;
    if (Math.hypot(ps.x - x, ps.z - z) < r + 2.0) return true;
    if (Math.hypot(MAP.spawn.x - x, MAP.spawn.z - z) < r + 2.5) return true;
    // Keep the middle of every opening clear so pathing never jams.
    for (const op of MAP.openings) {
      if (x > op.x0 - 1.5 && x < op.x1 + 1.5 && z > op.z0 - 1.5 && z < op.z1 + 1.5) return true;
    }
    return false;
  }

  _buildLights(q) {
    const ambient = new THREE.HemisphereLight(0x4a5c72, 0x14191d, 1.1);
    this.scene.add(ambient);
    this.hemi = ambient;

    let shadowBudget = q.shadows ? 2 : 0;
    let count = 0;
    for (const l of MAP.lights) {
      if (count >= q.dynamicLights) break;
      count++;
      const light = new THREE.PointLight(l.color, 0, l.d);
      light.position.set(l.x, l.y, l.z);
      if (shadowBudget > 0 && l.i >= 0.85) {
        light.castShadow = true;
        light.shadow.mapSize.set(q.shadowSize, q.shadowSize);
        light.shadow.bias = -0.005;
        light.shadow.camera.far = l.d;
        shadowBudget--;
      }
      this.root.add(light);

      // Housing + emergency lamp so the fixture reads when unpowered.
      const housing = box(0.7, 0.12, 0.3, Mats.darkMetal, l.x, l.y + 0.25, l.z);
      this.root.add(housing);
      const bulb = box(0.55, 0.06, 0.22, mat(`bulb${l.color}`, {
        color: 0x101010, emissive: l.color, emissiveIntensity: 0.05, roughness: 0.5,
      }), l.x, l.y + 0.16, l.z);
      this.root.add(bulb);

      this.lights.push({ light, base: l.i, zone: l.zone, bulb, flicker: Math.random() * 10 });
    }

    // Red emergency strips glow while the grid is down.
    this.emergency = [];
    this.emergencyLights = [];
    for (const l of MAP.lights) {
      const em = box(0.3, 0.08, 0.08, mat('emergencyLamp', {
        color: 0x140505, emissive: 0xff2b1e, emissiveIntensity: 1.4, roughness: 0.6,
      }), l.x + 0.6, l.y + 0.1, l.z);
      this.root.add(em);
      this.emergency.push(em);
      if (this.emergencyLights.length < 6) {
        const rl = new THREE.PointLight(0xff5a3a, 26, 19);
        rl.position.set(l.x + 0.6, l.y + 0.1, l.z);
        this.root.add(rl);
        this.emergencyLights.push({ light: rl, zone: l.zone });
      }
    }
  }

  /** Pipes, cable trays and signage that sell the facility. */
  _buildDressing() {
    for (const z of MAP.zones) {
      if (z.slope) continue;
      const rng = seededRandom((z.x0 * 31 + z.z0 * 17) | 0);
      const runs = Math.max(1, Math.round((z.x1 - z.x0) / 12));
      for (let i = 0; i < runs; i++) {
        const p = makePipeRun((z.x1 - z.x0) * 0.8, i);
        p.position.set((z.x0 + z.x1) / 2, z.y + z.h - 0.45, z.z0 + 1.2 + rng() * (z.z1 - z.z0 - 2.4));
        this.root.add(p);
      }
      const sign = new THREE.Mesh(
        new THREE.PlaneGeometry(2.6, 0.7),
        new THREE.MeshBasicMaterial({ map: Tex.label(z.name.split(' ')[0], { fg: '#8d948c' }) }),
      );
      sign.position.set((z.x0 + z.x1) / 2, z.y + z.h - 1.1, z.z0 + 0.28);
      this.root.add(sign);
    }
  }

  // ================================================================= colliders
  addCollider(x0, x1, z0, z1, y0, y1, platform = false, vaultable = false) {
    const c = { x0, x1, z0, z1, y0, y1, platform, vaultable };
    this.colliders.push(c);
    return c;
  }

  /** Register a machine footprint (blocks movement and pathing). */
  addObstacle(x, z, w, d, y, h, opts = {}) {
    this.addCollider(x - w / 2, x + w / 2, z - d / 2, z + d / 2, y, y + h, !!opts.platform);
    this.nav.block({ x0: x - w / 2, x1: x + w / 2, z0: z - d / 2, z1: z + d / 2 }, true, 0.1);
  }

  /** Highest walkable surface under a point, considering step-up height. */
  groundHeightAt(x, z, feetY = 0, step = 0.65) {
    let best = this.nav.heightAt(x, z);
    for (const c of this.colliders) {
      if (!c.platform) continue;
      if (x < c.x0 || x > c.x1 || z < c.z0 || z > c.z1) continue;
      if (c.y1 > best && c.y1 <= feetY + step + 0.02) best = c.y1;
    }
    return best;
  }

  /** Height of the nearest ledge in front of the player, for mantling. */
  ledgeAt(x, z, feetY, maxHeight = 1.7) {
    let best = null;
    for (const c of this.colliders) {
      if (x < c.x0 - 0.1 || x > c.x1 + 0.1 || z < c.z0 - 0.1 || z > c.z1 + 0.1) continue;
      if (c.y1 <= feetY + 0.2 || c.y1 > feetY + maxHeight) continue;
      if (!c.platform) continue;   // window sills are for zombies, not players
      if (best === null || c.y1 > best) best = c.y1;
    }
    return best;
  }

  /**
   * Slide-along-wall circle collision. `pos` is mutated.
   * Returns true when something was hit.
   */
  collide(pos, radius, feetY, headY) {
    let hit = false;
    for (let pass = 0; pass < 2; pass++) {
      for (const c of this.colliders) {
        if (c.y1 <= feetY + 0.06 || c.y0 >= headY) continue;
        const nx = clamp(pos.x, c.x0, c.x1);
        const nz = clamp(pos.z, c.z0, c.z1);
        const dx = pos.x - nx, dz = pos.z - nz;
        const d2 = dx * dx + dz * dz;
        if (d2 >= radius * radius) continue;
        hit = true;
        if (d2 > 1e-6) {
          const d = Math.sqrt(d2);
          pos.x += (dx / d) * (radius - d);
          pos.z += (dz / d) * (radius - d);
        } else {
          // Centre is inside the box — push out along the shallowest axis.
          const toL = pos.x - c.x0, toR = c.x1 - pos.x;
          const toB = pos.z - c.z0, toT = c.z1 - pos.z;
          const m = Math.min(toL, toR, toB, toT);
          if (m === toL) pos.x = c.x0 - radius;
          else if (m === toR) pos.x = c.x1 + radius;
          else if (m === toB) pos.z = c.z0 - radius;
          else pos.z = c.z1 + radius;
        }
      }
      if (!hit) break;
    }
    return hit;
  }

  /**
   * Ray vs. static geometry (slab test against every collider box).
   * Returns the closest hit, or null.
   */
  raycastWorld(origin, dir, maxDist = 200) {
    let bestT = maxDist;
    let best = null;
    for (const c of this.colliders) {
      let tmin = 0, tmax = bestT;
      let nAxis = 0, nSign = 0;
      const lo = [c.x0, c.y0, c.z0];
      const hi = [c.x1, c.y1, c.z1];
      const o = [origin.x, origin.y, origin.z];
      const d = [dir.x, dir.y, dir.z];
      let hit = true;
      for (let a = 0; a < 3; a++) {
        if (Math.abs(d[a]) < 1e-8) {
          if (o[a] < lo[a] || o[a] > hi[a]) { hit = false; break; }
          continue;
        }
        const inv = 1 / d[a];
        let t1 = (lo[a] - o[a]) * inv;
        let t2 = (hi[a] - o[a]) * inv;
        let sign = -1;
        if (t1 > t2) { const tt = t1; t1 = t2; t2 = tt; sign = 1; }
        if (t1 > tmin) { tmin = t1; nAxis = a; nSign = sign; }
        if (t2 < tmax) tmax = t2;
        if (tmin > tmax) { hit = false; break; }
      }
      if (!hit || tmin <= 0.01 || tmin >= bestT) continue;
      bestT = tmin;
      best = {
        dist: tmin,
        point: {
          x: origin.x + dir.x * tmin,
          y: origin.y + dir.y * tmin,
          z: origin.z + dir.z * tmin,
        },
        normal: { x: nAxis === 0 ? nSign : 0, y: nAxis === 1 ? nSign : 0, z: nAxis === 2 ? nSign : 0 },
        collider: c,
      };
    }
    return best;
  }

  /** Line-of-sight test against walls only (ignores platforms). */
  losBlocked(ax, ay, az, bx, by, bz) {
    const dx = bx - ax, dy = by - ay, dz = bz - az;
    const dist = Math.hypot(dx, dy, dz);
    const steps = Math.ceil(dist / 0.6);
    for (let i = 1; i < steps; i++) {
      const t = i / steps;
      const x = ax + dx * t, y = ay + dy * t, z = az + dz * t;
      for (const c of this.colliders) {
        if (c.platform) continue;
        if (x > c.x0 && x < c.x1 && z > c.z0 && z < c.z1 && y > c.y0 && y < c.y1) return true;
      }
    }
    return false;
  }

  // ================================================================= state
  isZoneActive(id) { return !!this.zoneActive.get(id); }

  unlockZone(id) {
    if (this.zoneActive.get(id)) return;
    this.zoneActive.set(id, true);
    this.nav.setZoneActive(this.zoneIndex.get(id), 1);
    this.emit('zoneUnlocked', id);
  }

  openDoor(door) {
    door.purchase();
    const ci = this.colliders.indexOf(door.collider);
    if (ci >= 0) this.colliders.splice(ci, 1);
    this.nav.block(door.rect, false, 0.2);
    for (const zid of door.opens) this.unlockZone(zid);
  }

  /** Barricades whose zone is currently reachable. */
  activeBarricades() {
    return this.barricades.filter((b) => this.isZoneActive(b.zone));
  }

  setPowered(on) {
    this.powered = on;
    for (const em of this.emergency) {
      em.material.emissiveIntensity = on ? 0.05 : 1.4;
    }
    for (const e of this.emergencyLights) e.light.intensity = on ? 0 : 26;
    this.emit('power', on);
  }

  update(dt, time) {
    for (const d of this.doors) d.update(dt);
    for (const b of this.barricades) b.update(dt);

    // Lights ramp up with the grid and flicker slightly when live.
    for (const l of this.lights) {
      const active = this.powered && this.isZoneActive(l.zone);
      const flick = active ? 0.86 + Math.sin(time * 9.3 + l.flicker) * 0.05 + Math.sin(time * 31 + l.flicker * 3) * 0.03 : 0;
      const target = active ? l.base * flick * 70 : l.base * 30;
      l.light.intensity += (target - l.light.intensity) * Math.min(1, dt * 6);
      l.bulb.material.emissiveIntensity = active ? 1.8 * flick : 0.05;
    }
    this.hemi.intensity += ((this.powered ? 1.5 : 1.05) - this.hemi.intensity) * Math.min(1, dt * 2);
  }

  dispose() {
    // Geometries and materials are shared across the whole game and are
    // released by Textures.dispose()/clearMaterials() on teardown.
    this.scene.remove(this.root);
    if (this.hemi) this.scene.remove(this.hemi);
    this.colliders.length = 0;
    this.clear();
  }
}
