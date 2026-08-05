/**
 * Uniform grid navigation.
 *
 * Instead of running A* per zombie we flood a Dijkstra "flow field" outward
 * from the player a few times a second. Every zombie then just reads the
 * gradient of the cell it stands in, which stays cheap with 40+ agents and
 * produces proper corridor-following behaviour around obstacles.
 */
const UNREACHABLE = 0xffff;

export class NavGrid {
  constructor(bounds, cell = 1.0) {
    this.cell = cell;
    this.x0 = Math.floor(bounds.x0);
    this.z0 = Math.floor(bounds.z0);
    this.w = Math.ceil((bounds.x1 - bounds.x0) / cell);
    this.h = Math.ceil((bounds.z1 - bounds.z0) / cell);
    const n = this.w * this.h;

    this.walk = new Uint8Array(n);       // 1 = base walkable geometry
    this.blockCount = new Uint8Array(n); // dynamic blockers (doors, props)
    this.height = new Float32Array(n);   // floor height per cell
    this.zone = new Int16Array(n).fill(-1);
    this.active = new Uint8Array(n);     // 1 = zone currently unlocked
    this.dist = new Uint16Array(n).fill(UNREACHABLE);
    this.flowX = new Int8Array(n);
    this.flowZ = new Int8Array(n);
    this._danger = new Float32Array(n);
  }

  idx(cx, cz) { return cz * this.w + cx; }
  inBounds(cx, cz) { return cx >= 0 && cz >= 0 && cx < this.w && cz < this.h; }
  toCellX(x) { return Math.floor((x - this.x0) / this.cell); }
  toCellZ(z) { return Math.floor((z - this.z0) / this.cell); }
  worldX(cx) { return this.x0 + (cx + 0.5) * this.cell; }
  worldZ(cz) { return this.z0 + (cz + 0.5) * this.cell; }

