# Parity study: do the fixed-arena instruments misprice what only pays when a fight is close?

Measured 2026-09-23, on `main` after the 72-stage campaign landed. **A measurement record. It
changes no content, fixes nothing, moves no baseline and proposes no retune.** The instrument is
`DevHarness.exe --parity-study` (`Tools/DevHarness/ParityStudy.cs`), which asserts nothing, writes
nothing and is not run by the gate. `--parity-study calib`, `controls`, `talents` and `stars` run one
section each (`talents` and `controls` run the calibration first), and `quick` runs everything at a
quarter of the samples. On a 12-thread machine `controls` takes about 90 seconds, `talents` about 6
minutes of CPU and `stars` about 7.

**MEASURED** means this instrument produced the number. **READ** means a claim taken from code or
content with no run behind it. Every delta is **paired**: the same seeds in both arms, the mean of the
per-seed difference, plus or minus one standard error, with t = mean / se. "WR" is win rate.
"Margin" is the guild study's measure: the share of HP the player side kept minus the share the
far side kept, in [-1, 1].

## The question

The tempo study (`Docs/TEMPO-STUDY.md`) found that a mechanic's worth in win rate concentrates at
parity: +22.7pp at 100% of recommended power and +1 to +4pp away from it. The simulated player
meets every stage at x1.15 to x1.20 of recommended. The three instruments that price kits all read
**output** in a **fixed arena**: the star band (`CheckStarBandByRole`), the authored-talent sweep, and
TALENTS WORTH PICKING (`CheckTalentsAreWorthPicking`). In that arena every enemy has 190,000 HP or
more (READ, `BuildHeroSweepArena`). If worth lives at parity, those instruments may systematically
undervalue control, tempo and disruption and overvalue raw output.

## Verdict

**Partly, and mostly for a different reason than parity.**

- **Control and debuff kits are undervalued by the sweep.** This is MEASURED over all 94 heroes. Kits
  carrying an enemy-facing control or debuff effect read on average +0.16 of their role's 5-star
  median higher at parity than in the sweep, relative to their tier's own shift (31 heroes against
  63, Welch t 4.0). **Tempo kits are not undervalued**: SpeedUp, TurnShift, EnergyGain and the swap
  family read -0.04, t -0.5, which agrees with the tempo study's finding that most of that family is
  inert.
- **Raw output is overvalued at the top.** Arden (x1.07 in the sweep, x0.26 / x0.44 at parity),
  Cyrra (x1.35, x0.72) and Hale III (+28.6% Attack-equivalent in the sweep, +1.3 / +1.9% at parity)
  are the clearest cases.
- **Of the 45 talents TALENTS WORTH PICKING calls flat, 16 come alive at parity with a material
  effect, and 29 do not.** Most of the 16 are not a parity effect as such. The sweep never fires
  their trigger, or it does not count the currency they pay in. 11 of the 16 still move the margin
  at x1.15, where the player actually plays. The effect there is only invisible in win rate, because
  base WR is 92%.
- **The sweep's ranking is mostly right.** The whole-roster rank correlation between the sweep ratio
  and the parity ratio is 0.84. Four enforced roles agree at 0.77 or better, and Breaker is the
  exception at 0.45 / 0.30.
- **Recommendation: keep the sweep, and read it alongside a parity ladder reading. Do not replace
  it.** The argument is in the last section.

## How it was measured

- **Ladder calibration (section L).** The pinned line (`BuildLineAt`, as in the tempo study) is geared
  and levelled to x of each stage's `recommendedPower`. It fights that stage's CEIL battle
  (`TempoCeil`) at 18 factors from x0.70 to x1.30, 40 seeds a cell. A stage's **parity power** is the
  factor whose win rate is nearest 50%. A stage is left out when that nearest reading is outside
  25%..75%, or when it sits at the grid's lowest factor, which is the line's level floor rather than a
  choice. **59 of the 72 stages have a parity power** (MEASURED). The 13 excluded are 1-1 to 1-8, 2-3,
  3-8, 4-3, 4-7 and 5-3: stages won at every power the instrument can reach. Pooled over all 72, the
  line wins 76% at x1.00, 92% at x1.15 and 95% at x1.20. A single ladder-wide factor is therefore
  not parity.
