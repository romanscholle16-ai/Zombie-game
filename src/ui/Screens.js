import * as THREE from 'three';
import { el, formatTime, formatNumber, clamp } from '../core/Util.js';
import { Settings, QUALITY_PRESETS, BIND_LABELS, keyLabel } from '../core/Settings.js';
import { SaveData } from '../core/SaveData.js';
import { Input } from '../core/Input.js';
import { Audio } from '../core/AudioEngine.js';
import { WEAPONS, CATEGORIES, RARITY, weaponsByCategory, KNIFE } from '../weapons/WeaponDefs.js';
import { PERK_LIST } from '../systems/Perks.js';
import { makeWeaponModel } from '../world/Props.js';
import { MAP } from '../world/MapData.js';
import { applyEnvironment } from '../world/Environment.js';

const frame = () => [
  el('div', { class: 'vignette-frame' }),
  el('div', { class: 'scanlines' }),
  el('div', { class: 'grain' }),
];

/**
 * All front-end screens. Each build* function returns a detached DOM tree the
 * ScreenManager swaps in; the WebGL menu backdrop renders behind them.
 */
export class ScreenManager {
  constructor(app) {
    this.app = app;
    this.root = document.getElementById('menu-root');
    this.current = null;
    this.stack = [];
    this.loadout = new LoadoutViewer();
    this._onKey = this._onKey.bind(this);
    window.addEventListener('keydown', this._onKey);
  }

  get active() { return this.current !== null; }

  show(name, opts = {}) {
    if (this.current === name && !opts.force) return;
    this.current = name;
    this.root.classList.add('active');
    this.root.innerHTML = '';
    const node = this._build(name, opts);
    this.root.appendChild(node);
    this.selIndex = 0;
    this._syncSelection();
  }

  push(name, opts) {
    if (this.current) this.stack.push(this.current);
    this.show(name, opts);
  }

  back() {
    const prev = this.stack.pop();
    Audio.play('ui', { kind: 'back' });
    if (prev) this.show(prev, { force: true });
    else this.show('main', { force: true });
  }

  hide() {
    this.current = null;
    this.stack.length = 0;
    this.root.classList.remove('active');
    this.root.innerHTML = '';
  }

  _build(name, opts) {
    switch (name) {
      case 'main': return this.buildMain();
      case 'solo': return this.buildSolo();
      case 'multiplayer': return this.buildMultiplayer();
      case 'loadout': return this.loadout.build(this);
      case 'stats': return this.buildStats();
      case 'settings': return this.buildSettings();
      case 'credits': return this.buildCredits();
      case 'pause': return this.buildPause();
      case 'gameover': return this.buildGameOver(opts.summary);
      default: return this.buildMain();
    }
  }

  // ---------------------------------------------------------------- nav
  _items() {
    return Array.from(this.root.querySelectorAll('.menu-item'));
  }

  _syncSelection() {
    const items = this._items();
    items.forEach((n, i) => n.classList.toggle('sel', i === this.selIndex));
  }

  _onKey(e) {
    if (!this.active) return;
    const items = this._items();
    if (e.code === 'Escape') {
      if (this.current === 'main') return;
      if (this.current === 'pause') { this.app.resume(); return; }
      e.preventDefault();
      this.back();
      return;
    }
    if (!items.length) return;
    if (e.code === 'ArrowDown' || e.code === 'ArrowUp') {
      e.preventDefault();
      this.selIndex = (this.selIndex + (e.code === 'ArrowDown' ? 1 : -1) + items.length) % items.length;
      Audio.play('ui', { kind: 'move' });
      this._syncSelection();
    } else if (e.code === 'Enter' || e.code === 'NumpadEnter') {
      e.preventDefault();
      items[this.selIndex]?.click();
    }
  }

