import { Emitter, clamp, rand, pick, randInt } from '../core/Util.js';
import { Audio } from '../core/AudioEngine.js';
import { randomTypeForRound } from '../entities/Zombie.js';

export const POINTS = {
  hit: 10,
  kill: 60,
  headshotBonus: 40,      // headshot kill = kill + bonus
  meleeKill: 130,
  repair: 10,
  roundBase: 50,
  roundPerLevel: 10,
  trapKill: 50,
};

const EARLY_COUNTS = [6, 8, 13, 18, 24, 27, 28, 28, 29, 33];

export const PHASE = { INTRO: 0, SPAWNING: 1, CLEARING: 2, INTERMISSION: 3, OVER: 4 };

/**
 * Drives the wave loop: how many, how tough, how fast, and what kind.
 */
export class RoundManager extends Emitter {
  constructor(world, zombies) {
    super();
    this.world = world;
    this.zombies = zombies;
    this.round = 0;
    this.phase = PHASE.INTRO;
    this.timer = 0;
    this.toSpawn = 0;
    this.spawnedThisRound = 0;
    this.killsThisRound = 0;
    this.spawnT = 0;
    this.special = null;
    this.intermission = 9.0;
    this.firstRoundDelay = 5.0;
  }

  // ------------------------------------------------------------- curves
  countForRound(r) {
    if (r <= EARLY_COUNTS.length) return EARLY_COUNTS[r - 1];
    return Math.min(140, Math.round(33 + (r - 10) * 2.3));
  }

  healthForRound(r) {
    if (r <= 9) return 50 + 100 * r;
    return Math.round(950 * Math.pow(1.1, r - 9));
  }

  aggressionForRound(r) { return clamp((r - 1) / 20, 0, 1); }

  damageScaleForRound(r) { return 1 + clamp((r - 5) / 45, 0, 0.9); }

  spawnIntervalForRound(r) {
    return clamp(2.7 - r * 0.115, 0.34, 2.7);
  }

  maxConcurrent(r) {
    return Math.min(this.zombies.maxConcurrent, 8 + Math.floor(r * 1.6));
  }

  specialForRound(r) {
    if (r > 0 && r % 12 === 0) return 'boss';
    if (r > 0 && r % 8 === 0) return 'horde';
    return null;
  }

  // ------------------------------------------------------------- flow
  start() {
    this.round = 0;
    this.phase = PHASE.INTRO;
    this.timer = this.firstRoundDelay;
    this.killsThisRound = 0;
  }

  beginRound(r) {
    this.round = r;
    this.special = this.specialForRound(r);
    this.toSpawn = this.countForRound(r);
    if (this.special === 'boss') this.toSpawn = Math.round(this.toSpawn * 0.6);
    this.spawnedThisRound = 0;
    this.killsThisRound = 0;
    this.bossesToSpawn = this.special === 'boss' ? 1 + Math.floor(r / 24) : 0;
    this.spawnT = 1.0;
    this.phase = PHASE.SPAWNING;
    Audio.play('roundstart', { volume: 1 });
    this.emit('roundStart', r, this.special);
  }

  endRound() {
    this.phase = PHASE.INTERMISSION;
    this.timer = this.intermission;
    this.emit('roundEnd', this.round);
  }

  get remaining() {
    return this.toSpawn - this.spawnedThisRound + this.zombies.livingCount;
  }

  // ------------------------------------------------------------- spawning
  _spawnPoints() {
    const bars = this.world.activeBarricades();
    // Prefer windows nearer the player so pressure builds where they are.
    return bars.length ? bars : null;
  }

  _spawnOne(playerPos) {
    const bars = this._spawnPoints();
    if (!bars) return false;

    // Weight toward the player, with a floor so far windows still feed.
    const weighted = bars.map((b) => {
      const d = Math.hypot(b.x - playerPos.x, b.z - playerPos.z);
      const crowd = b.attackers.size;
      return { b, w: Math.max(0.12, 1 / (1 + d * 0.06)) / (1 + crowd * 0.8) };
    });
    let total = 0;
    for (const e of weighted) total += e.w;
    let r = Math.random() * total;
    let chosen = weighted[0].b;
    for (const e of weighted) { r -= e.w; if (r <= 0) { chosen = e.b; break; } }

    const ax = (chosen.alcove[0] + chosen.alcove[2]) / 2;
    const az = (chosen.alcove[1] + chosen.alcove[3]) / 2;

    let type = 'normal';
    if (this.bossesToSpawn > 0) {
      type = 'boss';
      this.bossesToSpawn--;
    } else if (this.special === 'horde') {
      type = Math.random() < 0.78 ? 'runner' : 'normal';
    } else {
      type = randomTypeForRound(this.round);
    }

    this.zombies.spawn({
      type,
      x: ax + rand(-0.6, 0.6),
      y: chosen.y,
      z: az + rand(-0.6, 0.6),
      health: this.healthForRound(this.round),
      aggression: this.aggressionForRound(this.round),
      damageScale: this.damageScaleForRound(this.round),
      barricade: chosen,
    });
    this.spawnedThisRound++;
    return true;
  }

  onKill() {
    this.killsThisRound++;
  }

  update(dt, playerPos) {
    switch (this.phase) {
      case PHASE.INTRO:
        this.timer -= dt;
        if (this.timer <= 0) this.beginRound(1);
        break;

      case PHASE.SPAWNING: {
        this.spawnT -= dt;
        const cap = this.maxConcurrent(this.round);
        if (this.spawnT <= 0 && this.zombies.livingCount < cap) {
          this.spawnT = this.spawnIntervalForRound(this.round) * rand(0.7, 1.3);
          this._spawnOne(playerPos);
          if (this.spawnedThisRound >= this.toSpawn) this.phase = PHASE.CLEARING;
        }
        break;
      }

      case PHASE.CLEARING:
        if (this.zombies.livingCount === 0) this.endRound();
        break;

      case PHASE.INTERMISSION:
        this.timer -= dt;
        if (this.timer <= 0) this.beginRound(this.round + 1);
        break;

      default:
        break;
    }
  }

  /** Instantly clears the wave (used by the Purge drop). */
  forceClear() {
    this.spawnedThisRound = this.toSpawn;
    if (this.phase === PHASE.SPAWNING) this.phase = PHASE.CLEARING;
  }

  stop() { this.phase = PHASE.OVER; }
}

export { pick, randInt };
