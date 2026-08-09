import { Settings } from './Settings.js';
import { clamp, el } from './Util.js';

/**
 * On-screen controls for phones and tablets.
 *
 * Layout follows what a thumb can actually reach on a phone held in landscape:
 * a floating movement stick under the left thumb, free look anywhere on the
 * right, and a fire button in the bottom corner with the secondary actions
 * arced above it. Nothing is fixed in place except the buttons — the movement
 * stick springs up wherever the left thumb lands, which is what stops it
 * drifting out from under you mid-round.
 *
 * State is written into `Input.touch`, so gameplay code keeps asking the same
 * `isDown('fire')` questions it asks of a keyboard or a gamepad.
 */

/** Actions that get a button, in the order they arc away from the fire key. */
const BUTTONS = [
  { action: 'fire', label: 'FIRE', cls: 'tb-fire' },
  { action: 'ads', label: 'AIM', cls: 'tb-ads' },
  { action: 'reload', label: 'RELOAD', cls: 'tb-reload' },
  { action: 'jump', label: 'JUMP', cls: 'tb-jump' },
  { action: 'melee', label: 'KNIFE', cls: 'tb-melee' },
  { action: 'crouch', label: 'CROUCH', cls: 'tb-crouch' },
  { action: 'swap', label: 'SWAP', cls: 'tb-swap' },
  { action: 'interact', label: 'USE', cls: 'tb-use' },
];

const STICK_RADIUS = 62;      // px, at the reference DPI
const SPRINT_AT = 0.82;       // stick deflection that starts a sprint

export class TouchControls {
  constructor(input) {
    this.input = input;
    this.enabled = false;
    this.root = null;
    this.buttons = new Map();
    // Live pointers, keyed by pointerId, so several thumbs work at once.
    this.pointers = new Map();
    this.stick = { active: false, id: null, ox: 0, oy: 0, x: 0, y: 0 };
    this._onDown = this._onDown.bind(this);
    this._onMove = this._onMove.bind(this);
    this._onUp = this._onUp.bind(this);
  }

  /** True when this looks like a device that wants touch controls. */
  static shouldEnable() {
    if (Settings.get('touchControls') === 'on') return true;
    if (Settings.get('touchControls') === 'off') return false;
    const coarse = window.matchMedia?.('(pointer: coarse)').matches;
    const touch = navigator.maxTouchPoints > 0 || 'ontouchstart' in window;
    return !!(coarse && touch);
  }

  mount() {
    if (this.root) return;
    const left = Settings.get('touchLefty');
    this.root = el('div', { class: `touch-layer${left ? ' lefty' : ''}`, id: 'touch-layer' });

    this.stickEl = el('div', { class: 'tstick' },
      el('div', { class: 'tstick-ring' }),
      el('div', { class: 'tstick-nub' }));
    this.stickEl.style.display = 'none';
    this.root.appendChild(this.stickEl);
    this.stickNub = this.stickEl.querySelector('.tstick-nub');

    this.btnWrap = el('div', { class: 'tbuttons' });
    for (const b of BUTTONS) {
      const node = el('button', {
        class: `tbtn ${b.cls}`, 'data-action': b.action, type: 'button',
        'aria-label': b.label,
      }, el('span', { text: b.label }));
      this.btnWrap.appendChild(node);
      this.buttons.set(b.action, node);
    }
    this.root.appendChild(this.btnWrap);

    // Pause sits opposite the fire hand so it is never hit by accident.
    this.pauseBtn = el('button', { class: 'tbtn tb-pause', type: 'button', 'aria-label': 'Pause' },
      el('span', { text: 'II' }));
    this.root.appendChild(this.pauseBtn);

    document.getElementById('app').appendChild(this.root);

    this.root.addEventListener('pointerdown', this._onDown, { passive: false });
    this.root.addEventListener('pointermove', this._onMove, { passive: false });
    this.root.addEventListener('pointerup', this._onUp, { passive: false });
    this.root.addEventListener('pointercancel', this._onUp, { passive: false });
  }

  setEnabled(on) {
    this.enabled = on;
    if (on) this.mount();
    if (this.root) this.root.classList.toggle('on', on);
    if (!on) this.releaseAll();
  }

  /** The USE button only appears when there is something to use. */
  setInteractVisible(on) {
    this.buttons.get('interact')?.classList.toggle('avail', !!on);
  }

