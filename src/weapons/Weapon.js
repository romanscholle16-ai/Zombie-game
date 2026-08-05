import { upgradedDef, upgradeCost, UPGRADE_LEVELS } from './WeaponDefs.js';
import { Audio } from '../core/AudioEngine.js';
import { clamp } from '../core/Util.js';

/** Runtime state for one carried weapon. */
export class Weapon {
  constructor(baseDef, upgradeLevel = 0) {
    this.base = baseDef;
    this.upgradeLevel = upgradeLevel;
    this.def = upgradedDef(baseDef, upgradeLevel);

    this.mag = this.def.mag;
    this.reserve = this.def.reserve;
    this.cooldown = 0;
    this.reloading = false;
    this.reloadT = 0;
    this.reloadTotal = 0;
    this.shellsLoaded = 0;
    this.triggerHeld = false;
    this.burstLeft = 0;
    this.burstCooldown = 0;
    this.heat = 0;              // drives recoil climb
    this.shotsThisTrigger = 0;
  }

  get id() { return this.base.id; }
  get name() { return this.def.name; }
  get isEmpty() { return this.mag <= 0; }
  get isFull() { return this.mag >= this.def.mag; }
  get canReload() { return !this.reloading && this.mag < this.def.mag && this.reserve > 0; }
  get fireInterval() { return 60 / this.def.rpm; }
  get maxUpgrade() { return UPGRADE_LEVELS.length; }
  get nextUpgradeCost() { return upgradeCost(this.upgradeLevel); }

  upgrade() {
    if (this.upgradeLevel >= UPGRADE_LEVELS.length) return false;
    this.upgradeLevel++;
    this.def = upgradedDef(this.base, this.upgradeLevel);
    this.mag = this.def.mag;
    this.reserve = this.def.reserve;
    this.reloading = false;
    return true;
  }

  refillAmmo() {
    this.reserve = this.def.reserve;
    this.mag = this.def.mag;
    this.reloading = false;
  }

  addAmmo(fraction = 1) {
    this.reserve = clamp(this.reserve + Math.round(this.def.reserve * fraction), 0, this.def.reserve);
  }

  /** Returns true if the trigger state allows a shot this frame. */
  wantsToFire(held, pressed) {
    if (this.reloading || this.cooldown > 0) return false;
    if (this.def.fireMode === 'auto') return held;
    if (this.def.fireMode === 'burst') return pressed && this.burstCooldown <= 0;
    return pressed;
  }

  /** Consumes a round. Returns false when the magazine is dry. */
  consume() {
    if (this.mag <= 0) return false;
    this.mag--;
    this.cooldown = this.fireInterval;
    this.heat = Math.min(1, this.heat + 0.16);
    this.shotsThisTrigger++;
    return true;
  }

  startReload(speedMult = 1) {
    if (!this.canReload) return false;
    this.reloading = true;
    this.shellsLoaded = 0;
    const empty = this.mag <= 0;
    this.reloadTotal = (empty ? this.def.emptyReloadTime : this.def.reloadTime) / speedMult;
    this.reloadT = this.reloadTotal;
    Audio.play('reload', { stage: 0, volume: 0.9 });
    return true;
  }

  cancelReload() {
    if (!this.reloading) return;
    this.reloading = false;
    this.reloadT = 0;
  }

  update(dt, speedMult = 1) {
    this.cooldown = Math.max(0, this.cooldown - dt);
    this.burstCooldown = Math.max(0, this.burstCooldown - dt);
    this.heat = Math.max(0, this.heat - dt * 1.6);

    if (!this.reloading) return;
    this.reloadT -= dt;

    if (this.def.shellReload) {
      // Shotgun-style: one shell per cycle, interruptible.
      if (this.reloadT <= 0) {
        this.mag++;
        this.reserve--;
        this.shellsLoaded++;
        Audio.play('reload', { stage: 1, volume: 0.8 });
        if (this.mag >= this.def.mag || this.reserve <= 0) {
          this.reloading = false;
        } else {
          this.reloadTotal = this.def.reloadTime / speedMult;
          this.reloadT = this.reloadTotal;
        }
      }
      return;
    }

    // Magazine reload: two audible stages then the round transfer.
    const p = 1 - this.reloadT / this.reloadTotal;
    if (p > 0.45 && !this._stage1) { this._stage1 = true; Audio.play('reload', { stage: 1, volume: 0.85 }); }
    if (p > 0.85 && !this._stage2) { this._stage2 = true; Audio.play('reload', { stage: 2, volume: 0.7 }); }
    if (this.reloadT <= 0) {
      const need = this.def.mag - this.mag;
      const take = Math.min(need, this.reserve);
      this.mag += take;
      this.reserve -= take;
      this.reloading = false;
      this._stage1 = false;
      this._stage2 = false;
    }
  }

  get reloadProgress() {
    return this.reloading ? clamp(1 - this.reloadT / this.reloadTotal, 0, 1) : 1;
  }
}
