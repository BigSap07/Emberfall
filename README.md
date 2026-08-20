# Emberfall — original idle RPG prototype

A first playable vertical slice of an original mobile idle RPG: collectible heroes, automatic
3v3 turn-based combat rendered in 3D, campaign progression, summoning, idle income and local
saving. Portrait, touch-first, built for Android / BlueStacks.

Everything here is original: setting, hero names, abilities, terminology, UI and art direction.

---

## Getting it running

**Unity is not installed on this machine, so the project has never been opened in the editor.**
It was written to be opened without any manual setup.

1. Install **Unity 6 LTS (6000.0.x)** via Unity Hub, with the **Android Build Support** module
   (including *Android SDK & NDK Tools* and *OpenJDK*).
   Unity 2022.3 LTS also works — the project has no version-specific code.
2. In Unity Hub choose **Add → Add project from disk** and pick this folder.
   *Do not* create a new project on top of it: creating a project applies a template that may
   switch the render pipeline to URP or change the input backend.
3. Open the project. On first load Unity generates `Packages/manifest.json`, `Library/` and the
   remaining `ProjectSettings/` defaults, and `EmberfallProjectSetup` runs automatically to:
   - create `Assets/Scenes/Main.unity` (deliberately empty — see *Architecture*),
   - add it to Build Settings,
   - set product name, portrait orientation, bundle id and the Android backend.
4. Open `Assets/Scenes/Main.unity` (menu: **Emberfall → Open Main Scene**) and press **Play**.

If anything looks wrong, run **Emberfall → Set Up Project** to reapply all of the above.

### Render pipeline

The project targets the **Built-In Render Pipeline**. The two shaders in
`Assets/Resources/Shaders` also ship a URP sub-shader, so URP renders too, but Built-In is what
it was designed against. `GameApp` logs a warning at startup if an SRP is active.

---

## The loop that works today

```
MAIN SCREEN → HEROES → HERO DETAIL (level up, deploy)
            → CAMPAIGN (pick stage, see your banner)
            → BATTLE (auto 3v3, abilities, crits, deaths)
            → REWARDS (gold, shards, account XP, unlocks)
            → SUMMON (spend shards, new ally)
            → repeat
```

Progress is saved automatically and survives restarts.

---

## Architecture

The hard rule is that **combat is a pure function**. `CombatSimulation.Run()` resolves the entire
fight up front and returns an ordered list of `BattleEvent`s. The battle screen is a player for
that list — it never computes damage, never decides a target and cannot change the outcome.

```
Assets/Scripts/
  Core/          no UnityEngine reference at all (enforced by Emberfall.Core.asmdef)
    Json/        dependency-free JSON reader/writer, shared by content and saves
    Data/        HeroDef, EnemyDef, AbilityDef, StageDef, BalanceConfig, ContentDatabase
    Model/       PlayerProfile, HeroInstance
    Combat/      DeterministicRng, CombatUnit, CombatSimulation, BattleEvent
    Progression/ StatMath — every formula in one file
    Save/        SaveSerializer
    Session/     GameSession — the only place profile state is mutated
  Game/          Unity glue: bootstrap, content loading, save IO, synthesised audio
  UI/            theme, procedural sprite lab, widget builders, router, screens
  Battle/        camera/arena, procedural character rigs, effects, actors
  Editor/        project setup and the Android build menu
```

`Core` is where the game actually lives; the Unity layers only render it. That split is what
lets the whole simulation be tested without the editor (see *Offline verification*).

### Data-driven content

All static content is JSON in `Assets/Resources/Content/`:

| File | Contents |
|---|---|
| `heroes.json` | the six heroes: stats, visuals, basic attack, active, passive |
| `enemies.json` | eight Hollow enemy types including a boss |
| `campaign.json` | chapter 1, twelve stages with enemy line-ups and rewards |
| `balance.json` | every tuning constant — damage formula, costs, idle rates, summon odds |

Adding hero #7 means adding one JSON object. Nothing in the combat code knows any hero by name.
Abilities are lists of typed effects (`Damage`, `Heal`, `Shield`, `ApplyStatus`, `Cleanse`,
`EnergyGain`) with a targeting mode, so new behaviour is usually data, not code.

### No imported art

There are no models, textures, fonts or audio files in the project. Instead:

- **Characters** are assembled from Unity primitives at runtime by `UnitRig`, using a
  silhouette + weapon + palette recipe from each unit's `visual` block.
- **UI sprites** (rounded panels, rings, glows, hatching, hero sigils) are drawn pixel by pixel
  into textures at startup by `TextureLab` and cached.
