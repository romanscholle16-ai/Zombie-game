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
  constructor(def, floorY) {
    this.id = def.id;
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
    // Sill + head, forming the window aperture in the wall.
    this.group.add(box(3.2, 1.05, 0.5, frameMat, 0, 0.52, 0));
    this.group.add(box(3.2, 0.9, 0.5, frameMat, 0, 3.05, 0));
    this.group.add(box(0.4, 1.6, 0.5, frameMat, -1.6, 1.85, 0));
    this.group.add(box(0.4, 1.6, 0.5, frameMat, 1.6, 1.85, 0));
    // Backing darkness so the alcove reads as a void.
    const back = box(3.0, 1.5, 0.05, new THREE.MeshBasicMaterial({ color: 0x04060a }), 0, 1.85, 0.28);
    back.castShadow = false;
    this.group.add(back);
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
