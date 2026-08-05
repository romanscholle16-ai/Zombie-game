import { Emitter } from './Util.js';

const KEY = 'rotgrid.save.v1';

const EMPTY = {
  lifetimeKills: 0,
  lifetimeHeadshots: 0,
  highestRound: 0,
  gamesPlayed: 0,
  totalTimeSurvived: 0,
  totalPointsEarned: 0,
  totalDowns: 0,
  totalRevives: 0,
  barriersRepaired: 0,
  boxSpins: 0,
  weaponStats: {},   // id -> { kills, headshots, shotsFired, shotsHit, damage }
  perkPurchases: {}, // id -> count
  history: [],       // last 10 runs
};

class SaveStore extends Emitter {
  constructor() {
    super();
    this.data = structuredClone(EMPTY);
    this.load();
  }

  load() {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) this.data = { ...structuredClone(EMPTY), ...JSON.parse(raw) };
    } catch { /* ignore */ }
  }

  save() {
    try { localStorage.setItem(KEY, JSON.stringify(this.data)); } catch { /* ignore */ }
  }

  weapon(id) {
    if (!this.data.weaponStats[id]) {
      this.data.weaponStats[id] = { kills: 0, headshots: 0, shotsFired: 0, shotsHit: 0, damage: 0 };
    }
    return this.data.weaponStats[id];
  }

  /** Fold a completed run into lifetime totals. */
  recordRun(run) {
    const d = this.data;
    d.gamesPlayed++;
    d.lifetimeKills += run.kills;
    d.lifetimeHeadshots += run.headshots;
    d.totalTimeSurvived += run.time;
    d.totalPointsEarned += run.pointsEarned;
    d.totalDowns += run.downs;
    d.totalRevives += run.revives;
    d.barriersRepaired += run.repairs;
    d.boxSpins += run.boxSpins;
    const newBest = run.round > d.highestRound;
    if (newBest) d.highestRound = run.round;

    for (const [id, s] of Object.entries(run.weaponStats || {})) {
      const w = this.weapon(id);
      w.kills += s.kills || 0;
      w.headshots += s.headshots || 0;
      w.shotsFired += s.shotsFired || 0;
      w.shotsHit += s.shotsHit || 0;
      w.damage += s.damage || 0;
    }
    for (const id of run.perksBought || []) {
      d.perkPurchases[id] = (d.perkPurchases[id] || 0) + 1;
    }

    d.history.unshift({
      date: Date.now(), round: run.round, kills: run.kills,
      headshots: run.headshots, time: run.time, points: run.pointsEarned,
    });
    d.history = d.history.slice(0, 10);

    this.save();
    this.emit('update', d);
    return newBest;
  }

  reset() {
    this.data = structuredClone(EMPTY);
    this.save();
    this.emit('update', this.data);
  }
}

export const SaveData = new SaveStore();
