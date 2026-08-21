# Emberfall - combat stats

An evaluation of a proposal to add four stats to the combat pipeline: **Effect Hit**, **Effect
Resist**, **Defence**, and **Block**. The stated reason is that relic and hero design keeps
reaching for hooks that do not exist.

The reason is sound, and DESIGN-ASPECTS section 6 sharpens it into something stronger than the
proposal claims: the effect pipeline has a fixed number of hooks, DESIGN-RELICS claimed six of them
for six relics, DESIGN-ASPECTS needs six more for six aspects, and there are not twelve. Hooks are
contested. That is the real argument for adding a stat, and it is a better one than "too thin".

The list of four, however, does not survive contact with the code. Two of them do.

Reconciled against DESIGN-ASPECTS, DESIGN-WORLD, DESIGN-RELICS and DESIGN-BUILD-ORDER. Where this
document disagrees with one of them, the disagreement is named.

## Verdict

| Stat | Verdict | Reason |
|---|---|---|
| **Effect Hit** | **Keep** | Contests a binary outcome, on the one pipeline hook nothing else claims. |
| **Effect Resist** | **Keep** | Inseparable from Effect Hit. One mechanic, two ends. |
| **Defence** | **Cut. Already exists.** | Already in the damage formula at `CombatSimulation.cs:311-313`. |
| **Block** | **Cut as proposed.** | Its mean effect is exactly a Defence increase, and it is the most expensive of the four in RNG draws. |

### Defence is already there

The proposal asks for "Defence entering the damage formula". It entered a long time ago.
`EffectiveDefense` on `CombatUnit` already folds in `DefenseUp` and `DefenseDown` with a 0.2 floor;
`defenseConstant` (400) and `defensePerLevel` (24) are both live in `balance.json`; Orrin's
Emberskin is a `DefenseUp` of 0.45 and both Ryn and Nyx apply `DefenseDown`. The stat is authored,
resolved, mitigating, and status-modifiable.

Since the question was asked specifically, the full order inside `ApplyDamageEffect` is below.
**Defence applies after aspect factor and after crit, second to last.**

