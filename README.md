# Emberfall

An original idle RPG for Android phones: collect heroes, line up five of them, and watch automatic
turn-based battles while the campaign, idle income and a deep upgrade spine pull you forward.
Landscape, touch first, built in Unity. Everything is original: setting, heroes, abilities,
terminology, UI and art direction.

**This file is an overview.** `DESIGN-BUILD-ORDER.md` is the one document kept current, and it is
the source of truth for what is built, what is moving and what is owed. If the two disagree, it is
right. `CLAUDE.md` is the working agreement for agents. `Docs/LAUNCH-CHECKLIST.md` is what stands
between this build and the Play Store.

---

## What the game has today

**Heroes and combat**
- 94 heroes from one to five stars, all with authored kits (basic attack, active skill, passives,
  and up to three talents that ascension opens), six aspects with a type wheel, and ten relics.
- 5 against 5 automatic, turn-based battles. A fight is a pure, seeded function: the same inputs
  replay the same battle, and WATCH replays the hardest fight of a wave exactly.
- Battle HUD with plates, status icons, turn order, ability callouts and a per-hero stats panel.

**Progression**
- A campaign of six chapters and 72 stages. A stage is a ladder of waves of 12 battles that
  resolve in one tap, with a miniboss wall at stage 6 and a boss at stage 12 of every chapter, and
  a one-time wall chest on each.
- Idle income against a real clock, capped (`idleCapHours` in `balance.json`).
- Summoning with a pity rule, hero fusion (conversion) and ascension, star-gated level caps.
- Armor with rolls, tempers and attunements, a Forge, a Quartermaster shop, relic upgrades.

**Things to do besides the campaign**
- The Deep Recital, an endless floor ladder with Guardians.
- The Proving, fights against generated opponent lines, and friendly duels against friends.
- Guilds: founding, codes, roles, chat, a weekly five-floor guild dungeon with replays, the Guild
  Path and a guild tech tree.
- Daily orders, a login calendar and Wayfarer's Road (a free 28-day track), **off by default**
  behind `EMBERFALL_DAILY_LOOP_ON`.
- Weekly and monthly live events with free tracks.

**Store**
- A Store with packs, a starter offer, a monthly card, a growth fund, a paid pass track, event
  tracks, relic packs, hero contracts and skins. Everything bought is Sealed and never feeds a random
  mechanic (`Docs/DESIGN-SEALED.md`). **No real money moves yet**: the editor uses Unity IAP's fake
  store, a development build uses a debug store, and a release build says COMING SOON.

**Around the game**
- A guided first session with a feature unlock table (`onboarding.json`).
- Anonymous cloud accounts on Supabase with optional email, cloud save with conflict handling,
  friends and friend codes.
- Opt-in play stats for playtests, consent off by default (`Docs/PRIVACY-PLAYTEST.md`).
- Real music and sound effects with settings sliders.

**Art**
- All 94 hero badges are drawn. 31 heroes have battle sprite sheets; the other 63 draw as named
  placeholder mannequins until their sheets arrive (`Docs/PINOY-LIST.md`).
- 12 of chapter 1's 14 enemies and all 14 of chapter 2's have sheets; chapters 3 to 6 field
  placeholder enemies.
- Every chapter has painted battle backgrounds, including a boss backdrop for each stage 12.

---

## Getting it running

1. Install **Unity 6.5 (6000.5.9f1)** through Unity Hub, with **Android Build Support** (Android
   SDK and NDK tools, OpenJDK) and, for playtest builds, Windows build support.
2. In Unity Hub choose **Add, Add project from disk** and pick this folder. Do not create a new
   project on top of it: a template can switch the render pipeline.
3. Open the project. `EmberfallProjectSetup` creates the near-empty `Assets/Scenes/Main.unity`,
   adds it to Build Settings and applies the player settings (landscape, bundle id
   `com.emberfall.prototype`, Android backend). **Emberfall, Set Up Project** reapplies them.
4. **Emberfall, Open Main Scene**, then Play.

The project uses the **Built-In Render Pipeline**, not URP, and needs the `com.unity.ugui` package.
The UI is uGUI built from code; `GameBootstrap` builds the camera, canvas, UI and battlefield at
runtime, which is why the scene is empty.

