import * as THREE from 'three';
import { Tex, mat } from './Textures.js';
import { rand, seededRandom } from '../core/Util.js';

/**
 * Procedural prop + machine library. Everything is built from primitives and
 * canvas textures at runtime — the game ships with no imported art.
 */

const BOX = new THREE.BoxGeometry(1, 1, 1);
const CYL = new THREE.CylinderGeometry(0.5, 0.5, 1, 14);
const CYL_LO = new THREE.CylinderGeometry(0.5, 0.5, 1, 8);
const SPH = new THREE.SphereGeometry(0.5, 12, 10);

export function box(w, h, d, material, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(BOX, material);
  m.scale.set(w, h, d);
  m.position.set(x, y, z);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
}

export function cyl(r, h, material, x = 0, y = 0, z = 0, lo = false) {
  const m = new THREE.Mesh(lo ? CYL_LO : CYL, material);
  m.scale.set(r * 2, h, r * 2);
  m.position.set(x, y, z);
  m.castShadow = true;
  m.receiveShadow = true;
  return m;
}

export function sphere(r, material, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(SPH, material);
  m.scale.setScalar(r * 2);
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}

/** Scales a hex colour toward white without blowing out any channel. */
export function lighten(hex, factor) {
  const c = new THREE.Color(hex);
  c.multiplyScalar(factor);
  c.r = Math.min(1, c.r); c.g = Math.min(1, c.g); c.b = Math.min(1, c.b);
  return c.getHex();
}

// ------------------------------------------------------------------ materials
export const Mats = {
  get wood() { return mat('wood', { map: Tex.wood(1), roughness: 0.92, metalness: 0.02 }); },
  get plank() { return mat('plank', { map: Tex.wood(1), roughness: 0.95, color: 0xcaa06e }); },
  get metal() { return mat('metal', { map: Tex.metalPanel(1), roughness: 0.52, metalness: 0.75 }); },
  get rust() { return mat('rust', { map: Tex.rustMetal(1), roughness: 0.85, metalness: 0.4 }); },
  get darkMetal() { return mat('darkMetal', { color: 0x2b3033, roughness: 0.45, metalness: 0.85 }); },
  get concrete() { return mat('concreteM', { map: Tex.concrete(1), roughness: 0.96 }); },
  get plastic() { return mat('plastic', { color: 0x3a4247, roughness: 0.6, metalness: 0.1 }); },
  get glass() {
    return mat('glass', { color: 0x9fd4d0, roughness: 0.08, metalness: 0.1, transparent: true, opacity: 0.32 });
  },
  get hazard() { return mat('hazardM', { map: Tex.hazard(1), roughness: 0.8 }); },
  emissive(color, intensity = 2.2) {
    return mat(`em${color}${intensity}`, {
      color: 0x0a0a0a, emissive: color, emissiveIntensity: intensity, roughness: 0.4,
    });
  },
};

// ------------------------------------------------------------------ clutter
export function makeCrate(size = 1, seed = 0) {
  const g = new THREE.Group();
  const s = size * (0.8 + (seed % 5) * 0.08);
  const body = box(s, s * 0.86, s, Mats.wood, 0, s * 0.43, 0);
  g.add(body);
  const fm = mat('crateFrame', { color: 0x5a4529, roughness: 0.95 });
  const t = 0.06 * s;
  for (const [x, z] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) {
    g.add(box(t, s * 0.86, t, fm, x * s * 0.47, s * 0.43, z * s * 0.47));
  }
  g.add(box(s * 1.01, t, s * 1.01, fm, 0, s * 0.86 - t / 2, 0));
  g.userData.collider = { w: s, h: s * 0.86, d: s, platform: true };
  return g;
}

export function makeBarrel(seed = 0) {
  const g = new THREE.Group();
  const m = seed % 3 === 0 ? Mats.hazard : Mats.rust;
  const b = cyl(0.36, 1.1, m, 0, 0.55, 0);
  g.add(b);
  const rim = mat('barrelRim', { color: 0x3a3f42, roughness: 0.6, metalness: 0.7 });
  g.add(cyl(0.38, 0.06, rim, 0, 0.22, 0, true));
  g.add(cyl(0.38, 0.06, rim, 0, 0.88, 0, true));
  g.userData.collider = { w: 0.76, h: 1.1, d: 0.76, platform: true };
  return g;
}

