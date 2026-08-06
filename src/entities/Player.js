import * as THREE from 'three';
import { Settings } from '../core/Settings.js';
import { Input } from '../core/Input.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp, damp, lerp, Emitter } from '../core/Util.js';

const STAND_H = 1.72;
const CROUCH_H = 1.06;
const RADIUS = 0.42;
const GRAVITY = 19.5;

export const MOVE = {
  walk: 4.3,
  sprint: 6.9,
  crouch: 2.3,
  ads: 2.5,
  slideBurst: 9.4,
  slideTime: 0.72,
  jumpVel: 5.6,
  accelGround: 52,
  accelAir: 10,
  frictionGround: 11,
};

/**
 * First person controller: walk, sprint, crouch, slide, jump, mantle, ADS,
 * plus the survival state (health, armour, perks, points, downed/bleed-out).
 */
export class Player extends Emitter {
  constructor(world, camera) {
    super();
    this.world = world;
    this.camera = camera;

    this.pos = new THREE.Vector3(0, 0, 0);
    this.vel = new THREE.Vector3();
    this.yaw = 0;
    this.pitch = 0;
    this.eyeHeight = STAND_H;
    this.targetEyeHeight = STAND_H;

    this.grounded = true;
    this.crouching = false;
    this.sprinting = false;
    this.sliding = false;
    this.slideT = 0;
    this.mantling = null;
    this.wasGrounded = true;

    this.stamina = 1;
    this.staminaDrain = 0.24;
    this.staminaRegen = 0.32;

    this.maxHealth = 100;
    this.health = 100;
    this.armor = 0;
    this.maxArmor = 0;
    this.regenDelay = 4.2;
    this.regenRate = 26;
    this.sinceDamage = 99;

    this.downed = false;
    this.dead = false;
    this.bleedOut = 0;
    this.bleedOutMax = 30;
    this.selfRevives = 0;

    this.points = 500;
    this.perks = new Set();
    this.multiplier = 1;      // double points powerup
    this.instantKill = false;

    this.bob = 0;
    this.bobAmount = 0;
    this.shake = 0;
    this.shakeDecay = 5.5;
    this.recoilKick = new THREE.Vector2();
    this.viewRoll = 0;
    this.stepDist = 0;
    this.lastDamageDir = null;
    this.damageFlashers = [];

    this.stats = {
      kills: 0, headshots: 0, meleeKills: 0, repairs: 0, downs: 0, revives: 0,
      pointsEarned: 0, shotsFired: 0, shotsHit: 0, boxSpins: 0, perksBought: [],
      weaponStats: {},
    };
  }

  spawnAt(spawn) {
    this.pos.set(spawn.x, spawn.y ?? 0, spawn.z);
    this.yaw = spawn.yaw ?? 0;
    this.pitch = 0;
    this.vel.set(0, 0, 0);
  }

  get eyePos() {
    return new THREE.Vector3(this.pos.x, this.pos.y + this.eyeHeight, this.pos.z);
  }

  get forward() {
    return new THREE.Vector3(-Math.sin(this.yaw), 0, -Math.cos(this.yaw));
  }

  get isMoving() {
    return Math.hypot(this.vel.x, this.vel.z) > 0.6;
  }

  get speedScale() {
    let s = 1;
    if (this.perks.has('swift')) s *= 1.13;
    if (this.downed) s = 0.42;
    return s;
  }

  // ================================================================== combat
  takeDamage(amount, fromPos = null) {
    if (this.dead || amount <= 0) return;
    if (this.downed) {
      // Attacks while downed accelerate the bleed-out.
      this.bleedOut = Math.max(0, this.bleedOut - amount * 0.12);
      this.emit('hurt', amount, fromPos);
      return;
    }
    this.sinceDamage = 0;
    if (this.armor > 0) {
      const absorbed = Math.min(this.armor, amount * 0.65);
      this.armor -= absorbed;
      amount -= absorbed;
    }
    this.health -= amount;
    this.shake = Math.min(1.2, this.shake + amount * 0.02);
    if (fromPos) {
      this.lastDamageDir = Math.atan2(fromPos.x - this.pos.x, fromPos.z - this.pos.z);
      this.damageFlashers.push({ angle: this.lastDamageDir, t: 1.1 });
    }
    Audio.play('hurt', { volume: clamp(amount / 30, 0.4, 1) });
    this.emit('hurt', amount, fromPos);
    if (this.health <= 0) this._goDown();
  }

