# Summon presentation

Decision record, 2026-09-22. The reasoning here is durable; for what is built now, read
`DESIGN-BUILD-ORDER.md`.

The summon used to be a button press and a fading popup, and a ten-pull was a text list. It is now
a sequence with a buildup, a tell, a flash and a reveal, and a ten-pull ends on a summary grid.
This is a presentation change only. Nothing about the roll moved: `DoSummon` in `SummonScreen.cs`
still loops the single `GameSession.Summon`, and the rates, the opening guarantee, the drop pool and
the duplicate conversion are the code they were.

## Where it lives

| What | Where |
|---|---|
| The sequence itself | `SummonSequence` in `Assets/Scripts/UI/Summon/SummonSequence.cs` |
| Every duration | `SummonTiming` in `SummonTiming.cs` (engine-free) |
| The Fast summon preference | `SummonSettings` in `SummonSettings.cs` (PlayerPrefs) |
| The audio hook | `SummonAudio`, `ISummonAudio`, `SummonBeat` in `SummonAudio.cs` |
| The tell colours | `SummonNeutral`, `SummonTell`, `SummonTellHot`, `SummonDim` in `Emberforge.cs` |
| The press hold | `EmberforgeButtonSkin.HoldPressed` in `EmberforgeUI.cs` |
| The settings switch | the SUMMONING card in `SettingsScreen.cs` |
| The editor preview | `SummonScreen.DebugPreview`, and the Summon Preview row of `DebugCheatPanel` |
| The capture harness | `SummonSequenceShots` in `Assets/Editor` |

## The one rule: the sequence is a player

By the time the sequence runs, the roll has happened, the shards are spent, the hero is granted and
the save has been requested. The sequence is handed a list of `SummonOutcome`s and shows them, the
same way the battle screen plays a finished `BattleEvent` list. It never rolls and never picks.
That is why skipping, aborting and leaving the screen mid-summon all lose nothing but the show.

## The tell

The buildup leaks the rarity a beat before the hero is on screen. That gap is the payload.

**The tell never lies.** Its colour is read from the outcome being revealed, never from the batch's
best pull and never guessed. In a ten-pull every pull gets its own tell, so a gold light always
means the hero behind it is gold.

**The time before the tell is the same for every tier.** Higher tiers get extra time, but only
after the tell, so the length of the wait cannot leak the result before the colour does.

| Tier | Tell colour | Extra on the tell |
|---|---|---|
| 5 | gold, `RarityDeep(5)`, hot centre `RarityLit(5)` | a screen shake that decays over 0.45s, a second wider ring, strong light rays, 0.30s longer hold |
| 4 | purple, `RarityDeep(4)` | faint rays, 0.10s longer hold |
| 3 | blue, `RarityDeep(3)` | one ring |
| 2 | green, `RarityDeep(2)` | one ring |
| 1 | grey, `RarityDeep(1)` | one ring |

The colours ARE the rarity frame's colours, read through `Emberforge.SummonTell` rather than
written again, so the light and the frame the hero lands in cannot drift apart.

Tiers 1 and 2 are listed because they can really come out. See **A roll finding** below.

Before the tell, the light is `SummonNeutral`, a cool near-white. The first pass used a warm cream,
and the timing strip showed cream sits so close to gold that the gold tell was the smallest colour
change of the three. The neutral has to be a colour the tell moves away from.

**Star colour.** The reveal and the summary draw their stars and names in `RarityLit`, not in
`UITheme.StarColor`. The two palettes disagree: `StarColor(3)` is purple and the rarity frame for a
3-star is blue. On this screen a purple star row under a blue light and a blue frame would
contradict the tell, so the summon uses the frame's palette. Every other star row in the game still
uses `StarColor`. That disagreement is older than this pass and is flagged rather than fixed.

## The beats and the final timings

All times are seconds. **Full** is the default; **Fast** is the settings switch.

