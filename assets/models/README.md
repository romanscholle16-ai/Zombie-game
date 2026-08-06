# External models

Drop glTF (`.gltf`) or binary glTF (`.glb`) files in this folder and list them
in `manifest.json`. The game loads them at boot and uses them in place of the
built-in procedural models. **Nothing here is required** — with no manifest, or
with a file missing or broken, every system silently falls back to the model it
builds itself, so the game always runs.

## manifest.json

```json
{
  "zombie":        { "file": "zombie.glb", "scale": 1.0, "yaw": 180 },
  "zombie.runner": { "file": "zombie_fast.glb", "scale": 0.95 },
  "weapon.wasp9":  { "file": "guns/smg.glb", "scale": 0.9 },
  "prop.crates":   { "file": "props/crate.glb", "scale": 1.1 },
  "machine.pap":   { "file": "machines/press.glb", "scale": 1.0 }
}
```

Per-entry fields, all optional except `file`:

| field    | meaning |
|----------|---------|
| `scale`  | uniform scale applied on load |
| `yaw`    | Y rotation in degrees, for models that face the wrong way |
| `pitch`  | X rotation in degrees |
| `offset` | `[x, y, z]` shift in model units, applied after scale |

## Keys the game looks for

| key | used by |
|-----|---------|
| `zombie`, `zombie.runner`, `zombie.heavy`, `zombie.boss` | enemy models |
| `weapon.<id>` | view model, wall buys, crate reveal, loadout (ids in `src/weapons/WeaponDefs.js`) |
| `prop.crates`, `prop.barrels`, `prop.shelves`, `prop.tables`, `prop.generators` | map clutter |
| `machine.perk.<id>` | perk dispensers (`vital`, `rapid`, `swift`, `medic`, `endure`) |
| `machine.pap`, `machine.box` | upgrade machine, mystery crate |

## Budget

This is a browser game that has to hold 40 zombies at once. Keep zombie models
under roughly 8k triangles with a single 1k–2k atlas, props under 2k, weapons
under 6k. Prefer `.glb` with embedded textures. Draco-compressed files work.

## Licensing

Only add models you have the right to redistribute. Record every source and its
licence in `CREDITS.md` next to this file. Do not add assets extracted from
commercial games — that is the one thing that would make this project
undistributable.
