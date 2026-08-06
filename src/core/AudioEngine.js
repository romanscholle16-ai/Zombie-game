import { Settings } from './Settings.js';
import { clamp, rand, pick } from './Util.js';

/**
 * Fully procedural audio. Every sound is synthesised at runtime with the Web Audio
 * API — there are no sample files, so nothing is licensed from anyone.
 */
class AudioEngine {
  constructor() {
    this.ctx = null;
    this.ready = false;
    this.listener = { x: 0, y: 0, z: 0, fx: 0, fz: -1, rx: 1, rz: 0 };
    this.musicNodes = [];
    this.ambientNodes = [];
    this._lastGroan = 0;
  }

  init() {
    if (this.ctx) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    this.ctx = new AC();

    this.master = this.ctx.createGain();
    this.comp = this.ctx.createDynamicsCompressor();
    this.comp.threshold.value = -14;
    this.comp.knee.value = 26;
    this.comp.ratio.value = 8;
    this.comp.attack.value = 0.004;
    this.comp.release.value = 0.22;
    this.comp.connect(this.master);
    this.master.connect(this.ctx.destination);

    this.sfxBus = this.ctx.createGain();
    this.musicBus = this.ctx.createGain();
    this.sfxBus.connect(this.comp);
    this.musicBus.connect(this.comp);

    // Shared reverb for a damp, concrete-facility feel.
    this.reverb = this.ctx.createConvolver();
    this.reverb.buffer = this._impulse(2.1, 2.6);
    this.reverbGain = this.ctx.createGain();
    this.reverbGain.gain.value = 0.26;
    this.reverb.connect(this.reverbGain);
    this.reverbGain.connect(this.comp);

    this.noiseBuf = this._noise(2.0);
    this.applyVolumes();
    this.ready = true;

    Settings.on('change', (k) => {
      if (k.endsWith('Volume') || k === '*') this.applyVolumes();
    });
  }

  resume() {
    this.init();
    if (this.ctx?.state === 'suspended') this.ctx.resume();
  }

  applyVolumes() {
    if (!this.ctx) return;
    const m = Settings.get('masterVolume');
    this.master.gain.setTargetAtTime(m, this.ctx.currentTime, 0.05);
    this.sfxBus.gain.setTargetAtTime(Settings.get('sfxVolume'), this.ctx.currentTime, 0.05);
    this.musicBus.gain.setTargetAtTime(Settings.get('musicVolume') * 0.55, this.ctx.currentTime, 0.1);
  }

  // ------------------------------------------------------------------ buffers
  _noise(seconds) {
    const n = Math.floor(this.ctx.sampleRate * seconds);
    const buf = this.ctx.createBuffer(1, n, this.ctx.sampleRate);
    const d = buf.getChannelData(0);
    for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
    return buf;
  }

