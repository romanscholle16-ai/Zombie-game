import { formatTime, formatNumber, clamp, el } from '../core/Util.js';
import { Settings, keyLabel } from '../core/Settings.js';
import { PERKS } from '../systems/Perks.js';

/** DOM heads-up display. Kept out of WebGL so text stays crisp at any DPI. */
export class HUD {
  constructor() {
    this.root = document.getElementById('hud');
    this.$ = (id) => document.getElementById(id);
    this.round = this.$('hud-round');
    this.zleft = this.$('hud-zleft');
    this.name = this.$('hud-name');
    this.points = this.$('hud-points');
    this.time = this.$('hud-time');
    this.healthFill = this.$('hud-health-fill');
    this.armorFill = this.$('hud-armor-fill');
    this.perkRow = this.$('hud-perks');
    this.weapon = this.$('hud-weapon');
    this.mag = this.$('hud-mag');
    this.res = this.$('hud-res');
    this.weaponAlt = this.$('hud-weapon-alt');
    this.crosshair = this.$('crosshair');
    this.hitmarkerEl = this.$('hitmarker');
    this.scope = this.$('scope');
    this.scopeTube = this.scope?.querySelector('.scope-tube');
    this.prompt = this.$('prompt');
    this.toastStack = this.$('toast-stack');
    this.vignette = this.$('damage-vignette');
    this.lowPulse = this.$('low-health-pulse');
    this.dirWrap = this.$('dir-indicators');
    this.banner = this.$('round-banner');
    this.tray = this.$('powerup-tray');
    this.downed = this.$('downed-overlay');
    this.bleedFill = this.$('bleed-fill');

    this.lastPoints = 0;
    this.perkChips = new Map();
    this.dirNodes = [];
    this._hmTimer = null;
    this._promptState = '';
  }

  show() { this.root.classList.remove('hidden'); }
  hide() { this.root.classList.add('hidden'); }

  reset() {
    this.perkRow.innerHTML = '';
    this.perkChips.clear();
    this.tray.innerHTML = '';
    this.toastStack.innerHTML = '';
    this.dirWrap.innerHTML = '';
    this.dirNodes = [];
    this.downed.classList.add('hidden');
    this.vignette.style.opacity = 0;
    this.lowPulse.classList.remove('on');
    this.scope?.classList.add('hidden');
    this._scopeShown = false;
    this._scopeT = 0;
    this.lastPoints = 0;
  }

  // ------------------------------------------------------------- per frame
  update(state) {
    const { player, weapons, rounds, elapsed, powerups } = state;

    this.round.textContent = Math.max(1, rounds.round);
    this.zleft.textContent = Math.max(0, rounds.remaining);
    this.time.textContent = formatTime(elapsed);
    this.name.textContent = Settings.get('playerName') || 'OPERATOR';

    if (player.points !== this.lastPoints) {
      this.points.textContent = formatNumber(player.points);
      this.points.classList.add('bump');
      clearTimeout(this._ptTimer);
      this._ptTimer = setTimeout(() => this.points.classList.remove('bump'), 110);
      this.lastPoints = player.points;
    }

    const hp = clamp(player.health / player.maxHealth, 0, 1);
    this.healthFill.style.width = `${hp * 100}%`;
    this.healthFill.classList.toggle('hurt', hp < 0.45);
    this.armorFill.style.width = `${clamp(player.armor / 100, 0, 1) * 100}%`;
    this.lowPulse.classList.toggle('on', hp < 0.3 && !player.downed);

    // Damage vignette fades with time since the last hit.
    const dmgT = clamp(1 - player.sinceDamage / 1.6, 0, 1);
    this.vignette.style.opacity = String(Math.max(dmgT * 0.85, (1 - hp) * 0.35));

    this._updatePerks(player);
    this._updateWeapon(weapons);
    this._updateDirIndicators(player);
    this._updateTray(powerups);

    if (player.downed) {
      this.downed.classList.remove('hidden');
      this.bleedFill.style.width = `${clamp(player.bleedOut / player.bleedOutMax, 0, 1) * 100}%`;
    } else {
      this.downed.classList.add('hidden');
    }

    this._updateScope(state.scope);
    this.crosshair.classList.toggle('ads', weapons.ads);
    this.crosshair.classList.toggle('hidden-ch', !Settings.get('crosshair') || this._scopeT > 0.02);
  }

  /**
   * Sight picture for magnified optics.
   * @param {{t:number, sway:{x:number,y:number}}|null} s scope-in progress 0..1
   */
  _updateScope(s) {
    const t = s ? clamp(s.t, 0, 1) : 0;
    this._scopeT = t;
    if (!this.scope) return;
    if (t <= 0.001) {
      if (this._scopeShown) {
        this.scope.classList.add('hidden');
        this.scope.style.opacity = '0';
        this._scopeShown = false;
      }
      return;
    }
    if (!this._scopeShown) {
      this.scope.classList.remove('hidden');
      this._scopeShown = true;
    }
    // The tube irises in as you settle behind the glass, so the transition
    // reads as bringing the optic to your eye rather than a hard cut.
    const short = Math.min(window.innerWidth, window.innerHeight);
    const radius = short * (0.62 - 0.20 * t);
    this.scope.style.opacity = String(clamp((t - 0.35) / 0.5, 0, 1));
    this.scope.style.setProperty('--scope-r', `${radius.toFixed(1)}px`);
    if (this.scopeTube && s.sway) {
      this.scopeTube.style.setProperty('--scope-x', `${(s.sway.x * short * 0.035).toFixed(2)}px`);
      this.scopeTube.style.setProperty('--scope-y', `${(s.sway.y * short * 0.035).toFixed(2)}px`);
    }
  }

