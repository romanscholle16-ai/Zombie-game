import * as THREE from 'three';
import { makeWeaponModel, makeKnifeModel } from '../world/Props.js';
import { Tex } from '../world/Textures.js';
import { applyEnvironment } from '../world/Environment.js';
import { Assets } from '../world/AssetLibrary.js';
import { damp, clamp, rand } from '../core/Util.js';

const HIP = new THREE.Vector3(0.13, -0.105, -0.36);
const SPRINT = new THREE.Vector3(0.185, -0.2, -0.31);
const RELOAD = new THREE.Vector3(0.16, -0.25, -0.33);
const DOWN = new THREE.Vector3(0.17, -0.38, -0.3);
// The view model uses its own, narrower field of view so the gun keeps a
// consistent size no matter what the player sets for the world camera.
const VM_FOV = 65;
// View models are drawn well under life size. Now that the models have true
// firearm proportions, anything near 1:1 fills half the screen in a 65-degree
// view — this is the scale that keeps a 1.4 m bolt gun readable in frame.
const VM_SCALE = 0.48;

/**
 * Fills in the anchors the view model needs for a model that came from the
 * asset library. The manifest can state them explicitly; otherwise they are
 * derived from the bounding box, which is close enough to hold a gun by.
 */
function applyExternalWeaponMetadata(model, def) {
  const spec = model.userData.spec ?? {};
  const box3 = new THREE.Box3().setFromObject(model);
  const size = box3.getSize(new THREE.Vector3());
  const center = box3.getCenter(new THREE.Vector3());
  const u = model.userData;
  u.gripAnchor = spec.grip
    ? new THREE.Vector3(...spec.grip)
    : new THREE.Vector3(center.x, box3.min.y + size.y * 0.32, center.z - size.z * 0.16);
  u.muzzle = spec.muzzle
    ? new THREE.Vector3(...spec.muzzle)
    : new THREE.Vector3(center.x, center.y, box3.max.z);
  u.sightHeight = spec.sightHeight ?? box3.max.y;
  if (spec.opticMag) u.optic = { magnification: spec.opticMag, scoped: spec.opticMag >= 4 };
  else u.optic = def.shape?.optic
    ? { magnification: def.shape.opticMag ?? 2, scoped: (def.shape.opticMag ?? 2) >= 4 }
    : null;
}

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

    // Weapon models are built muzzle-forward along +z, but a three.js camera
    // looks down -z. Everything that belongs to the gun hangs off this flipped
    // node so the muzzle points away from the player instead of at them.
    this.gunRoot = new THREE.Group();
    this.gunRoot.rotation.y = Math.PI;
    this.holder.add(this.gunRoot);

    this.model = null;
    this.optic = null;
    this.knife = makeKnifeModel(VM_SCALE);
    this.knife.visible = false;
    this.knife.position.set(0.01, -0.02, 0.06);
    this.gunRoot.add(this.knife);

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
    this.gunRoot.add(this.flash);
    this.flashLight = new THREE.PointLight(0xffd0a0, 0, 3.0);
    this.gunRoot.add(this.flashLight);

    this.pos = HIP.clone();
    this.rot = new THREE.Vector3();
    this.recoil = { pos: 0, rot: 0, vel: 0 };
    this.sway = new THREE.Vector2();
    this.bobT = 0;
    this.adsT = 0;
    this.sprintT = 0;
    this.reloadT = 0;
    this.downT = 0;
    this.meleeT = 0;
    this.swapT = 0;
    this.flashT = 0;
    this.adsOffset = new THREE.Vector3(0, -0.085, -0.3);
  }

  setWeapon(def) {
    if (this.model) this.gunRoot.remove(this.model);
    // A supplied weapon model replaces the procedural one, but the procedural
    // build is what knows where the grip, muzzle and sights are — so when an
    // external model has no metadata of its own, measure it for the same.
    const ext = Assets.get(`weapon.${def.id}`, { scale: VM_SCALE });
    this.model = ext ?? makeWeaponModel(def, VM_SCALE);
    if (ext) applyExternalWeaponMetadata(ext, def);
    this.model.traverse((o) => { o.castShadow = false; o.receiveShadow = false; });
    this.gunRoot.add(this.model);
    // Hang the model off its grip so weapons of wildly different length all
    // sit in the hand rather than being centred on their own receivers.
    const anchor = this.model.userData.gripAnchor ?? new THREE.Vector3();
    this.model.position.set(-anchor.x * VM_SCALE, -anchor.y * VM_SCALE, -anchor.z * VM_SCALE);

    const sh = ((this.model.userData.sightHeight ?? 0.09) - anchor.y) * VM_SCALE;
    this.adsOffset.set(0, -sh - 0.012, -0.3);
    // The model reports where its bore actually exits, so tracers and the
    // flash sit on the muzzle device instead of a guess based on the envelope.
    this.muzzleLocal = (this.model.userData.muzzle ?? new THREE.Vector3(0, 0.012, 0.5))
      .clone().sub(anchor).multiplyScalar(VM_SCALE);
    this.optic = this.model.userData.optic ?? null;
    this.flash.position.copy(this.muzzleLocal);
    this.flashLight.position.copy(this.muzzleLocal);
    this.swapT = 1;
  }

  showKnife(on) {
    this.knifeOut = on;
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
    // Every pose is a damped weight rather than a hard switch. Sprint in
    // particular toggles on and off around the stamina floor, and swapping the
    // target vector outright made the gun strobe between two positions.
    this.adsT = damp(this.adsT, ads ? 1 : 0, weapon ? 1 / Math.max(0.06, weapon.def.adsTime * 0.42) : 12, dt);
    this.sprintT = damp(this.sprintT ?? 0, sprinting && !ads && !reloading && !downed ? 1 : 0, 9, dt);
    this.reloadT = damp(this.reloadT ?? 0, reloading && !downed ? 1 : 0, 11, dt);
    this.downT = damp(this.downT ?? 0, downed ? 1 : 0, 7, dt);

    const base = HIP.clone();
    if (this.sprintT > 0.001) base.lerp(SPRINT, this.sprintT);
    if (this.reloadT > 0.001) base.lerp(RELOAD, this.reloadT);
    if (this.adsT > 0.001) base.lerp(this.adsOffset, this.adsT * (1 - this.reloadT));
    if (this.downT > 0.001) base.lerp(DOWN, this.downT);

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

    const reloadTilt = Math.sin(performance.now() * 0.012) * 0.05 * this.reloadT;
    this.holder.rotation.set(
      -rec * 0.5 + this.sway.y * 1.6 + reloadTilt - meleeRot * 0.4,
      -this.sway.x * 2.6 + this.reloadT * 0.45 + meleeRot * 0.5,
      this.sway.x * 1.4 + this.sprintT * 0.28 + this.downT * 0.4 + meleeRot * 0.6,
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

  /** How much the world camera should zoom while aiming this weapon. */
  get adsZoom() {
    const m = this.optic?.magnification ?? 1;
    return Math.max(1.39, m * 1.1);
  }

  /** Sight-picture state for the HUD, or null when this optic has no tube. */
  scopeState() {
    if (!this.optic?.scoped || this.knifeOut) return null;
    return { t: this.adsT, sway: { x: this.sway.x, y: this.sway.y } };
  }

  render(renderer) {
    // Behind a magnified optic the weapon is out of the sight picture, so it
    // is not drawn at all once the scope has taken over the screen.
    if (this.optic?.scoped && this.adsT > 0.72) return;
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
    this.gunRoot.updateMatrixWorld();
    this.gunRoot.localToWorld(out);
    // The view model lives in its own space; map it in front of the player.
    out.applyMatrix4(playerCamera.matrixWorld);
    return out;
  }
}
