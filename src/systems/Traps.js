import * as THREE from 'three';
import { makeTrapPanel, makeTrapEmitter } from '../world/Props.js';
import { Interactable } from './Interaction.js';
import { MAP } from '../world/MapData.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp, rand, pick } from '../core/Util.js';

const ACTIVE_TIME = 22;
const COOLDOWN = 40;

const KINDS = {
  electric: { dps: 300, label: 'ARC PYLONS', color: 0x66d9ff, stun: true },
  fire: { dps: 240, label: 'FLAME VENTS', color: 0xff7b2e, burn: true },
  turret: { dps: 0, label: 'SENTRY GUN', color: 0xffd24a, shots: true },
};

class Trap extends Interactable {
  constructor(def, system) {
    super({
      id: def.id, x: def.panel.x, y: def.y + 1.2, z: def.panel.z, radius: 2.4, priority: 1,
    });
    this.def = def;
    this.kind = KINDS[def.kind];
    this.system = system;
    this.state = 'ready';
    this.t = 0;

    this.panel = makeTrapPanel(def.kind);
    this.panel.position.set(def.panel.x, def.y + 1.35, def.panel.z);
    this.panel.rotation.y = def.panel.rot;

    this.emitter = makeTrapEmitter(def.kind);
    this.emitter.position.set(def.x, def.y, def.z);
    this.emitter.rotation.y = def.rot;

    this.light = new THREE.PointLight(this.kind.color, 0, 20);
    this.light.position.set(def.x, def.y + 2.2, def.z);

    this.shotT = 0;
  }

  get powered() { return this.system.power.on; }

  prompt() {
    if (!this.powered) return { text: this.kind.label, sub: 'NO POWER', blocked: true };
    if (this.state === 'active') return { text: this.kind.label, sub: 'ACTIVE', blocked: true };
    if (this.state === 'cooldown') {
      return { text: this.kind.label, sub: `RECHARGING ${Math.ceil(this.t)}s`, blocked: true };
    }
    return { text: `ARM ${this.kind.label}`, cost: this.def.cost, sub: `${ACTIVE_TIME}s OF CARNAGE` };
  }

  activate(ctx) {
    if (!this.powered || this.state !== 'ready') return false;
    if (!ctx.player.spend(this.def.cost)) { ctx.deny(); return false; }
    this.state = 'active';
    this.t = ACTIVE_TIME;
    Audio.playAt('trap', { x: this.def.x, y: this.def.y + 1, z: this.def.z }, { kind: this.def.kind, volume: 1 });
    ctx.toast(`${this.kind.label} ARMED`, 'warn');
    this.system.world.nav.setDanger(this.def.area, 3);
    return true;
  }

  update(dt, time, ctx) {
    const face = this.panel.userData.face;
    const on = this.powered;

    if (this.state === 'active') {
      this.t -= dt;
      this.light.intensity = 90 + Math.sin(time * 30) * 40;
      face.material.emissiveIntensity = 2.4;
      this._runEffect(dt, time, ctx);
      if (this.t <= 0) {
        this.state = 'cooldown';
        this.t = COOLDOWN;
        this.system.world.nav.setDanger(this.def.area, 0);
      }
    } else {
      this.light.intensity *= Math.exp(-6 * dt);
      face.material.emissiveIntensity = on
        ? (this.state === 'ready' ? 1.4 + Math.sin(time * 2) * 0.4 : 0.35)
        : 0.05;
      if (this.state === 'cooldown') {
        this.t -= dt;
        if (this.t <= 0) this.state = 'ready';
      }
      this._idleVisual(dt, time);
    }
  }

  _idleVisual(dt, time) {
    if (this.def.kind === 'electric') {
      for (const c of this.emitter.userData.coils || []) {
        c.material.emissiveIntensity = this.state === 'active' ? 3 : 0.5;
      }
    } else if (this.def.kind === 'fire') {
      for (const j of this.emitter.userData.jets || []) j.visible = false;
    } else if (this.emitter.userData.head) {
      this.emitter.userData.head.rotation.y = Math.sin(time * 0.4) * 0.7;
    }
  }