  _updatePerks(player) {
    for (const id of player.perks) {
      if (this.perkChips.has(id)) continue;
      const p = PERKS[id];
      const chip = el('div', {
        class: 'perk-chip', title: `${p.name} — ${p.effect}`,
        style: `color:#${p.color.toString(16).padStart(6, '0')};border-color:#${p.color.toString(16).padStart(6, '0')}`,
        text: p.letter,
      });
      this.perkRow.appendChild(chip);
      this.perkChips.set(id, chip);
    }
    for (const [id, chip] of this.perkChips) {
      if (!player.perks.has(id)) {
        chip.remove();
        this.perkChips.delete(id);
      }
    }
  }

  _updateWeapon(w) {
    this.weapon.textContent = w.name;
    this.mag.textContent = w.reloading ? '--' : w.mag;
    this.res.textContent = w.reserve;
    this.mag.classList.toggle('low', !w.reloading && w.mag <= 3);
    let alt = '';
    if (w.reloading) alt = 'RELOADING';
    else if (w.upgradeLevel) alt = `REFIT ${'I'.repeat(w.upgradeLevel)}`;
    else if (w.other) alt = `${keyLabel(Settings.bind('swap'))} · ${w.other}`;
    this.weaponAlt.textContent = alt;
  }

  _updateDirIndicators(player) {
    const need = player.damageFlashers.length;
    while (this.dirNodes.length < need) {
      const n = el('div', { class: 'dir-ind' }, el('i', {}));
      this.dirWrap.appendChild(n);
      this.dirNodes.push(n);
    }
    this.dirNodes.forEach((n, i) => {
      const f = player.damageFlashers[i];
      if (!f) { n.style.display = 'none'; return; }
      n.style.display = 'block';
      const rel = f.angle - player.yaw;
      n.style.transform = `rotate(${-rel}rad)`;
      n.style.opacity = String(clamp(f.t, 0, 1));
    });
  }

  _updateTray(powerups) {
    const entries = powerups?.hudEntries?.() ?? [];
    const keys = entries.map((e) => e.id).join(',');
    if (keys !== this._trayKeys) {
      this._trayKeys = keys;
      this.tray.innerHTML = '';
      this._trayNodes = entries.map((e) => {
        const node = el('div', { class: 'pu-chip', style: `color:#${e.color.toString(16).padStart(6, '0')}` },
          el('span', { text: e.label }), el('span', { class: 'pu-t' }));
        this.tray.appendChild(node);
        return node;
      });
    }
    entries.forEach((e, i) => {
      const node = this._trayNodes?.[i];
      if (!node) return;
      node.querySelector('.pu-t').textContent = `${Math.ceil(e.t)}s`;
      node.classList.toggle('expiring', e.t < 5);
    });
  }

  // ------------------------------------------------------------- events
  hitmarker(crit, kill) {
    const h = this.hitmarkerEl;
    h.classList.remove('show', 'crit', 'kill');
    void h.offsetWidth; // restart the animation
    if (crit) h.classList.add('crit');
    if (kill) h.classList.add('kill');
    h.classList.add('show');
  }

  setPrompt(data) {
    if (!data) {
      if (this._promptState !== '') {
        this.prompt.classList.add('hidden');
        this._promptState = '';
      }
      return;
    }
    const key = keyLabel(Settings.bind('interact'));
    const sig = `${data.text}|${data.cost ?? ''}|${data.sub ?? ''}|${data.blocked ? 1 : 0}|${data.hold ?? ''}`;
    if (sig !== this._promptState) {
      this._promptState = sig;
      this.prompt.innerHTML = '';
      const line = el('div', {},
        data.blocked ? null : el('span', { class: 'pk', text: key }),
        el('span', { class: 'pt', text: data.text }));
      this.prompt.appendChild(line);
      // Price and description are both worth showing — you should not have to
      // buy a perk to find out what it does.
      if (data.cost !== undefined && data.cost !== null) {
        this.prompt.appendChild(el('span', { class: 'pc', text: `${formatNumber(data.cost)} POINTS` }));
      }
      if (data.sub) {
        this.prompt.appendChild(el('span', { class: 'pd', text: data.sub }));
      }
      if (data.hold !== undefined) {
        this.holdRing = el('span', { class: 'hold-ring' }, el('i', {}));
        this.prompt.appendChild(this.holdRing);
      } else {
        this.holdRing = null;
      }
      this.prompt.classList.remove('hidden');
      this.prompt.classList.toggle('deny', !!data.blocked);
    }
    if (this.holdRing && data.hold !== undefined) {
      this.holdRing.firstChild.style.width = `${clamp(data.hold, 0, 1) * 100}%`;
    }
  }

  denyFlash() {
    this.prompt.classList.add('deny');
    setTimeout(() => this.prompt.classList.remove('deny'), 320);
  }

  toast(text, kind = 'pts', big = false) {
    const t = el('div', { class: `toast ${kind}${big ? ' big' : ''}`, text });
    this.toastStack.appendChild(t);
    setTimeout(() => t.classList.add('fade'), 1400);
    setTimeout(() => t.remove(), 1950);
    while (this.toastStack.children.length > 5) this.toastStack.firstChild.remove();
  }

  pointsPopup(amount, label) {
    this.toast(`+${amount}${label ? ` ${label}` : ''}`, 'pts');
  }

  showRoundBanner(round, special) {
    const text = special === 'boss' ? `ROUND ${round} — ABOMINATION`
      : special === 'horde' ? `ROUND ${round} — HORDE`
        : `ROUND ${round}`;
    this.banner.querySelector('.rb-text').textContent = text;
    this.banner.classList.remove('show');
    void this.banner.offsetWidth;
    this.banner.classList.add('show');
    setTimeout(() => this.banner.classList.remove('show'), 2700);
  }
}