  /** Mark every cell whose centre lies in `rect` as walkable at height `y`. */
  addWalkableRect(rect, y, zoneIndex, slope = null) {
    const cx0 = Math.max(0, this.toCellX(rect.x0));
    const cx1 = Math.min(this.w - 1, this.toCellX(rect.x1 - 1e-4));
    const cz0 = Math.max(0, this.toCellZ(rect.z0));
    const cz1 = Math.min(this.h - 1, this.toCellZ(rect.z1 - 1e-4));
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        const i = this.idx(cx, cz);
        this.walk[i] = 1;
        this.zone[i] = zoneIndex;
        if (slope) {
          const v = slope.axis === 'x' ? this.worldX(cx) : this.worldZ(cz);
          const t = (v - slope.from) / (slope.to - slope.from);
          this.height[i] = slope.yFrom + (slope.yTo - slope.yFrom) * Math.min(1, Math.max(0, t));
        } else {
          this.height[i] = y;
        }
      }
    }
  }

  /** Add or remove a dynamic blocker over a rect (doors, crates, machines). */
  block(rect, on = true, pad = 0) {
    const cx0 = Math.max(0, this.toCellX(rect.x0 - pad));
    const cx1 = Math.min(this.w - 1, this.toCellX(rect.x1 + pad));
    const cz0 = Math.max(0, this.toCellZ(rect.z0 - pad));
    const cz1 = Math.min(this.h - 1, this.toCellZ(rect.z1 + pad));
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        const i = this.idx(cx, cz);
        if (on) this.blockCount[i] = Math.min(255, this.blockCount[i] + 1);
        else if (this.blockCount[i] > 0) this.blockCount[i]--;
      }
    }
  }

  setZoneActive(zoneIndex, on) {
    for (let i = 0; i < this.active.length; i++) {
      if (this.zone[i] === zoneIndex) this.active[i] = on ? 1 : 0;
    }
  }

  passable(i) {
    return this.walk[i] === 1 && this.blockCount[i] === 0 && this.active[i] === 1;
  }

  heightAt(x, z) {
    const cx = this.toCellX(x), cz = this.toCellZ(z);
    if (!this.inBounds(cx, cz)) return 0;
    const i = this.idx(cx, cz);
    return this.walk[i] ? this.height[i] : 0;
  }

  zoneAt(x, z) {
    const cx = this.toCellX(x), cz = this.toCellZ(z);
    if (!this.inBounds(cx, cz)) return -1;
    return this.zone[this.idx(cx, cz)];
  }

  /** Nearest passable cell to a world position (used when an agent clips a wall). */
  nearestPassable(x, z, radius = 4) {
    const cx = this.toCellX(x), cz = this.toCellZ(z);
    if (this.inBounds(cx, cz) && this.passable(this.idx(cx, cz))) return this.idx(cx, cz);
    for (let r = 1; r <= radius; r++) {
      for (let dz = -r; dz <= r; dz++) {
        for (let dx = -r; dx <= r; dx++) {
          if (Math.max(Math.abs(dx), Math.abs(dz)) !== r) continue;
          const nx = cx + dx, nz = cz + dz;
          if (!this.inBounds(nx, nz)) continue;
          const i = this.idx(nx, nz);
          if (this.passable(i)) return i;
        }
      }
    }
    return -1;
  }

  /** Temporary avoidance cost, e.g. around active traps. */
  setDanger(rect, value) {
    const cx0 = Math.max(0, this.toCellX(rect.x0));
    const cx1 = Math.min(this.w - 1, this.toCellX(rect.x1));
    const cz0 = Math.max(0, this.toCellZ(rect.z0));
    const cz1 = Math.min(this.h - 1, this.toCellZ(rect.z1));
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) this._danger[this.idx(cx, cz)] = value;
    }
  }
  clearDanger() { this._danger.fill(0); }

  /**
   * Dijkstra flood from one or more goal cells. Distances are stored in
   * fixed point (units of cell/4) so a Uint16 is plenty for this map size.
   */
  computeFlow(goals) {
    const { w, h, dist, flowX, flowZ } = this;
    dist.fill(UNREACHABLE);

    // Dial's algorithm: integer costs let us bucket by distance and sweep.
    const MAX_D = 16384;
    const buckets = this._buckets || (this._buckets = new Array(MAX_D));
    let minB = MAX_D, maxB = 0, live = 0;

    const push = (i, d) => {
      if (d >= dist[i] || d >= MAX_D) return;
      dist[i] = d;
      (buckets[d] || (buckets[d] = [])).push(i);
      if (d < minB) minB = d;
      if (d > maxB) maxB = d;
      live++;
    };

    for (const g of goals) {
      if (g < 0 || g >= dist.length || !this.passable(g)) continue;
      push(g, 0);
    }
    if (live === 0) { flowX.fill(0); flowZ.fill(0); return; }

    for (let d = minB; d <= maxB && live > 0; d++) {
      const arr = buckets[d];
      if (!arr || arr.length === 0) continue;
      for (let k = 0; k < arr.length; k++) {
        const i = arr[k];
        live--;
        if (dist[i] !== d) continue; // superseded by a shorter path
        const cx = i % w, cz = (i / w) | 0;
        for (let n = 0; n < 8; n++) {
          const dx = NX[n], dz = NZ[n];
          const nx = cx + dx, nz = cz + dz;
          if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
          const ni = this.idx(nx, nz);
          if (!this.passable(ni)) continue;
          // No corner cutting through wall diagonals.
          if (dx && dz && (!this.passable(this.idx(cx + dx, cz)) || !this.passable(this.idx(cx, cz + dz)))) continue;
          const step = dx && dz ? 6 : 4;                       // ~1.41 vs 1.0
          const climb = Math.round(Math.abs(this.height[ni] - this.height[i]) * 3);
          const nd = d + step + climb + Math.round(this._danger[ni] * 4);
          if (nd < dist[ni]) { push(ni, nd); maxB = Math.max(maxB, nd); }
        }
      }
      arr.length = 0;
    }
    for (let d = 0; d <= maxB; d++) if (buckets[d]) buckets[d].length = 0;

    // Derive the descent direction for each cell once, so agents do no search.
    for (let cz = 0; cz < h; cz++) {
      for (let cx = 0; cx < w; cx++) {
        const i = this.idx(cx, cz);
        if (dist[i] === UNREACHABLE) { flowX[i] = 0; flowZ[i] = 0; continue; }
        let best = dist[i], bx = 0, bz = 0;
        for (let n = 0; n < 8; n++) {
          const nx = cx + NX[n], nz = cz + NZ[n];
          if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
          const ni = this.idx(nx, nz);
          if (!this.passable(ni)) continue;
          if (dist[ni] < best) { best = dist[ni]; bx = NX[n]; bz = NZ[n]; }
        }
        flowX[i] = bx; flowZ[i] = bz;
      }
    }
  }

  /** Unit direction toward the goal from a world position, or null. */
  flowAt(x, z) {
    const cx = this.toCellX(x), cz = this.toCellZ(z);
    if (!this.inBounds(cx, cz)) return null;
    let i = this.idx(cx, cz);
    if (this.dist[i] === UNREACHABLE) {
      const alt = this.nearestPassable(x, z, 3);
      if (alt < 0 || this.dist[alt] === UNREACHABLE) return null;
      i = alt;
    }
    const fx = this.flowX[i], fz = this.flowZ[i];
    if (!fx && !fz) return { x: 0, z: 0, arrived: true };
    // Aim at the centre of the next cell for smoother turns.
    const tx = this.worldX(this.toCellX(x) + fx);
    const tz = this.worldZ(this.toCellZ(z) + fz);
    let dx = tx - x, dz = tz - z;
    const len = Math.hypot(dx, dz) || 1;
    return { x: dx / len, z: dz / len, arrived: false };
  }

  distanceAt(x, z) {
    const cx = this.toCellX(x), cz = this.toCellZ(z);
    if (!this.inBounds(cx, cz)) return Infinity;
    const d = this.dist[this.idx(cx, cz)];
    return d === UNREACHABLE ? Infinity : (d / 4) * this.cell;
  }

  cellIndexAt(x, z) {
    const cx = this.toCellX(x), cz = this.toCellZ(z);
    return this.inBounds(cx, cz) ? this.idx(cx, cz) : -1;
  }
}

const NX = [1, -1, 0, 0, 1, 1, -1, -1];
const NZ = [0, 0, 1, -1, 1, -1, 1, -1];
