import * as THREE from 'three';
import { Tex, mat } from '../world/Textures.js';
import { Assets } from '../world/AssetLibrary.js';
import { box } from '../world/Props.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp, rand, randInt, wrapAngle, lerp } from '../core/Util.js';

export const ZTYPE = {
  normal: {
    id: 'normal', label: 'SHAMBLER', healthMult: 1.0, speed: [1.35, 3.5], damage: 26,
    scale: 1.0, points: 1.0, color: null, staggerResist: 1.0,
  },
  runner: {
    id: 'runner', label: 'SPRINTER', healthMult: 0.72, speed: [4.4, 5.9], damage: 22,
    scale: 0.94, points: 1.15, color: 0x7f5a3c, staggerResist: 0.7,
  },
  heavy: {
    id: 'heavy', label: 'BRUTE', healthMult: 3.4, speed: [1.1, 2.1], damage: 46,
    scale: 1.28, points: 1.6, color: 0x4a5a4a, staggerResist: 2.6,
  },
  boss: {
    id: 'boss', label: 'ABOMINATION', healthMult: 26, speed: [2.0, 3.0], damage: 62,
    scale: 1.85, points: 6, color: 0x6a2a2a, staggerResist: 12,
  },
};

const PLAYER_CLEARANCE = 0.78;

const STATE = {
  IDLE: 0, BREACH: 1, VAULT: 2, CHASE: 3, ATTACK: 4, STAGGER: 5, DYING: 6, DEAD: 7,
};

/**
 * Hitboxes sit inside an external zombie mesh and must still be hit by
 * raycasts, so they are drawn — as nothing. `colorWrite: false` keeps them out
 * of the frame while leaving them fully present to the raycaster.
 */
const INVISIBLE_HITBOX = new THREE.MeshBasicMaterial({
  colorWrite: false, depthWrite: false, depthTest: false,
});

let UID = 0;

export class Zombie {
  constructor(scene) {
    this.scene = scene;
    this.id = ++UID;
    this.group = new THREE.Group();
    this.group.visible = false;
    scene.add(this.group);

    this.pos = new THREE.Vector3();
    this.vel = new THREE.Vector3();
    this.yaw = 0;
    this.state = STATE.DEAD;
    this.active = false;
    this.hitboxes = [];
    this.limbHealth = { leftLeg: 1, rightLeg: 1, leftArm: 1, rightArm: 1 };
    this.crawler = false;

    this._build();
  }

  /**
   * Swaps in a supplied zombie model, if one was provided.
   *
   * The procedural rig stays — invisible — because it is what carries the
   * hitboxes and the animation. The external model rides on the body node, so
   * it inherits position, facing and the death topple without needing to
   * expose a skeleton the game understands. Limb hitboxes therefore stay
   * approximate for external models, which is the accepted trade for letting
   * any humanoid mesh drop straight in.
   */
  _attachExternalModel() {
    // Zombies are pooled and reused across types, so the model is chosen once
    // here from the generic key rather than per spawn.
    const ext = Assets.get('zombie');
    if (!ext) return;
    this.external = ext;
    ext.traverse((o) => { if (o.isMesh) o.castShadow = true; });
    this.body.add(ext);
    // Hide the block figure but keep it in the scene graph for raycasts.
    for (const m of this.hitboxes) m.visible = false;
    for (const extra of [this.neck, this.head, this.legs.left.foot, this.legs.right.foot,
      this.arms.left.hand, this.arms.right.hand]) {
      if (extra) extra.visible = false;
    }
    this.head.traverse?.((o) => { if (o.isMesh) o.visible = false; });
    // Raycasts need to hit something, and an invisible mesh is skipped by the
    // hitbox gather — so mark these as raycast-only rather than hidden.
    for (const m of this.hitboxes) {
      m.visible = true;
      m.material = INVISIBLE_HITBOX;
    }
    if (ext.userData.animations?.length) {
      this.mixer = new THREE.AnimationMixer(ext);
      this.clips = ext.userData.animations;
    }
  }

