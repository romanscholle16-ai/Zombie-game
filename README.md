# ROTGRID

An original round-based first-person zombie survival game — buy your way deeper
into a dead research annex, restore the power, upgrade your gun, and see how far
you get before the horde takes the room back.

Ships in two forms:

| Build | Location | Status |
| --- | --- | --- |
| **Browser (three.js)** | repo root — `index.html`, `src/` | Complete and playable |
| **Unity (C#)** | `unity/Rotgrid/` | Full C# port of the same game |

---

## Running the browser build

It has **no build step and no dependencies to install** — three.js is vendored
in `vendor/three/`. It just needs to be served over HTTP (ES modules will not
load from `file://`).

```bash
# from the repo root, pick any one of these
python3 -m http.server 8080
# or
npx --yes serve -l 8080 .
# or
npm start
```

Then open <http://localhost:8080/>.

Click the canvas to lock the mouse. `ESC` pauses.

### One self-contained file

`npm run build:single` bundles the whole game — three.js, every module, all CSS —
into `dist/rotgrid.html`. That file needs no server and no assets; open it
directly or host it anywhere. Requires `npx esbuild` (or set `ESBUILD` to a
binary you already have).

### Controls

| Action | Key |
| --- | --- |
| Move | `W` `A` `S` `D` |
| Look | Mouse (or arrow keys where pointer lock is unavailable) |
| Sprint | `Left Shift` |
| Crouch / Slide | `Left Ctrl` (slide = sprint + crouch) |
| Jump / Mantle | `Space` (aim at a crate and jump to pull up) |
| Fire | `Left Mouse` |
| Aim down sights | `Right Mouse` |
| Reload | `R` |
| Melee | `V` |
| Swap weapon | `Q` (or `1` / `2`) |
| Interact / buy / repair | `F` (hold `F` on a broken barrier to renail it) |
| Flashlight | `L` |
| Pause | `ESC` |

Gamepads work too — sticks, triggers, and face buttons are mapped, with a
separate controller sensitivity slider in Settings. Every key is rebindable.

---

## Running the Unity build

Open `unity/Rotgrid/` as a project in **Unity 2021.3 LTS or newer** and press
**Play**. That is the whole setup.

There is nothing to wire up in the inspector: a `[RuntimeInitializeOnLoadMethod]`
hook spawns the game the moment play starts, in whatever scene happens to be
open — including a completely empty one. `Assets/Scenes/Boot.unity` is provided
for tidiness, and the menu item **Rotgrid ▸ Create Play Scene** will regenerate
it if you ever need to.

The Unity build targets the **Built-in Render Pipeline** and falls back to URP
shaders automatically if the project is set up that way. It uses the legacy
Input Manager (no packages required) and IMGUI for its menus and HUD, so it runs
on a stock Unity install with nothing else imported. Movement and world
collision are hand-rolled against an AABB list rather than Unity physics, so it
behaves identically to the browser build; only weapon raycasts use `Physics`,
against colliders on the zombie limbs.

### Type-checking without Unity

`unity/compile-check/UnityStubs.cs` is a stand-in for the slice of the Unity API
the game uses. It is deliberately kept **outside** `Assets/` so Unity ignores it.
With Mono or .NET installed you can type-check every gameplay script without
opening the editor:

```bash
unity/compile-check/check.sh      # needs mcs (mono-mcs) or set CSC=csc
```

---

## The game

### The loop

Spawn in the atrium with a pistol, a knife, 100 health and 500 points. Zombies
arrive in waves through boarded windows. Kill them for points; spend points on
doors, weapons, perks and traps. Each round brings more of them, tougher and
faster. Between rounds there is a short breather. When you go down and bleed
out, the run ends and the stats screen tells you how it went.

### The map — "Blacksite 7"

Nine connected zones, gated by three purchasable blast doors plus a power-sealed
chamber:

```
                    REFIT CHAMBER (Pack-a-Punch equivalent)
                          |  sealed until the grid is live
                    SEALED PASSAGE
                          |
  UNDERGROUND FACILITY ---+          BIOLOGY LAB
        |                                 |
   DESCENT RAMP --- door C (1250) --------+
                                          |
                                     NORTH CORRIDOR
                                          |
                                    door A (750)
                                          |
                        ARRIVALS ATRIUM (start) --- door B (1000) --- SERVICE
                                                                      CORRIDOR
                                                                          |
                                                                    POWER STATION
```

15 boarded windows feed the horde in; each has six planks that come off one at
a time and can be renailed for points.

### Systems

- **Rounds** — count, health, speed, aggression and damage all scale per round.
  Every 8th round is a *horde* round of sprinters; every 12th spawns an
  Abomination. Health follows the classic curve: `+100/round` to round 9, then
  `×1.1` compounding.
- **Zombie AI** — a Dijkstra flow field is flooded from the player a few times a
  second, so 40 agents path around corners, through doorways and down ramps
  without any per-agent search. They queue at windows, tear planks off, vault the
  sill, spread out with steering separation, stagger when hit hard, lose limbs,
  and crawl once their legs are gone.
- **Weapons** — 18 across pistols, SMGs, assault rifles, shotguns, LMGs, snipers
  and two wonder weapons (a chaining arc lance and a spore cannon), each with
  damage, fire rate, magazine, recoil, ADS speed, reload speed and a rarity tier.
  Hitscan with penetration, headshot and limb multipliers, spread that responds
  to movement, stance and sustained fire.
- **Weapon models** — each gun is assembled at runtime from the parts a real one
  has: upper and lower receiver, ejection port and brass deflector, charging
  handle, magwell, swept pistol grip and trigger guard, free-float handguard with
  vents and rail, stepped barrel with a gas block, muzzle device, collapsible or
  fixed stock, curved magazines, drums and tube optics on ring mounts. The forms
  are generic to their class — nothing reproduces any manufacturer's product,
  marking or trade dress.
- **Optics** — magnified scopes zoom the world camera by their true
  magnification, draw a real sight picture (opaque surround, round tube, etched
  mil-dot reticle) and drop the weapon out of frame once you are behind the
  glass. Everything else uses iron sights or a reflex tube.
- **Weapon audio** — every weapon has its own synthesised voice, layered from the
  action working, the supersonic crack, the pressure body, a resonant peak and
  the room tail. A refit brightens the resonance, so an upgrade is audible.
- **Wall buys** — eight mounted weapons, plus ammo refills and armour plating.
- **Mystery crate** — animated spin, rarity-weighted by round, relocates itself
  after a handful of uses.
- **Refit station** — the Pack-a-Punch equivalent, three tiers, each raising
  damage, magazine and reserve, and adding burn then explosive rounds.
- **Perks** — VITAL SURGE (health), QUICK HANDS (reload), LIGHT STEP (speed),
  SECOND WIND (bleed-out and one self-revive), IRON LUNG (sprint). All require
  power. All are lost when you go down. What each one does is on the buy prompt
  in game and in a full detail panel in the loadout viewer.
- **Power** — the grid starts dead. The breaker in the power station brings up
  the lights, the perk machines, the refit chamber and the traps.
- **Traps** — arc pylons, flame vents and a sentry gun, each armed with points,
  on a timer and a cooldown.
- **Drops** — Max Ammo, Instant Kill, Double Points, Purge, Fire Sale and Bonus
  Points, with on-screen timers.
- **Downed state** — crawl, bleed out, and self-revive once if you hold SECOND
  WIND.
- **Save data** — lifetime kills, highest round, runs, time survived, per-weapon
  accuracy and damage, and your last ten runs.

### Points

| Event | Points |
| --- | --- |
| Hit | 10 |
| Kill | 60 |
| Headshot kill | 100 |
| Melee kill | 130 |
| Trap kill | 50 |
| Plank repaired | 10 |
| Round cleared | 50 + 10 × round |

---

## Assets, and what this is not

**Everything in this game is generated at runtime.** Textures are drawn
procedurally into canvases (browser) or `Texture2D`s (Unity). Models are built
from primitives in code. Animation is written by hand, frame by frame. All audio
is synthesised — Web Audio graphs in the browser, PCM buffers baked into
`AudioClip`s in Unity. There is not a single imported image, mesh, font or sound
file anywhere in the repository.

The only third-party code is **three.js** (MIT, vendored in `vendor/three/`) as
the browser renderer.

Wave survival — buy doors, restore power, upgrade weapons, chase a higher round —
is a genre convention with a long lineage. Nothing here is taken from any
specific title: the map, the weapons, the perks, the enemies, the names, the UI
and the audio are all original to this project, and no trademarked or
copyrighted material is used or referenced.

---

## Layout

```
index.html              browser entry point
styles/main.css         all UI styling
vendor/three/           vendored three.js (MIT)
src/
  main.js               app shell: renderer, loop, screen routing
  Game.js               one run: wires the world, player and every system
  core/                 settings, save data, input, procedural audio, helpers
  world/                map data, world builder, nav grid, textures, props,
                        barricades, doors, environment map
  entities/             player controller, zombie, zombie manager
  weapons/              weapon definitions, runtime weapon, view model, firing
  systems/              rounds, perks, power, shop, crate, refit, traps, drops,
                        effects, interaction
  ui/                   menus, HUD, animated menu backdrop
unity/
  Rotgrid/Assets/Scripts/   the same game in C#
  compile-check/            UnityEngine stubs for offline type-checking
```

Each map, weapon, perk, enemy type and trap is a data entry, so adding more is
adding data rather than editing systems.