  _goDown() {
    this.health = 0;
    this.downed = true;
    this.stats.downs++;
    this.bleedOutMax = this.perks.has('medic') ? 45 : 30;
    this.bleedOut = this.bleedOutMax;
    this.targetEyeHeight = 0.6;
    this.sprinting = false;
    this.sliding = false;
    this.emit('downed');
  }

  reviveSelf() {
    this.downed = false;
    this.health = this.maxHealth * 0.5;
    this.armor = 0;
    this.sinceDamage = 0;
    this.stats.revives++;
    this.emit('revived');
  }

  die() {
    this.dead = true;
    this.downed = false;
    this.emit('died');
  }

  heal(amount) {
    this.health = clamp(this.health + amount, 0, this.maxHealth);
  }

  addArmor(amount) {
    this.maxArmor = Math.max(this.maxArmor, 100);
    this.armor = clamp(this.armor + amount, 0, this.maxArmor);
  }

  // ================================================================== economy
  addPoints(n, reason = '') {
    if (n <= 0) return 0;
    const total = Math.round(n * this.multiplier);
    this.points += total;
    this.stats.pointsEarned += total;
    this.emit('points', total, reason);
    return total;
  }

  spend(n) {
    if (this.points < n) return false;
    this.points -= n;
    this.emit('spend', n);
    return true;
  }

  givePerk(id, def) {
    if (this.perks.has(id)) return false;
    this.perks.add(id);
    this.stats.perksBought.push(id);
    if (id === 'vital') {
      this.maxHealth = 175;
      this.health = this.maxHealth;
    }
    if (id === 'medic') this.selfRevives = 1;
    if (id === 'endure') { this.staminaDrain = 0.13; this.staminaRegen = 0.5; }
    this.emit('perk', id, def);
    return true;
  }

  clearPerks() {
    this.perks.clear();
    this.maxHealth = 100;
    this.health = Math.min(this.health, this.maxHealth);
    this.selfRevives = 0;
    this.staminaDrain = 0.24;
    this.staminaRegen = 0.32;
    this.emit('perksCleared');
  }

  addShake(amount) {
    if (!Settings.get('screenShake')) return;
    this.shake = Math.min(1.6, this.shake + amount);
  }

  addRecoil(x, y) {
    this.recoilKick.x += x;
    this.recoilKick.y += y;
  }

  // ================================================================== update
  update(dt, opts = {}) {
    const { canMove = true, adsing = false, adsZoom = 1.39 } = opts;
    this.adsZoom = adsZoom;

    this._look(dt, canMove);
    if (this.dead) return;

    if (this.mantling) this._updateMantle(dt);
    else this._move(dt, canMove, adsing);

    this._vitals(dt);
    this._camera(dt, adsing);
  }

  _look(dt, canMove) {
    if (!canMove) { this.lastLook = { yaw: 0, pitch: 0 }; return; }
    const { yaw, pitch } = Input.lookDelta(dt);
    this.lastLook = { yaw, pitch };
    this.yaw += yaw;
    this.pitch = clamp(this.pitch + pitch, -Math.PI / 2 + 0.03, Math.PI / 2 - 0.03);
  }

