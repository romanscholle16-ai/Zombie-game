import * as THREE from 'three';
import { makeUpgradeMachine, makeWeaponModel } from '../world/Props.js';
import { Interactable } from './Interaction.js';
import { MAP } from '../world/MapData.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp } from '../core/Util.js';

const WORK_TIME = 3.6;
const HOLD_TIME = 12.0;

const STATE = { IDLE: 0, WORKING: 1, READY: 2 };

/** Weapon refit station — the Pack-a-Punch equivalent, with three tiers. */
export class UpgradeMachine {
  constructor(world, scene, interaction, power, ctxRef) {
    this.power = power;
    this.ctxRef = ctxRef;
    this.state = STATE.IDLE;
    this.t = 0;
    this.workingWeapon = null;

    const s = MAP.upgradeMachine;
    this.group = makeUpgradeMachine();
    this.group.position.set(s.x, s.y, s.z);
    this.group.rotation.y = s.rot;
    scene.add(this.group);
    const c = this.group.userData.collider;
    world.addObstacle(s.x, s.z, c.w, c.d, s.y, c.h);

    this.display = new THREE.Group();
    this.display.position.set(0, 1.35, 1.5);
    this.group.add(this.display);
    this.displayModel = null;

    const self = this;
    this.interactable = interaction.add(new (class extends Interactable {
      prompt(ctx) {
        if (!self.power.on) return { text: 'REFIT STATION', sub: 'NO POWER', blocked: true };
        if (self.state === STATE.WORKING) return { text: 'REFITTING…', blocked: true };
        if (self.state === STATE.READY) return { text: `COLLECT ${self.workingWeapon.name}` };
        const w = ctx.currentWeapon;
        if (!w) return { text: 'REFIT STATION', blocked: true };
        if (w.upgradeLevel >= w.maxUpgrade) {
          return { text: 'REFIT STATION', sub: 'WEAPON AT MAXIMUM', blocked: true };
        }
        return {
          text: `REFIT ${w.name}`,
          cost: w.nextUpgradeCost,
          sub: `TIER ${w.upgradeLevel + 1} OF ${w.maxUpgrade}`,
        };
      }
      activate(ctx) { return self.activate(ctx); }
    })({ id: 'pap', x: s.x, y: s.y, z: s.z + 1.6, radius: 3.0, priority: 2 }));
  }

  activate(ctx) {
    if (!this.power.on) return false;
    if (this.state === STATE.WORKING) return false;
    if (this.state === STATE.READY) {
      ctx.returnUpgraded(this.workingWeapon);
      this.workingWeapon = null;
      this.state = STATE.IDLE;
      this.display.visible = false;
      return true;
    }
    const w = ctx.currentWeapon;
    if (!w || w.upgradeLevel >= w.maxUpgrade) return false;
    const cost = w.nextUpgradeCost;
    if (!ctx.player.spend(cost)) { ctx.deny(); return false; }
    this.workingWeapon = ctx.takeWeaponForUpgrade();
    this.workingWeapon.upgrade();
    this.state = STATE.WORKING;
    this.t = WORK_TIME;
    Audio.playAt('pap', this.group.position, { volume: 1 });
    ctx.toast('REFIT IN PROGRESS', 'warn');
    return true;
  }

  _setDisplay(def) {
    if (this.displayModel) this.display.remove(this.displayModel);
    this.displayModel = makeWeaponModel(def, 1.2);
    this.display.add(this.displayModel);
    this.display.visible = true;
  }

  update(dt, time) {
    const on = this.power.on;
    for (const g of this.group.userData.glowParts) {
      if (g.material.emissiveIntensity !== undefined) {
        g.material.emissiveIntensity = on ? 1.8 + Math.sin(time * 4) * 0.7 : 0.05;
      }
    }
    this.group.userData.light.intensity = on ? 46 + Math.sin(time * 5) * 14 : 0;
    this.group.userData.ring.rotation.z = time * 1.4;

    if (this.state === STATE.WORKING) {
      this.t -= dt;
      const k = clamp(1 - this.t / WORK_TIME, 0, 1);
      this.group.userData.light.intensity = 70 + Math.sin(time * 30) * 45;
      if (this.t <= 0) {
        this.state = STATE.READY;
        this.t = HOLD_TIME;
        this._setDisplay(this.workingWeapon.def);
        Audio.playAt('boxstop', this.group.position, { volume: 1, rare: true });
        this.ctxRef()?.toast('REFIT COMPLETE', 'pts');
      }
      void k;
    } else if (this.state === STATE.READY) {
      this.t -= dt;
      if (this.displayModel) {
        this.displayModel.rotation.y = time * 1.8;
        this.displayModel.position.y = Math.sin(time * 3) * 0.06;
      }
      if (this.t <= 0) {
        // Nobody collected it — hand it back rather than deleting a weapon.
        this.ctxRef()?.returnUpgraded?.(this.workingWeapon);
        this.workingWeapon = null;
        this.state = STATE.IDLE;
        this.display.visible = false;
      }
    }
  }

  reset() {
    this.state = STATE.IDLE;
    this.workingWeapon = null;
    this.display.visible = false;
  }
}
