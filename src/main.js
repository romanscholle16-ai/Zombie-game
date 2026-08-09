import * as THREE from 'three';
import { Game } from './Game.js';
import { ScreenManager } from './ui/Screens.js';
import { MenuBackground } from './ui/MenuBackground.js';
import { Settings } from './core/Settings.js';
import { Input } from './core/Input.js';
import { TouchControls } from './core/TouchControls.js';
import { Audio } from './core/AudioEngine.js';
import { Assets } from './world/AssetLibrary.js';
import { clamp, el } from './core/Util.js';

// An artifact or any other host embeds this in a frame it sizes from our
// scrollHeight. Flag it before first layout so the stylesheet can put the app
// in flow instead of pinning it to a viewport the host has not sized yet.
if (window.self !== window.top) {
  document.documentElement.classList.add('embedded');
  document.body?.classList.add('embedded');
  document.addEventListener('DOMContentLoaded',
    () => document.body.classList.add('embedded'), { once: true });
}

const BLEND_VERT = `
varying vec2 vUv;
void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }`;

const BLEND_FRAG = `
uniform sampler2D tCurrent;
uniform sampler2D tPrev;
uniform float uAmount;
varying vec2 vUv;
void main(){
  vec4 c = texture2D(tCurrent, vUv);
  vec4 p = texture2D(tPrev, vUv);
  gl_FragColor = mix(c, p, uAmount);
}`;

/** Tuned so the unpowered facility still reads on a bright monitor. */
const BASE_EXPOSURE = 1.32;

const COPY_FRAG = `
uniform sampler2D tMap;
varying vec2 vUv;
void main(){ gl_FragColor = texture2D(tMap, vUv); }`;

/** Application shell: renderer, loop, screen routing, and run lifecycle. */
class App {
  constructor() {
    this.canvas = document.getElementById('gl');
    this.renderer = new THREE.WebGLRenderer({
      canvas: this.canvas,
      antialias: Settings.quality.aa,
      powerPreference: 'high-performance',
      stencil: false,
    });
    this.renderer.shadowMap.enabled = Settings.quality.shadows;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = BASE_EXPOSURE * Settings.get('brightness');
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;

    this.game = null;
    this.menuBg = new MenuBackground(this.renderer);
    this.screens = new ScreenManager(this);
    this.screens.loadout.attach(this.renderer);
    this.state = 'menu';
    this.clock = new THREE.Clock();
    this.fade = document.getElementById('fade');

    this._initPost();
    Input.attach(this.canvas);
    this.touch = new TouchControls(Input);
    this.touch.onPause = () => this.pause();
    this._applyTouchMode();
    this._wireInput();

    Settings.on('change:brightness', () => this.applyBrightness());
    // Changing the touch options mid-session should take effect immediately.
    Settings.on('change:touchControls', () => {
      Input.touchActive = TouchControls.shouldEnable();
      document.body.classList.toggle('touch', Input.touchActive);
      this.touch.setEnabled(Input.touchActive && this.state === 'playing' && !this.screens.active);
      this.applyQuality();
    });
    Settings.on('change:touchLefty', (v) => this.touch.root?.classList.toggle('lefty', !!v));
    window.addEventListener('resize', () => this.resize());
    // The host resizes the frame around us; a window resize event is not
    // guaranteed for that, so watch the app box itself.
    if (window.ResizeObserver) {
      const ro = new ResizeObserver(() => this.resize());
      ro.observe(document.getElementById('app'));
    }
    this.resize();
    this.applyQuality();

    window.__app = this;         // debug handle
    window.THREE = THREE;
    this.screens.show('main');
    requestAnimationFrame(() => this._boot());
    this.loop = this.loop.bind(this);
    requestAnimationFrame(this.loop);
  }

  async _boot() {
    const l = document.getElementById('loading');
    const tip = l.querySelector('.loading-tip');
    // External models are optional; if none are supplied this resolves at once
    // and every system falls back to its procedural model.
    await Assets.load((frac, key) => {
      tip.textContent = `Loading models — ${key} (${Math.round(frac * 100)}%)`;
    });
    if (Assets.entries.size) tip.textContent = `Loaded ${Assets.entries.size} models`;
    l.classList.add('done');
    setTimeout(() => l.remove(), 600);
  }

