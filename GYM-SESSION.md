# Gym session report

Six tasks, six commits, harness green after every one. Read this before opening Unity.

---

## What changed, per task

### 1. Chapter 1 now has an aspect identity, and the default team can't coast through it

`Assets/Resources/Content/campaign.json` — stages 5-12 lean increasingly **Gale** (Grave Moth
swarms), which is a straight -20%/-15% damage swing against the two Ember starters (Orrin, Kalyx)
and neutral to Wren (Verdant). Enemy levels in that back half were also raised. The harness bot
never swaps teams, so it now has to actually grind (level up, farm gold) to clear the back half
instead of steamrolling it — "real investment," per the brief.

Concretely: Ember beats Verdant, Verdant beats Tide, Tide beats Gale, Gale beats Ember. Nothing on
the starter roster beats Gale — **Maeve Corrin (Tide)** does, and she's sitting on the bench the
whole session. Swapping her in for an Ember starter, or grinding levels instead, are now both real
answers to the same problem. That's the intended lever for whenever team-swap UI exists.

**Per-stage win rate, default (unswapped) team:**

| Stage | Before | After |
|---|---|---|
| 1-1 .. 1-8 | 100% | 100% (unchanged — tutorial ramp) |
| 1-9 Mothlight Steps | 100% | **58.3%** |
| 1-10 The Grey Bulwark | 100% | **62.5%** |
| 1-11 Pyre of Names | 95.8% | **62.5%** |
| 1-12 The Sundered Colossus (boss) | 83.3% | **75%** |

Target was 60-75% on later stages. Three of four land inside it; 1-9 sits 1.7 points under. See
"Unsure about" below — I stopped tuning there on purpose.

### 2. Turn order has small seeded per-round speed variance

`CombatUnit.cs` / `CombatSimulation.cs` — speed was a fixed integer, so order was identical in
every fight forever, and Orrin (88 SPD) always acted dead last. `TurnOrder()` now re-rolls a ±8%
jitter per unit every time it's called (once per round) from the same seeded RNG combat already
uses. Close speeds can now flip order round to round; large gaps still can't. Same seed still
produces a bit-identical battle (harness `DETERMINISM` check, unchanged) — the jitter comes from
the request's own seeded stream, not from anything external.

New balance.json knob: `speedVariance` (0.08), same shape as the existing `damageVariance`.

### 3. Provoke now overrides every single-target enemy mode, not just most of them

`CombatSimulation.ResolveTargets()` — `FindTaunter()` already overrode FrontEnemy / LowestHpEnemy
/ HighestHpEnemy / HighestAttackEnemy / RandomEnemy correctly. `LastAttacker` and `LastTarget`
returned their remembered unit *before* the taunt check ever ran, so either mode could ignore an
active taunter. Nothing in the current content uses those two modes, so this was latent, not
exploitable today — but restructured so the taunt check runs once, up front, before any
single-enemy mode, closing the gap for whatever ability uses them next.

Added a `TARGETING` harness check that walks real battles, tracks Provoke apply/expire on player
units, and asserts every single-target enemy hit lands on the taunt holder while it's active
(distinguishing single-target from AoE by whether one enemy's turn produced one distinct victim or
several). I sanity-checked it by reverting the fix locally and re-running — it didn't flip, exactly
because no current enemy exercises the bug. The check is there for the next ability that does.

### 4. Five new harness checks

All in `Tools/DevHarness/Program.cs`, all green:

- **ASPECT CHART** — direct assertions against `AspectChart.Multiplier` for the whole cycle plus
  the Lumen/Umbra pair (which mutually counters — both directions are advantage, not one-and-one,
  confirmed by reading `Beats()`). No battle needed.
- **ABILITY POTENCY** — every Damage/Heal/Shield/EnergyGain effect on every hero and enemy ability
  must have a non-zero Power, Flat or SelfHpRatio. Currently all clean.