  // ---------------------------------------------------------------- main
  buildMain() {
    const item = (label, tag, fn, ghost = false) => el('button', {
      class: `menu-item${ghost ? ' ghost' : ''}`,
      onclick: () => { Audio.play('ui', { kind: 'select' }); fn(); },
      onmouseenter: (e) => {
        const items = this._items();
        this.selIndex = items.indexOf(e.currentTarget);
        this._syncSelection();
        Audio.play('ui', { kind: 'move' });
      },
    },
    el('span', { class: 'idx', text: tag }),
    el('span', { text: label }));

    const s = SaveData.data;
    return el('div', { class: 'screen no-dim' },
      ...frame(),
      el('div', { class: 'brand' },
        el('div', { class: 'brand-title', html: 'ROT<em>GRID</em>' }),
        el('div', { class: 'brand-sub', text: 'Round-Based Survival · Blacksite 7' }),
        el('div', { class: 'brand-rule' })),
      el('div', { class: 'menu-body' },
        el('div', { class: 'menu-list' },
          item('PLAY', '01', () => this.app.startGame()),
          item('SOLO MATCH', '02', () => this.push('solo')),
          item('MULTIPLAYER', '03', () => this.push('multiplayer')),
          item('LOADOUT VIEWER', '04', () => this.push('loadout')),
          item('STATISTICS', '05', () => this.push('stats')),
          item('SETTINGS', '06', () => this.push('settings')),
          item('CREDITS', '07', () => this.push('credits')),
          item('QUIT', '08', () => this.app.quit()))),
      el('div', { class: 'menu-foot' },
        el('span', { html: `HIGHEST ROUND <b>${s.highestRound}</b>` }),
        el('span', { html: `LIFETIME KILLS <b>${formatNumber(s.lifetimeKills)}</b>` }),
        el('span', { html: `RUNS <b>${s.gamesPlayed}</b>` }),
        el('span', { html: 'MOVE <b>WASD</b> · LOOK <b>MOUSE</b> or <b>ARROWS</b> · FIRE <b>LMB</b> · USE <b>F</b>' })));
  }

  // ---------------------------------------------------------------- solo
  buildSolo() {
    const nameInput = el('input', {
      type: 'text', value: Settings.get('playerName'), maxlength: '18',
      oninput: (e) => Settings.set('playerName', e.target.value.toUpperCase().slice(0, 18)),
    });

    return el('div', { class: 'screen' }, ...frame(),
      el('div', { class: 'panel' },
        el('div', { class: 'panel-head' },
          el('div', { class: 'panel-title', html: 'SOLO <span>MATCH</span>' }),
          el('div', { class: 'panel-hint', text: 'ESC to go back' })),
        el('div', { class: 'panel-scroll' },
          el('div', { class: 'set-group' },
            el('h3', { text: 'Operator' }),
            el('div', { class: 'set-row' },
              el('label', { text: 'Callsign' }), nameInput, el('span', { class: 'val', text: '' }))),
          el('div', { class: 'set-group' },
            el('h3', { text: 'Deployment' }),
            el('div', { class: 'stat-grid' },
              statCard('MAP', MAP.name, true),
              statCard('ZONE COUNT', String(MAP.zones.length)),
              statCard('STARTING POINTS', '500'),
              statCard('STARTING HEALTH', '100'),
              statCard('BLEED-OUT', '30s'),
              statCard('PLAYERS', '1 / 4'))),
          el('div', { class: 'set-group' },
            el('h3', { text: 'Starting loadout' }),
            el('div', { class: 'stat-grid' },
              statCard('PRIMARY', WEAPONS.sidearm.name, true),
              statCard('MELEE', KNIFE.name),
              statCard('PERKS', 'NONE'))),
          el('div', { class: 'set-group' },
            el('h3', { text: 'Objective' }),
            el('p', { class: 'wep-blurb', text: 'Hold the annex. Buy your way deeper, restore the grid, and survive as long as the horde lets you. There is no extraction.' }))),
        el('div', { class: 'panel-actions' },
          el('button', { class: 'btn primary menu-item', onclick: () => this.app.startGame(), text: 'DEPLOY' }),
          el('button', { class: 'btn menu-item', onclick: () => this.back(), text: 'BACK' }))));
  }