  // ------------------------------------------------------------------ post
  _initPost() {
    this.postScene = new THREE.Scene();
    this.postCam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
    const geo = new THREE.PlaneGeometry(2, 2);
    this.blendMat = new THREE.ShaderMaterial({
      uniforms: { tCurrent: { value: null }, tPrev: { value: null }, uAmount: { value: 0.0 } },
      vertexShader: BLEND_VERT, fragmentShader: BLEND_FRAG, depthTest: false, depthWrite: false,
    });
    this.copyMat = new THREE.ShaderMaterial({
      uniforms: { tMap: { value: null } },
      vertexShader: BLEND_VERT, fragmentShader: COPY_FRAG, depthTest: false, depthWrite: false,
    });
    this.quad = new THREE.Mesh(geo, this.blendMat);
    this.quad.frustumCulled = false;
    this.postScene.add(this.quad);
    this.rt = [null, null, null];
  }

  _ensureTargets(w, h) {
    const opts = { depthBuffer: true, stencilBuffer: false, type: THREE.UnsignedByteType };
    for (let i = 0; i < 3; i++) {
      if (this.rt[i]) this.rt[i].dispose();
      this.rt[i] = new THREE.WebGLRenderTarget(Math.max(2, w), Math.max(2, h), opts);
    }
    this.historyIndex = 1;
  }

  get motionBlurOn() {
    return Settings.get('motionBlur') && Settings.get('quality') !== 'low';
  }

  // ------------------------------------------------------------------ input
  _wireInput() {
    this.canvas.addEventListener('mousedown', () => {
      Audio.resume();
      if (this.state === 'playing' && !this.screens.active) Input.requestLock();
    });

    // Embedded frames often refuse pointer lock. Tell the player how to aim
    // rather than leaving them stuck facing one wall.
    Input.on('lockfallback', () => {
      if (this._toldAboutDrag) return;
      this._toldAboutDrag = true;
      document.body.classList.add('drag-look');
      this.game?.hud.toast('DRAG TO LOOK · CLICK TO FIRE', 'info', true);
    });

    Input.on('lockchange', (locked) => {
      document.body.classList.toggle('playing', locked);
      if (!locked && this.state === 'playing' && !this.screens.active && this.game && !this.game.over) {
        this.pause();
      }
    });

    window.addEventListener('keydown', (e) => {
      if (e.code !== 'Escape') return;
      if (this.state !== 'playing' || !this.game || this.game.over) return;
      e.preventDefault();
      if (this.screens.current === 'pause') this.resume();
      else if (!this.screens.active) this.pause();
    });

    // Any first interaction unlocks the audio context.
    const once = () => { Audio.resume(); Audio.startMusic(); window.removeEventListener('pointerdown', once); };
    window.addEventListener('pointerdown', once);
    window.addEventListener('keydown', once, { once: true });
  }

  // ------------------------------------------------------------------ flow
  async _fade(on) {
    this.fade.classList.toggle('on', on);
    await new Promise((r) => setTimeout(r, 400));
  }

  async startGame() {
    if (this.state === 'loading') return;
    this.state = 'loading';
    await this._fade(true);
    Audio.stopMusic();
    if (this.game) { this.game.dispose(); this.game = null; }
    this.screens.hide();
    this.game = new Game(this);
    this.game.hud.onPromptChange = (avail) => this.touch.setInteractVisible(avail);
    window.__game = this.game;   // debug handle
    this.resize();
    this.state = 'playing';
    this.touch.setEnabled(Input.touchActive);
    await this._fade(false);
    // Pointer lock is meaningless on a touchscreen and prompts on some phones.
    if (!Input.touchActive) Input.requestLock();
  }

