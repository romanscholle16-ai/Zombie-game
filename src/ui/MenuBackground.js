import * as THREE from 'three';
import { Zombie } from '../entities/Zombie.js';
import { Tex, mat } from '../world/Textures.js';
import { box, makeBarrel, makeCrate, Mats } from '../world/Props.js';
import { applyEnvironment } from '../world/Environment.js';
import { rand, clamp } from '../core/Util.js';

/**
 * The animated backdrop behind the menus: a fogged corridor with a slow
 * procession of zombies walking toward camera.
 */
export class MenuBackground {
  constructor(renderer) {
    this.scene = new THREE.Scene();
    if (renderer) applyEnvironment(this.scene, renderer, 0.45);
    this.scene.fog = new THREE.FogExp2(0x080b0d, 0.052);
    this.scene.background = new THREE.Color(0x05070a);

    this.camera = new THREE.PerspectiveCamera(52, 16 / 9, 0.1, 120);
    this.camera.position.set(1.6, 1.75, 7.5);
    this.camera.lookAt(0.4, 1.5, -14);

    // ---- corridor
    const floorMat = mat('mbFloor', { map: Tex.concreteFloor(14), roughness: 0.95 });
    const wallMat = mat('mbWall', { map: Tex.concrete(5), roughness: 0.96 });
    const floor = box(22, 0.4, 90, floorMat, 0, -0.2, -30);
    floor.receiveShadow = true;
    this.scene.add(floor);
    this.scene.add(box(22, 0.4, 90, mat('mbCeil', { color: 0x14181a, roughness: 1 }), 0, 5.2, -30));
    for (const s of [-1, 1]) {
      this.scene.add(box(0.6, 5.6, 90, wallMat, s * 7, 2.6, -30));
    }

    // Side alcoves + pipes to break the silhouette up.
    for (let i = 0; i < 8; i++) {
      const z = 2 - i * 9;
      for (const s of [-1, 1]) {
        this.scene.add(box(0.35, 3.4, 0.35, Mats.rust, s * 6.4, 1.7, z));
        this.scene.add(box(1.6, 0.16, 0.5, Mats.darkMetal, s * 6.0, 4.4, z + 2));
      }
      if (i % 2 === 0) {
        const c = makeCrate(1.1, i);
        c.position.set(rand(-5.5, 5.5), 0, z + rand(-2, 2));
        c.rotation.y = rand(0, 6.28);
        this.scene.add(c);
      } else {
        const b = makeBarrel(i);
        b.position.set(rand(-5.8, 5.8), 0, z + rand(-2, 2));
        this.scene.add(b);
      }
    }

    // ---- lighting: one swinging work lamp plus cold fill
    this.scene.add(new THREE.HemisphereLight(0x36485a, 0x0a0d12, 1.1));
    this.lamp = new THREE.PointLight(0xffc078, 110, 40);
    this.lamp.position.set(0, 4.0, -2);
    this.scene.add(this.lamp);
    this.lampBody = box(0.5, 0.16, 0.5, Mats.darkMetal, 0, 4.3, -2);
    this.scene.add(this.lampBody);

    this.backLight = new THREE.PointLight(0x66d9ff, 60, 44);
    this.backLight.position.set(0, 3.0, -26);
    this.scene.add(this.backLight);

    this.rim = new THREE.DirectionalLight(0x9fd93a, 0.9);
    this.rim.position.set(-4, 6, -18);
    this.scene.add(this.rim);

    // ---- dust motes
    const n = 400;
    const g = new THREE.BufferGeometry();
    const pos = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) {
      pos[i * 3] = rand(-7, 7);
      pos[i * 3 + 1] = rand(0.2, 5);
      pos[i * 3 + 2] = rand(-60, 8);
    }
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    this.dust = new THREE.Points(g, new THREE.PointsMaterial({
      size: 0.035, color: 0xbfd0c0, transparent: true, opacity: 0.5,
      map: Tex.particle(), depthWrite: false, blending: THREE.AdditiveBlending,
    }));
    this.scene.add(this.dust);

    // ---- the procession
    this.zombies = [];
    for (let i = 0; i < 7; i++) {
      const z = new Zombie(this.scene);
      z.spawn({
        type: i === 3 ? 'heavy' : i === 5 ? 'runner' : 'normal',
        x: rand(-4.5, 4.5), y: 0, z: -12 - i * 8,
        health: 100, aggression: 0.3, damageScale: 1,
      });
      z.state = 3;
      z.yaw = Math.PI;
      z.walkSpeed = rand(0.5, 1.0);
      this.zombies.push(z);
    }

    this.t = 0;
  }

  update(dt) {
    this.t += dt;
    const t = this.t;

    // Slow drifting camera with a subtle breathing motion.
    this.camera.position.x = 1.6 + Math.sin(t * 0.14) * 1.2;
    this.camera.position.y = 1.75 + Math.sin(t * 0.31) * 0.09;
    this.camera.position.z = 7.5 + Math.sin(t * 0.09) * 0.9;
    this.camera.lookAt(Math.sin(t * 0.11) * 1.2, 1.55 + Math.sin(t * 0.2) * 0.1, -16);

    // Swinging, failing work lamp.
    const swing = Math.sin(t * 0.9) * 0.9;
    this.lamp.position.x = swing;
    this.lampBody.position.x = swing;
    this.lampBody.rotation.z = -swing * 0.12;
    const flicker = Math.random() < 0.02 ? rand(0.1, 0.4) : 1;
    this.lamp.intensity = (105 + Math.sin(t * 6.2) * 18) * flicker;
    this.backLight.intensity = 55 + Math.sin(t * 1.7) * 14;

    this.dust.rotation.y = t * 0.01;

    for (const z of this.zombies) {
      z.pos.z += z.walkSpeed * dt;
      z.pos.x += Math.sin(t * 0.6 + z.phase) * 0.14 * dt;
      if (z.pos.z > 9) {
        z.pos.z = -62 - rand(0, 8);
        z.pos.x = rand(-4.6, 4.6);
        z.walkSpeed = rand(0.5, 1.05);
      }
      z.vel.set(0, 0, z.walkSpeed);
      z.group.position.copy(z.pos);
      z.group.rotation.y = Math.PI;
      z._animate(dt, t);
    }
  }

  resize(aspect) {
    this.camera.aspect = aspect;
    this.camera.updateProjectionMatrix();
  }

  dispose() {
    for (const z of this.zombies) z.despawn();
  }
}

export { clamp };