  // ---------------------------------------------------------------- MP
  buildMultiplayer() {
    const slots = ['YOU', 'OPEN', 'OPEN', 'OPEN'].map((s, i) => el('div', { class: `slot${i === 0 ? ' me' : ''}` },
      el('div', { class: 's1', text: `SLOT ${i + 1}` }),
      el('div', { class: 's2', text: i === 0 ? (Settings.get('playerName') || 'OPERATOR') : s })));

    return el('div', { class: 'screen' }, ...frame(),
      el('div', { class: 'panel' },
        el('div', { class: 'panel-head' },
          el('div', { class: 'panel-title', html: 'CO-OP <span>LOBBY</span>' }),
          el('div', { class: 'panel-hint', text: 'Placeholder — not yet online' })),
        el('div', { class: 'panel-scroll' },
          el('p', { class: 'mp-note', html:
            'Networked co-op is not enabled in this build. The systems underneath it already assume more than one survivor: '
            + 'the round manager scales spawn counts by player count, the downed state has a revive path, and every entity '
            + 'update is driven from a single authoritative tick.' }),
          el('div', { class: 'roster' }, ...slots),
          el('div', { class: 'set-group', style: 'margin-top:26px' },
            el('h3', { text: 'What is already wired for it' }),
            el('p', { class: 'mp-note', html:
              '<code>RoundManager</code> takes a player-count multiplier · <code>Player.downed</code> supports external revives · '
              + '<code>ZombieManager</code> targets the nearest survivor through a shared flow field · '
              + '<code>Economy</code> tracks points per player.' })),
          el('div', { class: 'set-group' },
            el('h3', { text: 'What is missing' }),
            el('p', { class: 'mp-note', text: 'Transport, state replication, and lobby matchmaking. Everything else is local-authoritative already.' }))),
        el('div', { class: 'panel-actions' },
          el('button', { class: 'btn menu-item', onclick: () => this.back(), text: 'BACK' }))));
  }

  // ---------------------------------------------------------------- stats
  buildStats() {
    const d = SaveData.data;
    const acc = (w) => (w.shotsFired ? `${((w.shotsHit / w.shotsFired) * 100).toFixed(1)}%` : '—');
    const rows = Object.entries(d.weaponStats)
      .sort((a, b) => b[1].kills - a[1].kills)
      .slice(0, 14)
      .map(([id, w]) => el('tr', {},
        el('td', { text: WEAPONS[id]?.name ?? id.toUpperCase() }),
        el('td', { text: formatNumber(w.kills) }),
        el('td', { text: formatNumber(w.headshots) }),
        el('td', { text: acc(w) }),
        el('td', { text: formatNumber(w.damage) })));

    const history = d.history.map((h) => el('tr', {},
      el('td', { text: new Date(h.date).toLocaleDateString() }),
      el('td', { text: String(h.round) }),
      el('td', { text: formatNumber(h.kills) }),
      el('td', { text: formatNumber(h.headshots) }),
      el('td', { text: formatTime(h.time) }),
      el('td', { text: formatNumber(h.points) })));

    return el('div', { class: 'screen' }, ...frame(),
      el('div', { class: 'panel' },
        el('div', { class: 'panel-head' },
          el('div', { class: 'panel-title', html: 'CAREER <span>RECORD</span>' }),
          el('div', { class: 'panel-hint', text: 'Stored locally on this device' })),
        el('div', { class: 'panel-scroll' },
          el('div', { class: 'stat-grid' },
            statCard('HIGHEST ROUND', String(d.highestRound), true),
            statCard('LIFETIME KILLS', formatNumber(d.lifetimeKills)),
            statCard('HEADSHOTS', formatNumber(d.lifetimeHeadshots)),
            statCard('RUNS PLAYED', formatNumber(d.gamesPlayed)),
            statCard('TIME SURVIVED', formatTime(d.totalTimeSurvived)),
            statCard('POINTS EARNED', formatNumber(d.totalPointsEarned)),
            statCard('BARRIERS REPAIRED', formatNumber(d.barriersRepaired)),
            statCard('CRATE SPINS', formatNumber(d.boxSpins)),
            statCard('TIMES DOWNED', formatNumber(d.totalDowns))),
          rows.length ? el('div', { class: 'set-group', style: 'margin-top:24px' },
            el('h3', { text: 'Weapon performance' }),
            el('table', { class: 'table' },
              el('thead', {}, el('tr', {},
                el('th', { text: 'Weapon' }), el('th', { text: 'Kills' }),
                el('th', { text: 'Headshots' }), el('th', { text: 'Accuracy' }), el('th', { text: 'Damage' }))),
              el('tbody', {}, ...rows))) : null,
          history.length ? el('div', { class: 'set-group', style: 'margin-top:24px' },
            el('h3', { text: 'Recent runs' }),
            el('table', { class: 'table' },
              el('thead', {}, el('tr', {},
                el('th', { text: 'Date' }), el('th', { text: 'Round' }), el('th', { text: 'Kills' }),
                el('th', { text: 'HS' }), el('th', { text: 'Time' }), el('th', { text: 'Points' }))),
              el('tbody', {}, ...history))) : null),
        el('div', { class: 'panel-actions' },
          el('button', { class: 'btn menu-item', onclick: () => this.back(), text: 'BACK' }),
          el('button', {
            class: 'btn danger menu-item',
            onclick: () => { SaveData.reset(); this.show('stats', { force: true }); },
            text: 'WIPE RECORD',
          }))));
  }

