import { Settings } from './Settings.js';
import { Emitter, clamp } from './Util.js';

const DEADZONE = 0.18;

/**
 * Unified keyboard / mouse / gamepad input.
 * Actions are resolved through the remappable bind table in Settings.
 */
class InputManager extends Emitter {
  constructor() {
    super();
    this.down = new Set();          // raw codes currently held
    this.pressed = new Set();       // codes that went down this frame
    this.released = new Set();
    this.mouseDX = 0;
    this.mouseDY = 0;
    this.wheel = 0;
    this.locked = false;
    this.enabled = false;           // gameplay input gate
    this.captureCallback = null;    // keybind rebinding hook
    this.gamepadIndex = null;
    this.pad = { lx: 0, ly: 0, rx: 0, ry: 0, buttons: new Set(), prevButtons: new Set() };
    this._bound = false;
  }

  attach(canvas) {
    if (this._bound) return;
    this._bound = true;
    this.canvas = canvas;

    window.addEventListener('keydown', this._onKeyDown);
    window.addEventListener('keyup', this._onKeyUp);
    window.addEventListener('mousedown', this._onMouseDown);
    window.addEventListener('mouseup', this._onMouseUp);
    window.addEventListener('mousemove', this._onMouseMove);
    window.addEventListener('wheel', this._onWheel, { passive: true });
    window.addEventListener('contextmenu', (e) => { if (this.locked) e.preventDefault(); });
    window.addEventListener('blur', () => this.clearHeld());
    document.addEventListener('pointerlockchange', this._onLockChange);
    window.addEventListener('gamepadconnected', (e) => { this.gamepadIndex = e.gamepad.index; this.emit('gamepad', true); });
    window.addEventListener('gamepaddisconnected', () => { this.gamepadIndex = null; this.emit('gamepad', false); });
  }

  // ---------------------------------------------------------------- pointer lock
  requestLock() {
    if (this.locked || !this.canvas) return;
    const p = this.canvas.requestPointerLock?.();
    if (p && typeof p.catch === 'function') p.catch(() => {});
  }

  releaseLock() {
    if (document.pointerLockElement) document.exitPointerLock();
  }

  _onLockChange = () => {
    this.locked = document.pointerLockElement === this.canvas;
    this.emit('lockchange', this.locked);
    if (!this.locked) this.clearHeld();
  };

  // ---------------------------------------------------------------- raw events
  _onKeyDown = (e) => {
    if (this.captureCallback) {
      e.preventDefault();
      const cb = this.captureCallback;
      this.captureCallback = null;
      cb(e.code === 'Escape' ? null : e.code);
      return;
    }
    if (e.code === 'Tab' || (e.code === 'Space' && this.locked)) e.preventDefault();
    if (e.repeat) return;
    this.down.add(e.code);
    this.pressed.add(e.code);
    this.emit('keydown', e.code, e);
  };

  _onKeyUp = (e) => {
    this.down.delete(e.code);
    this.released.add(e.code);
    this.emit('keyup', e.code);
  };

  _onMouseDown = (e) => {
    const code = `Mouse${e.button}`;
    if (this.captureCallback) {
      e.preventDefault();
      const cb = this.captureCallback;
      this.captureCallback = null;
      cb(code);
      return;
    }
    this.down.add(code);
    this.pressed.add(code);
    this.emit('mousedown', code, e);
  };

  _onMouseUp = (e) => {
    const code = `Mouse${e.button}`;
    this.down.delete(code);
    this.released.add(code);
  };

  _onMouseMove = (e) => {
    if (!this.locked) return;
    this.mouseDX += e.movementX || 0;
    this.mouseDY += e.movementY || 0;
  };

  _onWheel = (e) => { this.wheel += Math.sign(e.deltaY); };

  clearHeld() {
    this.down.clear();
    this.pad.buttons.clear();
  }

  /** Capture the next key/mouse press for rebinding. */
  captureNext(cb) { this.captureCallback = cb; }

  // ---------------------------------------------------------------- queries
  isDown(action) {
    const code = Settings.bind(action);
    if (code && this.down.has(code)) return true;
    return this._padAction(action, false);
  }