- **Duel.** The pinned line with the subject in its role's place (`NormalPlayLineFor`), level 40,
  against **the same line with no talent**, via `BattleRequest.Opponent`: no armor, no relics. The
  only difference between the sides is the variant, so power is matched by construction. There are
  2,000 seeds an arm.
- **Parity ladder.** That same line at each stage's parity power, against its CEIL, 60 seeds a
  stage, 59 stages = **3,540 fights an arm**.
- **Live ladder.** The same line at x1.15, where the simulated player meets its stages, all 72
  stages, 40 seeds each = **2,880 fights an arm**. It is reported and not used to class anything.
- **Today.** TALENTS WORTH PICKING recomputed exactly as the gate takes it: the sweep arena plus the
  talent's dark room where it has one, 40 seeds, own output. It reproduces the gate's **38 gain /
  45 flat / 7 lose** (MEASURED on this `main`, both by `--check CheckTalentsAreWorthPicking` and
  inside the study). The brief's 37 / 47 / 6 was not what `main` reads.
- **Worth in % Attack.** Every hero also gets Attack x1.05 in all three arenas as a yardstick.
  Pooled over the 30 talent-bearing heroes it reads +5.0% own output in the sweep, +0.026 margin in
  the duel and +0.016 margin on the parity ladder. A talent's worth is its reading over that
  yardstick, times five, so that "+2%" means "worth what +2% Attack is worth in this arena".
- **Classes.** TALENTS WORTH PICKING's own rule is applied to the duel and ladder readings: LOSE if
  any reading has mean < 0 and t <= -2, else GAIN if any has t >= 2, else FLAT. The rule is applied
  three ways: by margin, by win rate and by own output. **Material** additionally requires that a
  reading carrying the class is worth at least 1% Attack in the same direction.
- **Star band at parity.** The hero's line (the pinned line with the hero in its role's place),
  level 40, duels a fixed reference line: the pinned line, and separately `NormalPlayBalanceLine`. The
  reference line's **level** is walked until the hero's line wins half. The walk is coarse at 200
  fights a level, then five or more levels around the crossing at 600 each, with a logistic fit. The
  fitted level, converted to the reference line's PowerScore, is the line's strength. The hero's
  **worth** is that strength minus the strength of the same line with the hero's slot empty. The
  **parity ratio** is worth over the role's 5-star median worth, the same shape as the band's own
  ratio. One level near 40 is 532 power against the pinned reference and 479 against the balance
  reference. Every fitted level carries a standard error of 0.10 levels or less (MEASURED), about 2%
  of a 5-star's worth.
- **Parallel runner.** Fights run on all cores. `CombatSimulation.Run` is a pure function of its
  request (READ), and control C1 checks that claim. See also the fault note under **Caveats**.

## Controls: the arenas can tell working from inert

All MEASURED, 6 heroes (Kael, Aeron, Nereia, Seraphine, Isolde, Dahlia), 2,000 duel seeds and 3,540
parity-ladder fights an arm per hero.

| subject | duel, pooled | parity ladder, pooled | sweep |
|---|---|---|---|
| **placebo**: an empty fourth talent, selected | 0 of 12,000 fights change; every delta exactly 0 | 0 of 21,240 change; exactly 0 | +0.0% in all six |
| **sham**: Attack x1.002 | dWR +0.1 (t 0.9), dMrg t 2.6 | dWR +0.1 (t 3.7), dMrg t 4.9 | +0.1 to +0.2%, t 23 to 91 |
| Attack x1.05 | dWR +3.8 (t 10.5) | dWR +2.7 (t 16.8) | +4.1 to +5.5% |
| Max HP x1.10 (survival only) | dWR +4.1 (t 10.6) | dWR +6.4 (t 26.4) | +5.8 to +21.3% |
| Defense x1.25 (survival only) | dWR +3.6 (t 10.6) | dWR +1.8 (t 12.8) | +3.4 to +9.1% |
| Speed x1.10 (tempo only) | dWR **-1.5** (t -2.4) | dWR +1.7 (t 4.8) | -6.0 to +13.9%, mostly t < 2 |

