/**
 * Weapon catalogue. All names, silhouettes and stats are original.
 *
 * shape[] drives the procedural model in Props.makeWeaponModel().
 * boxWeight 0 keeps a weapon out of the mystery box.
 */

export const RARITY = {
  common: { label: 'COMMON', color: 0x8d948c, order: 0 },
  uncommon: { label: 'UNCOMMON', color: 0x5ad07a, order: 1 },
  rare: { label: 'RARE', color: 0x4aa3ff, order: 2 },
  epic: { label: 'EPIC', color: 0xb479ff, order: 3 },
  wonder: { label: 'WONDER', color: 0x9fd93a, order: 4 },
};

export const CATEGORIES = [
  { id: 'pistol', label: 'PISTOLS' },
  { id: 'smg', label: 'SUBMACHINE GUNS' },
  { id: 'ar', label: 'ASSAULT RIFLES' },
  { id: 'shotgun', label: 'SHOTGUNS' },
  { id: 'lmg', label: 'LIGHT MACHINE GUNS' },
  { id: 'sniper', label: 'SNIPER RIFLES' },
  { id: 'wonder', label: 'WONDER WEAPONS' },
];

const W = (def) => ({
  fireMode: 'auto',
  pellets: 1,
  headMult: 2.6,
  limbMult: 0.72,
  torsoMult: 1.0,
  range: 90,
  falloff: 0.55,
  penetration: 1,
  recoil: { v: 0.9, h: 0.4 },
  spread: { hip: 0.045, ads: 0.008, move: 0.03 },
  adsTime: 0.24,
  reloadTime: 2.0,
  emptyReloadTime: 2.6,
  moveScale: 1,
  boxWeight: 10,
  wallCost: 0,
  wallAmmoCost: 0,
  sound: 'medium',
  tint: 0x2e3337,
  accent: 0x53595e,
  shape: {},
  ...def,
});