| Beat | What happens | Full | Fast |
|---|---|---|---|
| Press | the pull button is held sunk, the room dims to `SummonDim` (0.93 black) | 0.22 | 0.12 |
| Buildup | a light grows at the centre from 0.3 to 0.9 scale, 24 motes pull inward, a rumble ramps from 0 to 6 units | 1.25 (3-star) | 0.30 |
| Tell | at this point into the buildup the light commits to its colour over 0.08s, punches, and a ring expands | at 0.80 | at 0.10 |
| Linger, 4-star | extra hold after the tell | +0.10 | +0.00 |
| Linger, 5-star | extra hold after the tell | +0.30 | +0.08 |
| Charge | the light swells in the last 0.14s of the buildup, the motes fade | inside buildup | inside buildup |
| Flash | white to full | 0.06 | 0.04 |
| Flash out | white fades while the hero appears under it | 0.24 | 0.18 |
| Portrait | scales 1.15 to 1.0, in its rarity frame, with the tier colour burning behind it | 0.34 | 0.26 |
| Name stamp | the name and title stamp in from 1.6 scale, starting at | 0.14 | 0.08 |
| Stars | stamp in, starting at | 0.30 | 0.16 |
| Star gap, 5-star only | each star pings in on its own, this far apart | 0.13 | 0.08 |
| Settled | NEW ALLY or the duplicate conversion, and TAP TO CONTINUE | 0.55 | 0.35 |

Tap to reveal on a 3-star is therefore 0.22 + 1.25 + 0.06 = 1.53s at Full, and 1.83s on a 5-star.
At Fast it is 0.46s and 0.54s.

The brief's starting numbers were 0.2 dim, a 1.2 buildup and a tell at 0.8. The tell stayed at 0.8,
the dim went to 0.22 and the buildup to 1.25 so a 3-star still has 0.45s of committed colour before
the flash. The tier lingers are new: without them a 5-star and a 3-star got the same length of
knowing, and the knowing is the payload.

### Ten-pull

The first pull plays the full sequence. Pulls 2 to 10 play a **mini tell** instead: the light pops
in already committed to that pull's own colour, rings, and flashes.

| Mini tell | Full | Fast |
|---|---|---|
| any tier | 0.34 | 0.18 |
| extra for a 5-star | +0.30, with the shake and the second ring | +0.08 |

A counter at the top reads `n / 10`. After the tenth reveal, a tap opens the summary grid: every
pull in its rarity frame with its stars and NEW or its refund, the batch totals in one line, and
the best pull lifted, lit in its tier colour behind the cell and labelled BEST PULL. The best pull is
the highest tier, then a new ally over a duplicate, then the earliest, so the same batch always
highlights the same cell.

## Skip rules

| Input | During press, buildup or flash | During the reveal, before settled | Reveal settled |
|---|---|---|---|
| Tap | straight to the reveal | completes the reveal at once | next pull, the summary, or done |
| Hold 0.45s | skips to the end of the whole summon | same | same |
| SKIP ALL (ten-pull only) | the summary grid | the summary grid | the summary grid |

- **The end of a summon is its result.** For a ten-pull a hold goes to the summary grid. For a
  single pull it goes to the hero, settled, because a skip that hid the result would skip past the
  point of summoning.
- **A tap never leaves a hero unseen.** A tap on a reveal still stamping in completes it rather than
  moving on, so a player tapping fast through buildups still sees each hero land for one tap.
- **A hold is visible.** After 0.12s a ring fills under the hint, so a hold is something a player
  can see coming rather than a gesture they already have to know.
- **The hint says how to skip** while the light builds: TAP TO SKIP with HOLD TO SKIP ALL on a
  ten-pull, or HOLD TO SKIP TO THE HERO on a single pull.
- **Fast summon** is a device preference in PlayerPrefs under `Emberfall.FastSummon`, not a save
  field. It survives restarts and changes nothing about the account.

## Audio hooks

There is no summon audio. The sequence calls one nullable hook at each beat, and nothing in the
sequence changes when real sounds arrive: write an `ISummonAudio` and assign `SummonAudio.Hook`.

| `SummonBeat` | Fires | `stars` | `index` |
|---|---|---|---|
| `Press` | the button goes down | tier of the first pull | 0 |
| `BuildupStart` | the light appears; also at each mini tell | tier of this pull | pull position |
| `Tell` | the light commits; fires even when skipped | tier of this pull | pull position |
| `Flash` | the white flash | tier of this pull | pull position |
| `Reveal` | the hero is on screen | tier of this pull | pull position |
| `StarPing` | once per star on a 5-star, once for the row otherwise | tier of this pull | star number |
| `Summary` | the ten-pull grid appears | 0 | 0 |
| `Skip` | any tap past the buildup, any hold, SKIP ALL | tier of this pull | pull position |

Until real sounds exist, `SummonScreen` installs `SfxSummonAudio`, which maps four beats onto the
game's existing synthesised tones (tap on press, cast on the first buildup, victory or reward on
the reveal, reward on the summary) so the screen keeps the sounds it made before. It adds no audio
file.

