# Tempo study: does turn order move outcomes?

Measured 2026-09-23. **A measurement record. It changes no content, fixes nothing, moves no
baseline.** The instrument is `DevHarness.exe --tempo-study` (`Tools/DevHarness/TempoStudy.cs`),
which asserts nothing and writes nothing. It runs in about five minutes. `--tempo-study quick`
runs every section at an eighth of the samples, and `speed`, `first`, `price`, `mech` and `probe` run one
section each.

**MEASURED** means a number this instrument produced. **READ** means a claim taken from the code or
from content without a run behind it. Every delta is **paired**: the same seeds in both arms, the mean
of the per-seed difference, plus or minus one standard error. "WR" is win rate. "Margin" is the
guild study's measure: the share of HP the heroes kept minus the share the enemies kept, in [-1, 1].

## The question

While cutting guild floor-condition candidates, the floor-conditions record concluded that "a round is
every living unit acting once, so turn order within a round barely changes outcomes", on the strength
of three rounds of enemies acting first moving the mean margin by 0.00 to 0.06. If that generalised,
Speed and the whole turn-manipulation family would be decorative.

## Verdict

**It does not generalise.** The floor reading reproduces here exactly (MEASURED): three rounds of
enemies acting first moves the campaign margin by +0.008. But **order in the early rounds is the one
place it does not matter**, because nothing dies there. Across whole fights:

- **Raw Speed decides fights.** On the campaign ladder, the same line always acting last clears
  73.8% of the CEIL battles it clears 87.1% of as authored, and 96.3% always acting first. In mirror
  duels, always last against always first is 23% against 77%.
- **The mechanism is deaths.** Every living unit gets one entry per round (READ), so acting first can
  only matter when somebody dies before their entry comes up. When that happens, acting first *is*
  acting more. Acting first in every round gave the heroes 1.35 more turns per fight and took 2.45
  from the enemies (MEASURED).
- **Most of the authored turn-manipulation family is inert or close to it.** That covers advances,
  bare delays, the OnTurnDelayed payloads, one-round Quickened, the swap's reorder and Disrupted's
  shift on the ladder. What does move outcomes (Aeron's Turn Debt, Disrupted's drain) does it through
  **Energy**, riding on a tempo effect. Section 3 has the list.

## How it was measured

- **Campaign ladder.** The harness's instrument line (`InstrumentLineFor`: the chapter 1 three-star
  line where it reaches, the pinned line elsewhere) was geared and levelled to each stage's
  `recommendedPower` by `BuildPinnedLineAt`. It fought the stage's CEIL battle, the last battle of its
  last wave. That is the only one of the stage's battles that is close by design; the rest are won
  nearly always and can show a margin but not a win-rate change. There are 24 stages.
- **Duels.** A loadout duel (`BattleRequest.Opponent`) between a five-hero line and itself: level 40,
  no armor, no relics, no talents. The player side carries the variant. **The mirror is unbiased**
  (MEASURED): over 11 lines and 13,200 fights the player side won 50.4% ± 0.4. Individual lines sit
  off 50% by a few points, which is why every duel delta is paired against its own mirror.
- **Sweep arena.** The hero-sweep arena (`BuildHeroSweepArena`, `BuildHeroSweepFillers`): the subject
  at level 40 beside the four stat-stick fillers. Every enemy there has 190,000 HP or more, so this
  arena reads the subject's **own output** (damage, healing and shielding credited to it), never a
  win.
- **Varying Speed** clones the hero's `HeroDef` and solves `BaseSpeed` so the built Speed is multiplied
  by the factor at that Level. Every other stat is untouched. "Always first" is x100 and "always last"
  is x0.01.
- **Removing a mechanic** is done on a private `ContentDatabase` loaded for the purpose. The effect
  stays in its ability and only its tempo is neutralised. A TurnShift gets magnitude 0. Slow and
  Quickened land with magnitude 0, so the status still stands and moves no Speed. `shiftBanksAs`
  TurnDebt becomes None. Disrupted's `storedCap` becomes 0 for its shift, or the status is never
  applied for all of it. SwapTurnOrder becomes a no-op. Per kit, **only the subject** carries the
  ablation.