  // ================================================================= model
  _build() {
    const skin = mat(`zskin${this.id % 5}`, { map: Tex.zombieSkin(this.id % 5), roughness: 0.92 });
    const cloth = mat(`zcloth${this.id % 4}`, { map: Tex.cloth(this.id % 4), roughness: 0.96 });

    this.body = new THREE.Group();
    this.group.add(this.body);

    // Torso + head
    this.torso = new THREE.Group();
    this.torso.position.y = 1.02;
    this.body.add(this.torso);
    this.torsoMesh = box(0.52, 0.66, 0.3, cloth, 0, 0, 0);
    this.torso.add(this.torsoMesh);
    this.pelvis = box(0.44, 0.28, 0.28, cloth, 0, -0.46, 0);
    this.body.add(this.pelvis);
    this.pelvis.position.y = 0.72;

    this.neck = box(0.14, 0.12, 0.14, skin, 0, 0.4, 0);
    this.torso.add(this.neck);
    this.head = new THREE.Group();
    this.head.position.set(0, 0.53, 0);
    this.torso.add(this.head);
    this.headMesh = box(0.26, 0.3, 0.26, skin, 0, 0, 0);
    this.head.add(this.headMesh);
    // Jaw + sunken eyes give the silhouette some read at distance.
    this.jaw = box(0.2, 0.09, 0.2, skin, 0, -0.16, 0.02);
    this.head.add(this.jaw);
    const eyeMat = mat('zeye', { color: 0x0a0a08, emissive: 0xb8d84a, emissiveIntensity: 0.5, roughness: 0.5 });
    this.head.add(box(0.05, 0.04, 0.03, eyeMat, -0.07, 0.03, 0.13));
    this.head.add(box(0.05, 0.04, 0.03, eyeMat, 0.07, 0.03, 0.13));

    // Arms
    this.arms = {};
    for (const [side, sx] of [['left', -1], ['right', 1]]) {
      const shoulder = new THREE.Group();
      shoulder.position.set(sx * 0.33, 0.24, 0);
      this.torso.add(shoulder);
      const upper = box(0.14, 0.36, 0.14, cloth, 0, -0.18, 0);
      shoulder.add(upper);
      const elbow = new THREE.Group();
      elbow.position.y = -0.36;
      shoulder.add(elbow);
      const fore = box(0.12, 0.34, 0.12, skin, 0, -0.17, 0);
      elbow.add(fore);
      const hand = box(0.13, 0.13, 0.15, skin, 0, -0.36, 0.02);
      elbow.add(hand);
      this.arms[side] = { shoulder, elbow, upper, fore, hand };
    }

    // Legs
    this.legs = {};
    for (const [side, sx] of [['left', -1], ['right', 1]]) {
      const hip = new THREE.Group();
      hip.position.set(sx * 0.14, 0.72, 0);
      this.body.add(hip);
      const thigh = box(0.18, 0.4, 0.18, cloth, 0, -0.2, 0);
      hip.add(thigh);
      const knee = new THREE.Group();
      knee.position.y = -0.4;
      hip.add(knee);
      const shin = box(0.16, 0.36, 0.16, cloth, 0, -0.18, 0);
      knee.add(shin);
      const foot = box(0.17, 0.1, 0.26, mat('zboot', { color: 0x22201c, roughness: 1 }), 0, -0.38, 0.04);
      knee.add(foot);
      this.legs[side] = { hip, knee, thigh, shin, foot };
    }

    // Raycast targets. userData.part drives the damage multiplier.
    const reg = (mesh, part) => {
      mesh.userData.part = part;
      mesh.userData.zombie = this;
      this.hitboxes.push(mesh);
    };
    reg(this.headMesh, 'head');
    reg(this.jaw, 'head');
    reg(this.torsoMesh, 'torso');
    reg(this.pelvis, 'torso');
    reg(this.arms.left.upper, 'leftArm');
    reg(this.arms.left.fore, 'leftArm');
    reg(this.arms.right.upper, 'rightArm');
    reg(this.arms.right.fore, 'rightArm');
    reg(this.legs.left.thigh, 'leftLeg');
    reg(this.legs.left.shin, 'leftLeg');
    reg(this.legs.right.thigh, 'rightLeg');
    reg(this.legs.right.shin, 'rightLeg');

    this._attachExternalModel();

    this.baseCloth = cloth;
    this.clothMeshes = [
      this.torsoMesh, this.pelvis,
      this.arms.left.upper, this.arms.right.upper,
      this.legs.left.thigh, this.legs.right.thigh,
      this.legs.left.shin, this.legs.right.shin,
    ];
    this.materials = { skin, cloth };
  }