| # | Step | Code |
|---|---|---|
| 1 | Base | `source.EffectiveAttack * effect.Power + effect.Flat` |
| 2 | **Aspect factor** | `raw *= AspectChart.Multiplier(source.Aspect, target.Aspect)` |
| 3 | Passive bonus vs status | `raw *= 1f + passive.BonusMultiplier` (Kalyx's Kindling only) |
| 4 | **Crit** | roll `EffectiveCritChance`, then `raw *= source.CritDamage` |
| 5 | **Defence** | `raw *= 1f - defense / (defense + DefenseConstant + DefensePerLevel * source.Level)` |
| 6 | Damage variance | `raw *= 1f + _rng.Spread(DamageVariance)` |
| 7 | Round and floor | `MidpointRounding.AwayFromZero`, clamped up to `MinDamage` |
| 8 | Shield, then HP | `DealRawDamage`: absorb into `Shield` first, remainder to `Hp` |

Because mitigation is multiplicative and lands after step 4, a crit and a non-crit are reduced by
the same *fraction*. Defence never blunts crits specifically, it scales everything equally. Worth
knowing before anyone tunes against the intuition that armour "absorbs spikes". Note also that step
3 is dead code the moment DESIGN-ASPECTS section 3 cuts Kindling, since Kalyx is its only user.

Nothing to build here. If the real complaint is that Defence has no *counterplay* stat the way the
proposal wants Effect Hit to counter Effect Resist, then the missing piece is armour penetration,
not Defence. Penetration is refused on the project's own rule: it reduces mitigation by a
percentage, which is reachable today by stacking `DefenseDown`, and it opens no design space
`DefenseDown` does not already open.

### Block does not earn its place

Block as proposed is "a chance to reduce an incoming hit". A `p` chance to reduce by `r` has an
expected value of `1 - p*r` on every hit, forever. That is a flat damage multiplier with extra
steps, purchasable today by raising `defense` in a unit's JSON. It is precisely the stat the
constraint forbids.

It is also the most expensive of the four. Block resolves per incoming hit, inside `DealRawDamage`,
the highest-frequency path in the simulation and the funnel every burn tick also passes through.
One new draw there costs more determinism than the other three combined, and DESIGN-ASPECTS gives
Ember a rule that multiplies burn ticks, so that frequency is about to rise.

Variance for its own sake is not enough of a case. A Bulwark that sometimes takes less is not
playing differently from a Bulwark that always takes a bit less; it is playing the same, noisier.

**What would earn its place.** Block becomes a design space the moment something can respond to it.
A `PassiveTrigger.OnBlock` makes "blocks feed the counterattack" writable. A Block that negates the
*status rider* on a hit rather than a slice of the damage makes a Bulwark the answer to a debuff
line, a role no hero fills. Either is a rule rather than a number and would clear the bar. Neither
is what was proposed.

One caution if that is pursued: under DESIGN-WORLD, interception and "the answer is always you"
are **Lumen's** identity, promoted there from DESIGN-ASPECTS' weaker version. A Block that
intercepts is competing with an aspect, and DESIGN-ASPECTS section 6 already shows what happens when
two systems claim one hook. Argue it against Lumen, not in a vacuum.

The rest of this document covers the two survivors.

---

## 1. Where Effect Hit and Effect Resist enter the pipeline

They are one opposed mechanic. Treat them as a pair throughout.

### The insertion point, and why it is uncontested

There is exactly one, and it already exists: `ApplyEffect`, the `EffectKind.ApplyStatus` branch,
`CombatSimulation.cs:257-269`:

```csharp
case EffectKind.ApplyStatus:
{
    if (!target.Alive || effect.Status == StatusKind.None) break;
    if (!_rng.Chance(effect.Chance)) break;          // <- the gate, today
    float magnitude = effect.Magnitude;
    if (effect.Status == StatusKind.Burn || effect.Status == StatusKind.Regen)
        magnitude = source.EffectiveAttack * effect.Magnitude;
    target.AddOrRefresh(effect.Status, Math.Max(1, effect.Duration), magnitude, source.Id);
    ...
}
```

The change is to the argument of `_rng.Chance`, and nothing else.

That matters more than it looks, given the hook scarcity DESIGN-ASPECTS raises. Checking every hook
already claimed:

| Claimant | Hook |
|---|---|
| Splitting Prism, Stormlance Oath, Warden's Debt | `effect.Target`, `effect.Hits` |
| Green Hour Vessel | the `ApplyHealValue` clamp |
| Tidewrought Metronome | the `AddEnergy` clamp |
| Verdict Sigil | `PassiveTrigger` |
| Ember | `AddOrRefresh` |
| Tide | `AddEnergy` |
| Verdant | `ApplyHealValue` |
| Gale | `TurnOrder` / the `Execute` turn loop |
| Umbra | `ExpireStatuses`, `Cleanse`, `ResolveTargets` visibility (per DESIGN-WORLD) |
| Lumen | `FindTaunter`, the shield-absorb block |

**Nothing claims the `_rng.Chance(effect.Chance)` gate.** Six relics and six aspects between them
take nine distinct hooks, and this is not one of them. DESIGN-RELICS lists `effect.Chance` as
modifiable and then builds no relic on it. Adding a contested landing roll therefore creates hook
space rather than competing for it, which is the opposite of what every other proposal on this
pipeline currently does.

### Order of operations

The roll stays exactly where it is: **before the effect is constructed**. On a miss, `magnitude` is
never computed, `AddOrRefresh` is never called, and no `StatusApplied` event is emitted.

This is not tidiness. `magnitude` for Burn and Regen is `source.EffectiveAttack * effect.Magnitude`,
so building first would read the caster's live attack for a status that never lands, and
`AddOrRefresh` mutates the target's status list, so building first would need an undo. Roll, then
build.

What changes is that the landing chance is no longer a constant read from JSON. It resolves from
three inputs at the moment of application:

```
landing = effect.Chance + source.EffectiveEffectHit - target.EffectiveEffectResist
```

Additive on the authored base, not multiplicative. Multiplicative would make one point of Effect
Hit worth nearly three times as much on Kalyx's Cinder Bloom (base 0.85) as on Blight Hound's bite
(base 0.35), which is backwards: the unreliable effect is the one the stat should rescue. Additive
keeps `effect.Chance` meaning "base landing chance before anyone's stats", which is what an author
expects when typing it.

Clamp the result, mirroring the existing `EffectiveCritChance` clamp. Floor at 0.05 so no matchup
is a guaranteed whiff. **Ceiling at 1.0, not 0.95** (section 2 explains why that number is
load-bearing).

### Only harmful statuses are contested

`StatusInfo.IsHarmful` already exists and already names the set: `AttackDown`, `DefenseDown`,
`Burn`, `Slow`. Only those roll against Effect Resist. Beneficial statuses (`Taunt`, `Shield`,
`DefenseUp`, `AttackUp`, `Focus`, `Regen`) apply through their authored `effect.Chance` untouched.

This is correctness, not simplification. Every beneficial status in the game is self-applied or
ally-applied: Orrin's Provoke targets `Self`, Maeve's Attack Up targets `AllAllies`. Contesting
those would mean a hero resisting his own ward, and would make Effect Resist an accidental downside
on your own team.

It also happens to protect two harness checks entirely (section 3), and it aligns the stat with
`EffectKind.Cleanse`, which already filters on the same predicate.

### The stats themselves

`EffectHit` and `EffectResist` are fractions, default `0f`, living exactly where `Attack` and
`Defense` live: `UnitDef` (with `ReadCommon` parsing `effectHit` / `effectResist`), `UnitStats` (set
in `Build()`), and `CombatUnit` (set in `FromDef`, with `EffectiveEffectHit` / `EffectiveEffectResist`
accessors shaped like `EffectiveAttack`).

**They must not scale with level or rarity.** `Build()` multiplies HP, Attack and Defence by
`1f + def.Growth * (level - 1)` and by the rarity multiplier. Applying that to Effect Resist makes
every high-level enemy incidentally debuff-immune, which nobody decided, and would make stage 1-9's
three level-30 Grave Moths immune to Burn as a side effect of their level. Copy the flat
`CritChance` line instead:

```csharp
s.EffectHit = def.EffectHit;        // flat, like CritChance
s.EffectResist = def.EffectResist;  // flat, like CritDamage
```

### The player has to be able to see it

DESIGN-BATTLE-READABILITY is explicit that the screen reads the record rather than recomputing it,
so a resist that leaves no trace is invisible RNG: the debuff simply does not appear and nothing
says why. That needs a new `BattleEventType.StatusResisted`, carrying `SourceId`, `TargetId` and
`Status`, emitted on the miss branch.

This is a real cost. DESIGN-RELICS deliberately avoided adding an event type to stay clear of Core
event work in flight, and this proposal cannot make that dodge: a relic altering a rule can borrow
the `PassiveTrigger` banner, but a debuff *failing* has no existing event meaning the right thing.
DESIGN-ASPECTS hits the same wall from another direction (Ember's stacking needs a count on the
status bar, which is a display gap rather than an unread record). Coordinate the enum addition
rather than landing it blind.

---

## 2. Determinism

### How the RNG is consumed today

`DeterministicRng` is a single xorshift32 stream shared by the whole battle. Every draw advances one
shared state, so **inserting or removing a single draw anywhere shifts every subsequent draw for the
rest of that battle**. Not just the roll that changed: the next unit's crit, the following round's
turn order, which enemy a `RandomEnemy` ability picks. Sequence position is the whole contract.

Current draw sites, all of them:

| Site | Draws | Frequency |
|---|---|---|
| `TurnOrder()` speed jitter | 1 per living unit | once per round |
| `ApplyDamageEffect` crit roll | 1 | per damage hit |
| `ApplyDamageEffect` damage variance | 1 | per damage hit |
| `ApplyEffect` status chance | 1 | per `ApplyStatus` resolution, **only if `0 < chance < 1`** |
| `ResolveTargets` random ally/enemy | 1 | per random-targeted effect |

That fourth row is the important one. `DeterministicRng.Chance` short-circuits:

```csharp
public bool Chance(float probability)
{
    if (probability >= 1f) return true;   // no draw
    if (probability <= 0f) return false;  // no draw
    return NextFloat() < probability;
}
```

A `chance` of 1.0 consumes **zero** draws. That is what makes a clean migration possible.

### How many new draws per turn

A turn is one ability. Counting `ApplyStatus` resolutions per turn:

- An ability with no harmful status: **0 new draws.**
- A single-target harmful status (Ryn's Piercing Gale, Nyx's Shadowmark): **0 or 1**, depending on
  whether it already rolled.
- An `AllEnemies` harmful status (Orrin's Cinderguard, Kalyx's Cinder Bloom, three enemy actives):
  resolves once per living target, so **0 to 3 today, 0 to 5 after the team size change**.

The honest number is smaller than that ceiling, because most already roll. Auditing all 18
`ApplyStatus` effects in shipped content:

| | Count | Draws today | Draws after |
|---|---|---|---|
| Beneficial (uncontested) | 9 | 0 | 0 |
| Harmful, `chance` already < 1 | 8 | 1 each | 1 each |
| Harmful, `chance` = 1.0 | 1 | **0** | **1** |

The single entry in that last row is **Nyx's Shadowmark**: `DefenseDown` 0.22 on `LastTarget`, no
`chance` authored, so it defaults to 1.0 and short-circuits. It fires `AfterBasicAttack`, meaning
every basic attack she throws. It is the only place in shipped content where the draw count changes
at all. (DESIGN-WORLD flags that this passive is due a rename, since marking is Lumen's verb under
that fiction and Nyx is Umbra. The mechanic is unaffected; only the name moves.)

### Can existing seeded results be preserved?

**Yes, for one phase, and this is worth exploiting.**

If `EffectHit` and `EffectResist` are `0f` on every unit and the implementation bypasses the
arithmetic entirely when both are zero:

```csharp
float landing = effect.Chance;
if (source.EffectiveEffectHit != 0f || target.EffectiveEffectResist != 0f)
    landing = Clamp(effect.Chance + source.EffectiveEffectHit - target.EffectiveEffectResist, 0.05f, 1f);
```

then `landing` is bit-identical to `effect.Chance`, the short-circuit behaves identically,
Shadowmark still consumes no draw, and **every seeded battle produces the same event list it does
today**. `CheckDeterminism`'s `BattleFingerprint` proves that rather than assuming it.

Two things break the guarantee if you are careless:

1. **Always running the arithmetic.** `0.9f + 0f - 0f` is exactly `0.9f` in IEEE 754, so values
   happen to survive, but relying on float identity across a formula you will later edit is a trap.
   Bypass explicitly.
2. **Clamping the ceiling at 0.95.** That drags Nyx's 1.0 to 0.95, turning a zero-draw
   short-circuit into a draw and desynchronising every battle she appears in. This is the whole
   reason the ceiling is 1.0.

So the migration is two phases, and only the second costs anything:

- **Phase A: add the stats, default zero, author nothing.** Provably a no-op. All twelve stages of
  balance data remain valid. Verified by the existing determinism fingerprint, not by eyeball.
- **Phase B: author non-zero values.** From the first non-zero value, every seed is a different
  battle and **all twelve stages of win-rate data are invalidated**. No partial preservation, no
  per-stage salvage, no way to keep old fingerprints.

Phase B is the retune, and section 4 argues it should happen exactly once for reasons that now go
well beyond this document.

---

## 3. What breaks

### StatMath

- `UnitStats` gains two fields; `Build()` gains two flat passthrough lines. Low risk **provided**
  they are not multiplied by `scale` or `rarityMultiplier` (section 1).
- **`PowerScore` is the trap.** It weights six stats into the number that drives `recommendedPower`
  on all twelve stages, gates `CheckStageWinnability` (which levels a team *to* each stage's
  recommended power), and is shown to the player. Adding Effect Hit and Effect Resist terms changes
  every Power number in the game at once and silently retunes the whole campaign.

  **Recommendation: do not touch `PowerScore` in this pass.** The two stats stay out of the Power
  formula and the twelve `recommendedPower` values keep meaning what they mean. A debuff specialist
  will read slightly under-powered on her card. That is smaller and far more reversible than
  re-deriving twelve stage thresholds against a formula that changed in the same pass as the
  pipeline. Revisit it as its own change.

### Harness checks

Under the harmful-only contest rule, the two checks that walk battle behaviour survive untouched.
That is not luck; it is why the rule is drawn there:

- **`CheckProvokeTargeting`** depends on Taunt landing. Taunt is beneficial and self-applied in both
  places it exists (`heroes.json:27`, `enemies.json:78`), so it is never contested. **No change.**
- **`CheckStatusExpiry`** filters to `e.SourceId == e.TargetId`, self-applied only, and every
  self-applied status in the game is beneficial. **No change.**

Needing rework:

- **`CheckDeterminism`** hardcodes `r.Units.Count == 6` and the label "3v3 battle built 6 units".
  Breaks on team size, not on the stats. Needs 10 and a new label.
- **`CheckUnderlevelledLoses`** asserts a level-1 team loses stage 1-12 at `winRate <= 0.15f`.
  Breaks hard on team size before the enemy refill: five level-1 heroes against three enemies will
  start winning. Re-baseline after retuning, not before.
- **`CheckStageWinnability`** is safe if `PowerScore` is left alone, and becomes a twelve-way
  failure if it is not.
- **`RunCampaignBalancePass`** is the win-rate table itself. Every number moves in phase B. Its
  `Check(profile.Heroes.Count > 3, "summoning grew the roster past the 3 starters")` also reads
  oddly once the banner holds five, though it still passes.
- **`CheckContent`** asserts `db.StartingHeroes.Count == 3`. Still true, but see the open question
  in section 4 about two empty slots.

Worth adding, mirroring `CheckAspectDirection`: a check that the landing formula moves in the right
direction, that a high-Effect-Hit source lands more often than a low one against the same target and
that Effect Resist reduces it. Pure arithmetic against the clamp, no battle needed.

### Not this document's checks, but the same pass

If the aspect work lands alongside (section 4), `Debrief.AspectFactorThreshold = 1.2f` is a silent
killer: DESIGN-ASPECTS section 4 narrows `AspectChart.Advantage` to 1.10, and `TryAspect` counts
damage where `e.AspectFactor >= 1.2f`. The comparison stops being true, the Aspect clause stops
firing, and no check fails. Verified present at `Debrief.cs:34`. Whoever runs the combined pass owns
that constant.

---

## 4. Interaction with team size 3 to 5

### One pass is correct, for a stronger reason than scheduling

The scheduling argument (two passes means retuning twelve stages twice) is true but weak alone. The
real argument is that these changes are **not independent**.

Every `AllEnemies` harmful status resolves once per living target: three landing rolls per cast at
3v3, five at 5v5. So the value of a point of Effect Hit is a direct function of team size, and
Orrin, Kalyx, Cinder Imp, Grave Moth, Ashen Revenant and Sundered Colossus all carry such an effect.
Tuning Effect Hit at 3v3 and then moving to 5v5 does not repeat the work, it invalidates the
conclusion.

DESIGN-ASPECTS section 8 reaches the same shape independently from the other side: the aspect
identities "want team size 5", and it recommends being built after the size move rather than before.

### Three changes, one retune

That is the thing to plan around. Three separate pieces of design all invalidate the same
twelve-stage win-rate table:

| Change | Pipeline surface | Invalidates the table? |
|---|---|---|
| Team size 3 to 5 | `BuildUnits`, `campaign.json`, save, UI | Yes |
| Effect Hit / Effect Resist (this doc) | one branch in `ApplyEffect` | Yes, at phase B |
| Aspect identities (DESIGN-ASPECTS) | most of `CombatSimulation` | Yes, plus every damage number via the narrowed wheel |

Bundling all three into a single code change would be unreviewable, and DESIGN-ASPECTS section 6
separately warns against building relics and aspects in one pass and expecting either spec to
survive. The resolution is to separate the *code* and share the *retune*: land the three changes in
sequence, declare every intermediate state explicitly untuned, and run the twelve-stage tuning
exactly once at the end. Any win-rate table produced before that last step is measuring a build that
will not ship.

### The known trap: MaxTeamSize gates enemy spawns

Confirmed, and independently flagged by DESIGN-ASPECTS section 7. `BuildUnits` reads:

```csharp
for (int i = 0; i < spawns.Count && i < _cfg.MaxTeamSize; i++)
```

Every stage in `campaign.json` has exactly **three** spawns (36 total). Raising `maxTeamSize` to 5
adds nothing to the enemy side, because `spawns.Count` becomes the binding constraint. The result is
five heroes against three enemies on all twelve stages: every stage trivially winnable, the balance
pass reporting near-total win rates, and `CheckUnderlevelledLoses` failing.

`campaign.json` needs refilling to five spawns per stage: **24 new spawns**, 36 to 60. Each needs an
enemy id and a level, and choosing those levels is tuning work, not data entry. Stage 1-9 (three
level-30 Grave Moths, subtitled "far too many of them") and stage 1-12 (boss plus two escorts) have
compositions that mean something; padding them to five is a decision, not a duplicated line.

### The second trap: three hardcoded 3s, not one

`MaxTeamSize` is not the only place the number lives:

- `PlayerProfile.cs:49` : `public readonly string[] Team = new string[3];`
- `HeroDetailScreen.cs:29` : `private readonly UIButton[] _slotButtons = new UIButton[3];`
- `BattleStage.cs:16-21` : `AllySlots` and `EnemySlots`, 3-element `Vector2` arrays

The first two are credited to DESIGN-ASPECTS section 7, which found them first. The third is the
one that fails quietly: placement clamps with
`AllySlots[Mathf.Clamp(snapshot.Slot, 0, AllySlots.Length - 1)]`, so units in slots 3 and 4 are
placed on top of the unit in slot 2. No crash, no warning, two invisible heroes. Ten units on a
portrait phone screen is a layout problem in its own right, and UI-AUDIT already documents that
screen as tight at current density.

Everything else in `PlayerProfile` (`TeamSlotOf`, `AssignToTeam`, `ClearTeamSlot`, `NormaliseTeam`,
`TeamHeroes`) iterates `Team.Length` rather than a literal, so those come along for free.

### Open question, for a human

Six heroes exist and three are starters. A five-slot banner on a new account leaves two slots that
`NormaliseTeam` cannot fill, because there is nothing to fill them with. So a new player fights 3
against 5, which is the opposite of the trivial-win problem and lands squarely on the experience
DESIGN-FIRST-TEN-MINUTES tunes.

Three options, none obviously right: grant five starters, unlock slots 4 and 5 as account rewards,
or scale enemy count to the player's filled slots. This needs deciding before the retune, because
the retune's answer depends on it.

### Combined work order

1. **Team size, structural only.** `maxTeamSize`, `PlayerProfile.Team`, `_slotButtons`, the battle
   stage positions, `SaveSerializer`. No tuning. Ends with a deliberately broken 5-versus-3 game.
2. **Refill `campaign.json` to five spawns per stage.** 24 new spawns, deliberate compositions.
   Ends with a 5-versus-5 game that runs and is not balanced.
3. **Resolve the starter-count question.** Blocks step 7.
4. **Effect Hit and Effect Resist, phase A.** Stats added, defaults zero, nothing authored. Verify
   no behavioural change against step 2's baseline.
5. **Effect Hit and Effect Resist, phase B.** Author values on six heroes and eight enemies, add
   `StatusResisted` and its battle-screen handling, add the direction check.
6. **Aspect identities, if they are in this pass at all** (DESIGN-ASPECTS, its own build order, its
   own contradictions with DESIGN-RELICS to resolve first). Owns `Debrief.AspectFactorThreshold`.
7. **Retune all twelve stages. Once, last.**

Step 7 is the only step producing a valid win-rate table. This mirrors DESIGN-BUILD-ORDER task 9 and
for the same stated reason: tuned earlier, it is tuned against a player who will not exist.

---

## 5. Save format impact

### What does not change

**Effect Hit and Effect Resist add nothing to the save.** Neither is player state. Both derive from
`UnitDef` through `StatMath.ForHero` every time a battle is built, exactly like Attack, Defence and
Crit Chance. The save stores `defId` and `level`; stats are recomputed on load. No new field, no
version bump, no migration, from the stat work. This matches DESIGN-ASPECTS' finding for the aspect
rules and DESIGN-RELICS' reasoning about why relics *do* cost a field: relics are player choices,
stats are content.

Also unchanged: the `heroes` array shape, gold, shards, `stagesCleared`, `playerLevel`, idle
timestamps, and every existing key.

### What does change

Only the team size work touches the save, and only the `team` array.

`SaveSerializer` writes `profile.Team.Length` entries and reads back with
`i < team.Count && i < profile.Team.Length`, then calls `NormaliseTeam()`. So a 3-entry array read
into a 5-slot field fills slots 0 to 2 from the save and auto-fills the rest. **Old save into new
code works with no migration.**

DESIGN-ASPECTS section 7 concludes from this that no version bump is needed. That is correct on the
upgrade path and this document agrees on the mechanism, but **recommends bumping anyway**, for a
case that analysis does not cover: the *downgrade* path. A 5-entry team array read by code with a
3-slot field truncates at `i < profile.Team.Length` and silently drops two heroes from the banner.
No error, no warning, no way for anything downstream to detect it. A version number is exactly the
mechanism for catching that, and the cost of bumping is one integer.

This is a judgement call about a hazard, not a correction of a fact. If the project never ships a
build that could read a newer save, DESIGN-ASPECTS is right and the bump is ceremony.

**Concretely:** `SaveSerializer.CurrentVersion` is 1. DESIGN-BUILD-ORDER task 6 (repeat cap) takes
it to 2, and DESIGN-RELICS says relics either ride that bump or take it to 3. If this pass lands
after the repeat cap, team size is version 3. If before, it is 2 and the repeat cap becomes 3.
Sequence it deliberately rather than discovering the collision in a merge.

---

## 6. Size

### Files touched

**Effect Hit and Effect Resist (10):**

| File | Change |
|---|---|
| `Core/Data/Enums.cs` | `BattleEventType.StatusResisted` |
| `Core/Combat/BattleEvent.cs` | event type handling |
| `Core/Data/Definitions.cs` | two `UnitDef` fields, two `ReadCommon` lines |
| `Core/Progression/StatMath.cs` | two `UnitStats` fields, two `Build` lines |
| `Core/Combat/CombatUnit.cs` | fields, two `Effective*` accessors, `FromDef`, `ToSnapshot` |
| `Core/Combat/CombatSimulation.cs` | the `ApplyStatus` gate, the miss event |
| `Resources/Content/heroes.json` | 6 heroes |
| `Resources/Content/enemies.json` | 8 enemies |
| `Tools/DevHarness/Program.cs` | direction check |
| `UI/Screens/BattleScreen.cs`, `HeroDetailScreen.cs` | render the resist, show the stats |

**Team size 3 to 5 (9):**

| File | Change |
|---|---|
| `Resources/Content/balance.json` | `maxTeamSize` |
| `Core/Model/PlayerProfile.cs` | `Team` array |
| `Core/Save/SaveSerializer.cs` | version |
| `Resources/Content/campaign.json` | 24 new spawns |
| `Battle/BattleStage.cs` | 5 ally and 5 enemy floor positions |
| `UI/Screens/HeroDetailScreen.cs` | `_slotButtons`, banner slot rows |
| `UI/Screens/MainMenuScreen.cs`, `CampaignScreen.cs` | team display |
| `Tools/DevHarness/Program.cs` | unit-count assertions |

Roughly **17 distinct files**, two of them (`Program.cs`, `HeroDetailScreen.cs`) touched by both
halves. This excludes the aspect work entirely, which DESIGN-ASPECTS section 7 costs at "most of"
a 630-line `CombatSimulation.cs` plus content and UI.

### Sessions

**Several. Three at minimum, four is the realistic number**, for the two changes this document
covers. Not one, and the file count is the least of the reasons.

- **Session 1: team size, structural.** Work order steps 1 and 2. The 24 new spawns are composition
  decisions and the 5-versus-5 portrait layout is a genuine design problem, not a constant bump.
  Plausibly splits in two if the battle stage fights back.
- **Session 2: Effect Hit and Effect Resist.** Steps 4 and 5. The pipeline change is small; content
  authoring across fourteen units and a new event type reaching the battle screen are not.
- **Session 3: the retune.** Step 7, with `RunCampaignBalancePass` as the instrument. The long one,
  and it cannot start until the others finish.

The starter-count question needs a human answer during session 1 or 2 and blocks session 3. If the
aspect work joins the same pass, add its own estimate on top and expect the retune session to grow
rather than the count to stay at three.

---

## 7. What this makes possible

### The `chance` hook is listed as available and used by nothing

DESIGN-RELICS names eight modifiable hooks in the effect pipeline. The third is **`effect.Chance`,
the `_rng.Chance` gate on `ApplyStatus`**. It then designs six relics, and not one uses it. As
section 1 shows, DESIGN-ASPECTS' six identities do not use it either. Twelve pieces of design over
two documents, nine hooks claimed, and this one left on the table by both.

The hook is dead because there is nothing on the other side of the roll. A relic shifting `chance`
from 0.7 to 0.85 is a percentage, which DESIGN-RELICS bans in its first sentence and again in its
closing test: if it cannot be written as one sentence without a number, it is a stat stick. A relic
that says *"your afflictions ignore resistance"* is a rule, passes that test, and is unwritable
today because resistance does not exist.

There is a useful distinction here, since DESIGN-ASPECTS section 2 finds a similar-looking gap and
resolves it the other way. `PassiveTrigger.OnKill` is wired, fired by `DealRawDamage`, and used by
no content: verified, no `OnKill` appears in either JSON file. But that hook is dead for want of
*content*, and Gale claims it. The `chance` hook is dead for want of a *stat*, and no amount of
content fixes it, because both sides of the roll are currently one authored constant. Same symptom,
different disease, and only the second one is an argument for a new stat.

### Umbra's debuffs would otherwise have no counterplay at all

This is the strongest case, and it only becomes visible with both new documents in hand.

DESIGN-ASPECTS gives Umbra: harmful statuses from an Umbra source cannot be cleansed and do not tick
down while the applier lives. DESIGN-WORLD then cuts the non-expiry half as belonging to Verdant,
and keeps the uncleansable half, which it judges correct: "removal of an option".

Follow that through. An Umbra debuff that lands cannot be cleansed. `EffectKind.Cleanse` is the
game's only removal mechanism, and it is Wren's. So under the agreed version of Umbra, **once the
debuff lands there is no counterplay whatsoever** apart from waiting it out. The only remaining
lever is whether it lands, and today that is a constant in a JSON file that the defender has no
input into.

Effect Resist is the defender's side of that roll, and without it the agreed Umbra identity ships
with an uncounterable mechanic. That is not a nice-to-have; it is the missing half of a design two
other documents have already committed to.

### Nyx is a debuff specialist with nothing to specialise in

Nyx Serrow is Umbra, `Stalker`, and the roster's only 0.28 crit chance. Her passive applies
`DefenseDown` after every basic attack with **no `chance` authored at all**, so it defaults to 1.0
and lands unconditionally.

Her signature works because a JSON field is missing, not because she is good at it. There is no way
to author a hero better at landing afflictions than Nyx, or worse, because there is no axis to be
better or worse on. With Effect Hit, "her mark always lands" becomes a consequence of Nyx being
built for it: a stat expressing an identity rather than a default papering over the absence of one.

DESIGN-WORLD independently reaches the roster's cross-wiring problem from the fiction side and
concludes the content is wrong rather than characterful. This is the same finding with a stat
attached: Umbra is described as removal, and removal that cannot fail is not a specialism.

### Tide's "control" is a comment in an enum

`Enums.cs` documents `Tide, // water / control`. DESIGN-WORLD renders the belief as "nothing should
remain fixed, redirects", and DESIGN-ASPECTS gives Tide the energy-overflow hook, which is
redirection but is not control in the debuff sense.

Maeve is the only Tide hero, and her kit is `AttackUp` plus `Focus` on allies plus energy drip:
buffing. The control half of that comment has never had a stat to sit on, and the aspect work as
specified does not give it one either. Effect Hit is the axis on which a control identity could
later be built. Flagging it rather than claiming it, since DESIGN-ASPECTS has already assigned Tide
its rule and this document is not reopening that.

### What it does not unblock, despite the temptation

**A boss that resists Provoke.** The obvious pitch, and it does not work. Taunt is applied with
`"target": "Self"` in both places it exists: Orrin taunts himself, Stone Warden taunts itself. The
status lands on the caster, so a target's Effect Resist never sees it. Enemy targeting is overridden
later and elsewhere, in `ResolveTargets` via `FindTaunter`, which reads `HasTaunt` on the opposing
side and contains no roll at all.

Making Sundered Colossus ignore Provoke needs a flag consulted in `FindTaunter`. Note that
DESIGN-WORLD promotes exactly that function to **Lumen's** core identity, so anyone adding a
resist there is modifying an aspect's hook and owes DESIGN-ASPECTS an argument. Either way it is a
different change, and claiming it here would be overselling.

**Lumen's absence.** Covered far better by DESIGN-ASPECTS section 5 and DESIGN-WORLD section 3, which
between them count the aspects, explain why Umbra's advantage has never once fired, and propose
relabelling Orrin. Nothing in this document bears on it. Noted only so it is not read as an
oversight.

---

## Summary

Add **Effect Hit** and **Effect Resist** as one opposed pair, contested only for harmful statuses,
resolved additively against the authored `effect.Chance` at the existing `_rng.Chance` gate, rolled
before the status is constructed, clamped to `[0.05, 1.0]`, flat across level and rarity, and kept
out of `PowerScore`. They land on the one pipeline hook that neither the six relics nor the six
aspects claimed, and they supply the only counterplay to an uncleansable Umbra debuff that two other
documents have already committed to shipping.

Do not add **Defence**. It has been in the damage formula the whole time, applying after aspect
factor and after crit, and its ordering there is documented above.

Do not add **Block** as proposed. Its mean effect is a Defence increase and it is the most expensive
draw in the simulation. Bring it back as `OnBlock`, or as status negation, and argue it against
Lumen.

Ship it in one pass with the team size change, because the two are coupled through `AllEnemies`
status resolution and cannot be tuned separately. Refill `campaign.json` to five spawns per stage
before measuring anything. Phase the stats in at zero first and prove the no-op against the existing
determinism fingerprint, then author values and accept that all twelve stages of win-rate data are
gone. Three separate pieces of design now invalidate that same table, so retune once, last, after
whichever of them are in flight have landed.

Three to four sessions for the two changes here. Seventeen files. One save version bump, and it
belongs to the team size change, not to the stats.