  // ---------------------------------------------------------------- settings
  buildSettings() {
    const rows = [];
    const slider = (label, key, min, max, step, fmt) => {
      const val = el('span', { class: 'val', text: fmt(Settings.get(key)) });
      const input = el('input', {
        type: 'range', min, max, step, value: Settings.get(key),
        oninput: (e) => {
          const v = parseFloat(e.target.value);
          Settings.set(key, v);
          val.textContent = fmt(Settings.get(key));
        },
        onchange: () => Audio.play('ui', { kind: 'tick' }),
      });
      return el('div', { class: 'set-row' }, el('label', { text: label }), input, val);
    };
    const toggle = (label, key) => {
      const t = el('div', { class: `toggle${Settings.get(key) ? ' on' : ''}` }, el('i', {}));
      t.addEventListener('click', () => {
        Settings.set(key, !Settings.get(key));
        t.classList.toggle('on', Settings.get(key));
        Audio.play('ui', { kind: 'select' });
        if (key === 'fullscreen') this.app.applyFullscreen();
      });
      return el('div', { class: 'set-row' }, el('label', { text: label }), t,
        el('span', { class: 'val', text: '' }));
    };
    const select = (label, key, options) => {
      const s = el('select', {
        onchange: (e) => { Settings.set(key, e.target.value); this.app.applyQuality(); },
      }, ...options.map(([v, l]) => el('option', { value: v, selected: Settings.get(key) === v }, l)));
      return el('div', { class: 'set-row' }, el('label', { text: label }), s, el('span', { class: 'val', text: '' }));
    };

    const pct = (v) => `${Math.round(v * 100)}%`;

    rows.push(el('div', { class: 'set-group' },
      el('h3', { text: 'Audio' }),
      slider('Master Volume', 'masterVolume', 0, 1, 0.01, pct),
      slider('Music Volume', 'musicVolume', 0, 1, 0.01, pct),
      slider('SFX Volume', 'sfxVolume', 0, 1, 0.01, pct)));

    rows.push(el('div', { class: 'set-group' },
      el('h3', { text: 'Controls' }),
      slider('Mouse Sensitivity', 'mouseSensitivity', 0.02, 1, 0.01, (v) => v.toFixed(2)),
      slider('Controller Sensitivity', 'controllerSensitivity', 0.4, 8, 0.1, (v) => v.toFixed(1)),
      toggle('Invert Vertical Look', 'invertY')));

    rows.push(el('div', { class: 'set-group' },
      el('h3', { text: 'Video' }),
      slider('Field of View', 'fov', 60, 120, 1, (v) => `${Math.round(v)}°`),
      slider('Brightness', 'brightness', 0.5, 2, 0.05, (v) => `${Math.round(v * 100)}%`),
      select('Graphics Quality', 'quality', Object.entries(QUALITY_PRESETS).map(([k, v]) => [k, v.label])),
      toggle('Motion Blur', 'motionBlur'),
      toggle('Screen Shake', 'screenShake'),
      toggle('Show Crosshair', 'crosshair'),
      toggle('Fullscreen', 'fullscreen')));

    // Keybinds
    const bindRows = Object.entries(BIND_LABELS).map(([action, label]) => {
      const btn = el('button', { class: 'keybind', text: keyLabel(Settings.bind(action)) });
      btn.addEventListener('click', () => {
        btn.classList.add('listening');
        btn.textContent = 'PRESS A KEY';
        Input.captureNext((code) => {
          btn.classList.remove('listening');
          if (code) Settings.setBind(action, code);
          this.show('settings', { force: true });
        });
      });
      return el('div', { class: 'set-row' }, el('label', { text: label }), btn, el('span', { class: 'val', text: '' }));
    });
    rows.push(el('div', { class: 'set-group' }, el('h3', { text: 'Key bindings' }), ...bindRows));

    return el('div', { class: 'screen' }, ...frame(),
      el('div', { class: 'panel' },
        el('div', { class: 'panel-head' },
          el('div', { class: 'panel-title', html: 'SET<span>TINGS</span>' }),
          el('div', { class: 'panel-hint', text: 'Saved automatically' })),
        el('div', { class: 'panel-scroll' }, ...rows),
        el('div', { class: 'panel-actions' },
          el('button', { class: 'btn menu-item', onclick: () => this.back(), text: 'BACK' }),
          el('button', {
            class: 'btn menu-item',
            onclick: () => { Settings.resetBinds(); this.show('settings', { force: true }); },
            text: 'RESET BINDS',
          }),
          el('button', {
            class: 'btn danger menu-item',
            onclick: () => { Settings.resetAll(); this.app.applyQuality(); this.show('settings', { force: true }); },
            text: 'RESET ALL',
          }))));
  }

