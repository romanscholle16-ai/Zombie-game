/**
 * Per-weapon gunshot voices.
 *
 * Every sound in this game is synthesised from oscillators and shaped noise at
 * runtime — nothing is sampled or imported. A shot is built from five layers,
 * and giving each weapon its own numbers is what stops seventeen guns from
 * sounding like one gun at three volumes:
 *
 *   mech   the action working — sear, bolt, slide. A short bright transient.
 *   crack  the supersonic snap of the bullet. High, fast, sweeps downward.
 *   body   the pressure pulse. A pitched thump that gives the shot its weight.
 *   ring   a resonant peak: barrel harmonic, receiver clang, capacitor whine.
 *   tail   room reflections. Long in a concrete hall, short under a can.
 *
 * Fields are all optional; DEFAULT_VOICE fills the gaps.
 */

export const DEFAULT_VOICE = {
  mech: 0.22, mechF: 3200, mechDur: 0.035,
  crackHi: 3000, crackLo: 180, crackDur: 0.2, crackQ: 0.9, crackLevel: 0.9,
  body: 110, bodyMul: 2.2, bodyDur: 0.18, bodyType: 'triangle', punch: 0.8,
  ring: 0, ringF: 900, ringQ: 9, ringDur: 0.14,
  tail: 0.45, tailDur: 0.3, tailF: 700,
  detune: 0.03,
};