  pause() {
    if (!this.game || this.game.over) return;
    this.game.paused = true;
    this.touch.setEnabled(false);
    Input.releaseLock();
    this.screens.stack.length = 0;
    this.screens.show('pause', { force: true });
  }

  resume() {
    if (!this.game) return;
    this.game.paused = false;
    this.screens.hide();
    this.touch.setEnabled(Input.touchActive);
    if (!Input.touchActive) Input.requestLock();
  }

  async restart() {
    await this.startGame();
  }

  async endToMenu() {
    this.state = 'loading';
    await this._fade(true);
    if (this.game) { this.game.dispose(); this.game = null; }
    this.touch.setEnabled(false);
    this.state = 'menu';
    this.screens.show('main', { force: true });
    Audio.startMusic();
    await this._fade(false);
  }

  onGameOver(summary) {
    this.state = 'gameover';
    this.touch.setEnabled(false);
    setTimeout(() => {
      this.screens.show('gameover', { force: true, summary });
      Audio.startMusic();
    }, 1400);
  }

  quit() {
    Audio.stopMusic();
    this.screens.hide();
    this.state = 'quit';
    document.getElementById('app').appendChild(el('div', {
      class: 'layer',
      style: 'background:#04060a;display:flex;align-items:center;justify-content:center;'
        + 'flex-direction:column;gap:14px;z-index:300;pointer-events:auto',
    },
    el('div', { style: 'letter-spacing:.4em;color:#9fd93a;font-size:26px;font-weight:800', text: 'SESSION ENDED' }),
    el('div', { style: 'letter-spacing:.24em;color:#8a8f8c;font-size:12px', text: 'You may close this tab.' }),
    el('button', { class: 'btn', text: 'RETURN', onclick: () => window.location.reload() })));
    window.close();
  }

  // ------------------------------------------------------------------ config
  applyBrightness() {
    this.renderer.toneMappingExposure = BASE_EXPOSURE * Settings.get('brightness');
  }

  /**
   * Turns the on-screen controls on for touch devices and picks defaults that
   * a phone can actually sustain. Only applied the first time — after that the
   * player's own settings win.
   */
  _applyTouchMode() {
    const on = TouchControls.shouldEnable();
    Input.touchActive = on;
    document.body.classList.toggle('touch', on);
    this.touch.setEnabled(false);          // menus first; enabled on match start
    if (!on || Settings.get('mobileTuned')) return;
    // A phone GPU will not hold 40 zombies with shadows and a blur pass.
    Settings.set('quality', 'low');
    Settings.set('motionBlur', false);
    Settings.set('fov', 74);
    Settings.set('mobileTuned', true);
    this.applyQuality();
  }

  applyQuality() {
    const q = Settings.quality;
    this.renderer.shadowMap.enabled = q.shadows;
    // Phones report device pixel ratios of 3 and up; rendering at that is the
    // single fastest way to melt a mobile GPU, so it is capped hard here.
    const ceiling = Input.touchActive ? 1.35 : 2.2;
    this.renderer.setPixelRatio(clamp(window.devicePixelRatio * q.pixelRatio, 0.5, ceiling));
    this.resize();
  }

  applyFullscreen() {
    const want = Settings.get('fullscreen');
    if (want && !document.fullscreenElement) {
      document.documentElement.requestFullscreen?.().catch(() => {});
    } else if (!want && document.fullscreenElement) {
      document.exitFullscreen?.().catch(() => {});
    }
  }

  resize() {
    // Measure the app box, not the window: when embedded the frame's height is
    // derived from our layout and the two do not agree until the host catches up.
    const host = document.getElementById('app');
    const r = host?.getBoundingClientRect();
    const w = Math.max(2, Math.round(r?.width || window.innerWidth));
    const h = Math.max(2, Math.round(r?.height || window.innerHeight));
    this.renderer.setSize(w, h, false);
    const dpr = this.renderer.getPixelRatio();
    this._ensureTargets(Math.floor(w * dpr), Math.floor(h * dpr));
    this.menuBg.resize(w / h);
    this.game?.resize(w, h);
  }