- **STATUS DURATION** — matches self-applied finite-duration statuses to their expiry event and
  asserts the expiry round is exactly `duration - 1` rounds after the apply round (the first
  decrement lands at the end of the same turn it's cast on self). Shield is excluded on purpose —
  early expiry from being fully absorbed is correct behaviour, not a schedule bug.
- **STAGE WINNABILITY** — levels a fresh team directly to each stage's own `recommendedPower`
  (bypassing the gold economy) and requires at least one win in 20 samples. **This immediately
  caught a real problem**: after the Task 1 tuning, stages 1-9 and 1-10 were unwinnable at the
  `recommendedPower` they advertised (0/20 at 5245 and 5558 team power against recPower 5200 and
  5500). Fixed by raising `recommendedPower` to what the content actually needs (5200→5550,
  5500→5850) rather than softening the fight back down — that number is a pure UI display value,
  not a combat input, so this was a documentation-accuracy fix, not a rebalance.
- **UNDERLEVELLED SANITY** — a level-1 starter team against the final stage must lose almost
  always (currently 0/20), confirming stat scaling actually matters rather than being decorative.

### 5. Balance report now prints damage share and healer impact per stage

`RunCampaignBalancePass` prints a second line under each stage: damage share per deployed hero
(attributed straight from the same 24 measurement battles), and how many of those wins would have
flipped to losses with **just Wren's healing** removed (a clone of her with only the Active's Heal
effect zeroed — Cleanse, her passive ward, her attack all untouched — substituted into her slot,
same seed, same everything else).

First read of the numbers (full table below): healing is irrelevant through 1-1..1-5 and
1-7/1-8 (0 flips every time), a warning sign at 1-6 (10/24), and **load-bearing** on the back half
— 1-10 and 1-11 are **100% dependent on it** (14/14, 13/13 wins), 1-9 is 2/5, 1-12 is 14/23. Damage
share also shows Orrin flipping from tank to primary damage source once fights run long enough for
his burn/attrition kit to dominate (12.7%→68.1% split at 1-9). This is exactly the kind of number
that was invisible before — worth watching if the back-half tuning gets touched again.

### 6. Deprecation warnings

`Assets/Editor/EmberfallProjectSetup.cs`, `Assets/Scripts/Game/GameApp.cs`:

- `PlayerSettings.SetApplicationIdentifier` / `SetScriptingBackend` / `SetManagedStrippingLevel`
  taking `BuildTargetGroup` → the `NamedBuildTarget` overloads (5 call sites).
- `AndroidSdkVersions.AndroidApiLevel23` → `AndroidApiLevel26`.
- `Object.FindObjectOfType<EventSystem>()` → `FindFirstObjectByType<EventSystem>()`.
- `AndroidArchitecture.X86_64` **dropped**, not routed around, from the release Android backend
  (now ARM64 only). **This removes the "x86-64 runs natively on 64-bit BlueStacks" fallback the
  release backend used to offer.** It still runs on BlueStacks through the ARM translation layer,
  same as the fast-iteration backend, just IL2CPP instead of Mono — but the "runs natively"
  guarantee is gone. Updated the menu label, in-editor log message, and README to say so instead
  of leaving a stale claim.

`Tools/UnityShim` extended to declare `UnityEditor.Build.NamedBuildTarget`, the new `PlayerSettings`
overloads, and `Object.FindFirstObjectByType`/`FindAnyObjectByType`/`FindObjectsByType`, so the
offline harness keeps compiling against the same surface Unity 6 expects. The old
`BuildTargetGroup`-based overloads were removed from the shim since nothing calls them anymore.

---

## Final harness state

```
CONTENT            7/7 ok
ASPECT CHART       1/1 ok
ABILITY POTENCY    1/1 ok
DETERMINISM        5/5 ok
TARGETING          2/2 ok
STATUS DURATION    2/2 ok
SAVE               10/10 ok
STAGE WINNABILITY  1/1 ok
UNDERLEVELLED      1/1 ok
CAMPAIGN BALANCE   2/2 ok  (full clear, 12/12 stages, roster grows past 3 starters)

RESULT: all checks passed
```

