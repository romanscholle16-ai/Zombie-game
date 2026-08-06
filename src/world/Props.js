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

/**
 * Perk dispenser: a battered vending cabinet, lit marquee on top, a rack of
 * bottles behind scratched glass, a dispense tray at waist height and a kick
 * plate that has seen better decades.
 */
export function makePerkMachine(perk) {
  const g = new THREE.Group();
  const hex = `#${perk.color.toString(16).padStart(6, '0')}`;
  const shell = mat(`perkShell${perk.id}`, {
    map: Tex.metalPanel(1), color: perk.color, roughness: 0.52, metalness: 0.42,
  });
  const dark = mat('perkDark', { color: 0x181d20, roughness: 0.7, metalness: 0.45 });
  const glowMat = Mats.emissive(perk.color, 1.8);

  const W = 1.18, H = 2.24, D = 0.86;

  // ---- carcass: back, sides, and a chamfered top
  g.add(box(W, H, 0.1, shell, 0, H / 2, -D / 2 + 0.05));
  for (const s of [-1, 1]) g.add(box(0.11, H, D, shell, s * (W / 2 - 0.055), H / 2, 0));
  g.add(box(W, 0.12, D, dark, 0, H - 0.06, 0));
  g.add(box(W * 0.99, 0.5, D * 0.62, shell, 0, H - 0.34, -D * 0.16));

  // ---- plinth and kick plate
  g.add(box(W + 0.08, 0.16, D + 0.06, dark, 0, 0.08, 0));
  g.add(box(W * 0.9, 0.2, 0.04, mat('perkKick', { map: Tex.rustMetal(1), roughness: 0.9, metalness: 0.5 }),
    0, 0.26, D / 2 - 0.01));

  // ---- lit marquee across the crown
  const marquee = new THREE.Mesh(
    new THREE.PlaneGeometry(W * 0.86, 0.34),
    new THREE.MeshBasicMaterial({ map: Tex.label(perk.short, { fg: hex, sub: `${perk.cost} PTS` }) }),
  );
  marquee.position.set(0, H - 0.3, D / 2 - 0.02);
  g.add(marquee);
  const marqueeFrame = box(W * 0.92, 0.05, 0.05, glowMat, 0, H - 0.13, D / 2 - 0.02);
  const marqueeFrame2 = box(W * 0.92, 0.05, 0.05, glowMat, 0, H - 0.47, D / 2 - 0.02);
  g.add(marqueeFrame, marqueeFrame2);

  // ---- display window: bottle rack behind glass
  // Backing panel sits behind the bottles, not in front of them.
  const bay = box(W * 0.82, 0.94, 0.06, mat('perkBay', { color: 0x0b0e10, roughness: 1 }),
    0, 1.34, D / 2 - 0.36);
  g.add(bay);
  const bottleMat = Mats.emissive(perk.color, 0.9);
  for (let row = 0; row < 2; row++) {
    for (let i = 0; i < 4; i++) {
      const b = cyl(0.058, 0.24, bottleMat, -0.3 + i * 0.2, 1.06 + row * 0.44, D / 2 - 0.19, true);
      g.add(b);
      g.add(cyl(0.026, 0.06, dark, -0.3 + i * 0.2, 1.21 + row * 0.44, D / 2 - 0.19, true));
    }
    g.add(box(W * 0.8, 0.02, 0.14, dark, 0, 0.93 + row * 0.44, D / 2 - 0.19));
  }
  const glassPane = new THREE.Mesh(
    new THREE.PlaneGeometry(W * 0.82, 0.96),
    mat('perkGlass', { color: 0xbcd8e4, roughness: 0.1, metalness: 0.2, transparent: true, opacity: 0.19 }),
  );
  glassPane.position.set(0, 1.34, D / 2 - 0.008);
  g.add(glassPane);
  // Frame around the glass.
  for (const [w, h, y] of [[W * 0.86, 0.05, 1.84], [W * 0.86, 0.05, 0.84]]) {
    g.add(box(w, h, 0.06, dark, 0, y, D / 2 - 0.01));
  }
  for (const s of [-1, 1]) g.add(box(0.05, 1.04, 0.06, dark, s * W * 0.42, 1.34, D / 2 - 0.01));

  // ---- selection buttons down the side of the glass
  for (let i = 0; i < 3; i++) {
    g.add(box(0.07, 0.05, 0.03, i === 1 ? glowMat : dark, W * 0.33, 0.72 - i * 0.09, D / 2 - 0.01));
  }

  // ---- dispense tray, and the bottle that drops into it
  g.add(box(W * 0.56, 0.3, 0.16, mat('perkSlot', { color: 0x07090a, roughness: 1 }), 0, 0.58, D / 2 - 0.06));
  g.add(box(W * 0.6, 0.04, 0.2, dark, 0, 0.42, D / 2 - 0.04));
  const flap = box(W * 0.52, 0.24, 0.02, dark, 0, 0.6, D / 2 + 0.02);
  g.add(flap);
  const bottle = cyl(0.062, 0.26, Mats.emissive(perk.color, 1.4), 0, 0.62, D / 2 - 0.04, true);
  bottle.visible = false;
  g.add(bottle);

  // ---- accent light bars up the corners
  const barL = box(0.05, 1.5, 0.05, glowMat, -W / 2 + 0.03, 1.2, D / 2 - 0.06);
  const barR = box(0.05, 1.5, 0.05, glowMat, W / 2 - 0.03, 1.2, D / 2 - 0.06);
  g.add(barL, barR);

  g.userData.glowParts = [marqueeFrame, marqueeFrame2, barL, barR, marquee];
  g.userData.bottle = bottle;
  g.userData.flap = flap;
  g.userData.collider = { w: W + 0.1, h: H, d: D + 0.06 };
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

/**
 * Weapon upgrade machine. Reads as a repurposed industrial press: a heavy
 * armoured cabinet, a hooded intake you feed a weapon into, hydraulic rams
 * either side, a hazard-striped apron and a stack of extraction ducting.
 */
export function makeUpgradeMachine() {
  const g = new THREE.Group();
  const shell = mat('papShell', { map: Tex.metalPanel(1), color: 0x6b6f74, roughness: 0.55, metalness: 0.7 });
  const hot = Mats.emissive(0xff8a3d, 2.4);
  const W = 2.7, H = 2.75, D = 1.7;

  // ---- carcass with a chamfered hood
  g.add(box(W, H * 0.78, D, shell, 0, H * 0.39 + 0.16, 0));
  g.add(box(W + 0.16, 0.22, D + 0.16, Mats.darkMetal, 0, 0.11, 0));
  g.add(box(W + 0.1, 0.2, D + 0.1, Mats.darkMetal, 0, H * 0.78 + 0.26, 0));
  const hood = box(W * 0.94, 0.46, D * 0.7, shell, 0, H - 0.2, D * 0.12);
  hood.rotation.x = -0.22;
  g.add(hood);

  // ---- hazard apron along the base, where you stand
  g.add(box(W * 0.98, 0.26, 0.06, Mats.hazard, 0, 0.35, D / 2 + 0.02));

  // ---- intake throat: recessed slot, glowing interior, iris ring
  g.add(box(1.62, 0.62, 0.34, mat('papSlot', { color: 0x060807, roughness: 1 }), 0, 1.38, D / 2 - 0.06));
  const throat = box(1.46, 0.46, 0.06, hot, 0, 1.38, D / 2 + 0.03);
  g.add(throat);
  const iris = new THREE.Mesh(new THREE.TorusGeometry(0.66, 0.055, 8, 30), hot);
  iris.position.set(0, 1.38, D / 2 + 0.07);
  g.add(iris);
  // Guide rollers inside the throat.
  for (const s of [-1, 1]) {
    const roller = cyl(0.05, 1.3, Mats.darkMetal, 0, 1.38 + s * 0.26, D / 2 - 0.02);
    roller.rotation.z = Math.PI / 2;
    g.add(roller);
  }

  // ---- hydraulic rams either side of the intake
  for (const s of [-1, 1]) {
    g.add(cyl(0.13, 1.2, Mats.darkMetal, s * 1.12, 1.5, D * 0.2));
    g.add(cyl(0.08, 0.5, mat('papRod', { color: 0xd2d8dc, roughness: 0.2, metalness: 0.95 }),
      s * 1.12, 2.2, D * 0.2));
    g.add(box(0.34, 0.12, 0.34, Mats.darkMetal, s * 1.12, 2.44, D * 0.2));
  }

  // ---- gauges and a control box on the right cheek
  for (let i = 0; i < 3; i++) {
    const gauge = cyl(0.09, 0.05, mat('papGauge', { color: 0x11161a, roughness: 0.4, metalness: 0.6 }),
      -0.95 + i * 0.3, 0.82, D / 2 + 0.01, true);
    gauge.rotation.x = Math.PI / 2;
    g.add(gauge);
    g.add(box(0.02, 0.06, 0.02, hot, -0.95 + i * 0.3, 0.84, D / 2 + 0.04));
  }
  g.add(box(0.44, 0.5, 0.14, Mats.darkMetal, 1.0, 0.85, D / 2 - 0.02));
  g.add(box(0.3, 0.16, 0.04, hot, 1.0, 0.98, D / 2 + 0.05));

  // ---- extraction ducting off the back and over the top
  const pipes = makePipeRun(2.6, 5);
  pipes.position.set(0, H + 0.12, -D * 0.28);
  g.add(pipes);
  for (const s of [-1, 1]) {
    const stack = cyl(0.16, 0.8, Mats.rust, s * 0.9, H + 0.5, -D * 0.28);
    g.add(stack);
  }

  // ---- signage
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(1.9, 0.56), new THREE.MeshBasicMaterial({
    map: Tex.label('REFIT', { fg: '#ff8a3d', sub: 'INSERT WEAPON' }),
  }));
  sign.position.set(0, 2.16, D / 2 + 0.06);
  sign.rotation.x = -0.22;
  g.add(sign);

  const light = new THREE.PointLight(0xff8a3d, 0, 12);
  light.position.set(0, 1.8, D / 2 + 0.7);
  g.add(light);

  g.userData.glowParts = [throat, iris, sign];
  g.userData.ring = iris;
  g.userData.light = light;
  g.userData.collider = { w: W + 0.2, h: H, d: D + 0.2 };
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
/*
 * Firearm builder.
 *
 * Guns are assembled from the parts a real one has — upper and lower receiver,
 * magwell, ejection port, charging handle, trigger group, handguard, gas block,
 * muzzle device, stock, sights — rather than a block with a stick on the front.
 * The forms are generic to their class: nothing here reproduces any particular
 * manufacturer's product, marking or trade dress.
 *
 * Convention: the bore runs along +z, up is +y, origin at the receiver centre.
 */

const _geoCache = new Map();
function cachedGeo(key, build) {
  let g = _geoCache.get(key);
  if (!g) { g = build(); _geoCache.set(key, g); }
  return g;
}

/** Cylinder lying along +z, which is how nearly every gun part is oriented. */
function cylZ(r, len, material, x = 0, y = 0, z = 0, seg = 12) {
  const m = new THREE.Mesh(
    cachedGeo(`cz${seg}`, () => new THREE.CylinderGeometry(0.5, 0.5, 1, seg)),
    material,
  );
  m.scale.set(r * 2, len, r * 2);
  m.rotation.x = Math.PI / 2;
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}

/** Tapered cylinder along +z — barrel steps, bells, cones. */
function taperZ(r0, r1, len, material, x = 0, y = 0, z = 0, seg = 12) {
  const m = new THREE.Mesh(
    cachedGeo(`tz${seg}${r0.toFixed(4)}${r1.toFixed(4)}`,
      () => new THREE.CylinderGeometry(r1, r0, 1, seg)),
    material,
  );
  m.scale.set(1, len, 1);
  m.rotation.x = Math.PI / 2;
  m.position.set(x, y, z);
  m.castShadow = true;
  return m;
}

function ring(r, tube, material, arc = Math.PI * 2, seg = 8, tub = 18) {
  const m = new THREE.Mesh(
    cachedGeo(`r${r.toFixed(4)}${tube.toFixed(4)}${arc.toFixed(3)}${seg}${tub}`,
      () => new THREE.TorusGeometry(r, tube, seg, tub, arc)),
    material,
  );
  m.castShadow = true;
  return m;
}

/** A length of accessory rail: base strip plus recoil lugs. */
function railSection(len, material, teeth = true) {
  const g = new THREE.Group();
  g.add(box(0.019, 0.007, len, material, 0, 0, 0));
  if (!teeth) return g;
  const n = Math.max(2, Math.min(14, Math.round(len / 0.024)));
  for (let i = 0; i < n; i++) {
    const t = n === 1 ? 0.5 : i / (n - 1);
    g.add(box(0.023, 0.006, 0.008, material, 0, 0.006, -len / 2 + 0.009 + t * (len - 0.018)));
  }
  return g;
}

/**
 * Box magazine that curves forward as it drops, the way a rifle mag does to
 * follow the taper of its cartridges.
 */
function curvedMag(len, w, d, material, curve = 0.5) {
  const g = new THREE.Group();
  const n = 6;
  const seg = len / n;
  let y = 0, z = 0;
  for (let i = 0; i < n; i++) {
    const a = curve * (i / n);
    g.add((() => {
      const s = box(w * (1 - i * 0.015), seg * 1.1, d, material,
        0, y - Math.cos(a) * seg / 2, z + Math.sin(a) * seg / 2);
      s.rotation.x = -a;
      return s;
    })());
    y -= Math.cos(a) * seg;
    z += Math.sin(a) * seg;
  }
  // Floorplate.
  const fp = box(w * 1.12, 0.012, d * 1.1, material, 0, y + 0.004, z);
  fp.rotation.x = -curve;
  g.add(fp);
  return g;
}

/** Birdcage flash hider, ported brake, or plain crown. */
function muzzleDevice(kind, bore, material, z) {
  const g = new THREE.Group();
  const r = Math.max(0.011, bore * 1.5);
  if (kind === 'brake') {
    g.add(cylZ(r * 1.25, 0.062, material, 0, 0, z + 0.031));
    for (let i = 0; i < 3; i++) {
      for (const s of [-1, 1]) {
        g.add(box(0.006, r * 2.2, 0.007, material, s * r * 1.1, 0, z + 0.014 + i * 0.016));
      }
    }
  } else if (kind === 'suppressor') {
    g.add(cylZ(r * 2.0, 0.16, material, 0, 0, z + 0.08));
    for (let i = 0; i < 4; i++) {
      const band = ring(r * 2.0, 0.004, material);
      band.position.set(0, 0, z + 0.03 + i * 0.036);
      g.add(band);
    }
  } else if (kind === 'none') {
    g.add(taperZ(r * 1.1, r * 0.95, 0.02, material, 0, 0, z + 0.01));
  } else {
    // Birdcage: a slotted cage over a stepped base.
    g.add(cylZ(r * 1.1, 0.014, material, 0, 0, z + 0.007));
    g.add(cylZ(r * 1.35, 0.048, material, 0, 0, z + 0.038));
    const slotMat = mat('gnSlot', { color: 0x0b0e10, roughness: 1 });
    for (let i = 0; i < 5; i++) {
      const a = -Math.PI * 0.35 + (i / 4) * Math.PI * 0.7;
      g.add(box(0.005, 0.005, 0.03, slotMat,
        Math.sin(a) * r * 1.3, Math.cos(a) * r * 1.3, z + 0.04));
    }
    const crown = ring(r * 1.35, 0.005, material);
    crown.position.set(0, 0, z + 0.062);
    g.add(crown);
  }
  return g;
}

/** Swept pistol grip with a backstrap, plus trigger guard and trigger. */
function gripAssembly(material, metal, x, y, z, { rake = 0.34, len = 0.13, guard = true } = {}) {
  const g = new THREE.Group();
  const n = 4;
  for (let i = 0; i < n; i++) {
    const t = i / n;
    const seg = box(0.032, len / n * 1.12, 0.05 - t * 0.012, material,
      0, -len * (t + 0.5 / n), Math.sin(rake) * len * (t + 0.5 / n) * -1);
    seg.rotation.x = rake * 0.55;
    g.add(seg);
  }
  // Beavertail where the web of the hand sits.
  g.add(box(0.03, 0.03, 0.045, material, 0, -0.012, -0.018));
  // Butt cap.
  g.add(box(0.034, 0.012, 0.05, metal, 0, -len - 0.005, -Math.sin(rake) * len));
  if (guard) {
    const tg = ring(0.028, 0.005, metal, Math.PI * 1.15, 6, 14);
    tg.rotation.set(0, Math.PI / 2, -Math.PI * 0.62);
    tg.position.set(0, -0.028, 0.031);
    g.add(tg);
    const trigger = box(0.008, 0.026, 0.007, metal, 0, -0.024, 0.026);
    trigger.rotation.x = 0.2;
    g.add(trigger);
  }
  g.position.set(x, y, z);
  return g;
}

/** Telescoping stock on a buffer tube, with cheek weld and rubber pad. */
function collapsibleStock(len, material, metal, rubber, z) {
  const g = new THREE.Group();
  g.add(cylZ(0.019, len * 0.96, metal, 0, 0.004, z - len * 0.48));
  const body = box(0.044, 0.062, len * 0.46, material, 0, -0.004, z - len * 0.6);
  g.add(body);
  // Cheek riser and the sling loop under it.
  g.add(box(0.05, 0.016, len * 0.5, material, 0, 0.032, z - len * 0.58));
  g.add(box(0.03, 0.03, 0.012, metal, 0, -0.03, z - len * 0.42));
  g.add(box(0.052, 0.086, 0.016, rubber, 0, -0.004, z - len * 0.94));
  return g;
}

/** One-piece stock with a wrist and comb — shotguns and bolt guns. */
function fixedStock(len, material, rubber, z, drop = 0.03) {
  const g = new THREE.Group();
  const wrist = box(0.038, 0.062, len * 0.42, material, 0, -0.018, z - len * 0.2);
  wrist.rotation.x = -0.09;
  g.add(wrist);
  const comb = box(0.046, 0.075, len * 0.52, material, 0, -drop, z - len * 0.66);
  comb.rotation.x = -0.04;
  g.add(comb);
  g.add(box(0.05, 0.026, len * 0.4, material, 0, -drop + 0.05, z - len * 0.7));
  g.add(box(0.05, 0.096, 0.018, rubber, 0, -drop - 0.006, z - len * 0.95));
  return g;
}

const FAMILY_BY_CATEGORY = {
  pistol: 'pistol', smg: 'smg', ar: 'ar', shotgun: 'shotgun',
  lmg: 'lmg', sniper: 'sniper', wonder: 'energy',
};

/**
 * Builds a weapon from its definition. Used by the view model, wall buys, the
 * mystery box reveal and the loadout viewer.
 *
 * @param {object} def    weapon definition from WeaponDefs
 * @param {number} scale  uniform scale applied at the end
 * @param {{detail?:number}} opts detail 0 drops fasteners and rail teeth
 */
export function makeWeaponModel(def, scale = 1, opts = {}) {
  const detail = opts.detail ?? 1;
  const g = new THREE.Group();
  const S = def.shape || {};
  const fam = S.family ?? FAMILY_BY_CATEGORY[def.category] ?? 'ar';

  // Tints are picked to read as dark gunmetal in the world, then lifted so
  // they still have form under the sparse lighting of a first-person view.
  const bodyHex = lighten(def.tint ?? 0x2e3337, 3.6);
  const accentHex = lighten(def.accent ?? 0x53595e, 2.3);
  const steel = mat(`gnS${bodyHex}`, {
    map: Tex.gunMetal(2), color: bodyHex, roughness: 0.4, metalness: 0.8,
  });
  const poly = mat(`gnP${bodyHex}`, {
    color: lighten(bodyHex, 0.72), roughness: 0.78, metalness: 0.05,
  });
  const accent = mat(`gnA${accentHex}`, { color: accentHex, roughness: 0.3, metalness: 0.78 });
  const grip = mat('gnGrip', { map: Tex.gunGrip(2), color: 0x8b949c, roughness: 0.95, metalness: 0.03 });
  const rubber = mat('gnRubber', { color: 0x1d2124, roughness: 0.98, metalness: 0.0 });
  const wood = mat('gnWood', { map: Tex.gunWood(1), color: 0xb08050, roughness: 0.6, metalness: 0.02 });
  const furniture = fam === 'shotgun' || S.wood ? wood : poly;

  const rec = S.recv ?? [0.06, 0.12, 0.4];
  // Real receivers are far narrower than they are tall; the old models were
  // square in section, which is most of why they read as toys.
  const W = Math.min(rec[0], 0.058);
  const H = rec[1];
  const D = rec[2];
  const bl = S.barrel ?? 0.3;
  const bore = S.bore ?? 0.0125;
  const boreY = H * 0.16;
  const front = D / 2;

  // ---------------------------------------------------------------- receiver
  if (fam === 'pistol' || fam === 'revolver') {
    // Slide over frame, with the ejection port cut into the right side.
    const slide = box(W, H * 0.5, D, steel, 0, boreY + H * 0.16, 0);
    g.add(slide);
    g.add(box(W * 0.72, H * 0.34, D * 0.86, steel, 0, boreY - H * 0.1, -D * 0.02));
    if (detail) {
      // Slide serrations at the rear, where you rack it.
      for (let i = 0; i < 6; i++) {
        g.add(box(W * 1.02, H * 0.34, 0.005, accent, 0, boreY + H * 0.16, -D * 0.42 + i * 0.011));
      }
      g.add(box(W * 1.04, H * 0.16, D * 0.2, steel, 0, boreY + H * 0.2, D * 0.02));
    }
  } else {
    // Upper receiver: flat-topped, with the ejection port and its deflector.
    g.add(box(W, H * 0.46, D, steel, 0, boreY + H * 0.14, 0));
    g.add(box(W * 0.98, H * 0.06, D * 0.98, accent, 0, boreY + H * 0.37, 0));
    // Lower receiver, deeper at the magwell.
    g.add(box(W * 0.94, H * 0.4, D * 0.82, furniture, 0, boreY - H * 0.16, -D * 0.02));
    if (detail) {
      const port = box(W * 1.03, H * 0.16, D * 0.22, mat('gnPort', { color: 0x0c0f11, roughness: 0.9 }),
        W * 0.02, boreY + H * 0.14, D * 0.1);
      g.add(port);
      // Brass deflector behind the port.
      const defl = box(W * 0.34, H * 0.13, 0.03, steel, W * 0.5, boreY + H * 0.16, D * 0.2);
      defl.rotation.z = 0.5;
      g.add(defl);
      // Charging handle at the rear of the upper.
      g.add(box(W * 1.5, H * 0.08, 0.02, accent, 0, boreY + H * 0.26, -D * 0.48));
      g.add(box(W * 0.3, H * 0.06, 0.05, accent, 0, boreY + H * 0.26, -D * 0.42));
      // Safety selector and magazine release.
      const sel = cylZ(0.008, W * 1.3, accent, 0, boreY - H * 0.1, -D * 0.24, 8);
      sel.rotation.set(0, Math.PI / 2, 0);
      g.add(sel);
      g.add(box(W * 1.2, 0.014, 0.014, accent, 0, boreY - H * 0.06, -D * 0.06));
    }
  }

  // ---------------------------------------------------------------- barrel
  const barrelMat = accent;
  if (fam === 'revolver') {
    // Underlugged barrel with a full-length rib.
    g.add(box(W * 0.7, H * 0.3, bl, barrelMat, 0, boreY + H * 0.12, front + bl / 2));
    g.add(cylZ(bore * 1.7, bl, barrelMat, 0, boreY + H * 0.12, front + bl / 2));
    g.add(box(W * 0.5, H * 0.16, bl * 0.8, steel, 0, boreY - H * 0.02, front + bl * 0.42));
    // Cylinder and its flutes.
    const cylr = H * 0.3;
    const drumMesh = cylZ(cylr, 0.09, steel, 0, boreY + H * 0.1, -D * 0.02, 12);
    g.add(drumMesh);
    for (let i = 0; i < 6 && detail; i++) {
      const a = (i / 6) * Math.PI * 2;
      g.add(cylZ(bore * 1.15, 0.095, mat('gnBore', { color: 0x0a0c0d, roughness: 1 }),
        Math.sin(a) * cylr * 0.62, boreY + H * 0.1 + Math.cos(a) * cylr * 0.62, -D * 0.02, 8));
    }
    g.userData.cylinder = drumMesh;
  } else if (fam === 'shotgun') {
    g.add(cylZ(bore * 1.28, bl, barrelMat, 0, boreY + H * 0.12, front + bl / 2));
    // Magazine tube slung under the barrel.
    g.add(cylZ(bore * 0.95, bl * 0.86, steel, 0, boreY + H * 0.12 - bore * 2.1, front + bl * 0.43));
    if (!S.drum) {
      // Pump slide.
      const pump = box(W * 1.15, H * 0.3, bl * 0.34, furniture, 0, boreY + H * 0.02, front + bl * 0.36);
      g.add(pump);
      if (detail) for (let i = 0; i < 5; i++) {
        g.add(box(W * 1.18, 0.008, 0.008, steel, 0, boreY + H * 0.02, front + bl * 0.22 + i * 0.018));
      }
      g.userData.pump = pump;
    }
    const crown = ring(bore * 1.3, 0.004, steel);
    crown.position.set(0, boreY + H * 0.12, front + bl);
    g.add(crown);
  } else if (fam === 'pistol') {
    g.add(cylZ(bore * 1.25, D * 0.92, barrelMat, 0, boreY + H * 0.16, D * 0.08));
    if (S.compensator) {
      g.add(box(W * 0.9, H * 0.2, 0.05, accent, 0, boreY + H * 0.22, front + 0.02));
    }
  } else {
    // Stepped barrel: thicker at the chamber, thinner past the gas block.
    const gasZ = front + bl * 0.52;
    g.add(cylZ(bore * 2.1, bl * 0.5, barrelMat, 0, boreY, front + bl * 0.25));
    g.add(cylZ(bore * 1.55, bl * 0.52, barrelMat, 0, boreY, front + bl * 0.75));
    // Gas block with its tube running back to the receiver.
    g.add(box(W * 0.52, H * 0.24, 0.036, steel, 0, boreY + H * 0.02, gasZ));
    g.add(cylZ(0.006, bl * 0.5, steel, 0, boreY + H * 0.12, front + bl * 0.28));
    if (fam === 'lmg') {
      // Carry handle and bipod legs.
      g.add(box(0.016, 0.05, 0.11, steel, W * 0.4, boreY + H * 0.42, front + bl * 0.1));
      for (const s of [-1, 1]) {
        const leg = cylZ(0.008, 0.2, steel, s * 0.02, boreY - H * 0.5, gasZ + 0.02, 6);
        leg.rotation.set(-1.15, 0, s * 0.28);
        g.add(leg);
      }
    }
  }

  // ---------------------------------------------------------------- handguard
  if (S.guard !== false && fam !== 'pistol' && fam !== 'revolver' && fam !== 'shotgun') {
    const hgLen = bl * (fam === 'sniper' ? 0.6 : 0.72);
    const hgZ = front + hgLen / 2 + 0.005;
    if (fam === 'sniper' || S.wood) {
      const stockFore = box(W * 1.25, H * 0.42, hgLen, furniture, 0, boreY - H * 0.12, hgZ);
      g.add(stockFore);
    } else {
      // Free-float tube, octagonal-ish, with vent slots and a short top rail.
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2 + Math.PI / 6;
        const p = box(W * 0.44, H * 0.12, hgLen, poly,
          Math.sin(a) * W * 0.44, boreY + Math.cos(a) * W * 0.44, hgZ);
        p.rotation.z = -a;
        g.add(p);
      }
      if (detail) {
        for (let i = 0; i < 4; i++) {
          for (const s of [-1, 1]) {
            g.add(box(0.004, 0.016, 0.028, mat('gnVent', { color: 0x0d1012, roughness: 1 }),
              s * W * 0.5, boreY, hgZ - hgLen * 0.3 + i * hgLen * 0.2));
          }
        }
        const r = railSection(hgLen * 0.9, accent, detail > 0);
        r.position.set(0, boreY + W * 0.56, hgZ);
        g.add(r);
      }
      // Angled foregrip where the support hand goes.
      if (S.foregrip !== false && fam !== 'sniper') {
        const fg = box(0.028, 0.075, 0.05, grip, 0, boreY - W * 0.75, hgZ + hgLen * 0.1);
        fg.rotation.x = -0.42;
        g.add(fg);
      }
    }
  }

  // ---------------------------------------------------------------- grip
  const gripZ = fam === 'pistol' || fam === 'revolver' ? -D * 0.22 : -D * 0.2;
  const gripY = fam === 'pistol' || fam === 'revolver' ? boreY - H * 0.24 : boreY - H * 0.3;
  g.add(gripAssembly(
    fam === 'shotgun' && S.wood ? wood : grip, steel, 0, gripY, gripZ,
    {
      rake: fam === 'pistol' || fam === 'revolver' ? 0.3 : 0.36,
      len: fam === 'pistol' || fam === 'revolver' ? 0.115 : 0.13,
    },
  ));

  // ---------------------------------------------------------------- feed
  if (S.drum) {
    const dr = fam === 'lmg' ? 0.1 : 0.085;
    const drum = cylZ(dr, 0.062, furniture, 0, boreY - H * 0.42 - dr * 0.5, D * 0.04, 16);
    g.add(drum);
    const lip = ring(dr * 0.96, 0.006, steel);
    lip.position.set(0, boreY - H * 0.42 - dr * 0.5, D * 0.04 + 0.032);
    g.add(lip);
    // Throat feeding the receiver.
    g.add(box(W * 0.7, H * 0.3, 0.05, furniture, 0, boreY - H * 0.32, D * 0.04));
    g.userData.magazine = drum;
  } else if (S.tube) {
    // Fed from the tube already under the barrel — nothing hangs below.
    g.userData.magazine = null;
  } else if (S.mag !== false) {
    const mLen = S.magLen ?? 0.2;
    const isPistolMag = fam === 'pistol';
    const m = isPistolMag
      ? box(0.028, mLen, 0.042, furniture, 0, gripY - mLen * 0.5 + 0.02, gripZ + 0.006)
      : curvedMag(mLen, 0.03, 0.05, furniture, S.magCurve ?? (fam === 'ar' ? 0.55 : 0.3));
    if (!isPistolMag) m.position.set(0, boreY - H * 0.36, D * (S.magZ ?? 0.04));
    else m.rotation.x = 0.3;
    g.add(m);
    g.userData.magazine = m;
  }

  // ---------------------------------------------------------------- stock
  if (S.stock !== false && fam !== 'pistol' && fam !== 'revolver') {
    const sLen = S.stockLen ?? 0.22;
    const backZ = -D / 2;
    g.add(fam === 'shotgun' || fam === 'sniper' || S.wood
      ? fixedStock(sLen, furniture, rubber, backZ, H * 0.22)
      : collapsibleStock(sLen, furniture, steel, rubber, backZ));
  }

  // ---------------------------------------------------------------- sights
  const topY = boreY + H * 0.4;
  let sightY = topY + 0.026;
  if (S.optic) {
    // Tube optic on ring mounts: objective bell, erector body, eyepiece.
    const mag = S.opticMag ?? (fam === 'sniper' ? 6 : 2);
    const big = mag >= 4;
    const tubeR = big ? 0.019 : 0.015;
    const oy = topY + tubeR + 0.019;
    const oz = -D * 0.06;
    const oLen = big ? 0.26 : 0.14;
    const optBody = mat('gnOptic', { color: 0x1a1e21, roughness: 0.42, metalness: 0.6 });
    g.add(cylZ(tubeR, oLen, optBody, 0, oy, oz));
    if (big) {
      g.add(taperZ(tubeR, tubeR * 1.6, 0.05, optBody, 0, oy, oz + oLen / 2 + 0.025));
      g.add(cylZ(tubeR * 1.6, 0.03, optBody, 0, oy, oz + oLen / 2 + 0.062));
      // Elevation and windage turrets.
      const elev = cylZ(0.011, 0.018, optBody, 0, oy + tubeR + 0.009, oz - 0.01, 10);
      elev.rotation.x = 0;    // stand it upright on top of the tube
      g.add(elev);
      const wind = cylZ(0.01, 0.016, optBody, tubeR + 0.008, oy, oz - 0.01, 10);
      wind.rotation.set(0, 0, Math.PI / 2);
      g.add(wind);
    }
    g.add(cylZ(tubeR * 1.25, 0.034, optBody, 0, oy, oz - oLen / 2 - 0.017));
    // Front lens: a dark disc with a faint coating tint, not a lamp.
    const lens = cylZ(big ? tubeR * 1.5 : tubeR * 0.9, 0.004,
      mat('gnLens', { color: 0x0a1418, emissive: 0x2b6a7d, emissiveIntensity: 0.35, roughness: 0.1, metalness: 0.9 }),
      0, oy, oz + (big ? oLen / 2 + 0.076 : oLen / 2 + 0.002), 14);
    g.add(lens);
    // Mount rings clamped to the receiver rail.
    for (const dz of [-oLen * 0.3, oLen * 0.3]) {
      const rr = ring(tubeR + 0.004, 0.005, accent);
      rr.position.set(0, oy, oz + dz);
      g.add(rr);
      g.add(box(0.022, 0.018, 0.016, accent, 0, oy - tubeR - 0.012, oz + dz));
    }
    sightY = oy;
    g.userData.optic = { magnification: mag, scoped: big };
  } else {
    // Irons: protected front post, rear aperture on a folding base.
    const fz = front + bl * 0.86;
    g.add(box(0.006, 0.026, 0.008, accent, 0, topY + 0.012, fz));
    const hood = ring(0.016, 0.003, accent, Math.PI, 6, 12);
    hood.rotation.z = 0;
    hood.position.set(0, topY + 0.012, fz);
    g.add(hood);
    g.add(box(0.026, 0.01, 0.022, accent, 0, topY - 0.002, fz));
    const ap = ring(0.011, 0.004, accent);
    ap.position.set(0, topY + 0.014, -D * 0.4);
    g.add(ap);
    g.add(box(0.03, 0.016, 0.018, accent, 0, topY + 0.001, -D * 0.4));
    if (fam !== 'pistol' && fam !== 'revolver' && detail) {
      const r = railSection(D * 0.7, accent, true);
      r.position.set(0, topY + 0.004, -D * 0.02);
      g.add(r);
    }
    sightY = topY + 0.014;
  }
  g.userData.sightHeight = sightY;

  // ---------------------------------------------------------------- energy
  if (S.cell) {
    const em = Mats.emissive(def.accent ?? 0x9fd93a, 2.4);
    const cell = cylZ(0.022, 0.13, em, 0, boreY + H * 0.02, -D * 0.12, 10);
    cell.rotation.set(0, 0, Math.PI / 2);
    g.add(cell);
    const coil = ring(bore * 2.4, 0.006, em);
    coil.position.set(0, boreY, front + bl * 0.72);
    g.add(coil);
    const coil2 = ring(bore * 2.8, 0.005, em);
    coil2.position.set(0, boreY, front + bl * 0.92);
    g.add(coil2);
    // Cooling fins along the emitter.
    for (let i = 0; i < 4; i++) {
      g.add(box(W * 1.3, 0.006, 0.01, accent, 0, boreY + H * 0.24, front + bl * 0.2 + i * 0.03));
    }
    g.userData.energyParts = [cell, coil, coil2];
  }

  // Muzzle device, and the exact bore exit for tracers and flash placement.
  const muzzleZ = fam === 'pistol' || fam === 'revolver'
    ? (fam === 'revolver' ? front + bl : D * 0.54)
    : front + bl;
  if (fam !== 'shotgun') {
    g.add(muzzleDevice(S.muzzle ?? (fam === 'pistol' ? 'none' : fam === 'sniper' ? 'brake' : 'cage'),
      bore, accent, muzzleZ));
  }
  g.userData.muzzle = new THREE.Vector3(0, boreY + (fam === 'pistol' ? H * 0.16 : 0), muzzleZ + 0.03);
  // Where the firing hand grips the weapon. The view model hangs everything
  // off this point, so a long rifle and a pistol both sit naturally in frame
  // instead of being centred on their own receivers.
  g.userData.gripAnchor = new THREE.Vector3(0, gripY - 0.035, gripZ + 0.02);

  g.userData.body = steel;
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
