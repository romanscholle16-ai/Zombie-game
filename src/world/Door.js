import * as THREE from 'three';
import { box, Mats } from './Props.js';
import { Tex } from './Textures.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp, smoothstep } from '../core/Util.js';

/**
 * A purchasable blast door. Buying one permanently opens the zones behind it
 * and expands both the playable area and the zombie spawn pool.
 */
export class Door {
  constructor(def, floorY, height) {
    this.def = def;
    this.id = def.id;
    this.name = def.name;
    this.cost = def.cost;
    this.requiresPower = !!def.requiresPower;
    this.opens = def.opens;
    this.open = false;
    this.t = 0;

    this.cx = (def.x0 + def.x1) / 2;
    this.cz = (def.z0 + def.z1) / 2;
    this.y = floorY;
    this.width = Math.max(def.x1 - def.x0, def.z1 - def.z0);
    this.alongX = def.x1 - def.x0 > def.z1 - def.z0;
    this.height = Math.min(height ?? 4, 4.2);

    this.rect = { x0: def.x0, x1: def.x1, z0: def.z0, z1: def.z1 };
    this.collider = {
      x0: def.x0 - (this.alongX ? 0 : 0.05), x1: def.x1 + (this.alongX ? 0 : 0.05),
      z0: def.z0 - (this.alongX ? 0.05 : 0), z1: def.z1 + (this.alongX ? 0.05 : 0),
      y0: floorY, y1: floorY + this.height, solid: true,
    };

    this.group = new THREE.Group();
    this.group.position.set(this.cx, floorY, this.cz);
    this.group.rotation.y = this.alongX ? 0 : Math.PI / 2;
    this._build();
  }

  _build() {
    const w = this.width, h = this.height;
    const leafW = w / 2;
    this.leaves = [];
    for (const s of [-1, 1]) {
      const leaf = new THREE.Group();
      leaf.add(box(leafW, h, 0.35, Mats.metal, 0, h / 2, 0));
      // Rib detail.
      for (let i = 0; i < 3; i++) {
        leaf.add(box(leafW * 0.82, 0.12, 0.42, Mats.darkMetal, 0, h * (0.25 + i * 0.25), 0));
      }
      leaf.add(box(0.14, h, 0.45, Mats.hazard, s * (leafW / 2 - 0.1), h / 2, 0));
      leaf.position.x = s * leafW / 2;
      this.group.add(leaf);
      this.leaves.push({ node: leaf, dir: s, home: s * leafW / 2 });
    }

    const label = this.requiresPower
      ? Tex.label('SEALED', { fg: '#e8a33d', sub: 'REQUIRES POWER' })
      : Tex.label(`${this.cost}`, { fg: '#9fd93a', sub: 'HOLD F TO OPEN' });
    for (const s of [-1, 1]) {
      const sign = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 0.85), new THREE.MeshBasicMaterial({ map: label }));
      sign.position.set(0, h * 0.62, s * 0.2);
      sign.rotation.y = s > 0 ? 0 : Math.PI;
      this.group.add(sign);
    }

    const lamp = box(0.16, 0.16, 0.5, Mats.emissive(0xff2b1e, 2.0), 0, h - 0.35, 0);
    this.group.add(lamp);
    this.lamp = lamp;
  }

  canBuy(points, powerOn) {
    if (this.open) return false;
    if (this.requiresPower) return powerOn;
    return points >= this.cost;
  }

  purchase() {
    if (this.open) return;
    this.open = true;
    Audio.playAt('door', { x: this.cx, y: this.y + 1.5, z: this.cz }, { volume: 1.1 });
    this.lamp.material = Mats.emissive(0x9fd93a, 1.6);
  }

  update(dt) {
    const target = this.open ? 1 : 0;
    if (this.t === target) return false;
    this.t = clamp(this.t + (target - this.t > 0 ? dt / 1.6 : -dt / 1.6), 0, 1);
    const e = smoothstep(this.t);
    for (const l of this.leaves) {
      l.node.position.x = l.home + l.dir * e * (this.width / 2 + 0.15);
      l.node.position.y = -e * 0.02;
    }
    if (this.t >= 1) {
      this.group.visible = false;
      return true; // finished opening
    }
    return false;
  }
}