  // ---------------------------------------------------------------- credits
  buildCredits() {
    return el('div', { class: 'screen' }, ...frame(),
      el('div', { class: 'panel' },
        el('div', { class: 'panel-head' },
          el('div', { class: 'panel-title', html: 'CRE<span>DITS</span>' })),
        el('div', { class: 'panel-scroll' },
          el('div', { class: 'credits-scroll' },
            el('h4', { text: 'Rotgrid' }),
            el('p', { text: 'An original round-based survival shooter.' }),
            el('h4', { text: 'Design & code' }),
            el('p', { text: 'Built from scratch for this project.' }),
            el('h4', { text: 'Art' }),
            el('p', { class: 'small', text: 'Every texture, model, animation and sound in this build is generated procedurally at runtime — canvas-drawn materials, primitive-built meshes, and Web Audio synthesis. No third-party art, audio or trademarked content is used anywhere.' }),
            el('h4', { text: 'Technology' }),
            el('p', { class: 'small', html: 'three.js (MIT) for rendering · Web Audio API for synthesis · vanilla ES modules, no build step.' }),
            el('h4', { text: 'Inspiration' }),
            el('p', { class: 'small', text: 'The wave-survival loop — buy doors, restore power, upgrade weapons, chase a higher round — is a genre convention with a long lineage. Nothing here is copied from any specific title: names, map, weapons, perks and audio are original to this project.' }),
            el('h4', { text: 'Thanks' }),
            el('p', { class: 'small', text: 'To everyone who ever said "one more round".' }))),
        el('div', { class: 'panel-actions' },
          el('button', { class: 'btn menu-item', onclick: () => this.back(), text: 'BACK' }))));
  }

  // ---------------------------------------------------------------- pause
  buildPause() {
    const btn = (label, fn, cls = '') => el('button', {
      class: `btn menu-item ${cls}`, text: label,
      onclick: () => { Audio.play('ui', { kind: 'select' }); fn(); },
    });
    return el('div', { class: 'screen' },
      el('div', { class: 'pause-wrap' },
        el('div', { class: 'pause-title', text: 'PAUSED' }),
        btn('RESUME', () => this.app.resume(), 'primary'),
        btn('SETTINGS', () => this.push('settings')),
        btn('RESTART RUN', () => this.app.restart()),
        btn('ABANDON RUN', () => this.app.endToMenu(), 'danger')));
  }

