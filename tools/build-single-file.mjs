/**
 * Bundles the browser build into one self-contained HTML file.
 *
 *   node tools/build-single-file.mjs
 *
 * Output: dist/rotgrid.html — no server, no imports, no assets. Open it
 * directly, or host it anywhere that serves a single file.
 *
 * Requires esbuild (npx esbuild is fine): the script shells out to whatever
 * `esbuild` binary it is given via ESBUILD, falling back to `npx esbuild`.
 */
import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const tmp = join(root, 'dist', '.bundle.js');
mkdirSync(join(root, 'dist'), { recursive: true });

const esbuild = process.env.ESBUILD || 'npx';
const args = process.env.ESBUILD
  ? []
  : ['--yes', 'esbuild'];

execFileSync(esbuild, [
  ...args,
  'src/main.js',
  '--bundle',
  '--format=esm',
  '--target=es2022',
  '--minify',
  `--outfile=${tmp}`,
  '--alias:three=./vendor/three/three.module.js',
], { cwd: root, stdio: 'inherit' });

const bundle = readFileSync(tmp, 'utf8');
const css = readFileSync(join(root, 'styles', 'main.css'), 'utf8');
const html = readFileSync(join(root, 'index.html'), 'utf8');

// Keep only what lives inside <body>, minus the module script tag.
const body = html
  .slice(html.indexOf('<body>') + 6, html.lastIndexOf('</body>'))
  .replace(/<script type="module"[^>]*><\/script>/, '')
  .trim();

const out = `<title>ROTGRID — Round-Based Zombie Survival</title>
<style>
${css}
</style>
${body}
<script type="module">
${bundle}
</script>
`;

writeFileSync(join(root, 'dist', 'rotgrid.html'), out);
rmSync(tmp, { force: true });
console.log(`dist/rotgrid.html — ${(out.length / 1024 / 1024).toFixed(2)} MB`);