  _inArea(z) {
    const a = this.def.area;
    return z.pos.x > a.x0 && z.pos.x < a.x1 && z.pos.z > a.z0 && z.pos.z < a.z1;
  }

  _runEffect(dt, time, ctx) {
    const targets = ctx.zombies.alive.filter((z) => z.active && z.state !== 6 && this._inArea(z));

    if (this.def.kind === 'electric') {
      for (const c of this.emitter.userData.coils || []) c.material.emissiveIntensity = 3.5;
      if (Math.random() < dt * 26) {
        const a = this.emitter.children[1]?.position ?? new THREE.Vector3();
        ctx.fx.arc(
          { x: this.def.x - 2.6, y: this.def.y + 2.9, z: this.def.z },
          { x: this.def.x + 2.6, y: this.def.y + 2.9, z: this.def.z },
          this.kind.color,
        );
        void a;
      }
      for (const z of targets) {
        const res = z.takeDamage(this.kind.dps * dt, 'torso', null, null);
        z.applySlow(0.45, 0.4);
        if (Math.random() < dt * 4) {
          ctx.fx.arc(
            { x: this.def.x, y: this.def.y + 2.6, z: this.def.z },
            { x: z.pos.x, y: z.pos.y + 1.2, z: z.pos.z }, this.kind.color,
          );
        }
        if (res.killed) ctx.onTrapKill(z, this);
      }
    } else if (this.def.kind === 'fire') {
      for (const j of this.emitter.userData.jets || []) {
        j.visible = true;
        j.scale.y = 1 + Math.sin(time * 24 + j.position.x) * 0.35;
      }
      if (Math.random() < dt * 30) {
        ctx.fx.emberBurst({
          x: this.def.x + rand(-3.5, 3.5), y: this.def.y + rand(0.3, 1.6), z: this.def.z + rand(-0.6, 0.6),
        });
      }
      for (const z of targets) {
        const res = z.takeDamage(this.kind.dps * dt, 'torso', null, null);
        z.applyBurn(90, 3);
        if (res.killed) ctx.onTrapKill(z, this);
      }
    } else {
      // Sentry: pick the closest target and put rounds into it.
      const head = this.emitter.userData.head;
      if (targets.length) {
        const t = targets.reduce((a, b) => (
          Math.hypot(a.pos.x - this.def.x, a.pos.z - this.def.z) < Math.hypot(b.pos.x - this.def.x, b.pos.z - this.def.z) ? a : b
        ));
        const ang = Math.atan2(t.pos.x - this.def.x, t.pos.z - this.def.z) - this.def.rot;
        if (head) head.rotation.y = ang;
        this.shotT -= dt;
        if (this.shotT <= 0) {
          this.shotT = 0.14;
          const from = { x: this.def.x, y: this.def.y + 0.85, z: this.def.z };
          const to = { x: t.pos.x, y: t.pos.y + 1.1, z: t.pos.z };
          ctx.fx.tracer(from, to, 0xffd24a);
          Audio.playAt('shoot', from, { weight: 'light', volume: 0.5 });
          const res = t.takeDamage(320, pick(['head', 'torso', 'torso']), { headMult: 2.2, torsoMult: 1 }, ctx.fx);
          if (res.killed) ctx.onTrapKill(t, this);
        }
      } else if (head) {
        head.rotation.y = Math.sin(time * 1.2) * 0.6;
      }
    }
  }
}

export class TrapSystem {
  constructor(world, scene, interaction, power) {
    this.world = world;
    this.power = power;
    this.traps = [];
    for (const def of MAP.traps) {
      const t = new Trap(def, this);
      scene.add(t.panel, t.emitter, t.light);
      interaction.add(t);
      this.traps.push(t);
    }
  }

  update(dt, time, ctx) {
    for (const t of this.traps) t.update(dt, time, ctx);
  }

  reset() {
    for (const t of this.traps) {
      t.state = 'ready';
      t.t = 0;
      this.world.nav.setDanger(t.def.area, 0);
    }
  }
}

export { clamp };