- **C1: the parallel runner reproduces the serial one.** 400 of 400 duel fights were byte-identical
  (`HeroSweepHash`), and two parallel runs of the same 1,180 ladder fights were identical.
- **C3: the duel mirror is fair.** Over all 30 talent heroes' control arms (n = 60,000), the player
  side wins 50.4%.
- **Real talents, too.** The inert ones read inert: Zella I, Aeron I, Vaelis I and Velis III change
  the event log but not one fight's margin, result or subject output, in any arena. The known traps
  read as losses: Kael III is -2.2% / -2.8% Attack (t -7.5 / -11.4), and Mireth I is -16.2% / -7.3%
  (t -23.3 / -16.4). Talents that gain everywhere, such as Oris I and Seraphine I, read as gains.

**What the controls also show, and why "material" exists.** The paired parity arenas are so
sensitive that the sham, **+0.2% Attack**, clears the class rule's t >= 2 for **24 of the 30
heroes** by margin. At these sample sizes, significance alone says a talent does *something*,
however small. So the class counts without a materiality floor are not comparable to the gate's
40-seed counts. Under the 1%-Attack floor, the sham clears for **1 of 30**. The material columns
below are the honest comparison.

**The duel has quirks of its own.** Speed x1.10 on a tank reads *negative* in the mirror (Kael
-11.6pp, Aeron -5.7pp) and positive on the ladder. Oris II reads +41.3pp WR in the duel and +5.0pp on
the ladder. Mireth III reads -8.4pp in the duel and +0.0 on the ladder. A duel against your own
mirror is a real arena (friend duels and the Proving are one), but it is not a campaign. **The
parity ladder is the reading to trust when the two disagree.**

## 1. The 90 talents at parity

MEASURED, 90 talents on 30 heroes, n per arm as above.

| reading | gain | flat | lose |
|---|---|---|---|
| today (sweep + dark rooms, own output), the gate's rule | **38** | **45** | **7** |
| today, material (>= 1% Attack) | 35 | 49 | 6 |
| parity, by margin, the gate's rule | 57 | 20 | 13 |
| parity, by win rate, the gate's rule | 49 | 31 | 10 |
| parity, by own output, the gate's rule | 62 | 18 | 10 |
| **parity, by margin, material** | **46** | **37** | **7** |
| *sham (Attack x1.002 on each of 30 heroes), by margin* | *24* | *6* | *0* |
| *sham, material* | *1* | *29* | *0* |

Rank agreement of worth in % Attack (Spearman, 90 talents): today against the duel 0.53, today
against the parity ladder 0.60, duel against the ladder 0.76.

**Every talent that changes material class** (29), from today to parity, with the worth in % Attack
read today / in the duel / on the parity ladder:

| talent | today -> parity | worth: today / duel / ladder | live ladder at x1.15 |
|---|---|---|---|
| Aeron III | flat -> gain | +0.0 / +8.9 / +7.2 | dMrg t 3.6, dWR +0.9 |
| Altheon I | flat -> gain | +0.2 / +0.9 / +1.1 | dMrg t 4.7 |
| Dahlia II | flat -> gain | +0.4 / +3.6 / +2.1 | dMrg t 11.9 |
| Edrin I | flat -> gain | +2.7 / +3.7 / +3.9 | dMrg t 8.5 |
| Elyra I | flat -> gain | +0.7 / +3.3 / +10.0 | dMrg t 9.7, dWR +1.1 |
| Kiva III | flat -> gain | -0.8 / +0.7 / +1.2 | dMrg t 4.3 |
| Marrow II | flat -> gain | +0.0 / +4.0 / +1.1 | dMrg t 4.7 |
| Marrow III | flat -> gain | +3.4 / +2.6 / +6.1 | dMrg t 5.3 |
| Merek I | flat -> gain | +0.0 / +0.9 / +2.2 | dMrg t 1.4 |
| Merek II | flat -> gain | +0.0 / +0.2 / +1.1 | dMrg t 3.4 |
| Merek III | flat -> gain | +0.0 / +2.5 / +5.7 | dMrg t 8.7 |
| Nereia III | flat -> gain | +0.5 / +0.1 / +1.9 | dMrg t 1.4 |
| Oris III | flat -> gain | +0.5 / +2.4 / +4.8 | dMrg t 3.6 |
| Sylven II | flat -> gain | +0.0 / +2.7 / +0.3 | dMrg t 5.1 |
| Talren I | flat -> gain | +0.3 / +0.7 / +2.3 | dMrg t 1.4 |
| Talren III | flat -> gain | +0.0 / +0.1 / +2.5 | dMrg t 6.4 |
| Zella III | flat -> gain | +0.0 / +5.6 / +6.1 | dMrg t 7.0 |
| Edrin III | flat -> lose | +0.8 / -1.0 / -3.6 | dMrg t 0.0 |
| Kael I | flat -> lose | +0.0 / -2.3 / -0.4 | dMrg t -1.5 (weak: no parity t reaches 3) |
| Nysera I | lose -> gain | -9.4 / +1.6 / +0.3 | dMrg t 1.1 |
| Rheia I | lose -> flat | -8.0 / +0.0 / -0.1 | dMrg t 0.5 |
| Rheia III | lose -> flat | -2.0 / +0.1 / +0.2 | dMrg t 3.4 |
| Altheon III | gain -> lose | +1.2 / -1.7 / -0.3 | dMrg t -2.4 |
| Mireth III | gain -> lose | +1.5 / -6.7 / +0.9 | dMrg t 3.5 (the loss is the duel only) |
| Elyra III | gain -> flat | +2.9 / +0.2 / +0.8 | dMrg t 0.8 |
| Isolde II | gain -> flat | +4.6 / +0.1 / +0.4 | dMrg t 2.0 |
| Isolde III | gain -> flat | +2.2 / +0.1 / +0.6 | dMrg t 0.5 |
| Vaelis III | gain -> flat | +3.2 / +0.2 / +0.1 | dMrg t -0.1 |
| Velis I | gain -> flat | +1.1 / +0.6 / +0.0 | dMrg t -0.5 |

Under the gate's own significance rule, without materiality, **36 talents change class**, and the
harness prints them all under "CLASS CHANGES". 23 of them are in the table above. The other 13 are
changes that only significance sees, each worth under 1% Attack: Altheon II, Aminah I, Aminah II,
Arden II, Brenna I, Brenna II, Caldus III, Kiva II, Mireth II, Rheia II, Rovan I, Seraphine III and
Sylven I. Going the other way, six material changes are invisible to significance alone, because
both readings clear t >= 2 in the same direction and only one is worth 1% Attack: Altheon I,
Dahlia II, Elyra III, Isolde II, Vaelis III and Velis I.

Four of today's seven costly talents read differently at parity. **Nysera I** (a TRADE entry: the
Dispel strips the taunting tank's Provoke) gains at parity, which vindicates its trade reason, and
so does **Kiva III** (+0.7 / +1.2% Attack). **Rheia I** and **Rheia III**, both TRADE entries, read
flat. The **two traps**, Kael III and
Mireth I, lose everywhere. **Marrow I** loses everywhere too: -2.2% / -7.7% Attack, which is more
than its own-output loss suggests. Its trade reason ("a share of three allies' damage") does not
buy the line anything at parity.

