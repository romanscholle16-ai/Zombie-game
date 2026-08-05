import { makePerkMachine } from '../world/Props.js';
import { Interactable } from './Interaction.js';
import { MAP } from '../world/MapData.js';
import { Audio } from '../core/AudioEngine.js';

/**
 * Perk definitions. Names and effects are original; the shape of the system
 * (buy a bottle from a machine, keep it until you go down) follows the
 * round-based survival template.
 */
export const PERKS = {
  vital: {
    id: 'vital', name: 'VITAL SURGE', short: 'VITAL', letter: 'V', cost: 2500, color: 0xc0392b,
    effect: 'Raises maximum health from 100 to 175.',
  },
  rapid: {
    id: 'rapid', name: 'QUICK HANDS', short: 'HANDS', letter: 'Q', cost: 3000, color: 0xe8a33d,
    effect: 'Reloads roughly twice as fast.',
  },
  swift: {
    id: 'swift', name: 'LIGHT STEP', short: 'STEP', letter: 'L', cost: 2000, color: 0x4aa3ff,
    effect: 'Move 13% faster and swap weapons quicker.',
  },
  medic: {
    id: 'medic', name: 'SECOND WIND', short: 'WIND', letter: 'S', cost: 1500, color: 0x9fd93a,
    effect: 'Longer bleed-out and one self-revive per life.',
  },
  endure: {
    id: 'endure', name: 'IRON LUNG', short: 'LUNG', letter: 'I', cost: 2000, color: 0xb479ff,
    effect: 'Sprint roughly twice as long and recover faster.',
  },
};

export const PERK_LIST = Object.values(PERKS);

class PerkMachine extends Interactable {
  constructor(spot, system) {
    const perk = PERKS[spot.perk];
    super({ id: `perk_${perk.id}`, x: spot.x, y: spot.y, z: spot.z, radius: 2.4, priority: 1 });
    this.perk = perk;
    this.system = system;
    this.spot = spot;
    this.group = makePerkMachine(perk);
    this.group.position.set(spot.x, spot.y, spot.z);
    this.group.rotation.y = spot.rot;
    this.dispenseT = 0;
  }

  get powered() { return this.system.power.on; }

  prompt(ctx) {
    if (!this.powered) return { text: this.perk.name, sub: 'NO POWER', blocked: true };
    if (ctx.player.perks.has(this.perk.id)) return { text: this.perk.name, sub: 'ALREADY ACTIVE', blocked: true };
    return { text: `BUY ${this.perk.name}`, cost: this.perk.cost, sub: this.perk.effect };
  }

  activate(ctx) {
    if (!this.powered) return false;
    const { player } = ctx;
    if (player.perks.has(this.perk.id)) return false;
    if (!player.spend(this.perk.cost)) { ctx.deny(); return false; }
    player.givePerk(this.perk.id, this.perk);
    Audio.play('perk', { volume: 1 });
    this.dispenseT = 1.6;
    ctx.toast(`${this.perk.name} ACQUIRED`, 'pts');
    return true;
  }

  update(dt, time) {
    const on = this.powered;
    for (const g of this.group.userData.glowParts) {
      if (g.material.emissiveIntensity !== undefined) {
        g.material.emissiveIntensity = on ? 1.4 + Math.sin(time * 3 + this.pos.x) * 0.5 : 0.04;
      } else if (g.material.color) {
        g.material.color.setScalar(on ? 1 : 0.22);
      }
    }
    const bottle = this.group.userData.bottle;
    if (this.dispenseT > 0) {
      this.dispenseT -= dt;
      bottle.visible = true;
      bottle.position.y = 0.66 + Math.sin(this.dispenseT * 4) * 0.03;
      if (this.dispenseT <= 0) bottle.visible = false;
    }
  }
}

export class PerkSystem {
  constructor(world, scene, interaction, power) {
    this.world = world;
    this.power = power;
    this.machines = [];
    for (const spot of MAP.perkSpots) {
      const m = new PerkMachine(spot, this);
      scene.add(m.group);
      interaction.add(m);
      this.machines.push(m);
      const c = m.group.userData.collider;
      world.addObstacle(spot.x, spot.z, c.w, c.d, spot.y, c.h);
    }
  }

  update(dt, time) {
    for (const m of this.machines) m.update(dt, time);
  }
}