**Cloud features** need `Assets/Resources/Config/supabase.json`, which is gitignored and never
committed (copy `supabase.template.json`). Without it the game runs fully offline and every cloud
page says LOCAL ONLY.

---

## Architecture

```
Assets/Scripts/
  Core/      the game itself, with no UnityEngine reference (enforced by its asmdef)
    Combat/      CombatSimulation, BattleEvent, statuses, targeting, talents
    Data/        definitions and ContentDatabase
    Session/     GameSession, the only place profile state is mutated
    Progression/ StatMath, daily loop, store offers, purchase ledger
    Save/        SaveSerializer (save version 26)
    Cloud/       sync decisions, guild rules, codecs
  Game/      Unity glue: bootstrap, content loading, save IO, audio, cloud transport, IAP adapter
  UI/        theme, widgets, router and the screens under UI/Screens
  Battle/    stage, actors, sprite animation, hit effects
  Dev/       the Dev Arena, compiled out of release builds
Assets/Editor/   project setup, build menu, capture tooling
```

- **Combat is a pure function.** `CombatSimulation.Run()` resolves the whole fight and returns an
  ordered list of `BattleEvent`s; the battle screen only plays it. No game logic lives in UI.
- **Content is data.** Balance, heroes, enemies, the campaign, relics, events, store offers and the
  rest are JSON in `Assets/Resources/Content`. Eight files are hashed into
  `ContentDatabase.ContentVersion` because fights read them; the others are not.
- **Saves are documents.** The whole profile is one versioned JSON document, migrated on load. A
  save from a newer build is refused rather than read.

## Checking it without the editor

`Tools/build-check.ps1` (PowerShell, Windows) compiles every script against a hand-written Unity
shim in `Tools/UnityShim` and runs `Tools/DevHarness`: content validation, determinism, save round
trips, the campaign power curve over many simulated players, the store and economy studies, and
hundreds of named checks. `-Quick` runs fewer simulated walks during work; only the full run counts
for a merge. CI runs it on every push (`.github/workflows/build-check.yml`).

`Tools/doc-lint.ps1` lints the markdown for line-number citations, dead commit hashes and dead
branch names.

## Building

| Menu | Result |
|---|---|
| Emberfall, Build Windows (Development) | a Windows player with the Dev Arena |
| Emberfall, Build Android APK | an APK on the fast backend (Mono, ARMv7) |
| Emberfall, Build Android APK (Release) | an APK on IL2CPP, ARM64 |
| Emberfall, Android, ... | switch between the two Android backends |
| Emberfall, Delete Local Save | wipe progress to replay the first session |
| Emberfall, Reveal Save Folder | open `persistentDataPath` |

Playtest packages come from `Tools/build-windows.ps1` (release or `-Development`, offline by default
or `-Cloud`), which zips the player with `Docs/PLAYTEST-README.md` inside. Every player prints
`BUILD <version> (<commit>)` in the Hub's corner. The current version is 0.3.0.

## Server

`Server/` holds the Supabase side: numbered SQL migrations (run in order in the SQL editor, never
automatically), operator queries, and Edge Functions for Google Play receipt validation. The game
talks to Supabase's REST endpoints with the anon key and the player's own token; the service role
key is never in the game. `Server/README.md` lists every migration and what is applied.
`Tools/supabase-rules-test.py`, `Tools/supabase-guild-rules-test.py` and
`Tools/supabase-launch-rules-test.py` prove the database rules against a live project.

## Where to read next

| Document | What it is |
|---|---|
| `DESIGN-BUILD-ORDER.md` | what is built, in progress, next, blocked, open and buggy |
| `Docs/LAUNCH-CHECKLIST.md` | what the Play Store launch still needs, prioritised, with owners |
| `CLAUDE.md` | ground rules for agents working in this repository |
| `DESIGN-*.md`, `Docs/DESIGN-*.md` | decision records, each true as of its date |
| `Docs/CLOUD-GAPS.md` | the save and cloud audit |
| `Docs/DESIGN-RECEIPT-VALIDATION.md` | Google Play purchase verification |
| `Docs/store/` | privacy policy draft, Play Store listing, data safety answers |
| `Docs/PINOY-LIST.md` | everything waiting on the designer and artist |