export const WEAPONS = {
  // ------------------------------------------------------------- pistols
  sidearm: W({
    id: 'sidearm', name: 'SIDEARM MK1', category: 'pistol', rarity: 'common',
    damage: 55, rpm: 400, fireMode: 'semi', mag: 8, reserve: 80,
    reloadTime: 1.5, emptyReloadTime: 1.9, adsTime: 0.18,
    recoil: { v: 1.5, h: 0.5 }, spread: { hip: 0.038, ads: 0.004, move: 0.026 },
    wallCost: 0, wallAmmoCost: 250, boxWeight: 0, sound: 'light',
    shape: { len: 0.5, recv: [0.055, 0.11, 0.2], barrel: 0.14, stock: false, magLen: 0.14, bore: 0.02 },
    blurb: 'Standard-issue service pistol. Reliable, unremarkable, and free.',
  }),
  magnus: W({
    id: 'magnus', name: 'MAGNUS .44', category: 'pistol', rarity: 'uncommon',
    damage: 210, rpm: 190, fireMode: 'semi', mag: 6, reserve: 48,
    reloadTime: 2.4, emptyReloadTime: 2.9, adsTime: 0.22,
    recoil: { v: 4.2, h: 1.4 }, spread: { hip: 0.055, ads: 0.003, move: 0.04 },
    headMult: 3.0, boxWeight: 12, sound: 'heavy', tint: 0x3b3f42, accent: 0x8d7a4a,
    shape: { len: 0.55, recv: [0.06, 0.125, 0.24], barrel: 0.2, stock: false, mag: false, drum: true, bore: 0.026 },
    blurb: 'Hand cannon. Six chances to remove a head from its shoulders.',
  }),
  hornet: W({
    id: 'hornet', name: 'HORNET AP', category: 'pistol', rarity: 'uncommon',
    damage: 62, rpm: 900, mag: 30, reserve: 210,
    reloadTime: 1.8, emptyReloadTime: 2.2, adsTime: 0.2,
    recoil: { v: 1.5, h: 1.1 }, spread: { hip: 0.07, ads: 0.02, move: 0.05 },
    wallCost: 900, wallAmmoCost: 450, boxWeight: 10, sound: 'light', tint: 0x33383c,
    shape: { len: 0.55, recv: [0.058, 0.11, 0.24], barrel: 0.16, stock: false, magLen: 0.24 },
    blurb: 'Full-auto machine pistol. Empties fast, hits like a swarm.',
  }),

  // ------------------------------------------------------------- SMGs
  wasp9: W({
    id: 'wasp9', name: 'WASP-9', category: 'smg', rarity: 'common',
    damage: 78, rpm: 820, mag: 32, reserve: 256,
    reloadTime: 1.9, emptyReloadTime: 2.5, adsTime: 0.2,
    recoil: { v: 1.1, h: 0.6 }, spread: { hip: 0.05, ads: 0.012, move: 0.035 },
    wallCost: 1000, wallAmmoCost: 500, boxWeight: 12, sound: 'light', tint: 0x2b3033,
    shape: { len: 0.72, recv: [0.075, 0.12, 0.3], barrel: 0.2, stockLen: 0.16, magLen: 0.22 },
    blurb: 'Compact 9mm. The dependable answer to the first ten rounds.',
  }),
  ripcord: W({
    id: 'ripcord', name: 'RIPCORD', category: 'smg', rarity: 'rare',
    damage: 92, rpm: 1000, mag: 40, reserve: 320,
    reloadTime: 2.1, emptyReloadTime: 2.7, adsTime: 0.22,
    recoil: { v: 1.3, h: 0.9 }, spread: { hip: 0.055, ads: 0.014, move: 0.04 },
    wallCost: 1300, wallAmmoCost: 600, boxWeight: 11, sound: 'light',
    tint: 0x363b3e, accent: 0x9a6b3a,
    shape: { len: 0.78, recv: [0.08, 0.13, 0.32], barrel: 0.22, stockLen: 0.2, magLen: 0.26, optic: true },
    blurb: 'Overclocked bolt group. Absurd rate of fire, appetite to match.',
  }),
  sleet: W({
    id: 'sleet', name: 'SLEET SMG', category: 'smg', rarity: 'epic',
    damage: 105, rpm: 900, mag: 45, reserve: 315,
    reloadTime: 2.0, emptyReloadTime: 2.6, adsTime: 0.19, penetration: 2,
    recoil: { v: 0.9, h: 0.5 }, spread: { hip: 0.042, ads: 0.008, move: 0.03 },
    boxWeight: 8, sound: 'medium', tint: 0x2a3440, accent: 0x6fa8c9,
    shape: { len: 0.8, recv: [0.082, 0.13, 0.34], barrel: 0.24, stockLen: 0.2, magLen: 0.28, optic: true },
    blurb: 'Cryo-treated barrel shroud. Stays accurate long past reason.',
  }),

  // ------------------------------------------------------------- rifles
  kestrel: W({
    id: 'kestrel', name: 'KESTREL AR', category: 'ar', rarity: 'uncommon',
    damage: 130, rpm: 700, mag: 30, reserve: 270,
    reloadTime: 2.2, emptyReloadTime: 2.9, adsTime: 0.26, penetration: 2,
    recoil: { v: 1.6, h: 0.6 }, spread: { hip: 0.06, ads: 0.005, move: 0.04 },
    wallCost: 1200, wallAmmoCost: 600, boxWeight: 12, sound: 'medium', tint: 0x33393c,
    shape: { len: 0.95, recv: [0.085, 0.135, 0.4], barrel: 0.3, stockLen: 0.24, magLen: 0.24 },
    blurb: 'Service rifle. Punches through a shambling queue without complaint.',
  }),
  vulture: W({
    id: 'vulture', name: 'VULTURE 7', category: 'ar', rarity: 'rare',
    damage: 155, rpm: 620, mag: 35, reserve: 280,
    reloadTime: 2.4, emptyReloadTime: 3.0, adsTime: 0.28, penetration: 3,
    recoil: { v: 2.0, h: 0.7 }, spread: { hip: 0.062, ads: 0.004, move: 0.045 },
    wallCost: 1400, wallAmmoCost: 700, boxWeight: 10, sound: 'medium',
    tint: 0x3a3a32, accent: 0x7a6f4a,
    shape: { len: 1.0, recv: [0.088, 0.14, 0.42], barrel: 0.34, stockLen: 0.24, magLen: 0.26, optic: true },
    blurb: 'Heavy-calibre battle rifle. Slower, meaner, worth the recoil.',
  }),
  talon: W({
    id: 'talon', name: 'TALON BURST', category: 'ar', rarity: 'epic',
    damage: 175, rpm: 900, fireMode: 'burst', burst: 3, burstDelay: 0.24,
    mag: 36, reserve: 288, reloadTime: 2.3, emptyReloadTime: 2.9, adsTime: 0.25, penetration: 3,
    recoil: { v: 1.8, h: 0.5 }, spread: { hip: 0.05, ads: 0.003, move: 0.04 },
    boxWeight: 8, sound: 'medium', tint: 0x2d3540, accent: 0x9fb0c4,
    shape: { len: 1.0, recv: [0.088, 0.14, 0.42], barrel: 0.34, stockLen: 0.26, magLen: 0.26, optic: true },
    blurb: 'Three-round burst with a locked trigger group. Surgical at range.',
  }),

  // ------------------------------------------------------------- shotguns
  breaker: W({
    id: 'breaker', name: 'BREAKER 12', category: 'shotgun', rarity: 'uncommon',
    damage: 60, pellets: 8, rpm: 90, fireMode: 'semi', mag: 6, reserve: 54,
    reloadTime: 0.55, emptyReloadTime: 0.55, shellReload: true, adsTime: 0.3,
    range: 22, falloff: 0.85, recoil: { v: 4.5, h: 1.2 },
    spread: { hip: 0.11, ads: 0.075, move: 0.13 }, penetration: 2,
    wallCost: 1200, wallAmmoCost: 600, boxWeight: 11, sound: 'shotgun',
    tint: 0x3a2f26, accent: 0x6a5236,
    shape: { len: 0.92, recv: [0.09, 0.14, 0.38], barrel: 0.36, stockLen: 0.24, mag: false, tube: true, bore: 0.038 },
    blurb: 'Pump-action crowd editor. Devastating inside four metres.',
  }),
  sawtooth: W({
    id: 'sawtooth', name: 'SAWTOOTH AA', category: 'shotgun', rarity: 'epic',
    damage: 52, pellets: 10, rpm: 240, mag: 12, reserve: 84,
    reloadTime: 3.0, emptyReloadTime: 3.6, adsTime: 0.32,
    range: 26, falloff: 0.8, recoil: { v: 3.2, h: 1.4 },
    spread: { hip: 0.1, ads: 0.07, move: 0.12 }, penetration: 3,
    boxWeight: 8, sound: 'shotgun', tint: 0x30363a, accent: 0x8a3f2a,
    shape: { len: 0.95, recv: [0.095, 0.15, 0.4], barrel: 0.34, stockLen: 0.24, magLen: 0.3, drum: true, bore: 0.038 },
    blurb: 'Fully automatic scattergun. Feed it points, it feeds you space.',
  }),

  // ------------------------------------------------------------- LMGs
  anvil: W({
    id: 'anvil', name: 'ANVIL LMG', category: 'lmg', rarity: 'rare',
    damage: 145, rpm: 640, mag: 100, reserve: 400,
    reloadTime: 4.2, emptyReloadTime: 5.0, adsTime: 0.42, penetration: 4,
    recoil: { v: 2.2, h: 1.0 }, spread: { hip: 0.09, ads: 0.008, move: 0.07 },
    moveScale: 0.88, wallCost: 1800, wallAmmoCost: 800, boxWeight: 9, sound: 'heavy',
    tint: 0x2f3336, accent: 0x555b5e,
    shape: { len: 1.15, recv: [0.1, 0.16, 0.48], barrel: 0.4, stockLen: 0.26, magLen: 0.3, drum: true },
    blurb: 'Hundred-round belt. The reload is a commitment; the burst is worth it.',
  }),
  grinder: W({
    id: 'grinder', name: 'GRINDER', category: 'lmg', rarity: 'epic',
    damage: 165, rpm: 780, mag: 125, reserve: 500,
    reloadTime: 4.6, emptyReloadTime: 5.4, adsTime: 0.46, penetration: 5,
    recoil: { v: 2.4, h: 1.2 }, spread: { hip: 0.095, ads: 0.01, move: 0.075 },
    moveScale: 0.84, boxWeight: 7, sound: 'heavy', tint: 0x35302c, accent: 0x8a6a3a,
    shape: { len: 1.2, recv: [0.105, 0.17, 0.5], barrel: 0.42, stockLen: 0.26, magLen: 0.32, drum: true },
    blurb: 'Rotary-fed monster. Holds a corridor by itself.',
  }),

  // ------------------------------------------------------------- snipers
  longshot: W({
    id: 'longshot', name: 'LONGSHOT', category: 'sniper', rarity: 'rare',
    damage: 620, rpm: 55, fireMode: 'semi', mag: 5, reserve: 45,
    reloadTime: 2.9, emptyReloadTime: 3.5, adsTime: 0.44, penetration: 6,
    headMult: 3.2, range: 200, falloff: 0.1,
    recoil: { v: 6.0, h: 1.0 }, spread: { hip: 0.12, ads: 0.0008, move: 0.1 },
    moveScale: 0.92, wallCost: 1600, wallAmmoCost: 700, boxWeight: 9, sound: 'heavy',
    tint: 0x2c3130, accent: 0x6a7a68,
    shape: { len: 1.25, recv: [0.085, 0.14, 0.5], barrel: 0.48, stockLen: 0.3, magLen: 0.18, optic: true, bore: 0.024 },
    blurb: 'Bolt-action anti-materiel rifle. One round, one corridor.',
  }),
  spire: W({
    id: 'spire', name: 'SPIRE RAILGUN', category: 'sniper', rarity: 'epic',
    damage: 900, rpm: 40, fireMode: 'semi', mag: 4, reserve: 32,
    reloadTime: 3.2, emptyReloadTime: 3.8, adsTime: 0.5, penetration: 10,
    headMult: 3.4, range: 240, falloff: 0.05,
    recoil: { v: 7.0, h: 0.6 }, spread: { hip: 0.14, ads: 0.0004, move: 0.11 },
    moveScale: 0.9, boxWeight: 6, sound: 'energy', tint: 0x24303a, accent: 0x66d9ff,
    shape: { len: 1.3, recv: [0.09, 0.15, 0.52], barrel: 0.5, stockLen: 0.3, magLen: 0.18, optic: true, cell: true },
    blurb: 'Magnetically accelerated slug. Passes through everything in the hall.',
  }),

  // ------------------------------------------------------------- wonder
  arclance: W({
    id: 'arclance', name: 'ARC LANCE', category: 'wonder', rarity: 'wonder',
    damage: 420, rpm: 160, fireMode: 'semi', mag: 12, reserve: 60,
    reloadTime: 3.4, emptyReloadTime: 3.9, adsTime: 0.3, penetration: 1,
    headMult: 2.0, range: 60, falloff: 0.2,
    recoil: { v: 2.2, h: 0.4 }, spread: { hip: 0.03, ads: 0.002, move: 0.02 },
    special: 'chain', chain: { targets: 5, radius: 6.5, falloff: 0.7 },
    boxWeight: 3, sound: 'energy', tint: 0x1e2a30, accent: 0x66d9ff,
    shape: { len: 1.05, recv: [0.1, 0.16, 0.44], barrel: 0.36, stockLen: 0.22, mag: false, optic: true, cell: true, bore: 0.05 },
    blurb: 'Prototype capacitor lance. Arcs between everything with a pulse.',
  }),
  rotcannon: W({
    id: 'rotcannon', name: 'BLIGHT CANNON', category: 'wonder', rarity: 'wonder',
    damage: 260, rpm: 90, fireMode: 'semi', mag: 8, reserve: 40,
    reloadTime: 3.6, emptyReloadTime: 4.2, adsTime: 0.34, penetration: 1,
    headMult: 1.6, range: 45, falloff: 0.3,
    recoil: { v: 3.4, h: 0.5 }, spread: { hip: 0.04, ads: 0.006, move: 0.03 },
    special: 'blast', blast: { radius: 4.4, damage: 380 },
    boxWeight: 3, sound: 'energy', tint: 0x232a1e, accent: 0x9fd93a,
    shape: { len: 1.0, recv: [0.11, 0.17, 0.42], barrel: 0.3, stockLen: 0.22, magLen: 0.24, cell: true, bore: 0.07 },
    blurb: 'Fires an unstable spore charge. Do not discharge at arm’s length.',
  }),
};

