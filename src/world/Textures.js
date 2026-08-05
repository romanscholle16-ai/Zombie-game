import * as THREE from 'three';
import { seededRandom, clamp } from '../core/Util.js';

/**
 * Every texture in the game is drawn procedurally into a canvas at boot.
 * Nothing is loaded from disk, so all art here is original.
 */
const cache = new Map();

function canvas(size = 256) {
  const c = document.createElement('canvas');
  c.width = c.height = size;
  return c;
}

function finish(c, repeat = 1, aniso = 8) {
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.repeat.set(repeat, repeat);
  t.anisotropy = aniso;
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

function noiseFill(ctx, size, rng, base, spread, alpha = 1) {
  const img = ctx.getImageData(0, 0, size, size);
  const d = img.data;
  for (let i = 0; i < d.length; i += 4) {
    const n = (rng() - 0.5) * spread;
    d[i] = clamp(base[0] + n, 0, 255);
    d[i + 1] = clamp(base[1] + n, 0, 255);
    d[i + 2] = clamp(base[2] + n, 0, 255);
    d[i + 3] = 255 * alpha;
  }
  ctx.putImageData(img, 0, 0);
}

/** Soft blotches used for grime, rust and damp patches. */
function blotches(ctx, size, rng, count, color, rMin, rMax, alphaMax = 0.25) {
  for (let i = 0; i < count; i++) {
    const x = rng() * size, y = rng() * size;
    const r = rMin + rng() * (rMax - rMin);
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, `rgba(${color},${(alphaMax * (0.4 + rng() * 0.6)).toFixed(3)})`);
    g.addColorStop(1, `rgba(${color},0)`);
    ctx.fillStyle = g;
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
  }
}

function make(key, fn, repeat, size = 256) {
  if (cache.has(key)) return cache.get(key);
  const c = canvas(size);
  fn(c.getContext('2d'), size, seededRandom(hash(key)));
  const t = finish(c, repeat);
  cache.set(key, t);
  return t;
}

function hash(s) {
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); }
  return h >>> 0;
}