export function makeShelf(seed = 0) {
  const g = new THREE.Group();
  const w = 2.2, h = 2.0, d = 0.6;
  const post = mat('shelfPost', { color: 0x4a5053, roughness: 0.6, metalness: 0.6 });
  for (const x of [-1, 1]) {
    for (const z of [-1, 1]) g.add(box(0.08, h, 0.08, post, x * (w / 2 - 0.06), h / 2, z * (d / 2 - 0.06)));
  }
  const rng = seededRandom(seed + 7);
  for (let i = 0; i < 3; i++) {
    g.add(box(w, 0.06, d, Mats.metal, 0, 0.45 + i * 0.66, 0));
    // Boxes on the shelf.
    const n = 1 + Math.floor(rng() * 3);
    for (let k = 0; k < n; k++) {
      const bw = 0.28 + rng() * 0.3;
      g.add(box(bw, 0.26, 0.36, Mats.wood, -w / 2 + 0.3 + rng() * (w - 0.6), 0.61 + i * 0.66, (rng() - 0.5) * 0.2));
    }
  }
  g.userData.collider = { w, h, d, platform: false };
  return g;
}

export function makeTable(seed = 0) {
  const g = new THREE.Group();
  const w = 2.0, h = 0.9, d = 0.9;
  g.add(box(w, 0.08, d, Mats.metal, 0, h, 0));
  const leg = mat('tableLeg', { color: 0x555b5e, roughness: 0.5, metalness: 0.7 });
  for (const x of [-1, 1]) for (const z of [-1, 1]) g.add(box(0.07, h, 0.07, leg, x * (w / 2 - 0.14), h / 2, z * (d / 2 - 0.12)));
  const rng = seededRandom(seed + 3);
  // Lab glassware.
  for (let i = 0; i < 3; i++) {
    const r = 0.07 + rng() * 0.05;
    g.add(cyl(r, 0.2 + rng() * 0.16, Mats.glass, (rng() - 0.5) * (w - 0.5), h + 0.14, (rng() - 0.5) * 0.4, true));
  }
  g.add(box(0.5, 0.14, 0.34, Mats.plastic, w * 0.28, h + 0.11, -0.1));
  g.userData.collider = { w, h, d, platform: true };
  return g;
}

export function makeGenerator(seed = 0) {
  const g = new THREE.Group();
  g.add(box(2.4, 1.5, 1.3, Mats.metal, 0, 0.75, 0));
  g.add(cyl(0.42, 2.0, Mats.rust, -0.7, 1.9, 0));
  g.add(box(0.9, 0.5, 1.0, Mats.darkMetal, 0.8, 1.7, 0));
  const strip = new THREE.Mesh(new THREE.PlaneGeometry(1.6, 0.16), Mats.emissive(0x7fe0a0, 1.4));
  strip.position.set(0, 1.1, 0.66);
  g.add(strip);
  g.userData.emissiveStrips = [strip];
  g.userData.collider = { w: 2.4, h: 2.9, d: 1.3, platform: false };
  void seed;
  return g;
}

export function makeDebris(seed = 0) {
  const rng = seededRandom(seed + 91);
  const g = new THREE.Group();
  const n = 2 + Math.floor(rng() * 4);
  for (let i = 0; i < n; i++) {
    const m = rng() > 0.5 ? Mats.concrete : Mats.wood;
    const w = 0.12 + rng() * 0.5, h = 0.06 + rng() * 0.18, d = 0.12 + rng() * 0.5;
    const b = box(w, h, d, m, (rng() - 0.5) * 1.6, h / 2, (rng() - 0.5) * 1.6);
    b.rotation.set(rng() * 0.3, rng() * Math.PI, rng() * 0.3);
    b.castShadow = false;
    g.add(b);
  }
  return g;
}

export function makePipeRun(length, seed = 0) {
  const g = new THREE.Group();
  const rng = seededRandom(seed + 17);
  for (let i = 0; i < 3; i++) {
    const r = 0.07 + rng() * 0.08;
    const p = cyl(r, length, Mats.rust, (i - 1) * 0.28, 0, 0);
    p.rotation.z = Math.PI / 2;
    g.add(p);
  }
  return g;
}