**Win rate concentrates at parity exactly as the tempo study found.** The mean |dWR| per talent is
**1.29pp on the parity ladder and 0.17pp on the live ladder at x1.15**, 7.6 times smaller. Margin
does not collapse the same way: 45 of the 90 still clear t >= 2 by margin at x1.15. A player at
x1.15 wins either way, and the talent changes how much HP they win with.

## 2. Today's 45 flat talents: artefact or content problem

MEASURED. "Dead" means dead in every arena this study has: the sweep, the dark room, the duel, the
parity ladder and the x1.15 ladder, all with the pinned line and the hero in their role's place. A
talent that needs a specific teammate or enemy the pinned line and the 72 stages never field could
still be alive elsewhere. That is a limit of "everywhere", not a finding.

**Alive at parity: a measurement artefact (16).** The talent is worth 1% Attack or more by margin
at parity.

| mechanism | talents |
|---|---|
| **The sweep never fires it, or does not count what it buys**, and it still moves margin at x1.15 (dMrg t >= 2) (11) | Aeron III, Edrin I, Elyra I, Marrow II, Marrow III, Merek II, Merek III, Oris III, Sylven II, Talren III, Zella III |
| **Parity only**: material at parity, nothing measurable at x1.15 (5) | Merek I, Nereia III, Talren I; and two losses: **Edrin III** (-1.0 / -3.6% Attack) and **Kael I** (-2.3 / -0.4%, weak) |

Most of the 11 are a **reach** failure rather than a currency one. Aeron III, Zella III and Merek III
raise their hero's *own output* by +4 to +10% in the parity arenas, and their sweep fight is
byte-identical with and without the talent. The sweep cannot reach the condition. **Marrow II** and
**Sylven II** are the **currency** cases: margin rises at parity while their own output barely
moves.

**Significant but immaterial: at the sham's noise floor (13).** Each clears t >= 2 somewhere, but
under 1% Attack, the same place the +0.2% Attack sham lands: Altheon II, Aminah I, Aminah II,
Brenna I, Brenna II, Caldus III, Darian I (by win rate only), Kiva II, Mireth II, Rheia II, Rovan I,
Seraphine III, Sylven I.

**Dead in every arena: a content problem (16).**

| kind | talents |
|---|---|
| **Cosmetic**: changes the event log, never one fight's margin, result or subject output | Aeron I, Darian III, Vaelis I, Velis III, Zella I |
| **Noise**: changes some fights, worth nothing measurable | Aeron II, Aminah III, Brenna III, Darian II, Kael II, Kiva I, Rovan II, Sylven III, Vaelis II, Velis II, Zella II |

**The split: 16 of the 45 are the instrument's blind spot, and 29 are a content problem.** That is
16 dead plus 13 worth less than a fifth of a percent of Attack either way.

## 3. The star band at parity

MEASURED, 94 heroes, two references each, plus a body reading against the pinned reference.

**Parity compresses the tiers, so the band's edges do not transfer.** The median ratio against the
role's 5-star median, by tier:

| tier | n | sweep | parity (pinned ref / balance ref) |
|---|---|---|---|
| 5-star | 30 | x1.00 | x1.00 / x1.00 |
| 4-star | 26 | x0.58 | x0.67 / x0.75 |
| 3-star | 18 | x0.36 | x0.62 / x0.53 |
| 2-star | 12 | x0.21 | x0.36 / x0.42 |
| 1-star | 8 | x0.12 | x0.16 / x0.23 |

In a close fight, a body with a lower-tier kit is worth much more of a 5-star than its output
suggests. HP and Defense are worth as much there as the controls show, and the sweep's output reads
them only through survival. Applying the sweep's band edges to parity ratios changes **17 of the 67
graded verdicts**, with both references agreeing. **15 of those 17 are a lower tier reading ABOVE its
band**: Toren, Siofra and Nadine (4-star); Petra, Tomas, Aster and Avera (3-star); Barr, Garron,
Nara, Kora and Sena (2-star); Hal, Ulric and Iri (1-star). That is the compression, not 15 separate
findings. The other two are 5-stars reading BELOW: **Cyrra** (x1.35 in the sweep, x0.72 / x0.73 at
parity) and **Talren**. The band's rule of thumb that a tier's ceiling is the tier above's median
would have to be re-derived on the parity scale before any parity verdict meant anything.

