# Tiny Planet Life Sim — Project Brief

*(working title; rename freely)*

## How to use this brief

You are building this game with me in Unity. I am a solo developer; you write the code and set up the project, I playtest on a phone and steer.

Work **one milestone at a time** (section 9). At the end of each milestone, stop, tell me exactly how to run it and what to try, and wait for my playtest feedback before starting the next. Do not build ahead: the whole point of the early milestones is to find out whether the core loop is fun before we invest in content.

If something in this brief is ambiguous, pick the simplest option that keeps the game data-driven, tell me what you chose, and keep going. Only stop to ask when the choice would be expensive to reverse.

---

## 1. The game in one paragraph

The player owns one small planet floating in space. They spin it with a finger and tap its surface to seed life. Plants grow over real time. The mix of plants and terrain attracts creatures; specific, undocumented combinations attract rare ones. Everything discovered is recorded in a codex. Growing life earns energy, which upgrades the planet and unlocks new biomes on it. Eventually the player can start additional planets, and each planet is generated from a seed with its own biomes, species pool and hidden rare-creature recipes, so no two planets play the same.

**Genre:** cozy idle / collection sim. **Session shape:** 1–3 minute check-ins several times a day, with occasional longer sessions.

## 2. Design pillars

Use these to settle any design question this brief does not answer.

1. **Touching the planet is the game.** Rotating and tapping must feel good on their own: inertia, smooth zoom, a small pop and sound on every plant. Menus are secondary.
2. **Discovery over instruction.** The player is never told the recipe for a rare creature. They get hints and work it out by experimenting. The codex shows silhouettes of what is still undiscovered.
3. **Every planet is different.** Variety comes from generation and rules, not from hand-built levels.
4. **Cozy, never punishing.** Nothing dies from neglect, no fail states, no timers that take things away. Coming back after a day should always feel like a reward.
5. **Small content, many combinations.** A modest number of species combined through placement and biome rules, rather than hundreds of bespoke assets.

## 3. Platform and technical baseline

- **Engine:** Unity 6 LTS (use whichever Unity 6 version is installed; check before creating the project), **URP**, new **Input System**.
- **Target:** Android first, portrait orientation, 60 fps on a mid-range phone. Keep everything iOS-compatible, but do not set up an iOS build.
- **Must also be fully playable in the Editor with a mouse** (drag = rotate, click = tap, scroll = zoom), because that is how most iteration will happen.
- **Art:** stylised low-poly 3D. Until milestone 4, use Unity primitives and flat colours only. Do not import asset packs or spend time on visuals early.
- **Packages:** keep to what Unity ships (URP, Input System, TextMeshPro, UI). Ask before adding any third-party dependency.
- **Offline only.** No backend, accounts, ads or purchases in this scope.

## 4. Core loop

```
tap planet → plant a seed (costs energy)
        ↓
plant grows through stages over real time (also while the app is closed)
        ↓
mature plants produce energy and change their surroundings
        ↓
surroundings satisfy spawn rules → creatures appear; rare ones need specific combos
        ↓
new species → codex entry + reward
        ↓
energy → unlock seeds, upgrade planet, open new biomes → more possible combinations
```

## 5. Systems

### 5.1 Planet and camera
- A sphere at the world origin. The camera orbits it; the planet itself does not move.
- One-finger drag rotates with inertia and damping. Pinch zooms between a full-planet view and a close surface view.
- A touch that moves less than a small threshold and ends quickly is a **tap**; anything else is a drag. A drag must never plant anything.
- Slow idle auto-rotation when the player is not touching.

### 5.2 Surface cells
The surface is divided into roughly 300–500 cells generated as a **Fibonacci lattice** on the sphere, with each cell's nearest neighbours precomputed. A cell stores:
- biome type
- moisture and fertility (0–1)
- its occupant, if any (one plant per cell)

All gameplay rules query cells and their neighbourhoods. Do not use physics overlap checks or scene searches for game logic.