// ------------------------------------------------------------------ machines
/** Wall-mounted weapon purchase plaque with a silhouette of the gun. */
export function makeWallBuy(weapon) {
  const g = new THREE.Group();
  const back = box(1.9, 1.1, 0.09, mat('wbBack', { color: 0x14181a, roughness: 0.9 }), 0, 0, 0);
  g.add(back);
  const frameMat = Mats.emissive(0x9fd93a, 0.9);
  g.add(box(1.96, 0.05, 0.1, frameMat, 0, 0.55, 0.01));
  g.add(box(1.96, 0.05, 0.1, frameMat, 0, -0.55, 0.01));

  const gun = makeWeaponModel(weapon, 0.55);
  gun.position.set(0, 0.06, 0.16);
  gun.rotation.set(0, Math.PI / 2, 0.06);
  g.add(gun);

  const sign = new THREE.Mesh(
    new THREE.PlaneGeometry(1.7, 0.42),
    new THREE.MeshBasicMaterial({ map: Tex.label(weapon.name, { sub: `${weapon.wallCost} PTS` }), transparent: false }),
  );
  sign.position.set(0, -0.36, 0.06);
  g.add(sign);
  g.userData.sign = sign;
  g.userData.weaponModel = gun;
  return g;
}

/** Perk dispenser cabinet. */
export function makePerkMachine(perk) {
  const g = new THREE.Group();
  const bodyMat = mat(`perkBody${perk.id}`, { color: perk.color, roughness: 0.45, metalness: 0.35 });
  g.add(box(1.15, 2.1, 0.9, bodyMat, 0, 1.05, 0));
  g.add(box(1.25, 0.14, 1.0, Mats.darkMetal, 0, 2.12, 0));
  g.add(box(1.25, 0.12, 1.0, Mats.darkMetal, 0, 0.06, 0));

  const glowMat = Mats.emissive(perk.color, 1.8);
  const panel = new THREE.Mesh(new THREE.PlaneGeometry(0.85, 0.55), new THREE.MeshBasicMaterial({
    map: Tex.label(perk.short, { fg: `#${perk.color.toString(16).padStart(6, '0')}`, sub: `${perk.cost}` }),
  }));
  panel.position.set(0, 1.5, 0.46);
  g.add(panel);

  const strip = box(0.9, 0.08, 0.04, glowMat, 0, 0.95, 0.46);
  g.add(strip);
  const strip2 = box(0.06, 1.5, 0.04, glowMat, -0.6, 1.2, 0.3);
  const strip3 = box(0.06, 1.5, 0.04, glowMat, 0.6, 1.2, 0.3);
  g.add(strip2, strip3);

  // Dispensing slot.
  g.add(box(0.5, 0.28, 0.14, mat('perkSlot', { color: 0x0a0c0a, roughness: 1 }), 0, 0.62, 0.44));
  const bottle = cyl(0.1, 0.3, Mats.emissive(perk.color, 1.2), 0, 0.66, 0.44, true);
  bottle.visible = false;
  g.add(bottle);

  g.userData.glowParts = [strip, strip2, strip3, panel];
  g.userData.bottle = bottle;
  g.userData.collider = { w: 1.25, h: 2.2, d: 1.0 };
  return g;
}

/** Mystery weapon crate. */
export function makeMysteryBox() {
  const g = new THREE.Group();
  const base = box(1.5, 0.95, 1.1, Mats.wood, 0, 0.47, 0);
  g.add(base);
  const trim = mat('boxTrim', { color: 0x6b5a34, roughness: 0.7, metalness: 0.3 });
  for (const x of [-1, 1]) g.add(box(0.08, 0.95, 1.14, trim, x * 0.72, 0.47, 0));
  const lid = new THREE.Group();
  const lidMesh = box(1.55, 0.16, 1.15, Mats.wood, 0, 0.08, 0);
  lid.add(lidMesh);
  lid.add(box(1.58, 0.05, 1.18, trim, 0, 0.17, 0));
  lid.position.set(0, 0.95, -0.55);
  g.add(lid);

  const glow = new THREE.PointLight(0x9fd93a, 0, 9);
  glow.position.set(0, 1.2, 0);
  g.add(glow);

  const question = new THREE.Mesh(
    new THREE.PlaneGeometry(0.8, 0.6),
    new THREE.MeshBasicMaterial({ map: Tex.label('?', { fg: '#e8c14a' }), transparent: false }),
  );
  question.position.set(0, 0.55, 0.57);
  g.add(question);

  g.userData.lid = lid;
  g.userData.glow = glow;
  g.userData.collider = { w: 1.6, h: 1.0, d: 1.2 };
  return g;
}