- **Ground, sky and effects** are generated textures on two custom shaders.
- **Sound** is synthesised in `Sfx` — sine sweeps and arpeggios, no audio assets.
- **Text** uses Unity's built-in `LegacyRuntime.ttf`, so there is no TextMeshPro import step.

This is why the repository is a few thousand lines of code and nothing else, and why there are
no `.meta` files to conflict — Unity generates them on first import.

### Why the scene is empty

`GameBootstrap` builds the camera, canvas, UI and battlefield from code via
`[RuntimeInitializeOnLoadMethod]`. The scene file contains nothing. That avoids hand-authored
scene YAML, makes the project version-agnostic, and means pressing Play from any scene works.

---

## Offline verification

Unity is not installed here, so the project ships with its own compile-and-test harness that
runs against the .NET Framework compiler already present on Windows:

```bash
pwsh "Tools/build-check.ps1"
```

It does two things:

1. **Compiles every script** — `Core`, the Unity runtime code and the editor scripts — against
   `Tools/UnityShim`, a hand-written subset of the UnityEngine/UnityEditor API with matching
   signatures. This catches typos, wrong overloads and missing usings without an editor.
2. **Runs `Tools/DevHarness`**, which loads the real content files and checks content validity,
   battle determinism, save round-tripping, prints a readable sample battle, then simulates a
   full player run through the campaign and reports win rate and fight length per stage.

`Tools/` sits outside `Assets/`, so Unity never sees the shim.

Because the harness compiler is C# 5, the project is written in C# 5-compatible syntax
(no string interpolation, no expression-bodied members). Unity supports C# 9 — this is a
deliberate trade to keep the offline check working, and can be dropped once Unity is installed.

---

## Building the APK

**Emberfall → Build Android APK** writes `Builds/Emberfall.apk` and opens the folder.

Android defaults applied by `EmberfallProjectSetup`:

| Setting | Value | Why |
|---|---|---|
| Scripting backend | Mono | fastest builds, no NDK needed |
| Architecture | ARMv7 | runs on BlueStacks through its ARM translation layer |
| Orientation | Portrait | phone-shaped UI |
| Min SDK | 23 | wide device coverage |
| Bundle id | `com.emberfall.prototype` | |

For a device-ready build use **Emberfall → Android → Release Backend (IL2CPP, ARM64 + x86-64)**
first — x86-64 also runs natively on 64-bit BlueStacks instances. That path needs the Android
NDK module installed and takes considerably longer to build.

Headless/CI:

```bash
Unity.exe -quit -batchmode -projectPath "<this folder>" -executeMethod Emberfall.EditorTools.EmberfallBuild.BuildFromCommandLine
```

### Installing in BlueStacks

1. Start BlueStacks 5 and let it finish booting.
2. Drag `Builds/Emberfall.apk` onto the BlueStacks window, or use the sidebar **Install APK**
   button and select the file.
3. Launch **Emberfall** from the BlueStacks home screen.
4. Set the instance to a portrait resolution (Settings → Display → Portrait, e.g. 1080×1920)
   so the UI is not letterboxed.

`adb` also works if you prefer it:

```bash
adb connect 127.0.0.1:5555
```

```bash
adb install -r Builds/Emberfall.apk
```

---

## Menu reference

| Menu item | Effect |
|---|---|
| Emberfall → Set Up Project | recreate scene, build settings and player settings |
| Emberfall → Open Main Scene | open `Assets/Scenes/Main.unity` |
| Emberfall → Delete Local Save | wipe progress so the next run is a first-time experience |
| Emberfall → Reveal Save Folder | open `persistentDataPath` |
| Emberfall → Android → … | switch between the fast-iteration and release backends |
| Emberfall → Build Android APK | build `Builds/Emberfall.apk` |

There is also a **…** button on the main screen in-game that resets progress, for testing the
first-run experience on device.

---

## Combat rules

- Turn order is by effective Speed each round; ties break toward the player, then slot, then id.
- A basic attack grants 28 energy; taking damage grants 9. At 100 energy the unit spends it on
  its active ability instead of attacking.
- `damage = attack × power × aspect × crit × (1 − defense/(defense + 320 + 18×level)) × ±6%`
- Aspects: Ember ▸ Verdant ▸ Tide ▸ Gale ▸ Ember, and Lumen ⇄ Umbra. ×1.20 / ×0.85.
- Statuses: Attack Up/Down, Guard Up/Down, Cinder (damage over time), Renewal, Ward (absorb),
  Provoke (taunt), Focus (crit up), Mire (slow).
- Wards absorb before health. Damage over time ticks at the start of the victim's turn.
- 30 rounds maximum; if time runs out the side with the higher remaining health fraction wins.
- Everything is seeded, so the same battle replays identically.