  // ================================================================= spawn
  spawn(opts) {
    const t = ZTYPE[opts.type] || ZTYPE.normal;
    this.type = t;
    this.typeId = t.id;
    this.maxHealth = Math.round(opts.health * t.healthMult);
    this.health = this.maxHealth;
    this.speed = lerp(t.speed[0], t.speed[1], clamp(opts.aggression, 0, 1)) * rand(0.9, 1.1);
    this.baseSpeed = this.speed;
    this.damage = t.damage * (opts.damageScale ?? 1);
    this.pointsMult = t.points;
    this.barricade = opts.barricade || null;
    this.state = this.barricade ? STATE.BREACH : STATE.CHASE;
    this.attackCd = rand(0, 0.5);
    this.staggerT = 0;
    this.deathT = 0;
    this.crawler = false;
    this.limbHealth = { leftLeg: 1, rightLeg: 1, leftArm: 1, rightArm: 1 };
    this.breachT = 0;
    this.vault = null;
    this.groanT = rand(1, 6);
    this.pathT = rand(0, 0.3);
    this.wanderAngle = rand(0, Math.PI * 2);
    this.lastFlow = new THREE.Vector3();
    this.active = true;
    this.burnT = 0;
    this.slowT = 0;

    this.pos.set(opts.x, opts.y, opts.z);
    this.vel.set(0, 0, 0);
    this.yaw = opts.yaw ?? rand(0, Math.PI * 2);

    const s = t.scale * rand(0.94, 1.06);
    this.scale = s;
    this.group.scale.setScalar(s);
    this.radius = 0.42 * s;
    this.height = 1.85 * s;
    this.group.visible = true;
    this.group.position.copy(this.pos);

    // Restore any parts blown off in a previous life.
    for (const side of ['left', 'right']) {
      this.arms[side].shoulder.visible = true;
      this.legs[side].hip.visible = true;
      this.arms[side].elbow.visible = true;
    }
    this.body.rotation.set(0, 0, 0);
    this.body.position.set(0, 0, 0);
    this.torso.rotation.set(0, 0, 0);
    this.head.rotation.set(0, 0, 0);

    // Typed zombies get their own tinted cloth material so nothing is shared.
    const clothMat = t.color
      ? mat(`zclothT${t.id}_${this.id % 4}`, { map: Tex.cloth(this.id % 4), color: t.color, roughness: 0.96 })
      : this.baseCloth;
    for (const m of this.clothMeshes) m.material = clothMat;
    this.phase = rand(0, Math.PI * 2);
  }

  despawn() {
    this.active = false;
    this.state = STATE.DEAD;
    this.group.visible = false;
  }

  // ================================================================= damage
  /**
   * @returns {{killed:boolean, headshot:boolean, damage:number, crit:boolean}}
   */
  takeDamage(amount, part = 'torso', def = null, fx = null, dir = null) {
    if (!this.active || this.state === STATE.DYING) {
      return { killed: false, headshot: false, damage: 0, crit: false };
    }
    let mult = 1;
    let headshot = false;
    if (part === 'head') { mult = def?.headMult ?? 2.6; headshot = true; }
    else if (part.endsWith('Arm') || part.endsWith('Leg')) mult = def?.limbMult ?? 0.72;
    else mult = def?.torsoMult ?? 1;

    const dmg = amount * mult;
    this.health -= dmg;

    if (this.limbHealth[part] !== undefined) {
      this.limbHealth[part] -= dmg / Math.max(60, this.maxHealth * 0.45);
      if (this.limbHealth[part] <= 0) this._severLimb(part, fx);
    }

    // Stagger: big relative hits interrupt the walk cycle.
    const staggerPower = dmg / (this.maxHealth * 0.22 * this.type.staggerResist);
    if (staggerPower > 1 && this.state !== STATE.VAULT) {
      this.staggerT = Math.min(0.45, 0.12 + staggerPower * 0.06);
      if (this.state === STATE.CHASE || this.state === STATE.ATTACK) this.state = STATE.STAGGER;
    }

    if (fx) {
      const p = this.hitPoint(part);
      fx.bloodBurst(p, dir, headshot ? 1.7 : 1);
      if (headshot) fx.boneBurst(p);
    }

    if (this.health <= 0) {
      this._die(headshot, fx, dir);
      return { killed: true, headshot, damage: dmg, crit: headshot };
    }
    return { killed: false, headshot, damage: dmg, crit: headshot };
  }