/** Weapon upgrade machine (Pack-a-Punch equivalent). */
export function makeUpgradeMachine() {
  const g = new THREE.Group();
  g.add(box(2.6, 2.6, 1.6, Mats.metal, 0, 1.3, 0));
  g.add(box(2.8, 0.2, 1.8, Mats.darkMetal, 0, 2.62, 0));
  g.add(box(2.8, 0.18, 1.8, Mats.darkMetal, 0, 0.09, 0));
  // Insertion slot with a glowing throat.
  g.add(box(1.5, 0.5, 0.3, mat('papSlot', { color: 0x070907, roughness: 1 }), 0, 1.35, 0.78));
  const throat = box(1.4, 0.42, 0.06, Mats.emissive(0xff8a3d, 2.4), 0, 1.35, 0.86);
  g.add(throat);
  const ring = new THREE.Mesh(new THREE.TorusGeometry(0.62, 0.05, 8, 28), Mats.emissive(0xff8a3d, 2.0));
  ring.position.set(0, 1.35, 0.9);
  g.add(ring);
  const pipes = makePipeRun(2.4, 5);
  pipes.position.set(0, 2.35, -0.5);
  g.add(pipes);
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(2.0, 0.6), new THREE.MeshBasicMaterial({
    map: Tex.label('REFIT', { fg: '#ff8a3d', sub: 'INSERT WEAPON' }),
  }));
  sign.position.set(0, 2.15, 0.82);
  g.add(sign);
  const light = new THREE.PointLight(0xff8a3d, 0, 12);
  light.position.set(0, 1.8, 1.2);
  g.add(light);

  g.userData.glowParts = [throat, ring, sign];
  g.userData.ring = ring;
  g.userData.light = light;
  g.userData.collider = { w: 2.8, h: 2.7, d: 1.8 };
  return g;
}

/** Main breaker for the power system. */
export function makePowerSwitch() {
  const g = new THREE.Group();
  g.add(box(1.6, 2.2, 0.5, Mats.metal, 0, 1.1, 0));
  g.add(box(1.7, 0.1, 0.6, Mats.darkMetal, 0, 2.2, 0));
  const dead = mat('breakerDead', { color: 0x2a1010, roughness: 0.7 });
  for (let i = 0; i < 3; i++) g.add(box(0.34, 0.34, 0.08, dead, (i - 1) * 0.42, 1.6, 0.28));
  const lever = box(0.16, 0.7, 0.16, Mats.hazard, 0, 0.85, 0.3);
  lever.rotation.x = 0.9;
  g.add(lever);
  const lamp = box(0.2, 0.2, 0.06, Mats.emissive(0xff2b1e, 2.0), 0.55, 1.95, 0.28);
  g.add(lamp);
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(1.3, 0.4), new THREE.MeshBasicMaterial({
    map: Tex.label('MAIN BREAKER', { fg: '#e8a33d' }),
  }));
  sign.position.set(0, 0.35, 0.27);
  g.add(sign);
  g.userData.lever = lever;
  g.userData.lamp = lamp;
  g.userData.collider = { w: 1.7, h: 2.3, d: 0.6 };
  return g;
}

/** Wall panel used to arm a trap. */
export function makeTrapPanel(kind) {
  const colors = { electric: 0x66d9ff, fire: 0xff7b2e, turret: 0xffd24a };
  const g = new THREE.Group();
  g.add(box(0.7, 0.9, 0.16, Mats.darkMetal, 0, 0, 0));
  const face = box(0.56, 0.72, 0.05, Mats.emissive(colors[kind] ?? 0x9fd93a, 1.6), 0, 0, 0.1);
  g.add(face);
  g.userData.face = face;
  g.userData.color = colors[kind] ?? 0x9fd93a;
  return g;
}

