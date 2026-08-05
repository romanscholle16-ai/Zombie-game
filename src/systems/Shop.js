import * as THREE from 'three';
import { makeWallBuy, box, Mats } from '../world/Props.js';
import { Tex } from '../world/Textures.js';
import { Interactable } from './Interaction.js';
import { MAP } from '../world/MapData.js';
import { WEAPONS } from '../weapons/WeaponDefs.js';
import { Audio } from '../core/AudioEngine.js';

class WallBuy extends Interactable {
  constructor(spot, scene) {
    const def = WEAPONS[spot.weapon];
    super({ id: spot.id, x: spot.x, y: spot.y + 1.2, z: spot.z, radius: 2.5, priority: 1 });
    this.def = def;
    this.group = makeWallBuy(def);
    this.group.position.set(spot.x, spot.y + 1.5, spot.z);
    this.group.rotation.y = spot.rot;
    scene.add(this.group);
  }

  prompt(ctx) {
    const owned = ctx.ownsWeapon(this.def.id);
    if (owned) {
      const full = ctx.weaponAmmoFull(this.def.id);
      if (full) return { text: this.def.name, sub: 'AMMO FULL', blocked: true };
      return { text: `${this.def.name} AMMO`, cost: this.def.wallAmmoCost };
    }
    if (!this.def.wallCost) return { text: this.def.name, sub: 'STANDARD ISSUE', blocked: true };
    return { text: `BUY ${this.def.name}`, cost: this.def.wallCost, sub: this.def.category.toUpperCase() };
  }

  activate(ctx) {
    const owned = ctx.ownsWeapon(this.def.id);
    if (owned) {
      if (ctx.weaponAmmoFull(this.def.id)) return false;
      if (!ctx.player.spend(this.def.wallAmmoCost)) { ctx.deny(); return false; }
      ctx.refillWeapon(this.def.id);
      Audio.play('ui', { kind: 'select' });
      ctx.toast('AMMO PURCHASED', 'pts');
      return true;
    }
    if (!this.def.wallCost) return false;
    if (!ctx.player.spend(this.def.wallCost)) { ctx.deny(); return false; }
    ctx.giveWeapon(this.def);
    Audio.play('ui', { kind: 'select' });
    return true;
  }

  update(dt, time) {
    const g = this.group.userData.weaponModel;
    if (g) g.position.y = 0.06 + Math.sin(time * 1.6 + this.pos.x) * 0.02;
    void dt;
  }
}

class ArmorStation extends Interactable {
  constructor(spot, scene) {
    super({ id: spot.id, x: spot.x, y: spot.y + 1.1, z: spot.z, radius: 2.3, priority: 1 });
    this.cost = spot.cost;
    this.group = new THREE.Group();
    this.group.add(box(1.2, 1.8, 0.35, Mats.darkMetal, 0, 0.9, 0));
    this.group.add(box(1.0, 0.28, 0.16, Mats.emissive(0x4aa3ff, 1.6), 0, 1.5, 0.2));
    for (let i = 0; i < 3; i++) {
      this.group.add(box(0.72, 0.1, 0.3, Mats.metal, 0, 0.45 + i * 0.32, 0.14));
    }
    const sign = new THREE.Mesh(new THREE.PlaneGeometry(1.1, 0.4), new THREE.MeshBasicMaterial({
      map: Tex.label('PLATING', { fg: '#4aa3ff', sub: `${spot.cost}` }),
    }));
    sign.position.set(0, 1.85, 0.2);
    this.group.add(sign);
    this.group.position.set(spot.x, spot.y, spot.z);
    this.group.rotation.y = spot.rot;
    scene.add(this.group);
  }

  prompt(ctx) {
    if (ctx.player.armor >= 100) return { text: 'ARMOUR PLATING', sub: 'PLATES FULL', blocked: true };
    return { text: 'ARMOUR PLATING', cost: this.cost, sub: 'ABSORBS 65% OF DAMAGE' };
  }

  activate(ctx) {
    if (ctx.player.armor >= 100) return false;
    if (!ctx.player.spend(this.cost)) { ctx.deny(); return false; }
    ctx.player.addArmor(50);
    Audio.play('ui', { kind: 'select' });
    ctx.toast('PLATES APPLIED', 'pts');
    return true;
  }

  update() {}
}

/** Wall-mounted purchases: weapons, ammo, and armour plating. */
export class ShopSystem {
  constructor(world, scene, interaction) {
    this.stations = [];
    for (const spot of MAP.wallBuys) {
      const w = new WallBuy(spot, scene);
      interaction.add(w);
      this.stations.push(w);
    }
    for (const spot of MAP.armorStations) {
      const a = new ArmorStation(spot, scene);
      interaction.add(a);
      this.stations.push(a);
    }
    void world;
  }

  update(dt, time) {
    for (const s of this.stations) s.update(dt, time);
  }
}