  _severLimb(part, fx) {
    const map = {
      leftArm: this.arms.left.elbow, rightArm: this.arms.right.elbow,
      leftLeg: this.legs.left.hip, rightLeg: this.legs.right.hip,
    };
    const node = map[part];
    if (!node || !node.visible) return;
    node.visible = false;
    const wp = new THREE.Vector3();
    node.getWorldPosition(wp);
    fx?.bloodBurst(wp, null, 2.2);
    if (part.endsWith('Leg')) {
      const legsGone = !this.legs.left.hip.visible || !this.legs.right.hip.visible;
      if (legsGone && !this.crawler) {
        this.crawler = true;
        this.speed = this.baseSpeed * 0.42;
        this.height *= 0.45;
      }
    }
  }

  _die(headshot, fx, dir) {
    this.state = STATE.DYING;
    this.deathT = 0;
    this.deathHeadshot = headshot;
    const p = this.pos.clone();
    p.y += 1.0;
    fx?.bloodBurst(p, dir, headshot ? 2.6 : 1.8);
    Audio.playAt('zdeath', { x: this.pos.x, y: this.pos.y + 1, z: this.pos.z }, { volume: 0.9 });
    if (this.barricade) this.barricade.attackers.delete(this);
  }

  hitPoint(part) {
    const nodeMap = {
      head: this.headMesh, torso: this.torsoMesh,
      leftArm: this.arms.left.fore, rightArm: this.arms.right.fore,
      leftLeg: this.legs.left.shin, rightLeg: this.legs.right.shin,
    };
    const n = nodeMap[part] || this.torsoMesh;
    const v = new THREE.Vector3();
    n.getWorldPosition(v);
    return v;
  }

  applyBurn(dps, seconds) {
    this.burnT = Math.max(this.burnT, seconds);
    this.burnDps = Math.max(this.burnDps || 0, dps);
  }

  applySlow(factor, seconds) {
    this.slowT = Math.max(this.slowT, seconds);
    this.slowFactor = Math.min(this.slowFactor ?? 1, factor);
  }

  // ================================================================= update
  update(dt, ctx) {
    if (!this.active) return;
    const { player, world, nav, time, fx } = ctx;

    if (this.burnT > 0) {
      this.burnT -= dt;
      this.health -= (this.burnDps || 0) * dt;
      if (Math.random() < dt * 6) fx?.emberBurst(this.pos.clone().setY(this.pos.y + 1));
      if (this.health <= 0 && this.state !== STATE.DYING) {
        this._die(false, fx, null);
        ctx.onBurnKill?.(this);
      }
    }
    if (this.slowT > 0) this.slowT -= dt;

    switch (this.state) {
      case STATE.BREACH: this._updateBreach(dt, ctx); break;
      case STATE.VAULT: this._updateVault(dt); break;
      case STATE.STAGGER:
        this.staggerT -= dt;
        if (this.staggerT <= 0) this.state = STATE.CHASE;
        this._face(player, dt);
        break;
      case STATE.ATTACK: this._updateAttack(dt, ctx); break;
      case STATE.CHASE: this._updateChase(dt, ctx); break;
      case STATE.DYING: this._updateDying(dt, ctx); return;
      default: break;
    }

    // Ambient groans.
    this.groanT -= dt;
    if (this.groanT <= 0) {
      this.groanT = rand(3.5, 11);
      Audio.maybeGroan(this.pos, time, this.type.id === 'heavy' ? 0.7 : this.type.id === 'boss' ? 0.5 : 1);
    }

    this.group.position.copy(this.pos);
    this.group.rotation.y = this.yaw;
    this._animate(dt, time);
    void world; void nav;
  }

  _speedNow() {
    let s = this.speed;
    if (this.slowT > 0) s *= this.slowFactor ?? 1;
    if (this.crawler) s *= 0.8;
    return s;
  }

  _updateBreach(dt, ctx) {
    const b = this.barricade;
    if (!b) { this.state = STATE.CHASE; return; }
    const dx = b.outside.x - this.pos.x, dz = b.outside.z - this.pos.z;
    const d = Math.hypot(dx, dz);
    if (d > 0.55) {
      // Walk to the outside of the window.
      const nx = dx / d, nz = dz / d;
      this._step(nx, nz, dt, this._speedNow() * 0.9, ctx);
      this._faceDir(nx, nz, dt);
      return;
    }
    b.attackers.add(this);
    this._faceDir(-b.nx, -b.nz, dt);
    if (b.isOpen) {
      this._startVault(b);
      return;
    }
    this.breachT -= dt;
    if (this.breachT <= 0) {
      this.breachT = 0.85;
      const broke = b.damage(0.55, ctx.fx);
      this.attackAnim = 0.4;
      if (broke) ctx.onBoardBroken?.(b, this);
    }
  }