  // ---------------------------------------------------------------- game over
  buildGameOver(s) {
    const stat = (k, v) => el('div', { class: 'go-stat' },
      el('div', { class: 'k', text: k }), el('div', { class: 'v', text: v }));
    return el('div', { class: 'screen' },
      el('div', { class: 'go-wrap' },
        el('div', { class: 'go-title', text: 'GAME OVER' }),
        el('div', { class: 'go-sub', text: `${MAP.name} · ${Settings.get('playerName') || 'OPERATOR'}` }),
        s.newBest ? el('div', { class: 'go-new', text: 'NEW PERSONAL BEST' }) : null,
        el('div', { class: 'go-stats' },
          stat('ROUNDS SURVIVED', String(s.roundsSurvived)),
          stat('HIGHEST ROUND', String(s.round)),
          stat('ZOMBIES KILLED', formatNumber(s.kills)),
          stat('HEADSHOTS', formatNumber(s.headshots)),
          stat('SURVIVAL TIME', formatTime(s.time)),
          stat('POINTS EARNED', formatNumber(s.pointsEarned))),
        el('div', { class: 'go-stats' },
          stat('ACCURACY', s.shotsFired ? `${((s.shotsHit / s.shotsFired) * 100).toFixed(1)}%` : '—'),
          stat('MELEE KILLS', formatNumber(s.meleeKills)),
          stat('BARRIERS FIXED', formatNumber(s.repairs))),
        el('div', { class: 'panel-actions', style: 'border:0;margin-top:18px' },
          el('button', { class: 'btn primary menu-item', text: 'RESTART', onclick: () => this.app.restart() }),
          el('button', { class: 'btn menu-item', text: 'RETURN TO MENU', onclick: () => this.app.endToMenu() }))));
  }
}

function statCard(k, v, acid = false) {
  return el('div', { class: 'stat-card' },
    el('div', { class: 'k', text: k }),
    el('div', { class: `v${acid ? ' acid' : ''}`, text: v }));
}

/* ==================================================================== */
/*  Loadout viewer — inspect every weapon with a live 3D preview.        */
/* ==================================================================== */
export class LoadoutViewer {
  constructor() {
    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(38, 1, 0.05, 20);
    this.camera.position.set(0, 0.1, 2.4);
    this.camera.lookAt(0, 0, 0);
    this.pivot = new THREE.Group();
    this.scene.add(this.pivot);

    const key = new THREE.DirectionalLight(0xffffff, 7.0);
    key.position.set(-1.4, 1.6, 1.6);
    this.scene.add(key);
    const rim = new THREE.DirectionalLight(0x9fd93a, 4.0);
    rim.position.set(1.6, 0.6, -1.4);
    this.scene.add(rim);
    this.scene.add(new THREE.AmbientLight(0x8b98a4, 4.0));

    this.model = null;
    this.selected = WEAPONS.sidearm;
    this.t = 0;
    this.viewEl = null;
  }

  attach(renderer) {
    applyEnvironment(this.scene, renderer, 0.7);
  }