**The honest per-hero signal is rank within tier.** The whole-roster Spearman between the sweep
ratio and the mean parity ratio is **0.84**. By role, sweep against the two parity references:

| role | enforced | Spearman |
|---|---|---|
| Bulwark | yes | 0.92 / 0.85 |
| Striker | yes | 1.00 / 1.00 |
| Breaker | yes | **0.45 / 0.30** |
| Mender | yes | 0.77 / 0.86 |
| Vanguard | yes | 0.87 / 0.92 |
| Marksman | yes | 0.82 / 0.90 |
| Warden, Stalker, Binder, Tinkerer | no | 0.50, 0.37 / 0.60, 0.90 / 1.00, 0.68 / 0.80 |

**Who moves, net of their tier's own shift** (the mean parity ratio minus the sweep ratio, minus the
tier median of the same):

- **Up.** Oris (5-star Tinkerer, +0.72: Disrupted, SpeedUp), Aster (3-star Breaker, +0.39:
  DamageTakenUp), Hale (5-star Marksman, +0.34: Vulnerable), Siofra (4-star Breaker, +0.31), Roan
  (3-star Stalker, +0.29: EffectResistDown), Aminah (5-star Mender, +0.28), Iolan (4-star Binder,
  +0.28: Disrupted, EffectHitDown), Nadine (4-star Vanguard, +0.27: Vulnerable).
- **Down.** Arden (5-star Tinkerer, -0.72), Cyrra (5-star Mender, -0.62), Edrin (5-star Marksman,
  -0.31), Darian (5-star Vanguard, -0.31), Rhys (4-star, -0.28), Leon (4-star, -0.27).

**By kit content** (READ from `heroes.json`, excluding talents; MEASURED residual):

| kit carries | heroes | residual against those without | Welch t |
|---|---|---|---|
| any enemy-facing control or debuff | 31 | **+0.160** | **4.0** |
| of which damage amplifiers (Vulnerable, DamageTakenUp, DefenseDown, EffectResistDown, BlockDown) | 12 | +0.164 | 3.0 |
| of which hard control and disruption (TurnShift, Slow, Disrupted, Stun, Silence, CancelCast, Taunt, Dispel, suppressions, the stat-downs) | 23 | +0.100 | 2.3 |
| tempo (SpeedUp, TurnShift, EnergyGain, SwapTurnOrder, ConsumeTurnDebt) | 17 | -0.040 | -0.5 |
| support (Heal, Shield, Guard, Cleanse, the ally stat-ups, RedirectLethal) | 42 | -0.065 | -1.7 |

The brief's hypothesis holds for **control and debuffs**. Their worth lands in teammates' damage or
in the enemy's lost turns, and the sweep credits neither. It does **not** hold for tempo.

**Kit over body.** The same slot filled by the hero's own stats with the sweep filler's token kit
gives a "body" worth. Worth minus body is what the kit adds, and its ratio over the role's 5-star
median tracks the sweep a little less closely than the parity ratio does (Spearman 0.82) and moves
the same heroes: Arden -0.76, Oris +0.76, Cyrra -0.68, Nadine +0.64, Iolan +0.54. It is the
difference of two fitted levels, so it is the noisier reading. It is printed and not relied on.

## 4. Should the sweep stay the primary instrument?

**Keep it primary for what it is good at, and read a parity ladder reading alongside it wherever a
verdict depends on worth. Do not replace it.**

For keeping it:

- **It agrees with parity where it can see.** Rank correlation is 0.84 across the roster and 0.77 or
  better in five of the six enforced roles. For damage-first kits the two instruments tell the same
  story.