  _impulse(seconds, decay) {
    const rate = this.ctx.sampleRate;
    const n = Math.floor(rate * seconds);
    const buf = this.ctx.createBuffer(2, n, rate);
    for (let c = 0; c < 2; c++) {
      const d = buf.getChannelData(c);
      for (let i = 0; i < n; i++) {
        d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / n, decay);
      }
    }
    return buf;
  }

  // ------------------------------------------------------------------ helpers
  get t() { return this.ctx.currentTime; }

  _out(gainNode, opts = {}) {
    const { pan = 0, reverb = 0.2 } = opts;
    let node = gainNode;
    if (pan !== 0 && this.ctx.createStereoPanner) {
      const p = this.ctx.createStereoPanner();
      p.pan.value = clamp(pan, -1, 1);
      node.connect(p);
      node = p;
    }
    node.connect(this.sfxBus);
    if (reverb > 0) {
      const rg = this.ctx.createGain();
      rg.gain.value = reverb;
      node.connect(rg);
      rg.connect(this.reverb);
    }
    return node;
  }

  _noiseSource(dur, playbackRate = 1) {
    const s = this.ctx.createBufferSource();
    s.buffer = this.noiseBuf;
    s.playbackRate.value = playbackRate;
    s.loop = true;
    s.start(this.t, rand(0, 1.5));
    s.stop(this.t + dur + 0.05);
    return s;
  }

  /** Spatialise a world position into {gain, pan} relative to the listener. */
  spatial(pos, maxDist = 46) {
    const l = this.listener;
    const dx = pos.x - l.x, dy = (pos.y ?? 0) - l.y, dz = pos.z - l.z;
    const dist = Math.hypot(dx, dy, dz);
    const gain = clamp(1 - dist / maxDist, 0, 1) ** 1.7;
    const pan = clamp((dx * l.rx + dz * l.rz) / Math.max(1.2, dist), -1, 1);
    return { gain, pan, dist };
  }

  setListener(pos, forward) {
    this.listener.x = pos.x; this.listener.y = pos.y; this.listener.z = pos.z;
    this.listener.fx = forward.x; this.listener.fz = forward.z;
    // Right vector = forward rotated -90 degrees around Y.
    this.listener.rx = -forward.z;
    this.listener.rz = forward.x;
  }

  // ------------------------------------------------------------------ SFX
  play(name, opts = {}) {
    if (!this.ready) return;
    if (this.ctx.state === 'suspended') return;
    const fn = this[`sfx_${name}`];
    if (fn) fn.call(this, opts);
  }

  playAt(name, pos, opts = {}) {
    if (!this.ready) return;
    const s = this.spatial(pos, opts.maxDist ?? 46);
    if (s.gain <= 0.005) return;
    this.play(name, { ...opts, pan: s.pan, volume: (opts.volume ?? 1) * s.gain });
  }

  // --- weapons -------------------------------------------------------------
  /** Fallback voices, used when a weapon has no entry of its own. */
  static WEIGHT_VOICES = {
    light:  { crackHi: 3600, crackLo: 240, crackDur: 0.14, body: 150, punch: 0.55, tailDur: 0.2 },
    medium: { crackHi: 3000, crackLo: 180, crackDur: 0.2,  body: 110, punch: 0.8,  tailDur: 0.3 },
    heavy:  { crackHi: 2400, crackLo: 120, crackDur: 0.3,  body: 78,  punch: 1.1,  tailDur: 0.5 },
    shotgun:{ crackHi: 2000, crackLo: 90,  crackDur: 0.4,  body: 62,  punch: 1.3,  tailDur: 0.6 },
    energy: { crackHi: 5200, crackLo: 400, crackDur: 0.3,  body: 220, punch: 0.9,  tailDur: 0.5,
      bodyType: 'sawtooth' },
  };

  /**
   * A gunshot, layered: action noise, supersonic crack, pressure body, a
   * resonant peak that gives the weapon its character, and room tail.
   * `voice` comes from WeaponVoices; `weight` is the coarse fallback.
   */
  sfx_shoot({ volume = 1, pan = 0, weight = 'medium', suppressed = false, voice = null } = {}) {
    const t = this.t;
    const p = {
      mech: 0.22, mechF: 3200, mechDur: 0.035,
      crackQ: 0.9, crackLevel: 0.9, bodyMul: 2.2, bodyDur: 0.18, bodyType: 'triangle',
      ring: 0, ringF: 900, ringQ: 9, ringDur: 0.14,
      tail: 0.45, tailF: 700, detune: 0.03,
      ...(AudioEngine.WEIGHT_VOICES[weight] ?? AudioEngine.WEIGHT_VOICES.medium),
      ...(voice ?? {}),
    };
    const v = volume * (suppressed ? 0.4 : 1);
    const jitter = 1 + (Math.random() - 0.5) * (p.detune ?? 0.03);

    // ---- capacitor charge, for the weapons that have one
    if (p.charge) {
      const c = this.ctx.createOscillator();
      c.type = 'sawtooth';
      c.frequency.setValueAtTime(p.charge.from, t - p.charge.dur);
      c.frequency.exponentialRampToValueAtTime(p.charge.to, t);
      const cg = this.ctx.createGain();
      cg.gain.setValueAtTime(0.0001, t - p.charge.dur);
      cg.gain.exponentialRampToValueAtTime(p.charge.level * v, t - 0.005);
      cg.gain.exponentialRampToValueAtTime(0.0001, t + 0.02);
      c.connect(cg);
      this._out(cg, { pan, reverb: 0.2 });
      c.start(Math.max(this.ctx.currentTime, t - p.charge.dur));
      c.stop(t + 0.06);
    }

    // ---- mechanical action: the sear breaking, the bolt travelling
    if (p.mech > 0.001) {
      const mn = this._noiseSource(p.mechDur, 1.4);
      const mf = this.ctx.createBiquadFilter();
      mf.type = 'bandpass';
      mf.frequency.value = p.mechF * jitter;
      mf.Q.value = 1.6;
      const mg = this.ctx.createGain();
      mg.gain.setValueAtTime(p.mech * v, t);
      mg.gain.exponentialRampToValueAtTime(0.0001, t + p.mechDur);
      mn.connect(mf); mf.connect(mg);
      this._out(mg, { pan, reverb: 0.08 });
    }

    // ---- crack: the bullet's shockwave, sweeping down as it leaves
    const n = this._noiseSource(p.crackDur, 1);
    const bp = this.ctx.createBiquadFilter();
    bp.type = 'bandpass';
    bp.frequency.setValueAtTime(p.crackHi * jitter, t);
    bp.frequency.exponentialRampToValueAtTime(p.crackLo, t + p.crackDur);
    bp.Q.value = suppressed ? 3.2 : p.crackQ;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(p.crackLevel * v, t + 0.004);
    g.gain.exponentialRampToValueAtTime(0.0001, t + p.crackDur);
    n.connect(bp); bp.connect(g);
    this._out(g, { pan, reverb: suppressed ? 0.1 : 0.45 });

    // ---- body: the pressure pulse that gives the shot its weight
    const o = this.ctx.createOscillator();
    o.type = p.bodyType;
    o.frequency.setValueAtTime(p.body * p.bodyMul * jitter, t);
    o.frequency.exponentialRampToValueAtTime(p.body * 0.5, t + p.bodyDur * 0.85);
    const og = this.ctx.createGain();
    og.gain.setValueAtTime(p.punch * 0.5 * v, t);
    og.gain.exponentialRampToValueAtTime(0.0001, t + p.bodyDur);
    o.connect(og);
    this._out(og, { pan, reverb: 0.25 });
    o.start(t); o.stop(t + p.bodyDur + 0.05);

    // ---- ring: barrel harmonic, receiver clang, or capacitor whine
    if (p.ring > 0.001) {
      const rn = this._noiseSource(p.ringDur, 1);
      const rf = this.ctx.createBiquadFilter();
      rf.type = 'bandpass';
      rf.frequency.value = p.ringF * jitter;
      rf.Q.value = p.ringQ;
      const rg = this.ctx.createGain();
      rg.gain.setValueAtTime(p.ring * v, t + 0.004);
      rg.gain.exponentialRampToValueAtTime(0.0001, t + p.ringDur);
      rn.connect(rf); rf.connect(rg);
      this._out(rg, { pan, reverb: 0.4 });
    }

    // ---- tail: the room answering back
    if (p.tail > 0.001 && !suppressed) {
      const tn = this._noiseSource(p.tailDur, 0.7);
      const tf = this.ctx.createBiquadFilter();
      tf.type = 'lowpass';
      tf.frequency.setValueAtTime(p.tailF, t);
      tf.frequency.exponentialRampToValueAtTime(Math.max(90, p.tailF * 0.25), t + p.tailDur);
      const tg = this.ctx.createGain();
      tg.gain.setValueAtTime(0.0001, t);
      tg.gain.exponentialRampToValueAtTime(p.tail * 0.32 * v, t + 0.03);
      tg.gain.exponentialRampToValueAtTime(0.0001, t + p.tailDur);
      tn.connect(tf); tf.connect(tg);
      this._out(tg, { pan, reverb: 0.85 });
    }
  }

  sfx_dryfire({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.05);
    const f = this.ctx.createBiquadFilter();
    f.type = 'highpass'; f.frequency.value = 2200;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.35 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.05);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.05 });
  }

  sfx_reload({ volume = 1, pan = 0, stage = 0 } = {}) {
    const t = this.t;
    const freqs = [900, 620, 1500];
    const o = this.ctx.createOscillator();
    o.type = 'square';
    o.frequency.setValueAtTime(freqs[stage % 3], t);
    o.frequency.exponentialRampToValueAtTime(freqs[stage % 3] * 0.55, t + 0.06);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.12 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.08);
    const n = this._noiseSource(0.08);
    const hp = this.ctx.createBiquadFilter();
    hp.type = 'highpass'; hp.frequency.value = 1400;
    const ng = this.ctx.createGain();
    ng.gain.setValueAtTime(0.16 * volume, t);
    ng.gain.exponentialRampToValueAtTime(0.0001, t + 0.07);
    o.connect(g); n.connect(hp); hp.connect(ng);
    this._out(g, { pan, reverb: 0.12 });
    this._out(ng, { pan, reverb: 0.12 });
    o.start(t); o.stop(t + 0.1);
  }

  sfx_melee({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.18);
    const f = this.ctx.createBiquadFilter();
    f.type = 'bandpass'; f.frequency.setValueAtTime(1800, t);
    f.frequency.exponentialRampToValueAtTime(420, t + 0.16);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.5 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.2 });
  }

  // --- impacts -------------------------------------------------------------
  sfx_hit({ volume = 1, pan = 0, crit = false } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.12, crit ? 1.5 : 1);
    const f = this.ctx.createBiquadFilter();
    f.type = 'lowpass';
    f.frequency.setValueAtTime(crit ? 2600 : 1200, t);
    f.frequency.exponentialRampToValueAtTime(300, t + 0.1);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.4 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.12);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.14 });
  }

  sfx_hitmarker({ volume = 1, crit = false } = {}) {
    const t = this.t;
    const o = this.ctx.createOscillator();
    o.type = 'square';
    o.frequency.value = crit ? 1750 : 1280;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.075 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.055);
    o.connect(g);
    g.connect(this.sfxBus);
    o.start(t); o.stop(t + 0.06);
  }

  sfx_ricochet({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const o = this.ctx.createOscillator();
    o.type = 'sine';
    o.frequency.setValueAtTime(rand(1800, 3200), t);
    o.frequency.exponentialRampToValueAtTime(rand(400, 800), t + 0.22);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.09 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.24);
    o.connect(g);
    this._out(g, { pan, reverb: 0.4 });
    o.start(t); o.stop(t + 0.26);
  }

  sfx_explosion({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(1.2, 0.6);
    const lp = this.ctx.createBiquadFilter();
    lp.type = 'lowpass';
    lp.frequency.setValueAtTime(1800, t);
    lp.frequency.exponentialRampToValueAtTime(90, t + 0.9);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(1.0 * volume, t + 0.02);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 1.1);
    n.connect(lp); lp.connect(g);
    this._out(g, { pan, reverb: 0.7 });

    const o = this.ctx.createOscillator();
    o.type = 'sine';
    o.frequency.setValueAtTime(90, t);
    o.frequency.exponentialRampToValueAtTime(24, t + 0.6);
    const og = this.ctx.createGain();
    og.gain.setValueAtTime(0.8 * volume, t);
    og.gain.exponentialRampToValueAtTime(0.0001, t + 0.7);
    o.connect(og);
    this._out(og, { pan, reverb: 0.3 });
    o.start(t); o.stop(t + 0.75);
  }

  // --- zombies -------------------------------------------------------------
  sfx_groan({ volume = 1, pan = 0, pitch = 1 } = {}) {
    const t = this.t;
    const dur = rand(0.6, 1.3);
    const base = rand(58, 96) * pitch;
    const carrier = this.ctx.createOscillator();
    carrier.type = 'sawtooth';
    carrier.frequency.setValueAtTime(base, t);
    carrier.frequency.linearRampToValueAtTime(base * rand(0.7, 1.25), t + dur);

    // Two formant band-passes give a throat-like vowel.
    const f1 = this.ctx.createBiquadFilter();
    f1.type = 'bandpass'; f1.frequency.value = rand(420, 620); f1.Q.value = 5;
    const f2 = this.ctx.createBiquadFilter();
    f2.type = 'bandpass'; f2.frequency.value = rand(950, 1500); f2.Q.value = 7;
    const mix = this.ctx.createGain();

    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.28 * volume, t + dur * 0.25);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);

    const breath = this._noiseSource(dur);
    const bf = this.ctx.createBiquadFilter();
    bf.type = 'bandpass'; bf.frequency.value = rand(700, 1600); bf.Q.value = 1.2;
    const bg = this.ctx.createGain();
    bg.gain.setValueAtTime(0.055 * volume, t);
    bg.gain.exponentialRampToValueAtTime(0.0001, t + dur);

    carrier.connect(f1); carrier.connect(f2);
    f1.connect(mix); f2.connect(mix); mix.connect(g);
    breath.connect(bf); bf.connect(bg);
    this._out(g, { pan, reverb: 0.5 });
    this._out(bg, { pan, reverb: 0.5 });
    carrier.start(t); carrier.stop(t + dur + 0.05);
  }

  sfx_zdeath({ volume = 1, pan = 0 } = {}) {
    this.sfx_groan({ volume: volume * 1.1, pan, pitch: 0.65 });
    const t = this.t;
    const n = this._noiseSource(0.35, 0.7);
    const f = this.ctx.createBiquadFilter();
    f.type = 'lowpass'; f.frequency.setValueAtTime(900, t);
    f.frequency.exponentialRampToValueAtTime(120, t + 0.3);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.3 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.35);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.3 });
  }

  sfx_zattack({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.22, 1.4);
    const f = this.ctx.createBiquadFilter();
    f.type = 'bandpass'; f.frequency.setValueAtTime(2400, t);
    f.frequency.exponentialRampToValueAtTime(300, t + 0.2);
    f.Q.value = 1.4;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.42 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.22);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.25 });
  }

  // --- world ---------------------------------------------------------------
  sfx_board({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.3, rand(0.8, 1.3));
    const f = this.ctx.createBiquadFilter();
    f.type = 'bandpass'; f.frequency.setValueAtTime(rand(700, 1400), t);
    f.frequency.exponentialRampToValueAtTime(180, t + 0.28); f.Q.value = 2.2;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.4 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.3);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.35 });
  }

  sfx_repair({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    for (let i = 0; i < 2; i++) {
      const o = this.ctx.createOscillator();
      o.type = 'triangle';
      o.frequency.setValueAtTime(rand(300, 460), t + i * 0.07);
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + i * 0.07);
      g.gain.exponentialRampToValueAtTime(0.22 * volume, t + i * 0.07 + 0.005);
      g.gain.exponentialRampToValueAtTime(0.0001, t + i * 0.07 + 0.1);
      o.connect(g);
      this._out(g, { pan, reverb: 0.2 });
      o.start(t + i * 0.07); o.stop(t + i * 0.07 + 0.12);
    }
  }

  sfx_door({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.9, 0.5);
    const f = this.ctx.createBiquadFilter();
    f.type = 'lowpass'; f.frequency.setValueAtTime(1400, t);
    f.frequency.exponentialRampToValueAtTime(200, t + 0.85);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.45 * volume, t + 0.1);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.9);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.55 });
  }

  sfx_power({ volume = 1 } = {}) {
    const t = this.t;
    // Breaker slam, then a rising hum as the grid spins up.
    const n = this._noiseSource(0.2, 0.8);
    const nf = this.ctx.createBiquadFilter();
    nf.type = 'lowpass'; nf.frequency.value = 900;
    const ng = this.ctx.createGain();
    ng.gain.setValueAtTime(0.7 * volume, t);
    ng.gain.exponentialRampToValueAtTime(0.0001, t + 0.25);
    n.connect(nf); nf.connect(ng);
    this._out(ng, { reverb: 0.6 });

    [55, 110, 165].forEach((f0, i) => {
      const o = this.ctx.createOscillator();
      o.type = i === 0 ? 'sawtooth' : 'sine';
      o.frequency.setValueAtTime(f0 * 0.4, t + 0.15);
      o.frequency.exponentialRampToValueAtTime(f0, t + 1.6);
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + 0.15);
      g.gain.linearRampToValueAtTime(0.16 * volume / (i + 1), t + 1.0);
      g.gain.exponentialRampToValueAtTime(0.0001, t + 2.6);
      o.connect(g);
      this._out(g, { reverb: 0.5 });
      o.start(t + 0.15); o.stop(t + 2.7);
    });
  }

  sfx_perk({ volume = 1 } = {}) {
    const t = this.t;
    // Short original 4-note jingle.
    const notes = [392, 523.25, 659.25, 784];
    notes.forEach((f, i) => {
      const o = this.ctx.createOscillator();
      o.type = 'triangle';
      o.frequency.value = f;
      const g = this.ctx.createGain();
      const st = t + i * 0.11;
      g.gain.setValueAtTime(0.0001, st);
      g.gain.exponentialRampToValueAtTime(0.2 * volume, st + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, st + 0.3);
      o.connect(g);
      this._out(g, { reverb: 0.4 });
      o.start(st); o.stop(st + 0.32);
    });
  }

  sfx_box({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const o = this.ctx.createOscillator();
    o.type = 'triangle';
    o.frequency.setValueAtTime(180, t);
    o.frequency.exponentialRampToValueAtTime(880, t + 1.6);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.14 * volume, t + 0.4);
    g.gain.setValueAtTime(0.14 * volume, t + 1.4);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 1.9);
    const trem = this.ctx.createOscillator();
    trem.frequency.value = 7;
    const tg = this.ctx.createGain();
    tg.gain.value = 0.06;
    trem.connect(tg); tg.connect(g.gain);
    o.connect(g);
    this._out(g, { pan, reverb: 0.5 });
    o.start(t); o.stop(t + 2.0);
    trem.start(t); trem.stop(t + 2.0);
  }

  sfx_boxstop({ volume = 1, pan = 0, rare = false } = {}) {
    const t = this.t;
    const chord = rare ? [523.25, 659.25, 830.6, 1046.5] : [392, 494, 587];
    chord.forEach((f, i) => {
      const o = this.ctx.createOscillator();
      o.type = 'sine';
      o.frequency.value = f;
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + i * 0.02);
      g.gain.exponentialRampToValueAtTime(0.18 * volume, t + 0.05);
      g.gain.exponentialRampToValueAtTime(0.0001, t + 1.4);
      o.connect(g);
      this._out(g, { pan, reverb: 0.6 });
      o.start(t); o.stop(t + 1.5);
    });
  }

  sfx_pap({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    const n = this._noiseSource(1.6, 1.2);
    const f = this.ctx.createBiquadFilter();
    f.type = 'bandpass';
    f.frequency.setValueAtTime(400, t);
    f.frequency.exponentialRampToValueAtTime(3800, t + 1.5);
    f.Q.value = 4;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.3 * volume, t + 0.6);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 1.7);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.5 });
  }

  sfx_powerup({ volume = 1, pan = 0 } = {}) {
    const t = this.t;
    [659.25, 880, 1174.7].forEach((f, i) => {
      const o = this.ctx.createOscillator();
      o.type = 'square';
      o.frequency.value = f;
      const g = this.ctx.createGain();
      const st = t + i * 0.07;
      g.gain.setValueAtTime(0.0001, st);
      g.gain.exponentialRampToValueAtTime(0.1 * volume, st + 0.01);
      g.gain.exponentialRampToValueAtTime(0.0001, st + 0.2);
      o.connect(g);
      this._out(g, { pan, reverb: 0.3 });
      o.start(st); o.stop(st + 0.22);
    });
  }

  sfx_trap({ volume = 1, pan = 0, kind = 'electric' } = {}) {
    const t = this.t;
    if (kind === 'electric') {
      const n = this._noiseSource(0.9, 2.4);
      const f = this.ctx.createBiquadFilter();
      f.type = 'highpass'; f.frequency.value = 2400;
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t);
      g.gain.linearRampToValueAtTime(0.3 * volume, t + 0.05);
      g.gain.exponentialRampToValueAtTime(0.0001, t + 0.9);
      n.connect(f); f.connect(g);
      this._out(g, { pan, reverb: 0.4 });
    } else if (kind === 'fire') {
      const n = this._noiseSource(1.4, 0.5);
      const f = this.ctx.createBiquadFilter();
      f.type = 'lowpass'; f.frequency.value = 700;
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t);
      g.gain.linearRampToValueAtTime(0.34 * volume, t + 0.2);
      g.gain.exponentialRampToValueAtTime(0.0001, t + 1.4);
      n.connect(f); f.connect(g);
      this._out(g, { pan, reverb: 0.35 });
    } else {
      for (let i = 0; i < 6; i++) this.sfx_shoot({ volume: volume * 0.5, pan, weight: 'light' });
    }
  }

  // --- player --------------------------------------------------------------
  sfx_hurt({ volume = 1 } = {}) {
    const t = this.t;
    const o = this.ctx.createOscillator();
    o.type = 'sine';
    o.frequency.setValueAtTime(rand(140, 200), t);
    o.frequency.exponentialRampToValueAtTime(70, t + 0.35);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.3 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.4);
    o.connect(g);
    this._out(g, { reverb: 0.15 });
    o.start(t); o.stop(t + 0.42);
  }

  sfx_step({ volume = 1, pan = 0, surface = 'concrete' } = {}) {
    const t = this.t;
    const n = this._noiseSource(0.1, surface === 'metal' ? 1.6 : 1);
    const f = this.ctx.createBiquadFilter();
    f.type = surface === 'metal' ? 'bandpass' : 'lowpass';
    f.frequency.value = surface === 'metal' ? 2200 : 620;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.1 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.1);
    n.connect(f); f.connect(g);
    this._out(g, { pan, reverb: 0.15 });
  }

  sfx_heartbeat({ volume = 1 } = {}) {
    const t = this.t;
    [0, 0.22].forEach((off, i) => {
      const o = this.ctx.createOscillator();
      o.type = 'sine';
      o.frequency.setValueAtTime(64, t + off);
      o.frequency.exponentialRampToValueAtTime(38, t + off + 0.14);
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, t + off);
      g.gain.exponentialRampToValueAtTime((i ? 0.2 : 0.32) * volume, t + off + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, t + off + 0.2);
      o.connect(g); g.connect(this.sfxBus);
      o.start(t + off); o.stop(t + off + 0.22);
    });
  }

  // --- UI ------------------------------------------------------------------
  sfx_ui({ volume = 1, kind = 'move' } = {}) {
    if (!this.ready) return;
    const t = this.t;
    const map = { move: 620, select: 880, back: 380, deny: 180, tick: 1200 };
    const o = this.ctx.createOscillator();
    o.type = kind === 'deny' ? 'sawtooth' : 'square';
    o.frequency.setValueAtTime(map[kind] ?? 620, t);
    if (kind === 'select') o.frequency.exponentialRampToValueAtTime(1320, t + 0.06);
    if (kind === 'deny') o.frequency.exponentialRampToValueAtTime(110, t + 0.14);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.06 * volume, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + (kind === 'deny' ? 0.16 : 0.08));
    o.connect(g); g.connect(this.sfxBus);
    o.start(t); o.stop(t + 0.2);
  }

  sfx_roundstart({ volume = 1 } = {}) {
    const t = this.t;
    // Low descending sting with a noise swell.
    const o = this.ctx.createOscillator();
    o.type = 'sawtooth';
    o.frequency.setValueAtTime(180, t);
    o.frequency.exponentialRampToValueAtTime(42, t + 2.2);
    const lp = this.ctx.createBiquadFilter();
    lp.type = 'lowpass'; lp.frequency.value = 700;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(0.26 * volume, t + 0.3);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 2.4);
    o.connect(lp); lp.connect(g);
    this._out(g, { reverb: 0.7 });
    o.start(t); o.stop(t + 2.5);

    const n = this._noiseSource(1.6, 0.7);
    const nf = this.ctx.createBiquadFilter();
    nf.type = 'bandpass'; nf.frequency.setValueAtTime(300, t);
    nf.frequency.exponentialRampToValueAtTime(3000, t + 1.2);
    const ng = this.ctx.createGain();
    ng.gain.setValueAtTime(0.0001, t);
    ng.gain.linearRampToValueAtTime(0.12 * volume, t + 1.1);
    ng.gain.exponentialRampToValueAtTime(0.0001, t + 1.7);
    n.connect(nf); nf.connect(ng);
    this._out(ng, { reverb: 0.6 });
  }

  // ------------------------------------------------------------------ beds
  startAmbient() {
    if (!this.ready || this.ambientNodes.length) return;
    const t = this.t;
    // Wind / room tone.
    const n = this.ctx.createBufferSource();
    n.buffer = this.noiseBuf;
    n.loop = true;
    const f = this.ctx.createBiquadFilter();
    f.type = 'lowpass'; f.frequency.value = 320; f.Q.value = 0.6;
    const g = this.ctx.createGain();
    g.gain.value = 0.0;
    g.gain.setTargetAtTime(0.09, t, 2);
    // Slow filter drift so the tone never sits still.
    const lfo = this.ctx.createOscillator();
    lfo.frequency.value = 0.06;
    const lg = this.ctx.createGain();
    lg.gain.value = 130;
    lfo.connect(lg); lg.connect(f.frequency);
    n.connect(f); f.connect(g); g.connect(this.musicBus);
    n.start(t); lfo.start(t);
    this.ambientNodes.push(n, lfo, g);

    // Distant metal groans of the structure.
    const drone = this.ctx.createOscillator();
    drone.type = 'sine';
    drone.frequency.value = 47;
    const dg = this.ctx.createGain();
    dg.gain.value = 0.05;
    const dl = this.ctx.createOscillator();
    dl.frequency.value = 0.031;
    const dlg = this.ctx.createGain();
    dlg.gain.value = 0.045;
    dl.connect(dlg); dlg.connect(dg.gain);
    drone.connect(dg); dg.connect(this.musicBus);
    drone.start(t); dl.start(t);
    this.ambientNodes.push(drone, dl, dg);
  }

  stopAmbient() {
    for (const n of this.ambientNodes) {
      try { n.stop?.(this.t + 0.1); } catch { /* gain nodes have no stop */ }
      try { n.disconnect(); } catch { /* already gone */ }
    }
    this.ambientNodes = [];
  }

  /** Menu music: slow original minor-key pad + pulse. */
  startMusic() {
    if (!this.ready || this.musicNodes.length) return;
    const t = this.t;
    const root = 55; // A1
    const voices = [1, 1.5, 1.7818, 2.3784];
    voices.forEach((mult, i) => {
      const o = this.ctx.createOscillator();
      o.type = i % 2 ? 'triangle' : 'sawtooth';
      o.frequency.value = root * mult;
      o.detune.value = rand(-9, 9);
      const f = this.ctx.createBiquadFilter();
      f.type = 'lowpass';
      f.frequency.value = 420 + i * 120;
      const g = this.ctx.createGain();
      g.gain.value = 0;
      g.gain.setTargetAtTime(0.05 / (i * 0.6 + 1), t, 3);
      const lfo = this.ctx.createOscillator();
      lfo.frequency.value = 0.05 + i * 0.017;
      const lg = this.ctx.createGain();
      lg.gain.value = 0.02;
      lfo.connect(lg); lg.connect(g.gain);
      o.connect(f); f.connect(g); g.connect(this.musicBus);
      o.start(t); lfo.start(t);
      this.musicNodes.push(o, lfo, g);
    });

    // Sparse heartbeat pulse under the pad.
    const pulse = () => {
      if (!this.musicNodes.length) return;
      const now = this.t;
      const o = this.ctx.createOscillator();
      o.type = 'sine';
      o.frequency.setValueAtTime(70, now);
      o.frequency.exponentialRampToValueAtTime(36, now + 0.3);
      const g = this.ctx.createGain();
      g.gain.setValueAtTime(0.0001, now);
      g.gain.exponentialRampToValueAtTime(0.12, now + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, now + 0.4);
      o.connect(g); g.connect(this.musicBus);
      o.start(now); o.stop(now + 0.45);
      this._pulseTimer = setTimeout(pulse, rand(1700, 2600));
    };
    this._pulseTimer = setTimeout(pulse, 1200);
  }

  stopMusic() {
    clearTimeout(this._pulseTimer);
    for (const n of this.musicNodes) {
      try { n.gain?.setTargetAtTime(0, this.t, 0.3); } catch { /* not a gain */ }
      try { n.stop?.(this.t + 1.2); } catch { /* not a source */ }
    }
    setTimeout(() => { this.musicNodes.forEach((n) => { try { n.disconnect(); } catch { /* gone */ } }); }, 1500);
    this.musicNodes = [];
  }

  /** Random ambient groan from a live zombie, rate-limited. */
  maybeGroan(pos, now, pitch = 1) {
    if (now - this._lastGroan < 0.55) return;
    this._lastGroan = now;
    this.playAt('groan', pos, { pitch, volume: 0.9, maxDist: 34 });
  }
}

export const Audio = new AudioEngine();
export { pick };
