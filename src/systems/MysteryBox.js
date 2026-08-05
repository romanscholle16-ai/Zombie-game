import * as THREE from 'three';
import { makeMysteryBox, makeWeaponModel } from '../world/Props.js';
import { Interactable } from './Interaction.js';
import { MAP } from '../world/MapData.js';
import { boxPool, RARITY } from '../weapons/WeaponDefs.js';
import { Audio } from '../core/AudioEngine.js';
import { rand, randInt, pick, weightedPick, clamp } from '../core/Util.js';

const SPIN_TIME = 2.9;
const GRAB_TIME = 9.0;
const BASE_COST = 950;

const STATE = { IDLE: 0, SPIN: 1, REVEAL: 2, LEAVING: 3, HIDDEN: 4 };

/**
 * The mystery crate. Spins for a random weapon, occasionally packs up and
 * relocates to another anchor so its position is never a given.
 */
export class MysteryBox {
  constructor(world, scene, interaction, ctxRef) {
    this.world = world;
    this.scene = scene;
    this.ctxRef = ctxRef;
    this.pool = boxPool();
    this.state = STATE.IDLE;
    this.t = 0;
    this.usesLeft = randInt(4, 8);
    this.fireSaleT = 0;
    this.pendingWeapon = null;

    this.group = makeMysteryBox();
    scene.add(this.group);

    this.display = new THREE.Group();
    this.display.visible = false;
    this.group.add(this.display);
    this.displayModel = null;

    this.beam = new THREE.Mesh(
      new THREE.CylinderGeometry(0.5, 0.12, 6, 12, 1, true),
      new THREE.MeshBasicMaterial({
        color: 0x9fd93a, transparent: true, opacity: 0, side: THREE.DoubleSide,
        blending: THREE.AdditiveBlending, depthWrite: false,
      }),
    );
    this.beam.position.y = 3.2;
    this.group.add(this.beam);

    const self = this;
    this.interactable = interaction.add(new (class extends Interactable {
      prompt(ctx) {
        if (self.state === STATE.SPIN) return { text: 'CRATE CYCLING', blocked: true };
        if (self.state === STATE.REVEAL) {
          return { text: `TAKE ${self.pendingWeapon.name}`, sub: RARITY[self.pendingWeapon.rarity].label };
        }
        if (self.state !== STATE.IDLE) return { text: 'CRATE SEALED', blocked: true };
        return { text: 'MYSTERY CRATE', cost: self.cost, sub: self.fireSaleT > 0 ? 'FIRE SALE' : `${self.usesLeft} CYCLES LEFT` };
      }
      activate(ctx) { return self.activate(ctx); }
    })({ id: 'box', x: 0, y: 0, z: 0, radius: 2.6, priority: 2 }));

    this.spotIndex = 0;
    this.moveTo(MAP.boxSpots[0], true);
  }

  get cost() { return this.fireSaleT > 0 ? 10 : BASE_COST; }
  get spot() { return MAP.boxSpots[this.spotIndex]; }

  /** Anchors inside zones the player can currently reach. */
  availableSpots() {
    return MAP.boxSpots.filter((s) => this.world.isZoneActive(s.zone));
  }

  moveTo(spot, immediate = false) {
    this.spotIndex = MAP.boxSpots.indexOf(spot);
    this.group.position.set(spot.x, spot.y, spot.z);
    this.group.rotation.y = spot.rot;
    this.interactable.pos.set(spot.x, spot.y + 0.6, spot.z);
    this.usesLeft = randInt(4, 8);
    this.state = STATE.IDLE;
    this.group.visible = true;
    this.group.scale.setScalar(1);
    if (!immediate) {
      Audio.playAt('boxstop', spot, { volume: 1 });
      this.ctxRef()?.toast('THE CRATE HAS MOVED', 'warn');
    }
  }

  relocate() {
    const options = this.availableSpots().filter((s) => s !== this.spot);
    const next = options.length ? pick(options) : this.spot;
    this.moveTo(next);
  }

  startFireSale(seconds) {
    this.fireSaleT = seconds;
  }

  activate(ctx) {
    if (this.state === STATE.REVEAL) {
      ctx.giveWeapon(this.pendingWeapon);
      this.state = STATE.IDLE;
      this.display.visible = false;
      this.pendingWeapon = null;
      this.t = 0;
      if (this.usesLeft <= 0) {
        this.state = STATE.LEAVING;
        this.t = 2.0;
      }
      return true;
    }
    if (this.state !== STATE.IDLE) return false;
    if (!ctx.player.spend(this.cost)) { ctx.deny(); return false; }
    ctx.player.stats.boxSpins++;
    this.state = STATE.SPIN;
    this.t = SPIN_TIME;
    this.usesLeft--;
    this.cycleT = 0;
    this.display.visible = true;
    Audio.playAt('box', this.group.position, { volume: 1 });
    return true;
  }