/** Physical trap emitters placed in the world. */
export function makeTrapEmitter(kind) {
  const g = new THREE.Group();
  if (kind === 'electric') {
    for (const s of [-1, 1]) {
      const pole = cyl(0.14, 3.2, Mats.darkMetal, s * 2.6, 1.6, 0);
      g.add(pole);
      const coil = new THREE.Mesh(new THREE.TorusGeometry(0.3, 0.05, 6, 18), Mats.emissive(0x66d9ff, 2.2));
      coil.position.set(s * 2.6, 2.9, 0);
      coil.rotation.x = Math.PI / 2;
      g.add(coil);
      g.userData.coils = (g.userData.coils || []).concat(coil);
    }
  } else if (kind === 'fire') {
    for (let i = 0; i < 4; i++) {
      const n = box(0.3, 0.3, 0.5, Mats.rust, (i - 1.5) * 2.2, 0.3, 0);
      g.add(n);
      const jet = cyl(0.1, 0.6, Mats.emissive(0xff7b2e, 2.6), (i - 1.5) * 2.2, 0.55, 0.2, true);
      jet.visible = false;
      g.add(jet);
      g.userData.jets = (g.userData.jets || []).concat(jet);
    }
  } else {
    const base = cyl(0.5, 0.5, Mats.darkMetal, 0, 0.25, 0);
    g.add(base);
    const head = box(0.7, 0.55, 0.9, Mats.metal, 0, 0.8, 0);
    g.add(head);
    const barrel = cyl(0.09, 1.0, Mats.darkMetal, 0, 0.85, 0.6);
    barrel.rotation.x = Math.PI / 2;
    g.add(barrel);
    const eye = box(0.14, 0.14, 0.05, Mats.emissive(0xffd24a, 2.2), 0, 1.0, 0.46);
    g.add(eye);
    g.userData.head = head;
    g.userData.muzzle = barrel;
    g.userData.eye = eye;
  }
  return g;
}

// ------------------------------------------------------------------ weapons
/**
 * Builds a chunky, readable weapon silhouette from the weapon definition.
 * Used for the view model, wall buys, the box reveal and the loadout viewer.
 */
export function makeWeaponModel(def, scale = 1) {
  const g = new THREE.Group();
  // Gun tints are picked to read as dark gunmetal in the world; lifted here so
  // they still have form under the sparse lighting of a first-person view.
  const bodyHex = lighten(def.tint ?? 0x2e3337, 4.2);
  const accentHex = lighten(def.accent ?? 0x53595e, 2.6);
  const body = mat(`gunBody${bodyHex}`, { color: bodyHex, roughness: 0.44, metalness: 0.5 });
  const grip = mat('gunGrip', { color: 0x4a5054, roughness: 0.88, metalness: 0.08 });
  const accent = mat(`gunAccent${accentHex}`, { color: accentHex, roughness: 0.32, metalness: 0.68 });

  const S = def.shape || {};
  const L = S.len ?? 0.9;
  const rec = S.recv ?? [0.1, 0.13, L * 0.45];

  // Receiver
  g.add(box(rec[0], rec[1], rec[2], body, 0, 0, 0));
  // Barrel
  const bl = S.barrel ?? L * 0.45;
  g.add(cyl(S.bore ?? 0.028, bl, accent, 0, 0.012, rec[2] / 2 + bl / 2, true));
  // Handguard
  if (S.guard !== false) {
    g.add(box(rec[0] * 0.86, rec[1] * 0.66, bl * 0.72, body, 0, -0.008, rec[2] / 2 + bl * 0.34));
  }
  // Grip
  const gp = box(0.072, 0.16, 0.09, grip, 0, -0.12, -rec[2] * 0.18);
  gp.rotation.x = -0.28;
  g.add(gp);
  // Magazine
  if (S.mag !== false) {
    const mg = box(0.07, S.magLen ?? 0.2, 0.075, grip, 0, -(S.magLen ?? 0.2) / 2 - 0.05, rec[2] * 0.06);
    mg.rotation.x = S.magTilt ?? 0.1;
    g.add(mg);
    g.userData.magazine = mg;
  }
  // Stock
  if (S.stock !== false) {
    g.add(box(0.07, 0.11, S.stockLen ?? 0.22, body, 0, -0.03, -rec[2] / 2 - (S.stockLen ?? 0.22) / 2));
    g.add(box(0.075, 0.15, 0.05, grip, 0, -0.03, -rec[2] / 2 - (S.stockLen ?? 0.22) - 0.02));
  }
  // Sights
  const rear = box(0.05, 0.045, 0.05, accent, 0, rec[1] / 2 + 0.02, -rec[2] * 0.26);
  const front = box(0.032, 0.05, 0.03, accent, 0, rec[1] / 2 + 0.022, rec[2] / 2 + bl * 0.82);
  g.add(rear, front);
  g.userData.sightHeight = rec[1] / 2 + 0.045;
  // Optic
  if (S.optic) {
    const o = cyl(0.045, 0.16, mat('optic', { color: 0x15181a, roughness: 0.4, metalness: 0.6 }), 0, rec[1] / 2 + 0.06, -rec[2] * 0.1);
    o.rotation.x = Math.PI / 2;
    g.add(o);
    const lens = cyl(0.04, 0.01, Mats.emissive(0x66d9ff, 0.9), 0, rec[1] / 2 + 0.06, -rec[2] * 0.1 - 0.08, true);
    lens.rotation.x = Math.PI / 2;
    g.add(lens);
    g.userData.sightHeight = rec[1] / 2 + 0.06;
  }
  // Drum / shell tube extras
  if (S.drum) {
    const d = cyl(0.13, 0.09, grip, 0, -0.14, rec[2] * 0.05);
    d.rotation.x = Math.PI / 2;
    g.add(d);
  }
  if (S.tube) {
    g.add(cyl(0.032, bl * 0.9, accent, 0, -0.05, rec[2] / 2 + bl * 0.45, true));
  }
  // Wonder-weapon energy cell
  if (S.cell) {
    const cell = cyl(0.06, 0.22, Mats.emissive(def.accent ?? 0x9fd93a, 2.4), 0, 0.07, -rec[2] * 0.05, true);
    cell.rotation.z = Math.PI / 2;
    g.add(cell);
    const coil = new THREE.Mesh(new THREE.TorusGeometry(0.06, 0.014, 6, 16), Mats.emissive(def.accent ?? 0x9fd93a, 2.4));
    coil.position.set(0, 0.012, rec[2] / 2 + bl * 0.7);
    coil.rotation.y = Math.PI / 2;
    g.add(coil);
    g.userData.energyParts = [cell, coil];
  }

  // Upgrade decoration (added when a weapon is refitted).
  g.userData.body = body;
  g.scale.setScalar(scale);
  return g;
}