## 1. Does raw Speed change who wins?

**Yes, strongly, and the curve saturates.** Campaign ladder, every hero's Speed scaled, enemies
untouched. 24 stages x 200 seeds = 4,800 fights per row (MEASURED). The fielded heroes average Speed 97.0;
the CEIL enemies average 116.2.

| Hero Speed | WR | dWR vs x1.00 | dMargin | hero turns | enemy turns |
|---|---|---|---|---|---|
| always last | 73.8% | -13.3 ± 0.8 | -0.077 | -1.39 | +1.90 |
| x0.80 | 78.1% | -9.1 ± 0.7 | -0.056 | -1.11 | +1.54 |
| x0.90 | 81.5% | -5.6 ± 0.7 | -0.035 | -0.60 | +0.98 |
| x0.95 | 83.3% | -3.8 ± 0.6 | -0.021 | -0.33 | +0.52 |
| **x1.00** | **87.1%** | | | | |
| x1.05 | 91.1% | +4.0 ± 0.6 | +0.025 | +0.27 | -0.59 |
| x1.10 | 94.3% | +7.2 ± 0.6 | +0.050 | +0.47 | -1.10 |
| x1.20 | 96.0% | +8.9 ± 0.5 | +0.081 | +0.68 | -1.83 |
| always first | 96.3% | +9.2 ± 0.5 | +0.085 | +1.35 | -2.45 |

By x1.20 the line outspeeds the enemy line and there is nothing left to buy. The curve is a **rank**,
not a quantity.

**Duels** (MEASURED): five lines against themselves, 1,200 seeds per cell, pooled over the five and
paired against the mirror.

| Player Speed | last | x0.80 | x0.90 | x0.95 | x1.05 | x1.10 | x1.20 | first |
|---|---|---|---|---|---|---|---|---|
| dWR | -30.0 | -24.2 | -13.8 | -6.9 | +2.4 | +9.7 | +17.7 | +23.9 |

The standard error is ± 0.9 in every cell. Per line, the chapter 1 three-star line goes from 3% to
99% and the balance line from 11% to 92%. **The pinned line runs backwards**: 31% last, 64% at x0.70
and 35% first. Moving one hero at a time (1,200 seeds each) finds the reason in Kael. Making Kael act
last **helps** his side by +11.1 ± 1.8, and making him act first costs -29.4 ± 1.9. One reading of
his kit, not tested here: his active strips a buff from each enemy he has marked, and there is more
to strip once the enemy has acted (READ). Aeron in that line loses both ways (-19.7 last, -7.1
first), so he wants the middle of the order.

**Sweep arena** (MEASURED, 94 heroes x 80 seeds x 3 arms): a hero's **own** output rises by 32.9% on
average when it always acts first. 89 of 94 move by 5% or more. Every sweep enemy is faster than
every hero, so as authored most heroes already act last there, and "always last" moves output by only
5.1%. The most sensitive kits are low-star bodies that die early: Rusk +105%, Till +83%, Quin +83%,
Cal +88%, Aster +78%, Fenn +77%. They get more turns in before they die.

## 2. Acting first, separately from acting more

**In this engine they are the same thing, and there is no other way to act more.** READ: `TurnOrder`
builds one entry per living unit per round. `ApplyTurnShift` and `ApplySwapTurnOrder` only reorder the
entries not yet taken. `BattleEventType.ExtraTurn` is retired, and nothing grants a second turn. So a
turn is gained or lost only through dying before your entry, or killing before theirs.

Campaign CEIL battles, the same fights in different orders, 4,800 per row (MEASURED):

| Ordering | WR | dWR | dMargin | hero turns | enemy turns |
|---|---|---|---|---|---|
| as authored (Speed sort) | 87.1% | | | 28.20 | 18.12 |
| enemies first, round 1 only | 86.3% | -0.8 ± 0.6 | -0.006 | -0.10 | +0.14 |
| enemies first, rounds 1-3 | 90.0% | +2.8 ± 0.6 | +0.008 | -0.22 | +0.43 |
| enemies first, every round | 75.3% | -11.9 ± 0.8 | -0.069 | -1.48 | +1.79 |
| heroes first, every round | 96.3% | +9.2 ± 0.5 | +0.085 | +1.35 | -2.45 |
| *Attack x1.03, order as authored* | 91.4% | +4.3 ± 0.4 | +0.027 | -0.42 | -0.59 |
| *Attack x1.10, order as authored* | 97.7% | +10.5 ± 0.5 | +0.083 | -1.59 | -1.90 |

