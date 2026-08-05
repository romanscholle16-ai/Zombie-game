/**
 * "BLACKSITE 7" — original survival map layout.
 *
 * The map is described declaratively so MapBuilder can generate geometry,
 * colliders and the navigation grid from one source of truth. Adding a new
 * map means adding another file shaped like this one.
 */

export const MAP = {
  id: 'blacksite7',
  name: 'BLACKSITE 7',
  tagline: 'Decommissioned research annex',
  ambientColor: 0x1b2430,
  fogColor: 0x0a0f12,

  /** Rooms are axis-aligned boxes. `slope` makes a ramp between two floor heights. */
  zones: [
    { id: 'atrium', name: 'ARRIVALS ATRIUM', x0: -12, x1: 12, z0: -12, z1: 12, y: 0, h: 5.2, style: 'concrete', unlocked: true },
    { id: 'n_hall', name: 'NORTH CORRIDOR', x0: -4, x1: 4, z0: -18, z1: -12, y: 0, h: 3.6, style: 'metal', needs: 'doorA' },
    { id: 'lab', name: 'BIOLOGY LAB', x0: -16, x1: 16, z0: -44, z1: -18, y: 0, h: 5.6, style: 'tile', needs: 'doorA' },
    { id: 'e_hall', name: 'SERVICE CORRIDOR', x0: 12, x1: 20, z0: -4, z1: 4, y: 0, h: 3.6, style: 'metal', needs: 'doorB' },
    { id: 'power', name: 'POWER STATION', x0: 20, x1: 44, z0: -14, z1: 12, y: 0, h: 6.4, style: 'metal', needs: 'doorB' },
    { id: 'w_ramp', name: 'DESCENT RAMP', x0: -28, x1: -16, z0: -34, z1: -26, y: 0, h: 4.2, style: 'concrete', needs: 'doorC',
      slope: { axis: 'x', from: -16, to: -28, yFrom: 0, yTo: -4 } },
    { id: 'under', name: 'UNDERGROUND FACILITY', x0: -48, x1: -28, z0: -40, z1: -14, y: -4, h: 5.4, style: 'dirt', needs: 'doorC' },
    { id: 'pap_hall', name: 'SEALED PASSAGE', x0: -42, x1: -36, z0: -48, z1: -40, y: -4, h: 3.4, style: 'metal', needs: 'doorPaP' },
    { id: 'pap', name: 'REFIT CHAMBER', x0: -52, x1: -32, z0: -64, z1: -48, y: -4, h: 6.0, style: 'metal', needs: 'doorPaP' },
  ],

  /** Wall cut-outs. Any wall segment overlapping one of these is not built. */
  openings: [
    { x0: -4, x1: 4, z0: -13, z1: -11 },      // atrium <-> north corridor
    { x0: -4, x1: 4, z0: -19, z1: -17 },      // north corridor <-> lab
    { x0: 11, x1: 13, z0: -4, z1: 4 },        // atrium <-> service corridor
    { x0: 19, x1: 21, z0: -4, z1: 4 },        // service corridor <-> power
    { x0: -17, x1: -15, z0: -34, z1: -26 },   // lab <-> ramp
    { x0: -29, x1: -27, z0: -34, z1: -26 },   // ramp <-> underground
    { x0: -42, x1: -36, z0: -41, z1: -39 },   // underground <-> sealed passage
    { x0: -42, x1: -36, z0: -49, z1: -47 },   // sealed passage <-> refit chamber
  ],

  /** Purchasable doors. `power` doors open for free once the grid is live. */
  doors: [
    { id: 'doorA', name: 'BLAST DOOR A', cost: 750, x0: -4, x1: 4, z0: -12.45, z1: -11.55, opens: ['n_hall', 'lab'], zone: 'atrium' },
    { id: 'doorB', name: 'BLAST DOOR B', cost: 1000, x0: 11.55, x1: 12.45, z0: -4, z1: 4, opens: ['e_hall', 'power'], zone: 'atrium' },
    { id: 'doorC', name: 'BLAST DOOR C', cost: 1250, x0: -16.45, x1: -15.55, z0: -34, z1: -26, opens: ['w_ramp', 'under'], zone: 'lab' },
    { id: 'doorPaP', name: 'REFIT SEAL', cost: 0, requiresPower: true, x0: -42, x1: -36, z0: -48.45, z1: -47.55, opens: ['pap_hall', 'pap'], zone: 'under' },
  ],

  /**
   * Barricaded windows. `n` is the outward normal. Zombies spawn in the alcove
   * behind each window, tear the boards down, then vault the sill.
   */
  windows: [
    { id: 'w_atr_s1', zone: 'atrium', x: -6, z: 12, nx: 0, nz: 1, alcove: [-8.6, 12, -3.4, 18.5] },
    { id: 'w_atr_s2', zone: 'atrium', x: 6, z: 12, nx: 0, nz: 1, alcove: [3.4, 12, 8.6, 18.5] },
    { id: 'w_atr_w1', zone: 'atrium', x: -12, z: -6, nx: -1, nz: 0, alcove: [-18.5, -8.6, -12, -3.4] },
    { id: 'w_atr_w2', zone: 'atrium', x: -12, z: 6, nx: -1, nz: 0, alcove: [-18.5, 3.4, -12, 8.6] },
    { id: 'w_atr_e', zone: 'atrium', x: 12, z: -9, nx: 1, nz: 0, alcove: [12, -11.4, 18.5, -6.6] },
    { id: 'w_lab_n1', zone: 'lab', x: -8, z: -44, nx: 0, nz: -1, alcove: [-10.6, -50.5, -5.4, -44] },
    { id: 'w_lab_n2', zone: 'lab', x: 8, z: -44, nx: 0, nz: -1, alcove: [5.4, -50.5, 10.6, -44] },
    { id: 'w_lab_w', zone: 'lab', x: -16, z: -23, nx: -1, nz: 0, alcove: [-22.5, -25.6, -16, -20.4] },
    { id: 'w_lab_e', zone: 'lab', x: 16, z: -30, nx: 1, nz: 0, alcove: [16, -32.6, 22.5, -27.4] },
    { id: 'w_pow_n', zone: 'power', x: 32, z: -14, nx: 0, nz: -1, alcove: [29.4, -20.5, 34.6, -14] },
    { id: 'w_pow_e', zone: 'power', x: 44, z: 0, nx: 1, nz: 0, alcove: [44, -2.6, 50.5, 2.6] },
    { id: 'w_pow_s', zone: 'power', x: 32, z: 12, nx: 0, nz: 1, alcove: [29.4, 12, 34.6, 18.5] },
    { id: 'w_und_w', zone: 'under', x: -48, z: -28, nx: -1, nz: 0, alcove: [-54.5, -30.6, -48, -25.4] },
    { id: 'w_und_n', zone: 'under', x: -33, z: -40, nx: 0, nz: -1, alcove: [-35.6, -46.5, -30.4, -40] },
    { id: 'w_und_s', zone: 'under', x: -38, z: -14, nx: 0, nz: 1, alcove: [-40.6, -14, -35.4, -8.5] },
  ],

  /** Wall-mounted weapon purchase points. `rot` faces the plaque into the room. */
  wallBuys: [
    { id: 'wb_pistol', weapon: 'sidearm', zone: 'atrium', x: 0, z: 11.7, rot: Math.PI, y: 0 },
    { id: 'wb_smg', weapon: 'wasp9', zone: 'atrium', x: -11.7, z: 0, rot: Math.PI / 2, y: 0 },
    { id: 'wb_shotgun', weapon: 'breaker', zone: 'lab', x: 15.7, z: -22, rot: -Math.PI / 2, y: 0 },
    { id: 'wb_ar', weapon: 'kestrel', zone: 'lab', x: -15.7, z: -38, rot: Math.PI / 2, y: 0 },
    { id: 'wb_smg2', weapon: 'ripcord', zone: 'power', x: 43.7, z: -8, rot: -Math.PI / 2, y: 0 },
    { id: 'wb_lmg', weapon: 'anvil', zone: 'power', x: 26, z: 11.7, rot: Math.PI, y: 0 },
    { id: 'wb_sniper', weapon: 'longshot', zone: 'under', x: -47.7, z: -20, rot: Math.PI / 2, y: -4 },
    { id: 'wb_ar2', weapon: 'vulture', zone: 'under', x: -32, z: -39.7, rot: 0, y: -4 },
  ],

  /** Armour plating dispensers. */
  armorStations: [
    { id: 'arm_atrium', zone: 'atrium', x: 11.7, z: 6, rot: -Math.PI / 2, y: 0, cost: 500 },
    { id: 'arm_power', zone: 'power', x: 20.3, z: -12, rot: Math.PI / 2, y: 0, cost: 500 },
  ],

  /** Perk machines. All require the power grid. */
  perkSpots: [
    { perk: 'vital', zone: 'lab', x: -13, z: -21, rot: Math.PI / 2, y: 0 },
    { perk: 'rapid', zone: 'power', x: 40, z: 9, rot: Math.PI, y: 0 },
    { perk: 'swift', zone: 'atrium', x: 9.5, z: 9.5, rot: -Math.PI * 0.75, y: 0 },
    { perk: 'medic', zone: 'under', x: -45, z: -17, rot: Math.PI / 2, y: -4 },
    { perk: 'endure', zone: 'lab', x: 13, z: -41, rot: -Math.PI / 2, y: 0 },
  ],

  /** Mystery box rotates between these anchors. */
  boxSpots: [
    { id: 'bs_atrium', zone: 'atrium', x: -7, z: -7, y: 0, rot: 0.6 },
    { id: 'bs_lab', zone: 'lab', x: 9, z: -33, y: 0, rot: -0.4 },
    { id: 'bs_power', zone: 'power', x: 25, z: 5, y: 0, rot: 1.2 },
    { id: 'bs_under', zone: 'under', x: -41, z: -22, y: -4, rot: -1.1 },
    { id: 'bs_pap', zone: 'pap', x: -36, z: -60, y: -4, rot: 2.4 },
  ],

  /** Pack-a-Punch equivalent. */
  upgradeMachine: { zone: 'pap', x: -42, z: -57, y: -4, rot: 0 },

  /** Power switch. */
  powerSwitch: { zone: 'power', x: 42.4, z: -11, y: 0, rot: -Math.PI / 2 },

  traps: [
    { id: 'trap_e', kind: 'electric', zone: 'e_hall', x: 16, z: 0, y: 0, cost: 1000, rot: 0,
      area: { x0: 12.5, x1: 19.5, z0: -3.5, z1: 3.5 }, panel: { x: 19.6, z: 3.4, rot: -Math.PI / 2 } },
    { id: 'trap_f', kind: 'fire', zone: 'lab', x: 0, z: -31, y: 0, cost: 1250, rot: 0,
      area: { x0: -5, x1: 5, z0: -36, z1: -26 }, panel: { x: -6.5, z: -26.5, rot: 0 } },
    { id: 'trap_t', kind: 'turret', zone: 'under', x: -36, z: -24, y: -4, cost: 1500, rot: Math.PI,
      area: { x0: -47, x1: -29, z0: -34, z1: -15 }, panel: { x: -34.5, z: -24, rot: -Math.PI / 2 } },
  ],

  /** Player start. */
  spawn: { x: 0, y: 0, z: 4, yaw: Math.PI },

  /** Static light anchors — intensity is modulated by the power system. */
  lights: [
    { x: 0, y: 3.9, z: 0, zone: 'atrium', color: 0xffd9a8, i: 1.0, d: 26 },
    { x: -7, y: 3.6, z: 7, zone: 'atrium', color: 0xbfd8ff, i: 0.55, d: 16 },
    { x: 7, y: 3.6, z: -7, zone: 'atrium', color: 0xbfd8ff, i: 0.55, d: 16 },
    { x: 0, y: 2.8, z: -15, zone: 'n_hall', color: 0x9fd93a, i: 0.5, d: 12 },
    { x: -8, y: 4.2, z: -24, zone: 'lab', color: 0xd8f0ff, i: 0.9, d: 24 },
    { x: 8, y: 4.2, z: -36, zone: 'lab', color: 0xd8f0ff, i: 0.9, d: 24 },
    { x: 16, y: 2.8, z: 0, zone: 'e_hall', color: 0xffb066, i: 0.5, d: 12 },
    { x: 30, y: 4.8, z: -4, zone: 'power', color: 0xffc98a, i: 1.0, d: 28 },
    { x: 38, y: 4.8, z: 6, zone: 'power', color: 0xffc98a, i: 0.8, d: 22 },
    { x: -22, y: -0.4, z: -30, zone: 'w_ramp', color: 0x88ff9c, i: 0.5, d: 14 },
    { x: -38, y: -0.6, z: -22, zone: 'under', color: 0x7fe0a0, i: 0.85, d: 26 },
    { x: -44, y: -0.6, z: -34, zone: 'under', color: 0x7fe0a0, i: 0.7, d: 22 },
    { x: -42, y: -0.8, z: -56, zone: 'pap', color: 0xff9c5a, i: 1.1, d: 26 },
  ],

  /** Decorative clutter seeds per zone (count, kinds). */
  clutter: [
    { zone: 'atrium', seed: 11, crates: 7, barrels: 4, shelves: 2, debris: 12 },
    { zone: 'lab', seed: 23, crates: 4, barrels: 3, tables: 6, shelves: 4, debris: 14 },
    { zone: 'power', seed: 37, crates: 6, barrels: 8, generators: 3, shelves: 2, debris: 10 },
    { zone: 'under', seed: 53, crates: 8, barrels: 6, shelves: 3, debris: 18 },
    { zone: 'pap', seed: 71, crates: 3, barrels: 4, debris: 8 },
    { zone: 'n_hall', seed: 83, crates: 1, debris: 4 },
    { zone: 'e_hall', seed: 97, barrels: 2, debris: 4 },
    { zone: 'w_ramp', seed: 101, debris: 6 },
  ],
};

export function zoneById(id) {
  return MAP.zones.find((z) => z.id === id);
}

/** Zones that become playable once `doorId` is bought. */
export function zonesForDoor(doorId) {
  return MAP.zones.filter((z) => z.needs === doorId);
}

export function mapBounds() {
  let x0 = Infinity, x1 = -Infinity, z0 = Infinity, z1 = -Infinity;
  const consider = (a, b, c, d) => {
    x0 = Math.min(x0, a); x1 = Math.max(x1, c);
    z0 = Math.min(z0, b); z1 = Math.max(z1, d);
  };
  for (const z of MAP.zones) consider(z.x0, z.z0, z.x1, z.z1);
  for (const w of MAP.windows) consider(w.alcove[0], w.alcove[1], w.alcove[2], w.alcove[3]);
  return { x0: x0 - 4, x1: x1 + 4, z0: z0 - 4, z1: z1 + 4 };
}
