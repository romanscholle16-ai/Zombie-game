import * as THREE from 'three';
import { Tex } from '../world/Textures.js';
import { Settings } from '../core/Settings.js';
import { rand, clamp } from '../core/Util.js';

const VERT = `
attribute float aSize;
attribute float aAlpha;
attribute vec3 aColor;
varying float vAlpha;
varying vec3 vColor;
void main() {
  vAlpha = aAlpha;
  vColor = aColor;
  vec4 mv = modelViewMatrix * vec4(position, 1.0);
  gl_PointSize = aSize * (300.0 / max(0.001, -mv.z));
  gl_Position = projectionMatrix * mv;
}`;

const FRAG = `
uniform sampler2D uMap;
varying float vAlpha;
varying vec3 vColor;
void main() {
  vec4 t = texture2D(uMap, gl_PointCoord);
  if (t.a * vAlpha < 0.01) discard;
  gl_FragColor = vec4(vColor, t.a * vAlpha);
}`;

class ParticleSystem {
  constructor(scene, capacity, blending, depthWrite = false) {
    this.capacity = capacity;
    this.count = 0;
    this.pos = new Float32Array(capacity * 3);
    this.vel = new Float32Array(capacity * 3);
    this.life = new Float32Array(capacity);
    this.maxLife = new Float32Array(capacity);
    this.size = new Float32Array(capacity);
    this.alpha = new Float32Array(capacity);
    this.color = new Float32Array(capacity * 3);
    this.gravity = new Float32Array(capacity);
    this.drag = new Float32Array(capacity);

    this.geo = new THREE.BufferGeometry();
    this.geo.setAttribute('position', new THREE.BufferAttribute(this.pos, 3));
    this.geo.setAttribute('aSize', new THREE.BufferAttribute(this.size, 1));
    this.geo.setAttribute('aAlpha', new THREE.BufferAttribute(this.alpha, 1));
    this.geo.setAttribute('aColor', new THREE.BufferAttribute(this.color, 3));
    this.geo.setDrawRange(0, 0);
    this.mat = new THREE.ShaderMaterial({
      uniforms: { uMap: { value: Tex.particle() } },
      vertexShader: VERT,
      fragmentShader: FRAG,
      transparent: true,
      depthWrite,
      blending,
    });
    this.points = new THREE.Points(this.geo, this.mat);
    this.points.frustumCulled = false;
    scene.add(this.points);
  }

  emit(p, v, opts = {}) {
    let i = this.count;
    if (i >= this.capacity) {
      // Recycle the oldest slot rather than dropping the effect.
      i = this._recycle = ((this._recycle ?? 0) + 1) % this.capacity;
    } else {
      this.count++;
    }
    const i3 = i * 3;
    this.pos[i3] = p.x; this.pos[i3 + 1] = p.y; this.pos[i3 + 2] = p.z;
    this.vel[i3] = v.x; this.vel[i3 + 1] = v.y; this.vel[i3 + 2] = v.z;
    const life = opts.life ?? 0.7;
    this.life[i] = life;
    this.maxLife[i] = life;
    this.size[i] = opts.size ?? 0.08;
    this.alpha[i] = opts.alpha ?? 1;
    const c = opts.color ?? 0xffffff;
    this.color[i3] = ((c >> 16) & 255) / 255;
    this.color[i3 + 1] = ((c >> 8) & 255) / 255;
    this.color[i3 + 2] = (c & 255) / 255;
    this.gravity[i] = opts.gravity ?? 9.0;
    this.drag[i] = opts.drag ?? 1.4;
  }

  update(dt) {
    let n = this.count;
    for (let i = 0; i < n; i++) {
      this.life[i] -= dt;
      if (this.life[i] <= 0) {
        // Swap-remove with the tail.
        const last = n - 1;
        if (i !== last) this._copy(last, i);
        n--;
        i--;
        continue;
      }
      const i3 = i * 3;
      const d = Math.exp(-this.drag[i] * dt);
      this.vel[i3] *= d;
      this.vel[i3 + 2] *= d;
      this.vel[i3 + 1] = this.vel[i3 + 1] * d - this.gravity[i] * dt;
      this.pos[i3] += this.vel[i3] * dt;
      this.pos[i3 + 1] += this.vel[i3 + 1] * dt;
      this.pos[i3 + 2] += this.vel[i3 + 2] * dt;
      this.alpha[i] = clamp(this.life[i] / this.maxLife[i], 0, 1);
    }
    this.count = n;
    this.geo.setDrawRange(0, n);
    this.geo.attributes.position.needsUpdate = true;
    this.geo.attributes.aAlpha.needsUpdate = true;
    this.geo.attributes.aSize.needsUpdate = true;
    this.geo.attributes.aColor.needsUpdate = true;
  }