- The first three rounds are where the floor study looked, and they are nearly free: nobody dies in
  them on a CEIL that lasts about eight rounds. Enemies first for rounds 1-3 even reads **+2.8** for
  the heroes. That fits kits that pay for acting after the enemy (Kael, above), but this row does not
  test it.
- Acting first every round is worth the same margin as **+10% Attack on every hero** (+0.085 against
  +0.083). It gets there by adding turns, where Attack gets there by shortening the fight.

## 3. Each mechanic, against a control with it removed

**Arenas.** Campaign: the pinned line with the subject in its role's place, at each stage's
`recommendedPower`, against each CEIL, 24 x 40 = 960 fights. Duel: that line against itself as
authored, 800 fights. Sweep: 200 fights. **Worth** is present minus removed, in WR points. "Moves"
counts `TurnOrderShift` events caused by the subject per fight, as authored (campaign / duel).

### Structurally inert: the engine drops them (READ, and MEASURED to be exactly zero)

- **An advance aimed at a unit that has already acted is left alone** (READ: `ApplyTurnShift`, and
  `CarriedDelay` carries delays only). So **a self-advance during one's own turn can never move
  anything.** Removing it changes not one fight in any arena (MEASURED, 0.0 ± 0.0). This applies to:
  - **Dett**, basic;
  - **Kora**, basic;
  - **Lydia**, basic;
  - **Sable**, basic;
  - **Oris**, basic: the "advances his turn" half of No Fixed Refrain.

  Dett and Kora never move a body at all. Lydia, Sable and Oris keep exactly the same move count with
  the advance removed, because their moves come from other effects.
- **An authored magnitude of zero:** Seraphine's The Closed Volume passive casts a TurnShift at
  magnitude 0.0, and `ApplyTurnShift` returns on a zero fraction (READ).
- **One-round Quickened laid during the holder's own turn.** Turn order is sorted once per round, at
  its top (READ: `TurnOrder` is called from `OpenRound`). A one-round status expires at the close of
  the holder's own turn, so it is gone before the next sort. MEASURED 0.0 ± 0.0 for:
  - **Bex**, basic;
  - **Jax**, Loose Spring.

  **Yara**'s basic lays it on the lowest-HP ally and reads +0.2 ± 0.1 on the ladder.

### Never reached in any of the three arenas (0 moves; dark here, not proven inert)

**Eira**'s Leave No Corner, **Vaelis**'s Opening Moves. **Linnet**'s active moved a body only in the
sweep (0.8 per fight) and never in a duel or on the ladder.

### Measured, per kit