// ------------------------------------------------------------------ surfaces
export const Tex = {
  concrete(repeat = 4) {
    return make('concrete', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [86, 88, 84], 34);
      blotches(ctx, s, rng, 26, '40,42,38', 12, 60, 0.35);
      blotches(ctx, s, rng, 12, '120,122,116', 10, 40, 0.18);
      // Hairline cracks.
      ctx.strokeStyle = 'rgba(38,40,36,.55)';
      for (let i = 0; i < 10; i++) {
        ctx.lineWidth = rng() * 1.4 + 0.3;
        ctx.beginPath();
        let x = rng() * s, y = rng() * s;
        ctx.moveTo(x, y);
        for (let k = 0; k < 7; k++) {
          x += (rng() - 0.5) * 46; y += (rng() - 0.5) * 46;
          ctx.lineTo(x, y);
        }
        ctx.stroke();
      }
    }, repeat);
  },

  concreteFloor(repeat = 12) {
    return make('concreteFloor', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [70, 72, 69], 26);
      blotches(ctx, s, rng, 30, '32,34,30', 14, 70, 0.4);
      blotches(ctx, s, rng, 8, '96,86,60', 18, 50, 0.14);
      // Expansion joints.
      ctx.strokeStyle = 'rgba(30,32,28,.75)';
      ctx.lineWidth = 2.5;
      ctx.beginPath();
      ctx.moveTo(0, s / 2); ctx.lineTo(s, s / 2);
      ctx.moveTo(s / 2, 0); ctx.lineTo(s / 2, s);
      ctx.stroke();
    }, repeat);
  },

  tile(repeat = 8) {
    return make('tile', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [150, 156, 150], 16);
      const n = 4, cell = s / n;
      for (let i = 0; i < n; i++) {
        for (let j = 0; j < n; j++) {
          ctx.fillStyle = `rgba(${170 + rng() * 22 | 0},${176 + rng() * 20 | 0},${168 + rng() * 20 | 0},1)`;
          ctx.fillRect(i * cell + 1.5, j * cell + 1.5, cell - 3, cell - 3);
        }
      }
      blotches(ctx, s, rng, 20, '60,72,60', 8, 34, 0.3);
      blotches(ctx, s, rng, 6, '110,20,14', 6, 26, 0.35);
    }, repeat);
  },

  metalPanel(repeat = 4) {
    return make('metalPanel', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [74, 78, 82], 20);
      // Panel seams + rivets.
      ctx.strokeStyle = 'rgba(28,30,33,.85)';
      ctx.lineWidth = 3;
      ctx.strokeRect(4, 4, s - 8, s - 8);
      ctx.lineWidth = 1.6;
      ctx.beginPath(); ctx.moveTo(s / 2, 4); ctx.lineTo(s / 2, s - 4); ctx.stroke();
      ctx.fillStyle = 'rgba(140,146,150,.5)';
      for (let i = 0; i < 4; i++) {
        for (let j = 0; j < 4; j++) {
          ctx.beginPath();
          ctx.arc(18 + i * (s - 36) / 3, 18 + j * (s - 36) / 3, 2.6, 0, Math.PI * 2);
          ctx.fill();
        }
      }
      blotches(ctx, s, rng, 18, '120,64,24', 6, 30, 0.4); // rust
    }, repeat);
  },

  rustMetal(repeat = 3) {
    return make('rustMetal', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [92, 62, 40], 30);
      blotches(ctx, s, rng, 40, '150,78,28', 8, 44, 0.45);
      blotches(ctx, s, rng, 18, '48,34,26', 10, 40, 0.5);
    }, repeat);
  },

  wood(repeat = 2) {
    return make('wood', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [104, 74, 44], 18);
      // Grain lines.
      for (let i = 0; i < 90; i++) {
        const y = rng() * s;
        ctx.strokeStyle = `rgba(${58 + rng() * 40 | 0},${38 + rng() * 26 | 0},${20 + rng() * 18 | 0},${0.25 + rng() * 0.4})`;
        ctx.lineWidth = 0.6 + rng() * 2.2;
        ctx.beginPath();
        ctx.moveTo(0, y);
        for (let x = 0; x <= s; x += 16) ctx.lineTo(x, y + Math.sin(x * 0.06 + i) * 2.4);
        ctx.stroke();
      }
      blotches(ctx, s, rng, 10, '40,26,14', 8, 30, 0.3);
    }, repeat);
  },

  dirt(repeat = 10) {
    return make('dirt', (ctx, s, rng) => {
      noiseFill(ctx, s, rng, [58, 50, 40], 30);
      blotches(ctx, s, rng, 40, '30,26,20', 10, 50, 0.5);
      blotches(ctx, s, rng, 14, '82,74,56', 8, 32, 0.25);
    }, repeat);
  },

  hazard(repeat = 1) {
    return make('hazard', (ctx, s) => {
      ctx.fillStyle = '#1b1d1a';
      ctx.fillRect(0, 0, s, s);
      ctx.fillStyle = '#c9a227';
      ctx.save();
      ctx.translate(s / 2, s / 2);
      ctx.rotate(Math.PI / 4);
      for (let i = -6; i < 6; i++) ctx.fillRect(i * 44, -s, 22, s * 2);
      ctx.restore();
      ctx.fillStyle = 'rgba(0,0,0,.25)';
      for (let i = 0; i < 200; i++) {
        ctx.fillRect(Math.random() * s, Math.random() * s, 2, 2);
      }
    }, repeat);
  },

  zombieSkin(seed = 0) {
    const key = `zskin${seed % 5}`;
    return make(key, (ctx, s, rng) => {
      const tints = [[92, 106, 78], [104, 98, 80], [78, 96, 88], [110, 96, 84], [86, 92, 96]];
      noiseFill(ctx, s, rng, tints[seed % 5], 26);
      blotches(ctx, s, rng, 26, '54,26,22', 6, 30, 0.55); // wounds
      blotches(ctx, s, rng, 14, '120,132,96', 8, 26, 0.3);
      blotches(ctx, s, rng, 8, '20,16,14', 5, 18, 0.6);   // rot
    }, 1, 128);
  },

  cloth(seed = 0) {
    const key = `cloth${seed % 4}`;
    return make(key, (ctx, s, rng) => {
      const tints = [[52, 56, 60], [64, 52, 44], [42, 50, 46], [58, 46, 56]];
      noiseFill(ctx, s, rng, tints[seed % 4], 22);
      ctx.strokeStyle = 'rgba(20,20,20,.35)';
      for (let i = 0; i < s; i += 6) {
        ctx.beginPath(); ctx.moveTo(0, i); ctx.lineTo(s, i); ctx.stroke();
      }
      blotches(ctx, s, rng, 16, '90,20,16', 5, 22, 0.5);
      blotches(ctx, s, rng, 10, '18,16,14', 8, 26, 0.5);
    }, 1, 128);
  },

  /** Emissive strip / screen texture used for machines and signage. */
  glow(color = '#9fd93a') {
    const key = `glow${color}`;
    return make(key, (ctx, s) => {
      const g = ctx.createLinearGradient(0, 0, 0, s);
      g.addColorStop(0, '#000');
      g.addColorStop(0.42, color);
      g.addColorStop(0.58, '#ffffff');
      g.addColorStop(1, '#000');
      ctx.fillStyle = g;
      ctx.fillRect(0, 0, s, s);
    }, 1, 64);
  },

  /** Radial soft particle sprite (blood, smoke, sparks). */
  particle() {
    if (cache.has('particle')) return cache.get('particle');
    const c = canvas(64);
    const ctx = c.getContext('2d');
    const g = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
    g.addColorStop(0, 'rgba(255,255,255,1)');
    g.addColorStop(0.35, 'rgba(255,255,255,.55)');
    g.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, 64, 64);
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    cache.set('particle', t);
    return t;
  },

  /** Text label drawn to a texture — used for machine signage. */
  label(text, { bg = '#0d100c', fg = '#9fd93a', sub = '' } = {}) {
    const key = `label:${text}:${sub}:${fg}`;
    if (cache.has(key)) return cache.get(key);
    const c = document.createElement('canvas');
    c.width = 512; c.height = 256;
    const ctx = c.getContext('2d');
    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, 512, 256);
    ctx.strokeStyle = fg;
    ctx.lineWidth = 6;
    ctx.strokeRect(10, 10, 492, 236);
    ctx.fillStyle = fg;
    ctx.textAlign = 'center';
    ctx.font = 'bold 64px Rajdhani, sans-serif';
    const lines = String(text).split('\n');
    lines.forEach((l, i) => ctx.fillText(l, 256, 110 + i * 62 - (lines.length - 1) * 31));
    if (sub) {
      ctx.font = '30px Rajdhani, sans-serif';
      ctx.fillStyle = 'rgba(255,255,255,.65)';
      ctx.fillText(sub, 256, 200);
    }
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    t.anisotropy = 8;
    cache.set(key, t);
    return t;
  },

  dispose() {
    for (const t of cache.values()) t.dispose?.();
    cache.clear();
  },
};

/** Shared material factory so draw calls batch well. */
const matCache = new Map();
export function mat(key, props) {
  if (matCache.has(key)) return matCache.get(key);
  const m = new THREE.MeshStandardMaterial(props);
  matCache.set(key, m);
  return m;
}
export function clearMaterials() {
  for (const m of matCache.values()) m.dispose();
  matCache.clear();
}