  _startVault(b) {
    this.state = STATE.VAULT;
    b.attackers.delete(this);
    this.vault = {
      t: 0,
      dur: this.type.id === 'runner' ? 0.55 : 0.85,
      from: this.pos.clone(),
      to: new THREE.Vector3(b.inside.x, b.y, b.inside.z),
      peak: b.y + 1.35,
    };
    this.barricade = null;
    this.yaw = Math.atan2(-b.nx, -b.nz);
  }

  _updateVault(dt) {
    const v = this.vault;
    v.t += dt;
    const t = clamp(v.t / v.dur, 0, 1);
    this.pos.lerpVectors(v.from, v.to, t);
    this.pos.y = lerp(v.from.y, v.to.y, t) + Math.sin(t * Math.PI) * (v.peak - v.from.y) * 0.8;
    // Hands forward, body pitched over the sill.
    this.body.rotation.x = -Math.sin(t * Math.PI) * 0.7;
    if (t >= 1) {
      this.vault = null;
      this.body.rotation.x = 0;
      this.state = STATE.CHASE;
    }
  }

  _updateChase(dt, ctx) {
    const { player, nav } = ctx;
    const dx = player.pos.x - this.pos.x;
    const dz = player.pos.z - this.pos.z;
    const distSq = dx * dx + dz * dz;
    const reach = this.radius + 0.95 + (this.crawler ? -0.1 : 0);

    if (distSq < reach * reach * 2.4 && Math.abs(player.pos.y - this.pos.y) < 2.2) {
      this.state = STATE.ATTACK;
      return;
    }

    // Follow the flow field, falling back to a straight line when off-grid.
    this.pathT -= dt;
    if (this.pathT <= 0) {
      this.pathT = 0.12 + Math.random() * 0.1;
      const flow = nav.flowAt(this.pos.x, this.pos.z);
      if (flow && !flow.arrived) {
        this.lastFlow.set(flow.x, 0, flow.z);
      } else {
        const d = Math.sqrt(distSq) || 1;
        this.lastFlow.set(dx / d, 0, dz / d);
      }
    }

    let mx = this.lastFlow.x, mz = this.lastFlow.z;
    // Separation so the horde spreads across a doorway instead of stacking.
    const sep = ctx.separation(this);
    mx += sep.x; mz += sep.z;
    const len = Math.hypot(mx, mz) || 1;
    mx /= len; mz /= len;

    this._step(mx, mz, dt, this._speedNow(), ctx);
    this._faceDir(mx, mz, dt);
  }

  _updateAttack(dt, ctx) {
    const { player } = ctx;
    const dx = player.pos.x - this.pos.x;
    const dz = player.pos.z - this.pos.z;
    const d = Math.hypot(dx, dz);
    const reach = this.radius + PLAYER_CLEARANCE + 0.75;
    if (d > reach + 0.6 || player.dead) {
      this.state = STATE.CHASE;
      return;
    }
    this._faceDir(dx / (d || 1), dz / (d || 1), dt, 10);
    // Shuffle in to keep contact.
    if (d > reach * 0.75) this._step(dx / (d || 1), dz / (d || 1), dt, this._speedNow() * 0.5, ctx);

    this.attackCd -= dt;
    if (this.attackCd <= 0) {
      this.attackCd = this.type.id === 'runner' ? 0.85 : 1.15;
      this.attackAnim = 0.45;
      Audio.playAt('zattack', this.pos, { volume: 0.8 });
      ctx.onAttack?.(this, this.damage);
    }
  }

  _updateDying(dt, ctx) {
    this.deathT += dt;
    const t = clamp(this.deathT / 0.9, 0, 1);
    this.body.rotation.x = -t * 1.5;
    this.body.position.y = -t * 0.35;
    this.group.position.copy(this.pos);
    this.group.scale.setScalar(this.scale * (1 - t * 0.08));
    if (this.deathT > 1.5) {
      ctx.onRemoved?.(this);
      this.despawn();
    }
  }

