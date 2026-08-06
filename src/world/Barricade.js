import * as THREE from 'three';
import { box, Mats } from './Props.js';
import { Audio } from '../core/AudioEngine.js';
import { rand, clamp } from '../core/Util.js';

export const BOARDS_PER_WINDOW = 6;
const BOARD_W = 2.7;

/**
 * A boarded window. Zombies queue at the outside, tear planks off one at a
 * time, then vault the sill. Players can renail planks for points.
 */
export class Barricade {
  constructor(def, floorY, wallHeight = 5.2) {
    this.id = def.id;
    this.wallHeight = wallHeight;
    this.zone = def.zone;
    this.x = def.x;
    this.z = def.z;
    this.y = floorY;
    this.nx = def.nx;
    this.nz = def.nz;
    this.alcove = def.alcove;

    // Outside staging point and the interior landing spot.
    this.outside = { x: def.x + def.nx * 2.0, z: def.z + def.nz * 2.0 };
    this.inside = { x: def.x - def.nx * 1.6, z: def.z - def.nz * 1.6 };

    this.boards = [];
    this.attackers = new Set();
    this.repairCooldown = 0;
    this.group = new THREE.Group();
    this.group.position.set(def.x, floorY, def.z);
    this.group.rotation.y = Math.atan2(def.nx, def.nz);

    this._buildFrame();
    for (let i = 0; i < BOARDS_PER_WINDOW; i++) this._makeBoard(i);
  }

  get boardCount() { return this.boards.filter((b) => b.intact).length; }
  get isOpen() { return this.boardCount === 0; }

  _buildFrame() {
    const frameMat = Mats.concrete;
    // The wall builder cuts a full-height gap for every window, so the frame
    // is responsible for filling everything except the aperture itself. Miss
    // the span above the head and you get a black hole punched through the
    // wall — which is exactly what it looks like from inside.
    const APERTURE_TOP = 2.6;
    const HEAD_H = 0.9;
    this.group.add(box(3.2, 1.05, 0.5, frameMat, 0, 0.52, 0));
    this.group.add(box(3.2, HEAD_H, 0.5, frameMat, 0, APERTURE_TOP + HEAD_H / 2, 0));
    this.group.add(box(0.4, 1.6, 0.5, frameMat, -1.6, 1.85, 0));
    this.group.add(box(0.4, 1.6, 0.5, frameMat, 1.6, 1.85, 0));

    // Infill from the head up to the ceiling. Slightly oversized so it never
    // leaves a hairline seam against the surrounding wall.
    const infillBase = APERTURE_TOP + HEAD_H;
    const infillH = Math.max(0.1, this.wallHeight + 0.4 - infillBase);
    const infill = box(3.24, infillH, 0.5, frameMat, 0, infillBase + infillH / 2, 0);
    infill.castShadow = false;
    this.group.add(infill);

    // The aperture stays open: the alcove behind it is a sealed shell, so you
    // can watch zombies crowd the gap and work the planks loose before the
    // first one gets through. A dim panel deep in the alcove backlights them
    // into silhouettes — far cheaper than a real light per window, and it
    // reads better besides.
    const haze = new THREE.Mesh(
      new THREE.PlaneGeometry(5.2, 3.4),
      // Unfogged: the alcove is only a few metres deep but the interior fog is
      // thick enough to swallow the panel entirely if it participates.
      new THREE.MeshBasicMaterial({ color: 0x46586c, fog: false }),
    );
    haze.position.set(0, 1.9, 6.15);
    haze.rotation.y = Math.PI;
    this.group.add(haze);
  }

  _makeBoard(i) {
    const y = 1.28 + (i % 3) * 0.5 + (i > 2 ? 0.06 : 0);
    const tilt = ((i * 37) % 13 - 6) * 0.022;
    const off = ((i * 53) % 11 - 5) * 0.045;
    const m = box(BOARD_W, 0.19, 0.09, Mats.plank, off, y, i > 2 ? -0.16 : 0.16);
    m.rotation.z = tilt;
    m.userData.baseY = y;
    this.group.add(m);
    // Nails.
    for (const s of [-1, 1]) {
      const n = box(0.03, 0.03, 0.13, Mats.metal, off + s * (BOARD_W / 2 - 0.16), y, (i > 2 ? -0.16 : 0.16));
      n.rotation.z = tilt;
      this.group.add(n);
      m.userData.nails = (m.userData.nails || []).concat(n);
    }
    const b = { mesh: m, intact: true, hp: 1, index: i };
    this.boards.push(b);
    return b;
  }

  /** Zombie tears at the barricade. Returns true when a plank comes off. */
  damage(amount, fx) {
    const live = this.boards.filter((b) => b.intact);
    if (!live.length) return false;
    const b = live[live.length - 1];
    b.hp -= amount;
    // Shake the plank while it is being worked on.
    b.mesh.position.x += rand(-0.02, 0.02);
    b.mesh.rotation.z += rand(-0.01, 0.01);
    if (b.hp <= 0) {
      b.intact = false;
      b.mesh.visible = false;
      b.mesh.userData.nails?.forEach((n) => { n.visible = false; });
      const wp = this.group.localToWorld(b.mesh.position.clone());
      fx?.woodBurst(wp);
      Audio.playAt('board', { x: this.x, y: this.y + 1.5, z: this.z }, { volume: 1 });
      return true;
    }
    return false;
  }

  /** Player repairs one plank. Returns points awarded, or 0. */
  repair(fx) {
    const broken = this.boards.find((b) => !b.intact);
    if (!broken) return 0;
    broken.intact = true;
    broken.hp = 1;
    broken.mesh.visible = true;
    broken.mesh.position.x = ((broken.index * 53) % 11 - 5) * 0.045;
    broken.mesh.rotation.z = ((broken.index * 37) % 13 - 6) * 0.022;
    broken.mesh.userData.nails?.forEach((n) => { n.visible = true; });
    Audio.playAt('repair', { x: this.x, y: this.y + 1.5, z: this.z }, { volume: 1 });
    fx?.sparkBurst(this.group.localToWorld(broken.mesh.position.clone()), 0xd9b46a, 6);
    return 10;
  }

  /** Rebuild everything (used by round transitions on empty windows). */
  restoreAll() {
    for (const b of this.boards) {
      if (b.intact) continue;
      b.intact = true;
      b.hp = 1;
      b.mesh.visible = true;
      b.mesh.userData.nails?.forEach((n) => { n.visible = true; });
    }
  }

  distanceTo(x, z) { return Math.hypot(x - this.x, z - this.z); }

  /** True when a player standing at (x,z) is on the interior side. */
  isInside(x, z) {
    return (x - this.x) * this.nx + (z - this.z) * this.nz < 0;
  }

  update(dt) {
    this.repairCooldown = clamp(this.repairCooldown - dt, 0, 10);
  }
}