  _move(dt, canMove, adsing) {
    const world = this.world;
    const axis = canMove ? Input.moveAxis() : { x: 0, y: 0 };
    const wantSprint = canMove && Input.isDown('sprint') && axis.y > 0.3 && !adsing && !this.downed;
    const wantCrouch = canMove && Input.isDown('crouch');

    // ---- slide
    if (this.sliding) {
      this.slideT -= dt;
      if (this.slideT <= 0 || !this.grounded) {
        this.sliding = false;
        this.crouching = wantCrouch;
      }
    } else if (wantCrouch && this.sprinting && this.grounded && !this.downed && this.stamina > 0.12) {
      this.sliding = true;
      this.slideT = MOVE.slideTime;
      this.stamina = Math.max(0, this.stamina - 0.16);
      const f = this.forward;
      this.vel.x = f.x * MOVE.slideBurst * this.speedScale;
      this.vel.z = f.z * MOVE.slideBurst * this.speedScale;
      Audio.play('step', { volume: 0.9, surface: 'concrete' });
    }
    this.crouching = this.sliding || (wantCrouch && this.grounded);

    // ---- stamina & sprint
    // Hysteresis: sprinting cuts out at empty but will not resume until a
    // little stamina is back. A single threshold makes the state chatter every
    // frame at the boundary, which strobes the sprint pose on the view model.
    const floor = this.sprinting ? 0.0 : 0.12;
    this.sprinting = wantSprint && this.stamina > floor && !this.crouching;
    if (this.sprinting) this.stamina = clamp(this.stamina - this.staminaDrain * dt, 0, 1);
    else this.stamina = clamp(this.stamina + this.staminaRegen * dt * (this.isMoving ? 0.6 : 1), 0, 1);

    // ---- desired velocity
    let speed = MOVE.walk;
    if (this.downed) speed = MOVE.crouch * 0.72;
    else if (this.sliding) speed = MOVE.slideBurst;
    else if (this.crouching) speed = MOVE.crouch;
    else if (this.sprinting) speed = MOVE.sprint;
    else if (adsing) speed = MOVE.ads;
    speed *= this.speedScale;

    // forward = (-sin, -cos), right = (cos, -sin)
    const sin = Math.sin(this.yaw), cos = Math.cos(this.yaw);
    let wx = -sin * axis.y + cos * axis.x;
    let wz = -cos * axis.y - sin * axis.x;
    const wl = Math.hypot(wx, wz);
    if (wl > 1) { wx /= wl; wz /= wl; }

    const accel = this.grounded ? MOVE.accelGround : MOVE.accelAir;
    if (!this.sliding) {
      const targetX = wx * speed, targetZ = wz * speed;
      this.vel.x = damp(this.vel.x, targetX, accel / 6, dt);
      this.vel.z = damp(this.vel.z, targetZ, accel / 6, dt);
      if (this.grounded && wl < 0.05) {
        const f = Math.exp(-MOVE.frictionGround * dt);
        this.vel.x *= f;
        this.vel.z *= f;
      }
    } else {
      const f = Math.exp(-3.4 * dt);
      this.vel.x *= f;
      this.vel.z *= f;
    }

    // ---- jump / mantle
    if (canMove && !this.downed && Input.wasPressed('jump')) {
      const ledge = world.ledgeAt(
        this.pos.x + this.forward.x * 0.7, this.pos.z + this.forward.z * 0.7, this.pos.y, 1.75,
      );
      if (ledge !== null && ledge > this.pos.y + 0.3) {
        this._startMantle(ledge);
      } else if (this.grounded) {
        this.vel.y = MOVE.jumpVel;
        this.grounded = false;
        this.sliding = false;
      }
    }

    // ---- integrate
    this.vel.y -= GRAVITY * dt;
    const next = { x: this.pos.x + this.vel.x * dt, z: this.pos.z + this.vel.z * dt };
    const height = this.crouching || this.downed ? CROUCH_H : STAND_H;
    const feet = this.pos.y;
    world.collide(next, RADIUS, feet, feet + height);
    this.pos.x = next.x;
    this.pos.z = next.z;

    this.pos.y += this.vel.y * dt;
    const ground = world.groundHeightAt(this.pos.x, this.pos.z, feet, this.grounded ? 0.66 : 0.12);
    if (this.pos.y <= ground + 0.001) {
      if (!this.wasGrounded && this.vel.y < -6) {
        this.addShake(clamp(-this.vel.y * 0.02, 0, 0.35));
        Audio.play('step', { volume: 0.8 });
      }
      this.pos.y = ground;
      this.vel.y = 0;
      this.grounded = true;
    } else {
      this.grounded = false;
    }
    this.wasGrounded = this.grounded;

    // Fell out of the world (should not happen, but never lose the player).
    if (this.pos.y < -40) {
      this.pos.y = 0;
      this.vel.set(0, 0, 0);
    }

    // ---- footsteps
    if (this.grounded) {
      const dist = Math.hypot(this.vel.x, this.vel.z) * dt;
      this.stepDist += dist;
      const stride = this.sprinting ? 2.1 : this.crouching ? 2.6 : 1.7;
      if (this.stepDist > stride) {
        this.stepDist = 0;
        Audio.play('step', { volume: this.sprinting ? 0.5 : 0.32 });
      }
    }

    this.targetEyeHeight = this.downed ? 0.55 : this.sliding ? 0.72 : this.crouching ? CROUCH_H : STAND_H;
  }

  _startMantle(targetY) {
    const f = this.forward;
    this.mantling = {
      t: 0,
      dur: 0.42,
      fromY: this.pos.y,
      toY: targetY + 0.02,
      fromX: this.pos.x, fromZ: this.pos.z,
      toX: this.pos.x + f.x * 1.05,
      toZ: this.pos.z + f.z * 1.05,
    };
    this.vel.set(0, 0, 0);
    Audio.play('step', { volume: 0.6 });
  }

