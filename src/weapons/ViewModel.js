import * as THREE from 'three';
import { makeWeaponModel, makeKnifeModel } from '../world/Props.js';
import { Tex } from '../world/Textures.js';
import { applyEnvironment } from '../world/Environment.js';
import { damp, clamp, rand } from '../core/Util.js';

const HIP = new THREE.Vector3(0.2, -0.185, -0.44);
const SPRINT = new THREE.Vector3(0.26, -0.26, -0.4);
const RELOAD = new THREE.Vector3(0.23, -0.33, -0.42);
const DOWN = new THREE.Vector3(0.22, -0.46, -0.4);
// The view model uses its own, narrower field of view so the gun keeps a
// consistent size no matter what the player sets for the world camera.
const VM_FOV = 65;
// View models are drawn a little under life size; guns read better slightly
// shrunk in a 65-degree view than they do at true scale.
const VM_SCALE = 0.66;

/**
 * First-person weapon rendering. Lives on a dedicated overlay scene so the
 * gun never clips into geometry.
 */
export class ViewModel {
  constructor(renderer) {
    this.scene = new THREE.Scene();
    if (renderer) applyEnvironment(this.scene, renderer, 1.1);
    this.camera = new THREE.PerspectiveCamera(VM_FOV, 1, 0.01, 6);
    this.root = new THREE.Group();
    this.scene.add(this.root);

    const key = new THREE.DirectionalLight(0xfff2e0, 9.0);
    key.position.set(-0.6, 1.0, 0.8);
    this.scene.add(key);
    const fill = new THREE.DirectionalLight(0x9ab6d0, 3.6);
    fill.position.set(0.8, -0.3, -0.6);
    this.scene.add(fill);
    this.scene.add(new THREE.AmbientLight(0x8fa2b2, 6.5));

    this.holder = new THREE.Group();
    this.root.add(this.holder);

    this.model = null;
    this.knife = makeKnifeModel(VM_SCALE);
    this.knife.visible = false;
    this.holder.add(this.knife);

    // Muzzle flash: cross-planes plus a punchy light.
    const flashMat = new THREE.MeshBasicMaterial({
      map: Tex.particle(), color: 0xffdca8, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false,
    });
    this.flash = new THREE.Group();
    for (let i = 0; i < 2; i++) {
      const p = new THREE.Mesh(new THREE.PlaneGeometry(0.32, 0.32), flashMat);
      p.rotation.z = i * Math.PI / 2;
      this.flash.add(p);
    }
    this.flash.visible = false;
    this.holder.add(this.flash);
    this.flashLight = new THREE.PointLight(0xffd0a0, 0, 3.0);
    this.holder.add(this.flashLight);

    this.pos = HIP.clone();
    this.rot = new THREE.Vector3();
    this.recoil = { pos: 0, rot: 0, vel: 0 };
    this.sway = new THREE.Vector2();
    this.bobT = 0;
    this.adsT = 0;
    this.meleeT = 0;
    this.swapT = 0;
    this.flashT = 0;
    this.adsOffset = new THREE.Vector3(0, -0.085, -0.3);
  }

  setWeapon(def) {
    if (this.model) this.holder.remove(this.model);
    this.model = makeWeaponModel(def, VM_SCALE);
    this.model.traverse((o) => { o.castShadow = false; o.receiveShadow = false; });
    this.holder.add(this.model);
    const sh = (this.model.userData.sightHeight ?? 0.09) * VM_SCALE;
    this.adsOffset.set(0, -sh - 0.015, -0.34);
    const muzzleZ = ((def.shape?.recv?.[2] ?? 0.35) / 2 + (def.shape?.barrel ?? 0.35)) * VM_SCALE;
    this.muzzleLocal = new THREE.Vector3(0, 0.012, muzzleZ);
    this.flash.position.copy(this.muzzleLocal);
    this.flashLight.position.copy(this.muzzleLocal);
    this.swapT = 1;
  }

  showKnife(on) {
    this.knife.visible = on;
    if (this.model) this.model.visible = !on;
  }

  fireKick(def) {
    const heavy = clamp((def.damage / 200) + (def.recoil.v / 6), 0.25, 1.5);
    this.recoil.vel += 6.5 * heavy;
    this.flashT = 0.055;
    this.flash.visible = true;
    this.flash.rotation.z = rand(0, Math.PI);
    const s = 0.7 + Math.random() * 0.7 + heavy * 0.35;
    this.flash.scale.setScalar(s);
    this.flashLight.intensity = 14 * heavy;
  }

  startMelee() { this.meleeT = 0.42; }
  startReload() { this.reloadAnim = true; }
  endReload() { this.reloadAnim = false; }