  // ------------------------------------------------------------------ loop
  loop() {
    requestAnimationFrame(this.loop);
    const dt = Math.min(0.05, this.clock.getDelta());
    Input.poll();

    if (this.state === 'playing' && this.game) {
      if (!this.game.paused) this.game.update(dt);
      else this.game.hud.setPrompt(null);
      this._renderGame(dt);
    } else if (this.state === 'gameover' && this.game) {
      this.game.update(dt);
      this._renderGame(dt);
    } else {
      this.menuBg.update(dt);
      this.renderer.setRenderTarget(null);
      this.renderer.render(this.menuBg.scene, this.menuBg.camera);
      this._renderLoadoutPreview(dt);
    }

    Input.endFrame();
  }

  _renderGame(dt) {
    const speed = this.game ? Math.hypot(this.game.player.vel.x, this.game.player.vel.z) : 0;
    const shake = this.game ? this.game.player.shake : 0;
    if (!this.motionBlurOn) {
      this.renderer.setRenderTarget(null);
      this.game.render();
      return;
    }

    const amount = clamp(0.12 + speed * 0.028 + shake * 0.22, 0, 0.55);
    const [scene, a, b] = this.rt;
    const prev = this.historyIndex === 1 ? a : b;
    const next = this.historyIndex === 1 ? b : a;

    this.renderer.setRenderTarget(scene);
    this.renderer.clear();
    this.game.render();

    this.quad.material = this.blendMat;
    this.blendMat.uniforms.tCurrent.value = scene.texture;
    this.blendMat.uniforms.tPrev.value = prev.texture;
    this.blendMat.uniforms.uAmount.value = amount;
    this.renderer.setRenderTarget(next);
    this.renderer.render(this.postScene, this.postCam);

    this.quad.material = this.copyMat;
    this.copyMat.uniforms.tMap.value = next.texture;
    this.renderer.setRenderTarget(null);
    this.renderer.render(this.postScene, this.postCam);

    this.historyIndex = this.historyIndex === 1 ? 2 : 1;
    void dt;
  }

  /** Renders the rotating weapon into the loadout screen's preview box. */
  _renderLoadoutPreview(dt) {
    if (this.screens.current !== 'loadout') return;
    const lv = this.screens.loadout;
    const r = lv.rect();
    if (!r) return;
    lv.update(dt);
    const dpr = this.renderer.getPixelRatio();
    const h = window.innerHeight;
    const x = Math.floor(r.left * dpr);
    const y = Math.floor((h - r.bottom) * dpr);
    const w = Math.floor(r.width * dpr);
    const hh = Math.floor(r.height * dpr);
    lv.camera.aspect = r.width / r.height;
    lv.camera.updateProjectionMatrix();
    this.renderer.setScissorTest(true);
    this.renderer.setViewport(x, y, w, hh);
    this.renderer.setScissor(x, y, w, hh);
    // Paint the panel's own backdrop so the menu corridor does not show through.
    const prevClear = this.renderer.getClearColor(new THREE.Color());
    const prevAlpha = this.renderer.getClearAlpha();
    this.renderer.setClearColor(0x0b1012, 1);
    this.renderer.clear(true, true, false);
    this.renderer.autoClear = false;
    this.renderer.render(lv.scene, lv.camera);
    this.renderer.autoClear = true;
    this.renderer.setClearColor(prevClear, prevAlpha);
    this.renderer.setScissorTest(false);
    const size = new THREE.Vector2();
    this.renderer.getSize(size);
    this.renderer.setViewport(0, 0, size.x, size.y);
  }
}

window.addEventListener('error', (e) => {
  // Surface boot failures instead of leaving a black screen.
  const l = document.getElementById('loading');
  if (l && !l.classList.contains('done')) {
    l.querySelector('.loading-tip').textContent = `Failed to start: ${e.message}`;
  }
});

// Register the offline worker so the game can be installed to a home screen.
// It is optional: a failure here just means no offline play.
if ('serviceWorker' in navigator && location.protocol.startsWith('http')) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  });
}

new App();
