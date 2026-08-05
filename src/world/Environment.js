import * as THREE from 'three';

/**
 * A tiny procedural environment map.
 *
 * Metallic PBR materials render black without something to reflect, so every
 * scene gets this pre-filtered gradient: cool overhead bounce, a warm horizon
 * band, and a near-black floor. It is generated from a canvas — no HDRI files.
 */
let cached = null;

export function getEnvironment(renderer) {
  if (cached) return cached;

  const c = document.createElement('canvas');
  c.width = 256;
  c.height = 128;
  const ctx = c.getContext('2d');

  const g = ctx.createLinearGradient(0, 0, 0, 128);
  g.addColorStop(0.0, '#3d4a58');   // ceiling bounce
  g.addColorStop(0.42, '#2a3138');
  g.addColorStop(0.55, '#3a3228');  // warm horizon
  g.addColorStop(0.75, '#15181b');
  g.addColorStop(1.0, '#080a0c');   // floor
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 256, 128);

  // Two soft highlights so metal has something to catch.
  for (const [x, y, r, col] of [[70, 40, 46, 'rgba(190,215,240,0.55)'], [190, 54, 34, 'rgba(255,190,120,0.4)']]) {
    const rg = ctx.createRadialGradient(x, y, 0, x, y, r);
    rg.addColorStop(0, col);
    rg.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = rg;
    ctx.fillRect(0, 0, 256, 128);
  }

  const tex = new THREE.CanvasTexture(c);
  tex.mapping = THREE.EquirectangularReflectionMapping;
  tex.colorSpace = THREE.SRGBColorSpace;

  const pmrem = new THREE.PMREMGenerator(renderer);
  pmrem.compileEquirectangularShader();
  const target = pmrem.fromEquirectangular(tex);
  cached = target.texture;
  tex.dispose();
  pmrem.dispose();
  return cached;
}

/** Applies the shared environment to a scene at a chosen strength. */
export function applyEnvironment(scene, renderer, intensity = 0.35) {
  scene.environment = getEnvironment(renderer);
  scene.environmentIntensity = intensity;
}