| Kit and mechanic | Moves (c / d) | Campaign worth | Duel worth | Sweep output |
|---|---|---|---|---|
| **Aeron**: delays banking Turn Debt, whole | 5.0 / 5.2 | **+20.3 ± 1.6** | **+23.0 ± 2.3** | +7.6% |
| Aeron: the **debt** alone (delay kept) | | **+18.2 ± 1.5** | **+27.6 ± 2.3** | +7.7% |
| Sable: active delay | 0.8 / 0.9 | +3.0 ± 1.1 | +5.1 ± 1.6 | +3.3% |
| Lydia: active delay | 0.8 / 0.6 | +0.8 ± 1.0 | +0.8 ± 0.7 | +0.4% |
| Avera: active delay | 0.3 / 0.3 | -0.1 ± 0.2 | +1.5 ± 0.8 | 0 |
| Merek: Interjecting Clause delay | 2.4 / 4.4 | +0.1 ± 1.0 | -1.1 ± 1.8 | 0 |
| Merek: Priority Inversion advance | | -0.5 ± 0.9 | +0.8 ± 1.8 | 0 |
| Bren: Reserved Passage advance | 0.2 / 0.4 | +0.2 ± 0.4 | +0.3 ± 1.2 | +0.4% |
| Finn: Aftershock Clock | 0.0 / 0.5 | 0.0 | -0.4 ± 0.8 | +9.0% |
| Tomas: basic delay | 1.6 / 0.2 | -0.4 ± 0.7 | +1.9 ± 1.0 | -2.4% |
| Jax: basic delay | 2.1 / 3.7 | -1.1 ± 0.7 | -6.4 ± 2.1 | -0.3% |
| Tamsin: basic and active delays | 1.7 / 3.7 | -0.2 ± 0.9 | -6.6 ± 2.3 | +1.7% |
| Till: basic delay | 1.3 / 3.1 | -1.4 ± 0.6 | -5.8 ± 2.1 | +0.5% |
| **OnTurnDelayed payloads**, shifts kept: Avera Encore Denied, Jax Loose Spring, Merek Unsettled Timing, Oris Restless Hands, Sable Never Waiting, Tamsin Unraveling Order | | each between -0.3 and +1.0 | each between -3.0 and +0.6 | ≤ 6.5% |
| **Disrupted**, shift only (Oris; drain kept) | 5.1 / 11.0 | -0.1 ± 0.8 | **+17.0 ± 2.0** | +2.3% |
| Disrupted whole, Oris | | +5.8 ± 1.0 | **+43.0 ± 1.9** | 0 |
| Disrupted whole, Avera (drain only) | | +5.9 ± 1.0 | **+23.3 ± 2.2** | +20.7% |
| Disrupted whole, Iolan (drain only) | | +1.3 ± 0.4 | +10.6 ± 1.4 | 0 |
| **Slow**, Corin | | -0.4 ± 0.6 (margin +0.031) | -3.3 ± 1.9 | +29.1% |
| Slow, Elyra | | +0.4 ± 0.5 (margin +0.040) | +10.9 ± 2.4 | -3.9% |
| Slow, Sena | | -0.1 ± 0.1 | -2.1 ± 0.8 | -1.2% |
| Slow, Tomas | | 0.0 ± 0.5 | -6.4 ± 1.6 | -2.1% |
| **Quickened** (+8% standing), 14 five-star passives | | Vaelis +3.0 ± 0.9, Aeron +2.7 ± 1.4, Oris -2.3 ± 1.1; the other 11 within ± 1 | Dahlia +7.3, Isolde +6.4, Edrin +5.5 (± 2.2); Oris -38.6; the rest within ± 2 | ≤ 10% |
| **Swap**, Merek: swap removed (its payload then lands on nobody) | 2.4 / 4.4 | +2.2 ± 1.3 | +6.8 ± 1.7 | -1.9% |
| Swap, Merek: payload removed, swap kept | | +2.6 ± 0.7 | +5.1 ± 0.9 | 0 |

The "each between" row covers six payloads. Per payload, campaign then duel: Avera Encore Denied
0.0 / +0.4, Jax Loose Spring 0.0 / -0.3, Merek Unsettled Timing -0.3 / -3.0, Oris Restless Hands
+0.1 / +0.6, Sable Never Waiting +1.0 / +0.6, Tamsin Unraveling Order +0.9 / -0.4.

Read per mechanic:

- **Delays** alone are worth about nothing on the ladder: pooled over 11 kits, +1.9 ± 0.3, and nearly
  all of that is Aeron. Sable's +3.0 is the one bare delay that clearly pays. In duels three delays read
  about -6. The obvious suspect, the delay landing on the opposing Kael, was tested and mostly does
  not hold. Against the same line with Kael replaced by Rovan or Brenna, 800 fights each, Till goes
  from -7.1 against Kael to -2.4 and -1.8. Jax and Tamsin read -1.4 to -4.6 against all three
  Bulwarks alike (MEASURED). Treat those as small, noisy and not understood.
- **Turn Debt is the exception, and it is not tempo.** Removing only the debt and keeping Aeron's
  delays costs his side 18 points on the ladder and 28 in duels. The delay itself is worth between
  -5 and +2. The debt pays in Energy: the debtor repays it at its next turn start (READ:
  `RepayTurnDebt`), and his Never Yield the Pace and Press Forward feed on it.
