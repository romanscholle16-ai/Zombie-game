import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

/**
 * Optional external model library.
 *
 * The game ships playable with nothing here — every model has a procedural
 * fallback, and a missing manifest, a missing file or a corrupt file all just
 * mean "use the built-in version". That is deliberate: art can be dropped in
 * and pulled out without touching game code, and a bad asset can never take
 * the build down.
 *
 * To use it, put glTF/GLB files under `assets/models/` and describe them in
 * `assets/models/manifest.json`:
 *
 *   {
 *     "zombie":       { "file": "zombie.glb", "scale": 1.0, "yaw": 180 },
 *     "weapon.wasp9": { "file": "guns/smg.glb", "scale": 0.9, "grip": [0,-0.06,-0.05] },
 *     "prop.crate":   { "file": "props/crate.glb", "scale": 1.2 }
 *   }
 *
 * Keys are looked up by the systems that build things: `zombie`, `weapon.<id>`,
 * `prop.<kind>`, `machine.perk.<id>`, `machine.pap`, `machine.box`.
 *
 * Licensing is the supplier's responsibility: only add models you have the
 * right to redistribute, and record their licences in assets/models/CREDITS.md.
 */

const MANIFEST_URL = 'assets/models/manifest.json';
const BASE = 'assets/models/';

class Library {
  constructor() {
    this.manifest = null;
    this.entries = new Map();   // key -> { scene, spec }
    this.ready = false;
    this.enabled = true;
    this._loader = null;
    this._warned = new Set();
  }

  get loader() {
    if (!this._loader) this._loader = new GLTFLoader();
    return this._loader;
  }

  /**
   * Reads the manifest and loads every model it names. Resolves either way —
   * failures are logged once and leave the library empty.
   */
  async load(onProgress = null) {
    if (this.ready) return this;
    this.ready = true;
    if (!this.enabled) return this;

    let manifest;
    try {
      const res = await fetch(MANIFEST_URL, { cache: 'force-cache' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      manifest = await res.json();
    } catch {
      // No manifest is the normal case, not an error worth shouting about.
      return this;
    }
    this.manifest = manifest;

    const keys = Object.keys(manifest);
    let done = 0;
    await Promise.all(keys.map(async (key) => {
      const spec = typeof manifest[key] === 'string' ? { file: manifest[key] } : manifest[key];
      try {
        const gltf = await this.loader.loadAsync(BASE + spec.file);
        this.entries.set(key, { scene: gltf.scene, animations: gltf.animations ?? [], spec });
      } catch (err) {
        console.warn(`[assets] "${key}" (${spec.file}) failed to load — using the built-in model.`, err.message);
      } finally {
        done++;
        onProgress?.(done / keys.length, key);
      }
    }));
    return this;
  }

  has(key) { return this.entries.has(key); }

  /**
   * A fresh instance of `key`, with the manifest's transform baked in, or null
   * when the key is absent. Callers are expected to fall back on null.
   */
  get(key, { scale = 1 } = {}) {
    const entry = this.entries.get(key);
    if (!entry) return null;
    const root = new THREE.Group();
    const inst = entry.scene.clone(true);
    const s = (entry.spec.scale ?? 1) * scale;
    inst.scale.setScalar(s);
    if (entry.spec.yaw) inst.rotation.y = (entry.spec.yaw * Math.PI) / 180;
    if (entry.spec.pitch) inst.rotation.x = (entry.spec.pitch * Math.PI) / 180;
    const o = entry.spec.offset;
    if (o) inst.position.set(o[0] * s, o[1] * s, o[2] * s);
    root.add(inst);
    root.userData.fromLibrary = true;
    root.userData.spec = entry.spec;
    root.userData.animations = entry.animations;
    // Shadows are the game's call, not the exporter's.
    root.traverse((n) => {
      if (!n.isMesh) return;
      n.castShadow = true;
      n.receiveShadow = true;
    });
    return root;
  }

  /**
   * Model for `key` if one was supplied, otherwise whatever `fallback()`
   * builds. This is the call sites' single entry point.
   */
  getOr(key, fallback, opts) {
    const m = this.get(key, opts);
    if (m) return m;
    if (!this._warned.has(key) && this.manifest && key in this.manifest) {
      this._warned.add(key);
    }
    return fallback();
  }

  /** Measured bounding box of a library model, for framing and colliders. */
  static measure(obj) {
    const box = new THREE.Box3().setFromObject(obj);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    return { box, size, center };
  }
}

export const Assets = new Library();