/** Melee is modelled as a weapon so upgrades and stats work uniformly. */
export const KNIFE = {
  id: 'knife', name: 'COMBAT KNIFE', category: 'melee', rarity: 'common',
  damage: 150, range: 2.5, rpm: 110, headMult: 1.0, blurb: 'Silent, free, and always in reach.',
};

export const WEAPON_LIST = Object.values(WEAPONS);

export function weaponsByCategory() {
  const out = new Map();
  for (const c of CATEGORIES) out.set(c.id, []);
  for (const w of WEAPON_LIST) out.get(w.category)?.push(w);
  for (const list of out.values()) {
    list.sort((a, b) => RARITY[a.rarity].order - RARITY[b.rarity].order || a.name.localeCompare(b.name));
  }
  return out;
}

/** Pool used by the mystery box. */
export function boxPool() {
  return WEAPON_LIST.filter((w) => w.boxWeight > 0);
}

// ------------------------------------------------------------------ upgrades
export const UPGRADE_LEVELS = [
  { level: 1, name: 'REFIT I', cost: 5000, damage: 2.4, mag: 1.5, reserve: 1.6, accent: 0x9fd93a },
  { level: 2, name: 'REFIT II', cost: 3500, damage: 1.6, mag: 1.2, reserve: 1.25, accent: 0x66d9ff },
  { level: 3, name: 'REFIT III', cost: 4500, damage: 1.5, mag: 1.15, reserve: 1.2, accent: 0xff8a3d },
];