  _updateMantle(dt) {
    const m = this.mantling;
    m.t += dt;
    const t = clamp(m.t / m.dur, 0, 1);
    // Up first, then forward — reads like a real pull-up.
    const up = clamp(t * 1.55, 0, 1);
    const fwd = clamp((t - 0.42) / 0.58, 0, 1);
    this.pos.y = lerp(m.fromY, m.toY, up * up * (3 - 2 * up));
    this.pos.x = lerp(m.fromX, m.toX, fwd);
    this.pos.z = lerp(m.fromZ, m.toZ, fwd);
    this.viewRoll = Math.sin(t * Math.PI) * 0.06;
    if (t >= 1) {
      this.mantling = null;
      this.grounded = true;
    }
  }

  _vitals(dt) {
    if (this.downed) {
      this.bleedOut -= dt;
      if (this.bleedOut <= 0) {
        if (this.selfRevives > 0) {
          this.selfRevives--;
          this.reviveSelf();
          Audio.play('perk', { volume: 0.8 });
        } else {
          this.die();
        }
      }
      return;
    }
    this.sinceDamage += dt;
    if (this.sinceDamage > this.regenDelay && this.health < this.maxHealth) {
      this.health = clamp(this.health + this.regenRate * dt, 0, this.maxHealth);
    }
    for (let i = this.damageFlashers.length - 1; i >= 0; i--) {
      this.damageFlashers[i].t -= dt;
      if (this.damageFlashers[i].t <= 0) this.damageFlashers.splice(i, 1);
    }
  }

  _camera(dt, adsing) {
    this.eyeHeight = damp(this.eyeHeight, this.targetEyeHeight, 12, dt);

    // Recoil recovers toward zero.
    this.recoilKick.x = damp(this.recoilKick.x, 0, 9, dt);
    this.recoilKick.y = damp(this.recoilKick.y, 0, 9, dt);

    // Head bob.
    const speed = Math.hypot(this.vel.x, this.vel.z);
    const target = this.grounded ? clamp(speed / MOVE.sprint, 0, 1) * (adsing ? 0.35 : 1) : 0;
    this.bobAmount = damp(this.bobAmount, target, 8, dt);
    this.bob += dt * (this.sprinting ? 13.5 : 9.5) * clamp(speed / 3, 0, 1.5);

    const bobY = Math.sin(this.bob * 2) * 0.035 * this.bobAmount;
    const bobX = Math.cos(this.bob) * 0.05 * this.bobAmount;
    const roll = Math.cos(this.bob) * 0.012 * this.bobAmount;

    this.shake = Math.max(0, this.shake - this.shakeDecay * dt * (0.5 + this.shake));
    const sh = this.shake * this.shake;
    const shx = (Math.random() - 0.5) * sh * 0.12;
    const shy = (Math.random() - 0.5) * sh * 0.12;
    const shr = (Math.random() - 0.5) * sh * 0.06;

    const slideRoll = this.sliding ? -0.09 : 0;
    this.viewRoll = damp(this.viewRoll, slideRoll, 8, dt);

    this.camera.position.set(
      this.pos.x + bobX * 0.4 + shx,
      this.pos.y + this.eyeHeight + bobY + shy,
      this.pos.z + shx * 0.4,
    );
    this.camera.rotation.set(0, 0, 0);
    this.camera.rotation.order = 'YXZ';
    this.camera.rotation.y = this.yaw + this.recoilKick.x;
    this.camera.rotation.x = clamp(this.pitch + this.recoilKick.y, -1.55, 1.55);
    this.camera.rotation.z = roll + this.viewRoll + shr + (this.downed ? 0.22 : 0);

    const fovBase = Settings.get('fov');
    // Magnified optics narrow the view by their true magnification, which is
    // what makes a 6x scope feel like a scope and not a slightly tighter hip.
    const zoom = Math.max(1.05, this.adsZoom ?? 1.39);
    const targetFov = adsing ? fovBase / zoom : this.sprinting ? fovBase * 1.06 : fovBase;
    if (Math.abs(this.camera.fov - targetFov) > 0.05) {
      this.camera.fov = damp(this.camera.fov, targetFov, zoom > 3 ? 15 : 11, dt);
      this.camera.updateProjectionMatrix();
    }
  }

  /** Snapshot used by the HUD. */
  hudState() {
    return {
      health: this.health,
      maxHealth: this.maxHealth,
      armor: this.armor,
      maxArmor: this.maxArmor,
      points: this.points,
      perks: Array.from(this.perks),
      downed: this.downed,
      bleed: this.bleedOut / this.bleedOutMax,
      stamina: this.stamina,
    };
  }
}

export { RADIUS as PLAYER_RADIUS, STAND_H, CROUCH_H };