  releaseAll() {
    this.pointers.clear();
    this.stick.active = false;
    this.stick.x = this.stick.y = 0;
    if (this.stickEl) this.stickEl.style.display = 'none';
    for (const node of this.buttons.values()) node.classList.remove('held');
    const t = this.input.touch;
    t.lx = t.ly = t.dx = t.dy = 0;
    t.buttons.clear();
  }

  // ------------------------------------------------------------------ events
  _actionAt(target) {
    const btn = target?.closest?.('.tbtn');
    return btn?.dataset?.action ?? null;
  }

  _onDown(e) {
    if (!this.enabled) return;
    if (e.target === this.pauseBtn || this.pauseBtn.contains(e.target)) {
      e.preventDefault();
      this.onPause?.();
      return;
    }
    const action = this._actionAt(e.target);
    e.preventDefault();

    if (action) {
      this.pointers.set(e.pointerId, { kind: 'button', action });
      this.input.touch.buttons.add(action);
      this.buttons.get(action)?.classList.add('held');
      return;
    }

    const leftHalf = Settings.get('touchLefty')
      ? e.clientX > window.innerWidth * 0.5
      : e.clientX < window.innerWidth * 0.45;

    if (leftHalf && !this.stick.active) {
      this.stick.active = true;
      this.stick.id = e.pointerId;
      this.stick.ox = e.clientX;
      this.stick.oy = e.clientY;
      this.stickEl.style.display = 'block';
      this.stickEl.style.left = `${e.clientX}px`;
      this.stickEl.style.top = `${e.clientY}px`;
      this.stickNub.style.transform = 'translate(-50%, -50%)';
      this.pointers.set(e.pointerId, { kind: 'stick' });
    } else {
      this.pointers.set(e.pointerId, { kind: 'look', lx: e.clientX, ly: e.clientY, moved: 0 });
    }
  }

  _onMove(e) {
    const p = this.pointers.get(e.pointerId);
    if (!p) return;
    e.preventDefault();

    if (p.kind === 'stick') {
      const dx = e.clientX - this.stick.ox;
      const dy = e.clientY - this.stick.oy;
      const len = Math.hypot(dx, dy);
      const r = STICK_RADIUS;
      const k = len > r ? r / len : 1;
      const nx = (dx * k) / r, ny = (dy * k) / r;
      this.stick.x = nx;
      this.stick.y = ny;
      this.stickNub.style.transform =
        `translate(calc(-50% + ${(dx * k).toFixed(1)}px), calc(-50% + ${(dy * k).toFixed(1)}px))`;
      const t = this.input.touch;
      t.lx = nx;
      t.ly = ny;
      // Pushing the stick to its edge is the sprint gesture; there is no room
      // on screen for a button nobody would take their thumb off fire to press.
      if (Math.hypot(nx, ny) > SPRINT_AT && ny < -0.25) t.buttons.add('sprint');
      else t.buttons.delete('sprint');
      return;
    }

    if (p.kind === 'look') {
      const dx = e.clientX - p.lx;
      const dy = e.clientY - p.ly;
      p.lx = e.clientX;
      p.ly = e.clientY;
      p.moved += Math.abs(dx) + Math.abs(dy);
      this.input.touch.dx += dx;
      this.input.touch.dy += dy;
    }
  }

  _onUp(e) {
    const p = this.pointers.get(e.pointerId);
    if (!p) return;
    this.pointers.delete(e.pointerId);
    e.preventDefault();

    if (p.kind === 'button') {
      this.input.touch.buttons.delete(p.action);
      this.buttons.get(p.action)?.classList.remove('held');
      return;
    }
    if (p.kind === 'stick') {
      this.stick.active = false;
      this.stick.x = this.stick.y = 0;
      this.stickEl.style.display = 'none';
      const t = this.input.touch;
      t.lx = t.ly = 0;
      t.buttons.delete('sprint');
      return;
    }
    // A tap on the look area that never really moved fires a shot, so you can
    // shoot with the aiming thumb without reaching for the button.
    if (p.kind === 'look' && p.moved < 12 && Settings.get('touchTapFire')) {
      this.input.touch.tapFire = 2;   // held for two frames so the edge is seen
    }
  }
}