### 5.3 Planting and growth
- Tap a cell with a seed selected → raycast to the sphere → resolve the nearest cell → if it is empty and the biome allows that plant, spend energy and place it, aligned to the surface normal.
- Each plant species has growth stages (seed → sprout → mature), each with a duration and a visual.
- Growth is computed from **UTC timestamps**, not from accumulated frame time, so offline progress works without extra code: on load, every plant's stage is derived from its plant time and the current time.
- Mature plants generate energy over time and may modify neighbouring cells (for example, raising moisture or fertility).
- Plants can be removed by the player for a partial refund.

### 5.4 Creatures and spawn rules
- Creatures are attracted, never placed. A spawn rule is a data asset containing:
  - conditions, e.g. "at least 3 of plant X within 2 cells of a water cell", "biome is Y", "plant A adjacent to plant B", "creature C already lives on the planet"
  - a check interval and a chance per check
  - a rarity tier: Common, Uncommon, Rare, Legendary
  - a population cap
- A simulation tick (about once per second, with catch-up for offline time capped at a sensible maximum) evaluates rules against the cell grid.
- Creatures wander across the surface between cells, staying near the habitat that attracted them. Movement follows the sphere, with the creature's up vector along the surface normal.
- Creatures leave if their conditions stop being met for long enough. They never die.
- Tapping a creature shows its name and a small reaction.

### 5.5 Codex
- Lists every plant and creature that can exist on the current planet. Undiscovered entries show a silhouette and a vague one-line hint ("Seems to like damp shade").
- First discovery triggers a celebratory moment and an energy reward.
- Shows per-planet and overall completion percentage.

### 5.6 Economy and progression
- One currency: **Energy**. Earned from mature plants and discoveries; spent on seeds, seed unlocks, and planet upgrades.
- **Planet level** rises with total life on the planet. Levels unlock new seed types and new biome regions on the same planet.
- Put every number (costs, durations, rates, chances) in data assets so I can tune without touching code. Use sensible placeholder values; balance comes later.

### 5.7 Biomes
- Start with three: **Meadow**, **Wetland** (contains water cells), **Rocky**. Later additions: Desert, Snow, Volcanic, Fungal.
- A biome defines: base moisture and fertility, which plants may grow there, a colour palette, and ambient decoration.
- On a new planet only one biome region is active; the rest are visibly dormant (grey, barren) until unlocked.

### 5.8 Planet generation
- A planet is fully determined by an integer **seed**. The seed decides:
  - the biome layout over the cells (noise-based regions)
  - the colour palette
  - which subset of the global species pool exists on this planet
  - one or two **signature rare creatures** whose recipe is assembled from randomised conditions drawn from that planet's own species
- The same seed must always produce the same planet.
- Multiple planets and a planet-select screen are a late milestone. Until then there is exactly one planet, but it must already be created through the generator from a seed.

### 5.9 Save and load
- Local JSON save in `Application.persistentDataPath`: planet seed, cell occupants with plant timestamps, living creatures, codex state, energy, unlocks, last-saved UTC time.
- Save on pause, on quit, and periodically.
- Include a save-format version number from the start.

## 6. Architecture

- **Simulation and presentation are separate.** The simulation (cells, plants, creatures, rules, economy) is plain C# with no `MonoBehaviour` and no scene references, so it can be ticked, saved, fast-forwarded and unit-tested on its own. MonoBehaviours only read simulation state and render it, and forward input as commands.
- **All content is data.** Plants, creatures, biomes, spawn rules and upgrades are `ScriptableObject` definitions. Adding a new species must not require new code unless it introduces a genuinely new kind of condition.
- **Spawn conditions are composable.** Model them as small condition types that can be combined in a rule, so new recipes are authored in the Inspector.
- **Communication through C# events** raised by the simulation (plant matured, creature spawned, species discovered, energy changed). UI and effects subscribe. Avoid a web of singletons; one composition root that wires things up is fine.
- **Time comes from an injectable clock**, so growth and offline progress can be tested and I can have a debug "skip 1 hour" button.
- **Pool** plant and creature views.

