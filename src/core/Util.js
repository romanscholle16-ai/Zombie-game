// Small shared helpers. No dependencies.

export const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
export const lerp = (a, b, t) => a + (b - a) * t;
export const invLerp = (a, b, v) => (b === a ? 0 : (v - a) / (b - a));
export const smoothstep = (t) => t * t * (3 - 2 * t);
export const rand = (a = 1, b) => (b === undefined ? Math.random() * a : a + Math.random() * (b - a));
export const randInt = (a, b) => Math.floor(rand(a, b + 1));
export const pick = (arr) => arr[Math.floor(Math.random() * arr.length)];
export const shuffle = (arr) => {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
};
export const TAU = Math.PI * 2;

/** Frame-rate independent exponential smoothing. */
export const damp = (a, b, lambda, dt) => lerp(a, b, 1 - Math.exp(-lambda * dt));

/** Wrap an angle into [-PI, PI]. */
export function wrapAngle(a) {
  while (a > Math.PI) a -= TAU;
  while (a < -Math.PI) a += TAU;
  return a;
}

/** Deterministic 32-bit PRNG (mulberry32) — used for repeatable map detail. */
export function seededRandom(seed) {
  let t = seed >>> 0;
  return function () {
    t = (t + 0x6d2b79f5) >>> 0;
    let x = Math.imul(t ^ (t >>> 15), 1 | t);
    x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x;
    return ((x ^ (x >>> 14)) >>> 0) / 4294967296;
  };
}

export function formatTime(sec) {
  sec = Math.max(0, Math.floor(sec));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = sec % 60;
  const pad = (n) => String(n).padStart(2, '0');
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
}

export function formatNumber(n) {
  return Math.floor(n).toLocaleString('en-US');
}

/** Weighted pick: entries is [{weight, ...}] */
export function weightedPick(entries, rng = Math.random) {
  let total = 0;
  for (const e of entries) total += e.weight;
  let r = rng() * total;
  for (const e of entries) {
    r -= e.weight;
    if (r <= 0) return e;
  }
  return entries[entries.length - 1];
}

/** Minimal event emitter. */
export class Emitter {
  constructor() { this._m = new Map(); }
  on(evt, fn) {
    if (!this._m.has(evt)) this._m.set(evt, new Set());
    this._m.get(evt).add(fn);
    return () => this.off(evt, fn);
  }
  off(evt, fn) { this._m.get(evt)?.delete(fn); }
  emit(evt, ...args) {
    const s = this._m.get(evt);
    if (!s) return;
    for (const fn of Array.from(s)) fn(...args);
  }
  clear() { this._m.clear(); }
}

/** Reusable object pool. */
export class Pool {
  constructor(factory, reset) {
    this.factory = factory;
    this.reset = reset;
    this.free = [];
    this.used = [];
  }
  acquire(...args) {
    const o = this.free.pop() ?? this.factory();
    this.reset?.(o, ...args);
    this.used.push(o);
    return o;
  }
  release(o) {
    const i = this.used.indexOf(o);
    if (i >= 0) this.used.splice(i, 1);
    this.free.push(o);
  }
  get active() { return this.used; }
}

export function el(tag, attrs = {}, ...children) {
  const n = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === 'class') n.className = v;
    else if (k === 'html') n.innerHTML = v;
    else if (k === 'text') n.textContent = v;
    else if (k.startsWith('on') && typeof v === 'function') n.addEventListener(k.slice(2), v);
    else if (v !== null && v !== undefined && v !== false) n.setAttribute(k, v === true ? '' : v);
  }
  for (const c of children.flat()) {
    if (c === null || c === undefined || c === false) continue;
    n.appendChild(typeof c === 'string' ? document.createTextNode(c) : c);
  }
  return n;
}
