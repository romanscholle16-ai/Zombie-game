import { Emitter, clamp } from './Util.js';

const KEY = 'rotgrid.settings.v1';

export const DEFAULT_BINDS = {
  forward: 'KeyW',
  back: 'KeyS',
  left: 'KeyA',
  right: 'KeyD',
  jump: 'Space',
  sprint: 'ShiftLeft',
  crouch: 'ControlLeft',
  interact: 'KeyF',
  reload: 'KeyR',
  melee: 'KeyV',
  swap: 'KeyQ',
  slot1: 'Digit1',
  slot2: 'Digit2',
  fire: 'Mouse0',
  ads: 'Mouse2',
  flashlight: 'KeyL',
  scoreboard: 'Tab',
};

export const BIND_LABELS = {
  forward: 'Move Forward', back: 'Move Backward', left: 'Strafe Left', right: 'Strafe Right',
  jump: 'Jump / Mantle', sprint: 'Sprint', crouch: 'Crouch / Slide', interact: 'Interact',
  reload: 'Reload', melee: 'Melee Attack', swap: 'Swap Weapon', slot1: 'Weapon Slot 1',
  slot2: 'Weapon Slot 2', fire: 'Fire', ads: 'Aim Down Sights', flashlight: 'Flashlight',
  scoreboard: 'Scoreboard',
};

export const QUALITY_PRESETS = {
  low:    { label: 'Low',    shadows: false, shadowSize: 512,  pixelRatio: 0.75, fogDensity: 0.05,  particles: 0.4, maxZombies: 18, dynamicLights: 2,  aa: false },
  medium: { label: 'Medium', shadows: true,  shadowSize: 1024, pixelRatio: 1.0,  fogDensity: 0.042, particles: 0.8, maxZombies: 26, dynamicLights: 5,  aa: false },
  high:   { label: 'High',   shadows: true,  shadowSize: 2048, pixelRatio: 1.0,  fogDensity: 0.036, particles: 1.0, maxZombies: 32, dynamicLights: 8,  aa: true  },
  ultra:  { label: 'Ultra',  shadows: true,  shadowSize: 2048, pixelRatio: 1.35, fogDensity: 0.03,  particles: 1.5, maxZombies: 40, dynamicLights: 12, aa: true  },
};

const DEFAULTS = {
  playerName: 'OPERATOR',
  masterVolume: 0.8,
  musicVolume: 0.5,
  sfxVolume: 0.9,
  mouseSensitivity: 0.16,
  controllerSensitivity: 2.2,
  invertY: false,
  fov: 80,
  brightness: 1.0,
  motionBlur: true,
  screenShake: true,
  quality: 'high',
  fullscreen: false,
  showDamageNumbers: true,
  crosshair: true,
  binds: { ...DEFAULT_BINDS },
};

class SettingsStore extends Emitter {
  constructor() {
    super();
    this.data = structuredClone(DEFAULTS);
    this.load();
  }

  load() {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        this.data = { ...this.data, ...parsed, binds: { ...DEFAULT_BINDS, ...(parsed.binds || {}) } };
      }
    } catch { /* corrupted storage — keep defaults */ }
  }

  save() {
    try { localStorage.setItem(KEY, JSON.stringify(this.data)); } catch { /* private mode */ }
  }

  get(k) { return this.data[k]; }

  set(k, v) {
    if (k === 'fov') v = clamp(v, 60, 120);
    if (['masterVolume', 'musicVolume', 'sfxVolume'].includes(k)) v = clamp(v, 0, 1);
    if (k === 'brightness') v = clamp(v, 0.5, 2.0);
    if (k === 'mouseSensitivity') v = clamp(v, 0.02, 1.0);
    if (k === 'controllerSensitivity') v = clamp(v, 0.4, 8);
    if (this.data[k] === v) return;
    this.data[k] = v;
    this.save();
    this.emit('change', k, v);
    this.emit(`change:${k}`, v);
  }

  bind(action) { return this.data.binds[action]; }

  setBind(action, code) {
    // A code may only be used once; clear conflicts first.
    for (const [a, c] of Object.entries(this.data.binds)) {
      if (c === code && a !== action) this.data.binds[a] = null;
    }
    this.data.binds[action] = code;
    this.save();
    this.emit('change', 'binds', this.data.binds);
    this.emit('change:binds', this.data.binds);
  }

  resetBinds() {
    this.data.binds = { ...DEFAULT_BINDS };
    this.save();
    this.emit('change:binds', this.data.binds);
  }

  resetAll() {
    this.data = structuredClone(DEFAULTS);
    this.save();
    this.emit('change', '*', this.data);
  }

  get quality() { return QUALITY_PRESETS[this.data.quality] ?? QUALITY_PRESETS.high; }
}

export const Settings = new SettingsStore();

/** Human readable label for an input code. */
export function keyLabel(code) {
  if (!code) return '—';
  if (code.startsWith('Mouse')) {
    const n = +code.slice(5);
    return ['LMB', 'MMB', 'RMB', 'M4', 'M5'][n === 1 ? 1 : n === 2 ? 2 : n] ?? `M${n}`;
  }
  if (code.startsWith('Key')) return code.slice(3);
  if (code.startsWith('Digit')) return code.slice(5);
  if (code.startsWith('Numpad')) return `NUM ${code.slice(6)}`;
  if (code.startsWith('Arrow')) return code.slice(5).toUpperCase();
  return ({
    ShiftLeft: 'L SHIFT', ShiftRight: 'R SHIFT', ControlLeft: 'L CTRL', ControlRight: 'R CTRL',
    AltLeft: 'L ALT', AltRight: 'R ALT', Space: 'SPACE', Tab: 'TAB', Escape: 'ESC',
    Backquote: '`', Minus: '-', Equal: '=', BracketLeft: '[', BracketRight: ']',
    Semicolon: ';', Quote: "'", Comma: ',', Period: '.', Slash: '/', Backslash: '\\',
    CapsLock: 'CAPS', Enter: 'ENTER',
  })[code] ?? code.toUpperCase();
}