/** Keyed by weapon id. Anything missing falls back to the class default. */
export const VOICES = {
  // ---------------------------------------------------------------- pistols
  sidearm: {
    mech: 0.34, mechF: 4200, crackHi: 4200, crackLo: 300, crackDur: 0.13, crackQ: 1.2,
    body: 165, bodyDur: 0.1, punch: 0.5, ring: 0.16, ringF: 1750, tail: 0.32, tailDur: 0.2,
  },
  magnus: {
    // Big revolver: no slide noise, enormous body, long bare-muzzle tail.
    mech: 0.1, mechF: 2400, crackHi: 3400, crackLo: 130, crackDur: 0.3, crackQ: 0.7, crackLevel: 1.15,
    body: 74, bodyMul: 3.0, bodyDur: 0.34, punch: 1.45, ring: 0.22, ringF: 520, ringQ: 6,
    tail: 0.85, tailDur: 0.72, tailF: 420,
  },
  hornet: {
    // Machine pistol: fast, tinny, almost no low end.
    mech: 0.4, mechF: 5200, mechDur: 0.025, crackHi: 5000, crackLo: 420, crackDur: 0.09, crackQ: 1.6,
    body: 210, bodyDur: 0.07, punch: 0.36, ring: 0.24, ringF: 2400, tail: 0.24, tailDur: 0.14,
    detune: 0.06,
  },

  // ---------------------------------------------------------------- SMGs
  wasp9: {
    mech: 0.38, mechF: 4600, mechDur: 0.028, crackHi: 4400, crackLo: 340, crackDur: 0.1, crackQ: 1.4,
    body: 180, bodyDur: 0.08, punch: 0.44, ring: 0.18, ringF: 2050, tail: 0.3, tailDur: 0.17,
    detune: 0.05,
  },
  ripcord: {
    // Overclocked bolt: shorter, sharper, with a mechanical rattle on top.
    mech: 0.52, mechF: 5600, mechDur: 0.022, crackHi: 5200, crackLo: 400, crackDur: 0.075, crackQ: 1.9,
    body: 196, bodyDur: 0.06, punch: 0.4, ring: 0.3, ringF: 2750, ringDur: 0.09,
    tail: 0.26, tailDur: 0.13, detune: 0.08,
  },
  sleet: {
    // Suppressed-ish: the crack is choked and the tail is nearly gone.
    mech: 0.46, mechF: 3400, crackHi: 2600, crackLo: 300, crackDur: 0.11, crackQ: 3.4, crackLevel: 0.62,
    body: 150, bodyDur: 0.1, punch: 0.5, ring: 0.12, ringF: 1200, tail: 0.12, tailDur: 0.1, tailF: 400,
  },

  // ---------------------------------------------------------------- rifles
  kestrel: {
    mech: 0.28, mechF: 3800, crackHi: 3800, crackLo: 220, crackDur: 0.16, crackQ: 1.1,
    body: 128, bodyDur: 0.14, punch: 0.82, ring: 0.2, ringF: 1500, tail: 0.5, tailDur: 0.34,
  },
  vulture: {
    // Heavier calibre: lower everything, more shove.
    mech: 0.24, mechF: 3200, crackHi: 3200, crackLo: 170, crackDur: 0.21, crackQ: 0.95,
    body: 96, bodyMul: 2.6, bodyDur: 0.2, punch: 1.1, ring: 0.24, ringF: 1050, ringQ: 7,
    tail: 0.62, tailDur: 0.46, tailF: 560,
  },
  talon: {
    // Locked burst group: very tight, metallic, almost mechanical.
    mech: 0.44, mechF: 4800, mechDur: 0.02, crackHi: 4600, crackLo: 260, crackDur: 0.12, crackQ: 1.8,
    body: 140, bodyDur: 0.1, punch: 0.7, ring: 0.34, ringF: 2200, ringQ: 12, ringDur: 0.11,
    tail: 0.42, tailDur: 0.26,
  },

  // ---------------------------------------------------------------- shotguns
  breaker: {
    // Pump gun: a wide, low boom with a wall of tail behind it.
    mech: 0.3, mechF: 2200, mechDur: 0.06, crackHi: 2400, crackLo: 90, crackDur: 0.36, crackQ: 0.5,
    crackLevel: 1.2, body: 62, bodyMul: 3.2, bodyDur: 0.3, punch: 1.35, bodyType: 'sine',
    ring: 0.1, ringF: 400, tail: 0.9, tailDur: 0.68, tailF: 320,
  },
  sawtooth: {
    mech: 0.36, mechF: 3000, crackHi: 2800, crackLo: 110, crackDur: 0.26, crackQ: 0.6, crackLevel: 1.05,
    body: 74, bodyMul: 2.8, bodyDur: 0.22, punch: 1.1, ring: 0.18, ringF: 620,
    tail: 0.7, tailDur: 0.48, tailF: 380,
  },

  // ---------------------------------------------------------------- LMGs
  anvil: {
    // Belt-fed: heavy body, audible link ejection, cavernous tail.
    mech: 0.42, mechF: 2800, mechDur: 0.05, crackHi: 3000, crackLo: 150, crackDur: 0.2, crackQ: 0.85,
    body: 88, bodyMul: 2.7, bodyDur: 0.19, punch: 1.15, ring: 0.26, ringF: 860, ringQ: 6,
    tail: 0.7, tailDur: 0.52, tailF: 500,
  },
  grinder: {
    mech: 0.5, mechF: 3100, mechDur: 0.045, crackHi: 3300, crackLo: 160, crackDur: 0.17, crackQ: 0.95,
    body: 100, bodyMul: 2.5, bodyDur: 0.16, punch: 1.05, ring: 0.32, ringF: 1150, ringQ: 8,
    tail: 0.66, tailDur: 0.44,
  },

  // ---------------------------------------------------------------- snipers
  longshot: {
    // Anti-materiel rifle: brutal crack, muzzle brake slap, very long tail.
    mech: 0.2, mechF: 2600, mechDur: 0.07, crackHi: 5400, crackLo: 120, crackDur: 0.28, crackQ: 0.6,
    crackLevel: 1.3, body: 66, bodyMul: 3.4, bodyDur: 0.3, punch: 1.6,
    ring: 0.3, ringF: 700, ringQ: 5, ringDur: 0.24, tail: 1.0, tailDur: 0.95, tailF: 460,
  },
  spire: {
    // Railgun: a rising capacitor whine, then a hard electromagnetic snap.
    mech: 0.16, mechF: 6200, mechDur: 0.09, crackHi: 6400, crackLo: 260, crackDur: 0.22, crackQ: 1.5,
    body: 130, bodyMul: 4.2, bodyDur: 0.26, bodyType: 'sawtooth', punch: 1.3,
    ring: 0.5, ringF: 3200, ringQ: 16, ringDur: 0.4, tail: 0.8, tailDur: 0.66, tailF: 1400,
    charge: { from: 240, to: 2600, dur: 0.1, level: 0.3 },
  },

  // ---------------------------------------------------------------- wonder
  arclance: {
    // Capacitor discharge: almost no crack, a fat buzzing body, singing tail.
    mech: 0.12, mechF: 5000, crackHi: 5600, crackLo: 900, crackDur: 0.13, crackQ: 2.6, crackLevel: 0.5,
    body: 190, bodyMul: 3.6, bodyDur: 0.3, bodyType: 'sawtooth', punch: 1.0,
    ring: 0.55, ringF: 2400, ringQ: 18, ringDur: 0.5, tail: 0.75, tailDur: 0.6, tailF: 1800,
    charge: { from: 400, to: 1900, dur: 0.07, level: 0.26 },
  },
  rotcannon: {
    // Spore charge: wet, low, and unpleasant. Detuned pair for a sick warble.
    mech: 0.2, mechF: 1800, mechDur: 0.06, crackHi: 1600, crackLo: 70, crackDur: 0.34, crackQ: 0.45,
    crackLevel: 0.95, body: 54, bodyMul: 3.8, bodyDur: 0.38, bodyType: 'square', punch: 1.3,
    ring: 0.4, ringF: 300, ringQ: 4, ringDur: 0.42, tail: 0.85, tailDur: 0.8, tailF: 260,
    detune: 0.22,
  },
};

/**
 * Voice for a weapon definition. Refitted weapons keep their identity but gain
 * a brighter resonant edge, so an upgrade is audible as well as visible.
 */
const cache = new Map();

export function voiceFor(def) {
  // Called on every shot, up to sixteen times a second — build each weapon's
  // voice once and hand back the same object.
  const key = `${def.id}#${def.upgradeLevel ?? 0}`;
  const hit = cache.get(key);
  if (hit) return hit;
  const base = VOICES[def.id] ?? {};
  const v = { ...DEFAULT_VOICE, ...base };
  const lvl = def.upgradeLevel ?? 0;
  if (lvl > 0) {
    v.ring = Math.min(0.7, (v.ring || 0.15) + 0.12 * lvl);
    v.ringF = v.ringF * (1 + 0.12 * lvl);
    v.punch *= 1 + 0.1 * lvl;
    v.tail = Math.min(1.2, v.tail * (1 + 0.12 * lvl));
    v.tailDur *= 1 + 0.15 * lvl;
  }
  cache.set(key, v);
  return v;
}