  wasPressed(action) {
    const code = Settings.bind(action);
    if (code && this.pressed.has(code)) return true;
    return this._padAction(action, true);
  }

  rawDown(code) { return this.down.has(code); }
  rawPressed(code) { return this.pressed.has(code); }

  _padAction(action, edge) {
    const b = PAD_MAP[action];
    if (b === undefined) return false;
    if (Array.isArray(b)) return b.some((x) => this._padBtn(x, edge));
    return this._padBtn(b, edge);
  }

  _padBtn(i, edge) {
    if (edge) return this.pad.buttons.has(i) && !this.pad.prevButtons.has(i);
    return this.pad.buttons.has(i);
  }

  /** Movement axes from keyboard + left stick, normalised. */
  moveAxis() {
    let x = 0, y = 0;
    if (this.isDown('forward')) y += 1;
    if (this.isDown('back')) y -= 1;
    if (this.isDown('right')) x += 1;
    if (this.isDown('left')) x -= 1;
    x += this.pad.lx;
    y += -this.pad.ly;
    const len = Math.hypot(x, y);
    if (len > 1) { x /= len; y /= len; }
    return { x, y };
  }

  /** Look delta in radians for this frame. */
  lookDelta(dt) {
    const sens = Settings.get('mouseSensitivity');
    const inv = Settings.get('invertY') ? -1 : 1;
    let yaw = -this.mouseDX * sens * 0.0022;
    let pitch = -this.mouseDY * sens * 0.0022 * inv;

    const cs = Settings.get('controllerSensitivity');
    const curve = (v) => Math.sign(v) * v * v;   // quadratic response for fine aim
    yaw += -curve(this.pad.rx) * cs * dt;
    pitch += -curve(this.pad.ry) * cs * dt * inv;

    // Arrow keys always steer, so the game stays playable anywhere pointer
    // lock is unavailable (embedded frames, restricted browsers).
    const ARROW = 2.2 * dt;
    if (this.down.has('ArrowLeft')) yaw += ARROW;
    if (this.down.has('ArrowRight')) yaw -= ARROW;
    if (this.down.has('ArrowUp')) pitch += ARROW * inv;
    if (this.down.has('ArrowDown')) pitch -= ARROW * inv;

    this.mouseDX = 0;
    this.mouseDY = 0;
    return { yaw, pitch };
  }

  // ---------------------------------------------------------------- per-frame
  poll() {
    const pads = navigator.getGamepads ? navigator.getGamepads() : [];
    const gp = this.gamepadIndex !== null ? pads[this.gamepadIndex] : Array.from(pads).find(Boolean);
    this.pad.prevButtons = new Set(this.pad.buttons);
    this.pad.buttons = new Set();
    if (gp) {
      const dz = (v) => (Math.abs(v) < DEADZONE ? 0 : (v - Math.sign(v) * DEADZONE) / (1 - DEADZONE));
      this.pad.lx = dz(gp.axes[0] || 0);
      this.pad.ly = dz(gp.axes[1] || 0);
      this.pad.rx = dz(gp.axes[2] || 0);
      this.pad.ry = dz(gp.axes[3] || 0);
      gp.buttons.forEach((b, i) => { if (b.pressed || b.value > 0.4) this.pad.buttons.add(i); });
      this.padConnected = true;
    } else {
      this.pad.lx = this.pad.ly = this.pad.rx = this.pad.ry = 0;
      this.padConnected = false;
    }
  }

  endFrame() {
    this.pressed.clear();
    this.released.clear();
    this.wheel = 0;
  }
}

// Standard gamepad mapping (Xbox-style layout indices).
const PAD_MAP = {
  fire: 7, ads: 6, jump: 0, melee: 1, crouch: 10, sprint: 11,
  // X doubles as reload/interact (contextual), Y swaps weapons.
  reload: 2, interact: 2, swap: 3, slot1: 14, slot2: 15, scoreboard: 8,
};

export const Input = new InputManager();
export { clamp };