- **Disrupted's value is its Energy drain.** Its shift is worth nothing on the ladder (-0.1 ± 0.8) and
  +17 in duels, where both sides fight over the same slots. Avera's and Iolan's Disrupted carry no
  shift at all and are worth +23 and +11 in duels.
- **Slow** is about zero in win rate on the ladder. For Corin and Elyra it is a real margin gain
  (+0.03 to +0.04). In the sweep, Corin's three stacks put the enemies below his Speed and his output
  rises 29%. Enemy Slows, mechanic-wide on the ladder, cost the heroes -0.8 ± 0.4.
- **Quickened** as a standing +8% is about zero on the ladder. It pays only where it changes a rank.
  **Oris's -38.6 is targeting, not tempo** (MEASURED, section 4).
- **Swap.** Removing the payload alone costs as much as removing the whole swap (+2.6 against +2.2 on
  the ladder, +5.1 against +6.8 in duels). So **the reorder itself is worth about zero**, and Merek's
  swap pays through the Skill Damage Down it lays on the body it pushed back.

Mechanic-wide on the ladder (instrument lines, heroes and enemies ablated at once, 1,920 fights),
MEASURED:

| Mechanic | Worth |
|---|---|
| every TurnShift | +18.9 ± 1.1 (Aeron is in the pinned line) |
| Turn Debt | +14.2 ± 1.0 |
| Disrupted | +11.6 ± 0.8 (Avera is in the chapter 1 line) |
| advances | +0.3 ± 0.2 |
| OnTurnDelayed payloads | +0.1 ± 0.1 |
| Quickened | +1.3 ± 0.8 |
| Slow (only enemies carry it in these lines) | -0.8 ± 0.4 |

Talent-carried tempo is **not measured**: no fight selected a talent. That leaves out Aeron I (Slow)
and II (advance), Elyra II (Slow), Merek I (delay) and II (a swap payload).

## 4. When it matters

**Order moves the margin by roughly the same amount in every fight. It changes the winner only when
the fight is within that amount of even, and only once bodies start dying.**

- **Closeness** (MEASURED, ladder, always last against always first, 3,840 per row):

  | Line level | Base WR | dWR | dMargin |
  |---|---|---|---|
  | 85% of `recommendedPower` | 23% | +3.8 ± 0.3 | +0.132 |
  | 100% | 87% | +22.7 ± 0.7 | +0.162 |
  | 115% | 100% | +1.0 ± 0.2 | +0.122 |

- **Duels at a level gap** (MEASURED, 2,400 per row): with the opponent at level 34, 37, 40, 43 and
  46, the last-to-first swing is +2.3, +24.9, **+42.3**, +27.3 and +4.2 points. The margin moves by
  +0.13 to +0.39 at every gap.
- **Length.** On stages 1-1 to 1-5 the CEIL is over in 2.8 to 6 rounds, and last against first moves
  the margin by +0.03 to +0.16 and the win rate by 0. On stages 1-6 to 1-11 fights run 7.4 to 9 rounds
  and the win rate swings by +36 to +56 points. The effect lives in the rounds where units die.
- **Whose turn moves** matters as much as whether it moves. A healer or support acting first is
  worth a lot: Nereia first +19.1, Isara +16.3, Avera +42.7. A reactive Bulwark acting first costs
  his side: Kael first -29.4.
- **Speed is also a targeting input.** `HighestSpeedEnemy` and `LowestSpeedEnemy` read it (READ). Oris's
  Quickened makes him the fastest body on his side, and so the target of an opposing Oris's basic
  strike. Against a mirror it costs -35.1 ± 2.2 and he dies 67% of the time against 32%. Against the
  pinned and balance lines, which carry no Oris, it is worth -0.1 ± 0.3 and +0.4 ± 0.3 (MEASURED, 800
  each). No enemy in `enemies.json` targets by Speed (READ), so this is a PvP-only effect.

## 5. Is Speed worth its place in the power formula?

**Yes, and it is not overpriced.** `StatMath.PowerScore` charges 1.9 per point of Speed against 2.4
per point of Attack.

- **Share of power** (MEASURED): on the instrument line, Speed is 13.6% of power on average. That
  falls from 18.7% at 1-1 to 10.6% at 2-12, because Speed grows 0.35 per Level while HP, Attack and
  Defense grow by `growth` (READ: `StatMath.Build`).