const UPGRADE_PREFIX = ['', 'RECLAIMED ', 'ASCENDANT ', 'APEX '];

/** Returns a derived definition for `def` upgraded to `level`. */
export function upgradedDef(def, level) {
  if (!level) return def;
  const out = { ...def, shape: { ...def.shape }, recoil: { ...def.recoil }, spread: { ...def.spread } };
  let dmg = 1, mag = 1, res = 1, accent = def.accent;
  for (let i = 0; i < level && i < UPGRADE_LEVELS.length; i++) {
    dmg *= UPGRADE_LEVELS[i].damage;
    mag *= UPGRADE_LEVELS[i].mag;
    res *= UPGRADE_LEVELS[i].reserve;
    accent = UPGRADE_LEVELS[i].accent;
  }
  out.damage = Math.round(def.damage * dmg);
  out.mag = Math.round(def.mag * mag);
  out.reserve = Math.round(def.reserve * res);
  out.recoil = { v: def.recoil.v * 0.82, h: def.recoil.h * 0.82 };
  out.reloadTime = def.reloadTime * 0.88;
  out.emptyReloadTime = def.emptyReloadTime * 0.88;
  out.penetration = def.penetration + level;
  out.accent = accent;
  out.upgradeLevel = level;
  out.name = UPGRADE_PREFIX[Math.min(level, 3)] + def.name;
  out.shape.cell = true;
  // Refits add an area effect once the weapon is fully worked over.
  if (level >= 2 && !def.special) out.special = 'burn';
  if (level >= 3 && out.special === 'burn') { out.special = 'blast'; out.blast = { radius: 3.0, damage: out.damage * 0.6 }; }
  return out;
}

export function upgradeCost(level) {
  return UPGRADE_LEVELS[Math.min(level, UPGRADE_LEVELS.length - 1)].cost;
}
