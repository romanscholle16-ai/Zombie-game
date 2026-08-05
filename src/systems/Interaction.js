import * as THREE from 'three';

/**
 * Central registry for everything the player can walk up to and use.
 * Systems push items in; the game loop asks for the best candidate each frame.
 */
export class InteractionSystem {
  constructor() {
    this.items = [];
    this._v = new THREE.Vector3();
  }

  add(item) {
    this.items.push(item);
    return item;
  }

  remove(item) {
    const i = this.items.indexOf(item);
    if (i >= 0) this.items.splice(i, 1);
  }

  clear() { this.items.length = 0; }

  /**
   * Nearest usable item within range, biased toward whatever the player is
   * actually looking at.
   */
  findBest(player, forward) {
    let best = null;
    let bestScore = -Infinity;
    const px = player.pos.x, py = player.pos.y, pz = player.pos.z;
    for (const it of this.items) {
      if (it.enabled === false) continue;
      const dx = it.pos.x - px, dz = it.pos.z - pz;
      const dy = (it.pos.y ?? py) - py;
      if (Math.abs(dy) > 3.2) continue;
      const dist = Math.hypot(dx, dz);
      const range = it.radius ?? 2.6;
      if (dist > range) continue;
      const len = dist || 1e-3;
      const dot = (dx / len) * forward.x + (dz / len) * forward.z;
      if (dot < -0.35 && dist > 1.3) continue;   // behind the player
      const score = dot * 2.2 - dist * 0.5 + (it.priority ?? 0);
      if (score > bestScore) { bestScore = score; best = it; }
    }
    return best;
  }
}

/** Convenience base so every machine exposes the same surface. */
export class Interactable {
  constructor({ id, x, y, z, radius = 2.6, holdTime = 0, priority = 0 }) {
    this.id = id;
    this.pos = new THREE.Vector3(x, y, z);
    this.radius = radius;
    this.holdTime = holdTime;
    this.priority = priority;
    this.enabled = true;
  }

  /** @returns {{text:string, cost?:number, sub?:string, blocked?:boolean}} */
  prompt() { return { text: 'USE' }; }

  /** @returns {boolean} true when the interaction consumed the press. */
  activate() { return false; }
}
