import * as THREE from 'three';
import { Game } from './Game.js';
import { ScreenManager } from './ui/Screens.js';
import { MenuBackground } from './ui/MenuBackground.js';
import { Settings } from './core/Settings.js';
import { Input } from './core/Input.js';
import { Audio } from './core/AudioEngine.js';
import { Assets } from './world/AssetLibrary.js';
import { clamp, el } from './core/Util.js';

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
    this._wireInput();

    Settings.on('change:brightness', () => this.applyBrightness());
    window.addEventListener('resize', () => this.resize());
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
    window.__game = this.game;   // debug handle
    this.resize();
    this.state = 'playing';
    await this._fade(false);
    Input.requestLock();
  }

  pause() {
    if (!this.game || this.game.over) return;
    this.game.paused = true;
    Input.releaseLock();
    this.screens.stack.length = 0;
    this.screens.show('pause', { force: true });
  }

  resume() {
    if (!this.game) return;
    this.game.paused = false;
    this.screens.hide();
    Input.requestLock();
  }

  async restart() {
    await this.startGame();
  }

  async endToMenu() {
    this.state = 'loading';
    await this._fade(true);
    if (this.game) { this.game.dispose(); this.game = null; }
    this.state = 'menu';
    this.screens.show('main', { force: true });
    Audio.startMusic();
    await this._fade(false);
  }

  onGameOver(summary) {
    this.state = 'gameover';
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

  applyQuality() {
    const q = Settings.quality;
    this.renderer.shadowMap.enabled = q.shadows;
    this.renderer.setPixelRatio(clamp(window.devicePixelRatio * q.pixelRatio, 0.5, 2.2));
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
    const w = window.innerWidth, h = window.innerHeight;
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

new App();