- **Value for the same power** (MEASURED, ladder, 4,800 per row):

  | Bought | dWR | dMargin |
  |---|---|---|
  | +10% Speed | **+7.2 ± 0.6** | +0.050 |
  | the same power as Attack | +4.2 ± 0.4 | +0.029 |
  | +20% Speed | **+8.9 ± 0.5** | +0.081 |
  | the same power as Attack | +7.4 ± 0.4 | +0.056 |

  In duels +10% Speed is worth +9.7 ± 0.9.

**What is wrong with the price is its shape, not its level.** Speed's value is a rank against the
opposing line. It pays steeply until the line outspeeds the enemy, then stops: x1.2 and always-first
read the same. The formula prices it linearly and forever. Today's lines sit below the CEIL enemies'
Speed (97 against 116), which is the steep part, so the 1.9 weight under-prices the first 10% and
over-prices anything past parity. That is a content and economy question. Nothing here changes it.

## Content decisions for Pinoy

Kits whose authored value rests on a mechanic this study found inert or near-inert. **These are
content decisions, not engine ones:** the engine does what it says.

1. **Can never fire (structural).** Dett (basic self-advance), Kora (basic self-advance), Lydia (basic
   self-advance), Sable (basic self-advance), Oris (the "advances his turn" half of No Fixed Refrain),
   Seraphine (The Closed Volume's zero-magnitude shift), Bex (basic one-round Quickened on himself),
   Jax (Loose Spring's one-round Quickened). Yara's one-round Quickened on an ally is +0.2.
2. **Never reached in these arenas.** Eira's Leave No Corner and Vaelis's Opening Moves. Linnet's
   active shift outside the sweep. Worth a room of their own before a verdict.
3. **Measured at about zero on the ladder.**
   - Bare delays: Avera (active), Finn (Aftershock Clock), Jax (basic), Linnet, Lydia (active), Merek
     (Interjecting Clause), Tamsin, Till and Tomas.
   - Advances: Bren (Reserved Passage) and Merek (Priority Inversion).
   - Every OnTurnDelayed payload: Avera's Encore Denied, Jax's Loose Spring, Merek's Unsettled Timing,
     Oris's Restless Hands, Sable's Never Waiting and Tamsin's Unraveling Order.
   - The standing +8% Quickened on Altheon, Aminah, Arden, Kiva, Nysera, Sylven, Talren and Velis.
     Dahlia, Edrin and Isolde also read about zero on the ladder, but pay +5 to +7 in duels.
   - The hero Slows in win rate: Corin, Elyra, Sena and Tomas. Corin and Elyra do pay in margin.
   - Merek's swap as a reorder. Its payload pays.
   - Disrupted's shift on the ladder (Oris).
4. **Tempo-shaped but paying through Energy.** Aeron's Turn Debt and every Disrupted (Avera, Iolan,
   Oris). These are the family's real contributors, and they are not tempo.

## Also found (READ, not fixed; this pass changes no code)

Several comments in `CombatSimulation.cs` still say the Disrupted and Turn Debt paths are unreachable.
`RepayTurnDebt`, `InterceptDisrupted`, the `ConsumeTurnDebt` case and the Momentum Breaker branch of
`AddEnergy` all say so, as does the `TurnShift` case's "ShiftBanksAs is None for every shift in shipped
content". Aeron's basic and active bank Turn Debt, and Oris, Avera and Iolan apply Disrupted. This
study measured both firing in every arena.

## Limits

- The campaign is read only on each stage's CEIL battle, with the harness's instrument lines. The
  other 143 battles of a stage are won nearly always and can move only in margin.
- No talents, relics or armor were used in duels, and every duel was at level 40.
- Paired arms share a seed but not an RNG path once the fights diverge, so the paired standard errors
  are honest but not as tight as a byte-identical control would give.
- Neutralising a TurnShift also silences everything answering OnTurnDelayed. That is why those
  payloads are measured as their own row, and why the swap's reorder is read as a difference of two
  rows rather than directly.
- Per-kit rows at about ± 2 standard errors are expected to include a few false readings across this
  many kits. The structural zeros and the double-digit readings are not in that class.