Final campaign balance table (default team, no swaps — this is Task 5's new per-stage report):

| Stage | Team pwr | Rec pwr | Win rate | Avg rounds | Dmg share (Wren / Kalyx / Orrin) | Healer flips |
|---|---|---|---|---|---|---|
| 1-1 | 3692 | 2600 | 100% | 5 | 20.6% / 59.3% / 20% | 0/24 |
| 1-2 | 3999 | 3000 | 100% | 6 | 22.2% / 57% / 20.8% | 0/24 |
| 1-3 | 4159 | 3300 | 100% | 6 | 19.3% / 59.7% / 21% | 0/24 |
| 1-4 | 4393 | 3700 | 100% | 8 | 17% / 59.2% / 23.8% | 0/24 |
| 1-5 | 4627 | 4000 | 100% | 9 | 19.1% / 57.3% / 23.6% | 0/24 |
| 1-6 | 4778 | 4300 | 100% | 15 | 19.7% / 45.3% / 35% | 10/24 |
| 1-7 | 4929 | 4600 | 100% | 10 | 22.9% / 56.2% / 20.9% | 0/24 |
| 1-8 | 5167 | 4900 | 100% | 13 | 24.4% / 40.2% / 35.4% | 0/24 |
| 1-9 | 5558 | 5550 | 58.3% | 27.5 | 19.2% / 12.7% / 68.1% | 2/5 |
| 1-10 | 5863 | 5850 | 62.5% | 30 | 19% / 21.9% / 59.1% | **14/14** |
| 1-11 | 6327 | 5900 | 62.5% | 14.7 | 16% / 29.8% / 54.1% | **13/13** |
| 1-12 (boss) | 6643 | 6400 | 75% | 18 | 14.5% / 41.9% / 43.6% | 14/23 |

Final run also produced 6 heroes owned (roster grows past the 3 starters via summoning), rank 4,
hero cap Lv24 — all from a single simulated player session across the whole campaign.

---

## What I'm unsure about

- **Stage 9 sits at 58.3%, not 60-75%.** The balance pass has a real feedback loop — how hard one
  stage is changes how much the bot grinds before it, which changes the power the *next* stage
  gets measured at. Small level changes on one stage sometimes flipped a neighboring stage's win
  rate by 40+ points. I spent a long time hand-converging stages 9-12 toward the target band and
  landed 3 of 4 inside it; pushing 1-9 the last 2 points risked destabilizing 10-12 again (I saw
  this happen twice). I judged "close and stable" better than "chasing one more point and
  reopening three settled numbers." If you want 1-9 tighter, that's the one lever left to pull.
- **The "10 warnings" count is unverified.** Unity was closed all session (per your instructions),
  so I fixed exactly the four API categories you named, using known Unity 6 migration paths, and
  confirmed everything still compiles clean against the offline shim — but I never saw Unity's own
  Console. If it's not exactly zero warnings when you open it, the gap is probably something not on
  your list (e.g. `AndroidArchitecture.ARMv7` itself may also warn now — I left it alone since you
  didn't mention it and it's still driving the fast-iteration/BlueStacks path).
- **`recommendedPower` is a pure UI label**, not a combat input — I confirmed this by grep before
  touching anything (`Assets/Scripts/UI/Screens/CampaignScreen.cs:190` is its only other reader).
  The Task 4 winnability check bumping it for stages 9/10 is honest, not a workaround, but flagging
  it since it's the kind of thing that's easy to miss on a read-through.
- **UI-AUDIT.md** was produced by a background agent while I worked — I verified the file exists
  and skimmed its structure, but I have not independently checked its specific line-number claims
  against the UI source the way I checked everything else in this report. Treat it as a first pass.

---

## What to check first when you open Unity

1. **Console warning count** on first project load — should be near/at zero after the Task 6
   changes; if not, the culprit is likely `ARMv7` (see above) rather than anything I touched.
2. **Emberfall → Set Up Project**, then **Emberfall → Android → Release Backend (IL2CPP, ARM64)**
   — confirm it still builds; the menu item lost its `x86-64` suffix and the target architecture is
   now ARM64-only.
3. **Play the game past stage 8** with the default starter team — this is where the aspect-identity
   tuning actually bites. Stages 1-8 should feel exactly like before; 9 onward should feel like a
   real fight, not a wall. If it feels like a wall rather than "hard but fair," 1-9's 58.3% (vs the
   60-75% target) is the most likely place it's landing wrong.
4. **`pwsh Tools/build-check.ps1`** after any further change — it's still your only source of
   truth remotely, and it's green right now.

Everything outside `Assets/Scripts/UI` and `Assets/Scripts/Battle` per your scope; no C# 9 syntax
introduced; harness never left failing between commits.