- **It is isolated.** It reads the subject alone beside four stat sticks. The parity readings are
  line readings, and a hero's worth there depends on the line it was measured against. Within a
  role, the rank agreement between the two reference lines runs from 1.00 (Striker) down to 0.63 (Tinkerer).
- **It is cheap and stable.** It takes seconds, versus minutes. It did not move when the campaign
  was replaced wholesale, while the parity ladder had to be re-calibrated from scratch (24 stages
  to 72, 16 parity stages to 59).
- **Its scale is the one the band was built on.** The parity scale compresses the lower tiers by up
  to x1.7, so replacing the instrument means re-deriving every band edge. That is a design decision
  about what a star is worth, not a measurement fix.

Against leaving it alone:

- **It is blind in two measured places.** Reach: 11 talents it calls flat move margin even at x1.15,
  most with their sweep fight byte-identical, which means the sweep never fired them. Currency:
  control and debuff kits sit +0.16 of a 5-star median below where they fight (t 4.0), and a handful
  of 5-star output kits (Arden, Cyrra) sit well above.
- **Parity is where the choice matters.** The player meets walls at parity. Away from walls, a
  talent moves win rate by 0.17pp on average and nothing is at stake. An instrument that prices a
  talent in a fight no one can lose is pricing the regime where the price does not matter.

Why not the duel as the second reading: it is a mirror. Speed reads negative there for tanks, and
Oris II and Mireth III read five to ten times their ladder value. **The parity ladder is the second
instrument**, with the duel as a cross-check.

In practice that means two things, both READ recommendations for whoever owns the gate, with
nothing built here:

- Do not call a talent "does nothing" on the sweep alone. The 16 in section 2 are the list.
- Do not act on a star verdict for a control or debuff kit from the sweep alone. Compare rank within
  tier at parity, not the band edges, until the edges are re-derived.

## Caveats

- **One line.** Every parity reading uses the pinned line with the subject in its role's place.
  The reference-line swap is the only check on line dependence, and it moves some roles' rank
  (Breaker pinned against balance, 0.73; Tinkerer, 0.63).
- **The parity ladder is 59 of 72 stages.** The 13 excluded are all won at any reachable power.
- **Kit tagging is READ** from effect and status names in `heroes.json`. A hero "carries control"
  if any non-talent ability has such an effect, with no weighting by how often it lands.
- **A fault in the parallel runner, found and not explained.** Two of the four `stars` runs, this
  session's first and the previous session's last, died with an unhandled `AggregateException`
  after the 94-hero read, in the body reading. Two runs of the same build completed, and one of them
  printed every number above. Control C1 shows the parallel and serial runners agree fight for fight
  in the duel and the ladder. `ParRun` now catches a fight that throws, re-runs it serially, and
  reports both. That tells a parallel-only race from a real exception without trusting either
  silently. The run with that guard in place recorded **0 faulting fights**, and its output was
  byte-identical to the earlier completed run, line for line except the timings. The fault is
  therefore intermittent, and its cause is not established. It coincided both times with two other
  worktrees' DevHarness runs sharing the machine. It never produced a number. Any future `stars`
  run should still check the "fights that threw in parallel" line.
- **Worth in % Attack is a linear yardstick.** Attack x1.05 is assumed to be worth five times
  Attack x1.01. That holds well enough to class, not to rank finely.

## What was kept from the stopped session

The previous session wrote `ParityStudy.cs` whole: calibration, controls, the talent reading with
materiality, the star reading with the logistic L50 and the body reading. It measured all of it on the
24-stage campaign and wrote no reading. Its numbers were taken before the campaign changed and are
not used here. Everything above was re-measured after rebasing on the 72-stage `main`. Two changes
were made to the instrument: `SweepRatio` is now set before the kit-ratio section reads it (it
read 0 there, so that section's rank correlation and verdict count were empty), and `ParRun` now
catches and reports faulting fights.
