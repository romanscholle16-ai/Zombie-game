import { makePowerSwitch, Mats } from '../world/Props.js';
import { Interactable } from './Interaction.js';
import { MAP } from '../world/MapData.js';
import { Audio } from '../core/AudioEngine.js';
import { Emitter } from '../core/Util.js';

/**
 * The facility starts dark. Throwing the breaker brings up perk machines,
 * the refit chamber, traps and the lights.
 */
export class PowerSystem extends Emitter {
  constructor(world, scene, interaction) {
    super();
    this.world = world;
    this.on = false;
    this.activatingT = 0;

    const s = MAP.powerSwitch;
    this.group = makePowerSwitch();
    this.group.position.set(s.x, s.y, s.z);
    this.group.rotation.y = s.rot;
    scene.add(this.group);
    world.addObstacle(s.x, s.z, 1.2, 0.7, s.y, 2.3);

    const self = this;
    this.interactable = interaction.add(new (class extends Interactable {
      prompt() {
        return self.on
          ? { text: 'MAIN BREAKER', sub: 'GRID ONLINE', blocked: true }
          : { text: 'ACTIVATE POWER', sub: 'RESTORES THE FACILITY GRID' };
      }
      activate(ctx) {
        if (self.on) return false;
        self.activate(ctx);
        return true;
      }
    })({ id: 'power', x: s.x, y: s.y, z: s.z, radius: 2.8, priority: 2 }));
  }

  activate(ctx) {
    if (this.on) return;
    this.on = true;
    this.activatingT = 2.6;
    this.world.setPowered(true);
    this.group.userData.lever.rotation.x = -0.9;
    this.group.userData.lamp.material = Mats.emissive(0x9fd93a, 2.2);
    Audio.play('power', { volume: 1 });
    ctx?.toast('POWER RESTORED', 'pts', true);
    ctx?.player.addShake(0.5);
    this.emit('on');
  }

  reset() {
    this.on = false;
    this.world.setPowered(false);
  }

  update(dt) {
    if (this.activatingT > 0) this.activatingT -= dt;
  }
}