  _copy(from, to) {
    const f3 = from * 3, t3 = to * 3;
    for (let k = 0; k < 3; k++) {
      this.pos[t3 + k] = this.pos[f3 + k];
      this.vel[t3 + k] = this.vel[f3 + k];
      this.color[t3 + k] = this.color[f3 + k];
    }
    this.life[to] = this.life[from];
    this.maxLife[to] = this.maxLife[from];
    this.size[to] = this.size[from];
    this.alpha[to] = this.alpha[from];
    this.gravity[to] = this.gravity[from];
    this.drag[to] = this.drag[from];
  }
}

/** All the transient visual noise: blood, sparks, tracers, explosions. */
export class Effects {
  constructor(scene) {
    this.scene = scene;
    const q = Settings.quality;
    this.scale = q.particles;
    this.solid = new ParticleSystem(scene, Math.round(900 * this.scale) + 200, THREE.NormalBlending);
    this.additive = new ParticleSystem(scene, Math.round(700 * this.scale) + 200, THREE.AdditiveBlending);

    // Tracer pool.
    this.tracers = [];
    this.tracerGeo = new THREE.BufferGeometry();
    const tm = new THREE.LineBasicMaterial({
      color: 0xffd9a0, transparent: true, opacity: 0.7, blending: THREE.AdditiveBlending, depthWrite: false,
    });
    for (let i = 0; i < 24; i++) {
      const g = new THREE.BufferGeometry();
      g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(6), 3));
      const line = new THREE.Line(g, tm.clone());
      line.visible = false;
      line.frustumCulled = false;
      scene.add(line);
      this.tracers.push({ line, t: 0 });
    }

    // Decal pool for blood pools and impact marks.
    this.decals = [];
    const decalGeo = new THREE.PlaneGeometry(1, 1);
    for (let i = 0; i < 48; i++) {
      const m = new THREE.Mesh(decalGeo, new THREE.MeshBasicMaterial({
        map: Tex.particle(), color: 0x4a0a08, transparent: true, opacity: 0, depthWrite: false,
      }));
      m.rotation.x = -Math.PI / 2;
      m.visible = false;
      scene.add(m);
      this.decals.push({ mesh: m, t: 0 });
    }
    this.decalIndex = 0;

    // Explosion flashes.
    this.flashes = [];
    for (let i = 0; i < 6; i++) {
      const l = new THREE.PointLight(0xffa860, 0, 22);
      l.visible = false;
      scene.add(l);
      this.flashes.push({ light: l, t: 0, max: 0 });
    }
  }

  // ------------------------------------------------------------ emitters
  bloodBurst(p, dir = null, scale = 1) {
    const n = Math.round(10 * scale * this.scale) + 3;
    for (let i = 0; i < n; i++) {
      const v = {
        x: rand(-2.2, 2.2) + (dir?.x ?? 0) * 2.4,
        y: rand(0.6, 3.6) * scale,
        z: rand(-2.2, 2.2) + (dir?.z ?? 0) * 2.4,
      };
      this.solid.emit(p, v, {
        color: i % 5 === 0 ? 0x8c1410 : 0x5e0d0a,
        size: rand(0.04, 0.12) * scale,
        life: rand(0.35, 0.85),
        gravity: 11,
        drag: 1.1,
      });
    }
    // Fine mist.
    for (let i = 0; i < Math.round(4 * scale * this.scale); i++) {
      this.solid.emit(p, { x: rand(-1, 1), y: rand(0.3, 1.4), z: rand(-1, 1) }, {
        color: 0x3a0806, size: rand(0.16, 0.32), life: rand(0.5, 1.0), gravity: 1.2, drag: 2.6, alpha: 0.55,
      });
    }
  }

  boneBurst(p) {
    for (let i = 0; i < Math.round(5 * this.scale) + 2; i++) {
      this.solid.emit(p, { x: rand(-2.4, 2.4), y: rand(1.5, 4.2), z: rand(-2.4, 2.4) }, {
        color: 0xd8d2bc, size: rand(0.03, 0.07), life: rand(0.6, 1.2), gravity: 13, drag: 0.8,
      });
    }
  }

  woodBurst(p) {
    for (let i = 0; i < Math.round(12 * this.scale) + 4; i++) {
      this.solid.emit(p, { x: rand(-2.6, 2.6), y: rand(0.5, 3.4), z: rand(-2.6, 2.6) }, {
        color: [0x8a6a3c, 0x6a4f2a, 0xa9884f][i % 3], size: rand(0.03, 0.1), life: rand(0.5, 1.1), gravity: 12, drag: 1.0,
      });
    }
  }

  sparkBurst(p, color = 0xffc46a, count = 12) {
    for (let i = 0; i < Math.round(count * this.scale); i++) {
      this.additive.emit(p, { x: rand(-3.5, 3.5), y: rand(0.4, 4.0), z: rand(-3.5, 3.5) }, {
        color, size: rand(0.02, 0.06), life: rand(0.18, 0.5), gravity: 14, drag: 1.6,
      });
    }
  }

  emberBurst(p) {
    for (let i = 0; i < Math.round(3 * this.scale) + 1; i++) {
      this.additive.emit(p, { x: rand(-0.5, 0.5), y: rand(0.6, 1.8), z: rand(-0.5, 0.5) }, {
        color: [0xff7b2e, 0xffc04a][i % 2], size: rand(0.05, 0.12), life: rand(0.4, 0.9), gravity: -1.4, drag: 1.2,
      });
    }
  }

  impactPuff(p, normal = null, color = 0x8a8f88) {
    for (let i = 0; i < Math.round(6 * this.scale) + 2; i++) {
      this.solid.emit(p, {
        x: rand(-1.4, 1.4) + (normal?.x ?? 0) * 1.6,
        y: rand(0.2, 1.6),
        z: rand(-1.4, 1.4) + (normal?.z ?? 0) * 1.6,
      }, { color, size: rand(0.05, 0.16), life: rand(0.25, 0.6), gravity: 3.4, drag: 2.4, alpha: 0.7 });
    }
    this.sparkBurst(p, 0xffd9a0, 4);
  }

  smoke(p, count = 8, color = 0x2a2e30) {
    for (let i = 0; i < Math.round(count * this.scale); i++) {
      this.solid.emit(p, { x: rand(-1, 1), y: rand(0.8, 2.4), z: rand(-1, 1) }, {
        color, size: rand(0.4, 1.1), life: rand(1.0, 2.2), gravity: -0.6, drag: 1.8, alpha: 0.45,
      });
    }
  }

  explosion(p, radius = 4, color = 0xffa860) {
    this.sparkBurst(p, color, 34);
    this.smoke(p, 14);
    for (let i = 0; i < Math.round(18 * this.scale); i++) {
      this.additive.emit(p, {
        x: rand(-1, 1) * radius * 2, y: rand(0, 1) * radius * 1.6, z: rand(-1, 1) * radius * 2,
      }, { color, size: rand(0.2, 0.6), life: rand(0.2, 0.6), gravity: 2, drag: 3.2 });
    }
    const f = this.flashes.find((x) => !x.light.visible) ?? this.flashes[0];
    f.light.position.copy(p);
    f.light.color.setHex(color);
    f.light.distance = radius * 5;
    f.light.intensity = 420;
    f.max = 420;
    f.t = 0.4;
    f.light.visible = true;
  }

  bloodPool(p, groundY) {
    const d = this.decals[this.decalIndex];
    this.decalIndex = (this.decalIndex + 1) % this.decals.length;
    d.mesh.position.set(p.x, (groundY ?? p.y - 1.0) + 0.03, p.z);
    d.mesh.scale.setScalar(rand(1.0, 2.0));
    d.mesh.rotation.z = rand(0, Math.PI * 2);
    d.mesh.material.opacity = 0.72;
    d.mesh.visible = true;
    d.t = 26;
  }

  tracer(from, to, color = 0xffd9a0) {
    const t = this.tracers.find((x) => !x.line.visible);
    if (!t) return;
    const a = t.line.geometry.attributes.position.array;
    a[0] = from.x; a[1] = from.y; a[2] = from.z;
    a[3] = to.x; a[4] = to.y; a[5] = to.z;
    t.line.geometry.attributes.position.needsUpdate = true;
    t.line.material.color.setHex(color);
    t.line.material.opacity = 0.75;
    t.line.visible = true;
    t.t = 0.06;
  }

  /** Persistent electric arc between two points (wonder weapon + trap). */
  arc(from, to, color = 0x66d9ff) {
    const steps = 7;
    for (let i = 0; i <= steps; i++) {
      const k = i / steps;
      const p = {
        x: from.x + (to.x - from.x) * k + rand(-0.28, 0.28),
        y: from.y + (to.y - from.y) * k + rand(-0.28, 0.28),
        z: from.z + (to.z - from.z) * k + rand(-0.28, 0.28),
      };
      this.additive.emit(p, { x: rand(-0.4, 0.4), y: rand(-0.4, 0.4), z: rand(-0.4, 0.4) }, {
        color, size: rand(0.08, 0.2), life: rand(0.08, 0.2), gravity: 0, drag: 4,
      });
    }
  }

  update(dt) {
    this.solid.update(dt);
    this.additive.update(dt);
    for (const t of this.tracers) {
      if (!t.line.visible) continue;
      t.t -= dt;
      t.line.material.opacity = Math.max(0, t.t / 0.06) * 0.75;
      if (t.t <= 0) t.line.visible = false;
    }
    for (const d of this.decals) {
      if (!d.mesh.visible) continue;
      d.t -= dt;
      if (d.t < 4) d.mesh.material.opacity = Math.max(0, d.t / 4) * 0.72;
      if (d.t <= 0) d.mesh.visible = false;
    }
    for (const f of this.flashes) {
      if (!f.light.visible) continue;
      f.t -= dt;
      f.light.intensity = Math.max(0, f.t / 0.4) * f.max;
      if (f.t <= 0) f.light.visible = false;
    }
  }

  reset() {
    this.solid.count = 0;
    this.additive.count = 0;
    for (const t of this.tracers) t.line.visible = false;
    for (const d of this.decals) d.mesh.visible = false;
    for (const f of this.flashes) f.light.visible = false;
  }
}