  _step(dx, dz, dt, speed, ctx) {
    const nx = this.pos.x + dx * speed * dt;
    const nz = this.pos.z + dz * speed * dt;
    const next = { x: nx, z: nz };
    ctx.world.collide(next, this.radius, this.pos.y, this.pos.y + this.height);
    // Never walk inside the player — their arms reach, their heads do not.
    const p = ctx.player.pos;
    const minD = this.radius + PLAYER_CLEARANCE;
    const px = next.x - p.x, pz = next.z - p.z;
    const pd2 = px * px + pz * pz;
    if (pd2 < minD * minD && Math.abs(p.y - this.pos.y) < 2.0) {
      const pd = Math.sqrt(pd2) || 1e-4;
      next.x = p.x + (px / pd) * minD;
      next.z = p.z + (pz / pd) * minD;
    }
    this.pos.x = next.x;
    this.pos.z = next.z;
    const ground = ctx.nav.heightAt(this.pos.x, this.pos.z);
    this.pos.y = lerp(this.pos.y, ground, clamp(dt * 9, 0, 1));
    this.vel.set(dx * speed, 0, dz * speed);
  }

  _face(player, dt) {
    const dx = player.pos.x - this.pos.x, dz = player.pos.z - this.pos.z;
    const d = Math.hypot(dx, dz) || 1;
    this._faceDir(dx / d, dz / d, dt);
  }

  _faceDir(dx, dz, dt, rate = 6) {
    const target = Math.atan2(dx, dz);
    this.yaw += wrapAngle(target - this.yaw) * clamp(dt * rate, 0, 1);
  }

  // ================================================================= anim
  _animate(dt, time) {
    const moving = Math.hypot(this.vel.x, this.vel.z);
    const cadence = this.crawler ? 6.5 : 2.2 + moving * 1.5;
    const t = time * cadence + this.phase;
    const amp = clamp(moving / 3, 0.2, 1.2);

    if (this.crawler) {
      this.body.rotation.x = -1.25;
      this.body.position.y = -0.62 * this.scale;
      this.arms.left.shoulder.rotation.x = Math.sin(t) * 1.1 - 0.6;
      this.arms.right.shoulder.rotation.x = -Math.sin(t) * 1.1 - 0.6;
    } else {
      // Classic lurch: heavy forward lean with an uneven gait.
      this.body.rotation.x = -0.12 - Math.sin(t * 2) * 0.03 * amp;
      this.body.rotation.z = Math.sin(t) * 0.07 * amp;
      this.body.position.y = Math.abs(Math.sin(t)) * 0.045 * amp;

      const swing = Math.sin(t) * 0.75 * amp;
      this.legs.left.hip.rotation.x = swing;
      this.legs.right.hip.rotation.x = -swing;
      this.legs.left.knee.rotation.x = Math.max(0, -swing) * 0.9;
      this.legs.right.knee.rotation.x = Math.max(0, swing) * 0.9;

      const reach = this.state === STATE.CHASE || this.state === STATE.ATTACK ? -1.25 : -0.5;
      this.arms.left.shoulder.rotation.x = reach + Math.sin(t + 0.6) * 0.16 * amp;
      this.arms.right.shoulder.rotation.x = reach - Math.sin(t + 0.6) * 0.16 * amp;
      this.arms.left.shoulder.rotation.z = 0.22;
      this.arms.right.shoulder.rotation.z = -0.22;
      this.arms.left.elbow.rotation.x = -0.4;
      this.arms.right.elbow.rotation.x = -0.4;
    }

    // Attack lunge.
    if (this.attackAnim > 0) {
      this.attackAnim = Math.max(0, this.attackAnim - dt);
      const a = Math.sin((1 - this.attackAnim / 0.45) * Math.PI);
      this.arms.left.shoulder.rotation.x -= a * 1.1;
      this.arms.right.shoulder.rotation.x -= a * 1.1;
      this.torso.rotation.x = -a * 0.25;
    } else {
      this.torso.rotation.x = lerp(this.torso.rotation.x, 0, clamp(dt * 8, 0, 1));
    }

    if (this.state === STATE.STAGGER) {
      this.body.rotation.z += Math.sin(time * 42) * 0.12;
      this.torso.rotation.x = 0.2;
    }

    // Head lolls toward the player-ish direction.
    this.head.rotation.z = Math.sin(t * 0.7) * 0.12;
    this.jaw.rotation.x = 0.2 + Math.sin(t * 1.6) * 0.18;
  }
}

export { STATE as ZSTATE };
export function randomTypeForRound(round, rng = Math.random) {
  if (round >= 8 && rng() < clamp((round - 6) * 0.028, 0, 0.32)) return 'runner';
  if (round >= 12 && rng() < clamp((round - 10) * 0.014, 0, 0.16)) return 'heavy';
  return 'normal';
}
export { randInt };