Suggested layout:

```
Assets/_Game/
  Scripts/
    Simulation/     # pure C#: grid, plants, creatures, rules, economy, generator, save
    Presentation/   # MonoBehaviours: planet view, camera, plant/creature views, VFX
    Input/
    UI/
    Data/           # ScriptableObject definitions
  Content/          # ScriptableObject instances: Plants/, Creatures/, Biomes/, Rules/
  Prefabs/
  Scenes/
  Tests/            # EditMode tests for the simulation
```

Write EditMode tests for the parts that are easy to get subtly wrong: deterministic generation from a seed, growth stage from timestamps, spawn-rule evaluation, save/load round-trip.

## 7. UI

Minimal, thumb-reachable, and never covering the planet more than necessary.

- **HUD:** energy counter (top), seed tray (bottom, horizontally scrollable), codex button, upgrades button.
- **Codex screen:** grid of entries with silhouettes, detail panel, completion percentage.
- **Upgrades screen:** planet level, biome unlocks, seed unlocks.
- **Debug panel (development builds only):** add energy, skip time, set seed and regenerate, unlock everything.

Use uGUI or UI Toolkit, whichever you can deliver most reliably; stay consistent once chosen.

## 8. Explicitly out of scope

Do not build these, and do not add hooks for them:

- monetisation, ads, in-app purchases
- accounts, cloud save, multiplayer, sharing, leaderboards
- notifications
- localisation (English strings only, but keep them out of code logic)
- narrative, quests, tutorials beyond one or two contextual hints
- creature breeding, genetics, or feeding systems

## 9. Milestones

Each milestone ends with a playable build and a short note: what was built, how to run it, what to test, known issues.

### M1 — The toy
Rotate, zoom, tap to plant. One biome, three plant species as coloured primitives, timestamp-based growth through stages, energy income, a seed tray, save/load.
**Done when:** I can plant a garden, close the app, reopen it later and see it has grown, on both Editor and an Android device; drags never plant by accident; frame rate holds with 300 plants.

### M2 — Life arrives
The cell grid drives spawn rules. Four common creatures and one rare creature with a non-obvious recipe. Creatures wander on the sphere. Codex with silhouettes, hints and discovery rewards.
**Done when:** a new player can discover the common creatures by experimenting, and the rare one feels like an earned "aha".

**→ Decision point.** After M2 I decide whether the loop is fun. Expect design changes here; do not start M3 without my go-ahead.

### M3 — A planet that grows
Three biomes with different plant sets, dormant regions that unlock, planet levels, seed unlocks, upgrades screen. Content to roughly 10 plants and 12 creatures.
**Done when:** there is about an hour of progression before content runs out.

### M4 — Look and feel
Replace primitives with low-poly art (I will choose the asset source with you at this point), a space skybox, lighting, planting and discovery effects, sound, haptics, UI styling.
**Done when:** a screenshot looks like a game someone would download.

### M5 — Many planets
Seed-driven generation exposed to the player: unlock a second planet, planet-select screen, per-planet species subsets and signature rare creatures, overall codex across planets.
**Done when:** two planets from different seeds clearly feel and play differently.

## 10. Working rules

- Verify before you report. Enter Play mode, check the console is free of errors, and run the tests. If you could not verify something (for example, on-device touch), say so plainly.
- If Unity Editor tooling is available to you, use it to create scenes, prefabs and assets directly. If not, write the scripts and give me precise, numbered Editor steps.
- Prefer small, readable code over clever abstractions. Do not add systems, options or configurability this brief does not ask for.
- Keep commits small and scoped to one change, with messages that say what changed and why.
- The two reference screenshots I may share show **mood only**: a small round world in space, dense with life, seen from above. This must be an original game. Do not reproduce any existing game's name, characters, art, UI or levels.