  update(dt, ctx) {
    const {
      ads = false, moving = 0, sprinting = false, reloading = false,
      lookDX = 0, lookDY = 0, downed = false, weapon = null,
    } = ctx;

    // ---- sway from look input
    this.sway.x = damp(this.sway.x, clamp(-lookDX * 2.4, -0.06, 0.06), 9, dt);
    this.sway.y = damp(this.sway.y, clamp(lookDY * 2.4, -0.06, 0.06), 9, dt);

    // ---- target pose
    this.adsT = damp(this.adsT, ads ? 1 : 0, weapon ? 1 / Math.max(0.06, weapon.def.adsTime * 0.42) : 12, dt);
    let target = HIP;
    if (reloading) target = RELOAD;
    else if (sprinting && !ads) target = SPRINT;
    if (downed) target = DOWN;

    const base = new THREE.Vector3().copy(target);
    if (this.adsT > 0.001 && !reloading) base.lerp(this.adsOffset, this.adsT);

    // ---- bob
    this.bobT += dt * (sprinting ? 12 : 8.5) * clamp(moving, 0, 1.4);
    const bobScale = (1 - this.adsT * 0.82) * clamp(moving, 0, 1);
    const bx = Math.cos(this.bobT) * 0.016 * bobScale;
    const by = Math.abs(Math.sin(this.bobT)) * 0.014 * bobScale;

    // ---- recoil spring
    this.recoil.vel -= this.recoil.pos * 210 * dt;
    this.recoil.vel *= Math.exp(-13 * dt);
    this.recoil.pos += this.recoil.vel * dt;
    const rec = this.recoil.pos * (1 - this.adsT * 0.35);

    // ---- melee swing
    let meleeX = 0, meleeRot = 0, meleeZ = 0;
    if (this.meleeT > 0) {
      this.meleeT = Math.max(0, this.meleeT - dt);
      const t = 1 - this.meleeT / 0.42;
      const swing = Math.sin(clamp(t, 0, 1) * Math.PI);
      meleeX = -swing * 0.22;
      meleeZ = swing * 0.2;
      meleeRot = swing * 1.15;
    }

    // ---- weapon swap dip
    if (this.swapT > 0) this.swapT = Math.max(0, this.swapT - dt * 2.6);
    const swapDip = this.swapT * this.swapT * 0.22;

    this.holder.position.set(
      base.x + bx + this.sway.x + meleeX,
      base.y + by + this.sway.y - swapDip - rec * 0.05,
      base.z - rec * 0.09 + meleeZ,
    );

    const reloadTilt = reloading ? Math.sin(performance.now() * 0.012) * 0.05 : 0;
    this.holder.rotation.set(
      -rec * 0.5 + this.sway.y * 1.6 + reloadTilt - meleeRot * 0.4,
      -this.sway.x * 2.6 + (reloading ? 0.45 : 0) + meleeRot * 0.5,
      this.sway.x * 1.4 + (sprinting && !ads ? 0.28 : 0) + (downed ? 0.4 : 0) + meleeRot * 0.6,
    );

    // ---- muzzle flash decay
    if (this.flashT > 0) {
      this.flashT -= dt;
      this.flashLight.intensity *= Math.exp(-26 * dt);
      if (this.flashT <= 0) {
        this.flash.visible = false;
        this.flashLight.intensity = 0;
      }
    }

    // ---- magazine drop during reload
    const magNode = this.model?.userData?.magazine;
    if (magNode) {
      const p = reloading && weapon ? weapon.reloadProgress : 1;
      const drop = reloading ? (p < 0.5 ? p * 2 : (1 - p) * 2) : 0;
      magNode.position.y = magNode.userData.homeY ?? (magNode.userData.homeY = magNode.position.y);
      magNode.position.y -= drop * 0.22;
      magNode.visible = !(reloading && p > 0.15 && p < 0.55);
    }

    // ---- energy weapons pulse
    const energy = this.model?.userData?.energyParts;
    if (energy) {
      const k = 1.6 + Math.sin(performance.now() * 0.006) * 0.7;
      for (const e of energy) e.material.emissiveIntensity = k;
    }
  }

  render(renderer) {
    this.camera.fov = VM_FOV - this.adsT * 8;
    this.camera.updateProjectionMatrix();
    renderer.autoClear = false;
    renderer.clearDepth();
    renderer.render(this.scene, this.camera);
    renderer.autoClear = true;
  }

  resize(aspect) {
    this.camera.aspect = aspect;
    this.camera.updateProjectionMatrix();
  }

  /** World-space muzzle position, for tracers and audio. */
  muzzleWorld(playerCamera, out = new THREE.Vector3()) {
    if (!this.model) return out.copy(playerCamera.position);
    out.copy(this.muzzleLocal ?? new THREE.Vector3());
    this.holder.localToWorld(out);
    // The view model lives in its own space; map it in front of the player.
    out.applyMatrix4(playerCamera.matrixWorld);
    return out;
  }
}