## Cost

Everything is uGUI images on TextureLab sprites that already existed. No particle system, no
shader, no post-processing, no new texture.

- The sequence sits on **its own nested Canvas**, so the motes and rings moving every frame rebuild
  that canvas's mesh and not the screen's.
- **About 40 graphics** are live during the buildup: 24 motes, 4 ray streaks, 3 glows, 2 rings,
  the dim, the flash and the hint. About 20 during the reveal. Rebuilding a mesh of that size every
  frame is small.
- **Overdraw is the real cost.** At the gold tell the screen carries the full-screen dim, a glow
  about 900 units across, a second ring expanding to about 1300 units and four ray streaks: roughly
  three full-screen layers of alpha at the peak, for under half a second. The flash adds one more
  for 0.3s. That is fine on a mid-range phone and is the first thing to measure on a low-end one.
- **Memory:** the sprites are TextureLab's cached procedural textures, about 0.8 MB, generated once.
- **Nothing is simulated.** Every frame is posed from the pull, the phase and the time, including
  the motes, which are a hash of their index and the time. No per-frame allocation.

## Testing without grinding

The live weights make every real pull a 5-star, so the 3-star and 4-star paths cannot be reached by
playing at all. `SummonScreen.DebugPreview` plays the sequence for heroes of chosen tiers **without
rolling**: no `GameSession.Summon` call, no shards spent, nothing granted, nothing saved. It does not
force the roll, because forcing the roll would be a change to the summon logic.

- **Editor only.** It is compiled inside `#if UNITY_EDITOR`, so a player build does not contain it.
- **Clearly marked.** The sequence prints EDITOR PREVIEW, NOTHING WAS ROLLED OR GRANTED in the
  corner for the whole of a preview, including in the captures.
- **Reached from** the cheat panel (Emberfall, Debug, Cheat Panel: 3-star, 4-star, 5-star, x10
  mixed, and a Fast summon toggle) and from `SummonSequenceShots`.

`SummonSequenceShots` photographs each beat by freezing the sequence at an exact instant through
`SummonSequence.FreezeAt`, so two runs produce the same pictures. It also writes a timing strip, a
5-star pull every 0.1s, which is how the pacing was tuned.

## What is left for Pinoy to replace

All of these are placeholders built from the kit, and each is one sprite reference to swap:

- **The burst.** The light is three stacked `TextureLab.Glow` sprites, the rings are
  `TextureLab.Ring`, the rays are a stretched Glow and the motes are a small Glow. A painted burst,
  a painted ring and a painted spark sheet for each tier would carry far more character.
- **The rarity frames.** The reveal uses `EmberforgeUI.PortraitFrame`, the same frame every screen
  uses. A summon-only frame at reveal size, or a painted card, would make the reveal feel like an
  event rather than the portrait from the hero list, enlarged.
- **The star.** `TextureLab.Star` reads as a pentagon at 38 units. A painted star would make the
  5-star ping land much harder.
- **The flash.** A flat white rectangle. A radial white burst would read as light rather than a cut.
- **A reveal background.** The hero appears over the dimmed Rift. A tier-coloured backdrop behind
  the hero would help the 3-star, which has the least going on.

## A roll finding, left alone

`GameSession.Summon` sends the first three pulls of an account through `RollUnownedHero`, which draws
uniformly from every unowned hero **of any tier** and ignores the summon weights entirely. With the
live weights at 100% 5-star, the Rift's odds line says EVERY PULL IS 5-STAR, while a fresh account's
first three pulls can land a 1-star, 2-star, 3-star or 4-star hero. Either the odds line or the
guarantee is wrong. This pass did not touch it; the tell handles every tier honestly either way.

## A harness check that belongs here

Not added, because `Tools/DevHarness/Program.cs` is being edited on another branch.
`SummonTiming.cs` is engine-free so it can join the harness's compile list, and the check would
assert, for both `Full` and `Fast`:

- the tell lands inside the buildup: `0 < Tell < Buildup`;
- the time before the tell does not depend on tier, which holds if `BuildupFor(s) - Tell` only
  grows with the tier and `Tell` is not a function of it;
- a tier's committed hold never shrinks as the tier rises: `LingerFor(3) <= LingerFor(4) <= LingerFor(5)`;
- the mini tell is shorter than a full buildup: `MiniFor(5) < BuildupFor(3)`;
- Fast is actually fast: `Fast.Dim + Fast.BuildupFor(5) <= 0.5`.