/** Simple bladed melee model. */
export function makeKnifeModel(scale = 1) {
  const g = new THREE.Group();
  const blade = box(0.03, 0.09, 0.32, mat('blade', { color: 0xd6dde1, roughness: 0.26, metalness: 0.7 }), 0, 0, 0.2);
  const tip = new THREE.Mesh(new THREE.ConeGeometry(0.05, 0.12, 4), blade.material);
  tip.position.set(0, 0, 0.42);
  tip.rotation.x = Math.PI / 2;
  g.add(blade, tip);
  g.add(box(0.05, 0.11, 0.05, mat('guard', { color: 0x30353a, roughness: 0.5, metalness: 0.7 }), 0, 0, 0.02));
  g.add(box(0.042, 0.05, 0.18, mat('handle', { color: 0x33383c, roughness: 0.95 }), 0, 0, -0.1));
  g.scale.setScalar(scale);
  return g;
}

/** Floating powerup icon. */
export function makePowerupIcon(color, glyph) {
  const g = new THREE.Group();
  const shell = new THREE.Mesh(
    new THREE.IcosahedronGeometry(0.34, 0),
    mat(`puShell${color}`, { color, emissive: color, emissiveIntensity: 1.6, roughness: 0.3, metalness: 0.4,
      transparent: true, opacity: 0.9 }),
  );
  g.add(shell);
  const face = new THREE.Mesh(new THREE.PlaneGeometry(0.44, 0.44), new THREE.MeshBasicMaterial({
    map: Tex.label(glyph, { fg: '#ffffff', bg: '#000000' }), transparent: true, opacity: 0.95,
  }));
  face.position.z = 0.35;
  g.add(face);
  const back = face.clone();
  back.position.z = -0.35;
  back.rotation.y = Math.PI;
  g.add(back);
  const light = new THREE.PointLight(color, 16, 7);
  g.add(light);
  g.userData.shell = shell;
  g.userData.light = light;
  g.userData.faces = [face, back];
  return g;
}

export function randomProp(kind, seed) {
  switch (kind) {
    case 'crates': return makeCrate(1 + rand(-0.15, 0.35), seed);
    case 'barrels': return makeBarrel(seed);
    case 'shelves': return makeShelf(seed);
    case 'tables': return makeTable(seed);
    case 'generators': return makeGenerator(seed);
    default: return makeDebris(seed);
  }
}