  build(manager) {
    const byCat = weaponsByCategory();
    const list = el('div', { class: 'wep-list' });
    const detail = el('div', { class: 'wep-detail' });
    this.viewEl = el('div', { class: 'wep-view' });

    const renderDetail = () => {
      const w = this.selected;
      detail.innerHTML = '';
      const bars = [
        ['Damage', clamp(w.damage / 900, 0, 1), w.damage],
        ['Fire Rate', clamp(w.rpm / 1000, 0, 1), `${w.rpm} rpm`],
        ['Magazine', clamp(w.mag / 125, 0, 1), w.mag],
        ['Reserve', clamp(w.reserve / 500, 0, 1), w.reserve],
        ['Recoil', clamp(w.recoil.v / 7, 0, 1), w.recoil.v.toFixed(1)],
        ['ADS Speed', clamp(1 - w.adsTime / 0.5, 0, 1), `${w.adsTime.toFixed(2)}s`],
        ['Reload', clamp(1 - w.reloadTime / 5, 0, 1), `${w.reloadTime.toFixed(1)}s`],
        ['Mobility', clamp(w.moveScale ?? 1, 0, 1), `${Math.round((w.moveScale ?? 1) * 100)}%`],
      ];
      detail.appendChild(this.viewEl);
      detail.appendChild(el('div', { class: 'wep-meta' },
        el('div', {},
          el('div', { class: 'wep-title', text: w.name }),
          el('div', { class: 'wep-sub', text: `${w.category.toUpperCase()} · ${RARITY[w.rarity].label}` }),
          el('p', { class: 'wep-blurb', style: 'margin-top:12px', text: w.blurb || '' }),
          el('p', { class: 'wep-blurb', style: 'margin-top:10px',
            text: w.wallCost ? `Wall purchase: ${formatNumber(w.wallCost)} · Ammo: ${formatNumber(w.wallAmmoCost)}`
              : w.boxWeight ? 'Mystery crate only' : 'Standard issue' })),
        el('div', {}, ...bars.map(([k, v, raw]) => el('div', { class: 'bar-row' },
          el('span', { class: 'bk', text: k }),
          el('div', { class: 'bar-track' }, el('div', { class: 'bar-fill', style: `width:${v * 100}%` })),
          el('span', { class: 'bv', text: String(raw) }))))));
      this.setModel(w);
    };

    for (const cat of CATEGORIES) {
      const items = byCat.get(cat.id) ?? [];
      if (!items.length) continue;
      list.appendChild(el('div', { class: 'wep-cat', text: cat.label }));
      for (const w of items) {
        const btn = el('button', {
          class: `wep-btn${w === this.selected ? ' sel' : ''}`,
          onclick: () => {
            this.selected = w;
            list.querySelectorAll('.wep-btn').forEach((b) => b.classList.remove('sel'));
            btn.classList.add('sel');
            Audio.play('ui', { kind: 'move' });
            renderDetail();
          },
        }, el('span', { class: `rar ${w.rarity}` }), el('span', { text: w.name }));
        list.appendChild(btn);
      }
    }

    // Perk reference lives here too, so the screen is a real reference sheet.
    list.appendChild(el('div', { class: 'wep-cat', text: 'PERKS' }));
    for (const p of PERK_LIST) {
      list.appendChild(el('div', {
        class: 'wep-btn', style: 'cursor:default', title: p.effect,
      }, el('span', { class: 'rar uncommon', style: `background:#${p.color.toString(16).padStart(6, '0')}` }),
      el('span', { text: `${p.name} · ${formatNumber(p.cost)}` })));
    }

    const node = el('div', { class: 'screen' }, ...frame(),
      el('div', { class: 'panel' },
        el('div', { class: 'panel-head' },
          el('div', { class: 'panel-title', html: 'LOAD<span>OUT</span>' }),
          el('div', { class: 'panel-hint', text: 'Every weapon in the pool' })),
        el('div', { class: 'loadout-wrap' }, list, detail),
        el('div', { class: 'panel-actions' },
          el('button', { class: 'btn menu-item', onclick: () => manager.back(), text: 'BACK' }))));

    renderDetail();
    return node;
  }

  setModel(def) {
    if (this.model) this.pivot.remove(this.model);
    this.model = makeWeaponModel(def, 1);
    this.pivot.add(this.model);
    // Frame the weapon regardless of how long it is.
    const bounds = new THREE.Box3().setFromObject(this.model);
    const sphere = bounds.getBoundingSphere(new THREE.Sphere());
    const dist = sphere.radius / Math.tan((this.camera.fov * Math.PI) / 360) * 1.35;
    this.camera.position.set(0, sphere.center.y + 0.05, Math.max(0.8, dist));
    this.camera.lookAt(0, sphere.center.y, 0);
    this.model.position.y -= sphere.center.y;
  }

  update(dt) {
    this.t += dt;
    this.pivot.rotation.y = this.t * 0.55;
    this.pivot.rotation.x = Math.sin(this.t * 0.4) * 0.12;
    this.pivot.position.y = Math.sin(this.t * 0.8) * 0.03;
  }

  /** Screen rect of the preview box, for viewport rendering. */
  rect() {
    if (!this.viewEl || !this.viewEl.isConnected) return null;
    const r = this.viewEl.getBoundingClientRect();
    if (r.width < 8 || r.height < 8) return null;
    return r;
  }
}