  _rollWeapon(round) {
    // Higher rounds tilt the pool toward better hardware.
    const bias = clamp((round - 4) / 22, 0, 1);
    const entries = this.pool.map((w) => {
      const order = RARITY[w.rarity].order;
      const weight = w.boxWeight * (1 + order * bias * 1.6) * (order === 4 ? 0.55 + bias : 1);
      return { weight, w };
    });
    return weightedPick(entries).w;
  }

  _setDisplay(def) {
    if (this.displayModel) this.display.remove(this.displayModel);
    this.displayModel = makeWeaponModel(def, 1.15);
    this.displayModel.position.set(0, 1.55, 0);
    this.display.add(this.displayModel);
  }

  update(dt, time, round) {
    if (this.fireSaleT > 0) this.fireSaleT -= dt;
    const lid = this.group.userData.lid;
    const glow = this.group.userData.glow;

    switch (this.state) {
      case STATE.SPIN: {
        this.t -= dt;
        this.cycleT -= dt;
        lid.rotation.x = -clamp((SPIN_TIME - this.t) * 3, 0, 1) * 1.25;
        glow.intensity = 48 + Math.sin(time * 22) * 22;
        this.beam.material.opacity = 0.16 + Math.sin(time * 18) * 0.06;
        if (this.cycleT <= 0) {
          // Slow the carousel down as it settles.
          const p = 1 - this.t / SPIN_TIME;
          this.cycleT = 0.07 + p * p * 0.36;
          this._setDisplay(pick(this.pool));
        }
        if (this.displayModel) {
          this.displayModel.rotation.y = time * 3.2;
          this.displayModel.position.y = 1.55 + Math.sin(time * 6) * 0.05;
        }
        if (this.t <= 0) {
          this.pendingWeapon = this._rollWeapon(round);
          this._setDisplay(this.pendingWeapon);
          this.state = STATE.REVEAL;
          this.t = GRAB_TIME;
          const rare = RARITY[this.pendingWeapon.rarity].order >= 3;
          Audio.playAt('boxstop', this.group.position, { volume: 1, rare });
          this.ctxRef()?.toast(this.pendingWeapon.name, rare ? 'warn' : 'pts');
        }
        break;
      }
      case STATE.REVEAL: {
        this.t -= dt;
        glow.intensity = 40 + Math.sin(time * 6) * 12;
        this.beam.material.opacity = 0.12;
        if (this.displayModel) {
          this.displayModel.rotation.y = time * 1.6;
          this.displayModel.position.y = 1.55 + Math.sin(time * 3) * 0.06;
        }
        if (this.t <= 0) {
          this.display.visible = false;
          this.pendingWeapon = null;
          this.state = this.usesLeft <= 0 ? STATE.LEAVING : STATE.IDLE;
          this.t = this.usesLeft <= 0 ? 2.0 : 0;
          lid.rotation.x = 0;
        }
        break;
      }
      case STATE.LEAVING: {
        this.t -= dt;
        const k = clamp(1 - this.t / 2.0, 0, 1);
        this.group.position.y = this.spot.y + k * k * 9;
        this.group.rotation.y += dt * 5;
        this.group.scale.setScalar(1 - k * 0.5);
        this.beam.material.opacity = 0.3 * (1 - k);
        if (this.t <= 0) {
          this.group.visible = false;
          this.state = STATE.HIDDEN;
          this.t = 1.2;
        }
        break;
      }
      case STATE.HIDDEN: {
        this.t -= dt;
        if (this.t <= 0) this.relocate();
        break;
      }
      default: {
        lid.rotation.x += (0 - lid.rotation.x) * Math.min(1, dt * 6);
        glow.intensity = this.fireSaleT > 0 ? 34 + Math.sin(time * 12) * 16 : 9 + Math.sin(time * 1.4) * 4;
        this.beam.material.opacity = this.fireSaleT > 0 ? 0.14 : 0.05;
        break;
      }
    }
  }

  reset() {
    this.state = STATE.IDLE;
    this.fireSaleT = 0;
    this.display.visible = false;
    this.moveTo(MAP.boxSpots[0], true);
  }
}

export { rand };
