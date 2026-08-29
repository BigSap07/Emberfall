# Emberfall - armor

Six equipment slots per hero, filled by generated pieces that carry one guaranteed primary stat
and a rarity-gated number of random substats. No unique effects, no pipeline hooks, no art. Every
bonus is an addition or a multiplication on a stat the simulation already reads.

This document was written against the source, not against the other design docs. The audit that
preceded it is section 1, and it contradicts `DESIGN-COMBAT-STATS.md`'s body (though not its
superseding banner) and contradicts the brief this system was commissioned from. Read section 1
before arguing with anything below it.

---

## 1. The audit this is built on

The brief assumed six stats on `UnitDef` were dormant - present on the data model, consumed by
nothing. **All six are live.** So are Effect Hit and Effect Resist, which the brief guessed might
still be phase-zero.

Every stat field on `UnitDef` (`Definitions.cs`), and where live combat reads it:

| Field | Status | Read at |
| --- | --- | --- |
| `BaseHp` `BaseAttack` `BaseDefense` `BaseSpeed` | live | `StatMath.Build` -> `CombatUnit.FromDef` |
| `CritChance` | live | `ApplyDamageEffect` step 3 |
| `CritDamage` | live | `ApplyDamageEffect` step 3 |
| `Growth` `StartEnergy` | live | level scaling, starting energy |
| `EffectHit` | live | `CombatSimulation.ResolveLandingChance`, called from the `ApplyStatus` branch |
| `EffectResist` | live | same |
| `SkillDamage` | live | step 4, gated on `ability.Kind != BasicAttack` |
| `Block` | live | step 0, `_rng.Chance(target.EffectiveBlock)`, negates the whole hit, fires `PassiveTrigger.OnBlock` |
| `ArmorBreak` | live | step 6, scales the mitigation **ratio**, not `EffectiveDefense` |
| `ControlImmunity` | live | the `ApplyStatus` branch, Silence and Stun landing only |
| `DamageReduction` | live | step 7 |
| `HolyDamage` | live | step 6.5, flat, added back after the Defense multiply so Defense cannot scale it |

Each of the six has a dedicated harness check already: `CheckSkillDamageActiveOnly`,
`CheckArmorBreakVsDefense`, `CheckDamageReductionVsDefense`, `CheckHolyDamageBypassesDefense`,
`CheckBlockNegatesHit`, `CheckControlImmunityResistsMore`.

`DESIGN-COMBAT-STATS.md` opens with a banner retracting its own Block verdict - "Block shipped, in
the shape this document said would earn its place". The body below that banner is the stale part.
Nothing in this system depends on that document being correct.

### What is actually dormant is content, not code

Exactly one unit authors any of the six: Kael, in `heroes.json`, and only `block: 0.08` is
non-zero. `CheckNoContentAuthorsNewMechanicsYet` in the harness whitelists Kael by id and fails any
other hero or enemy that authors one, because a non-zero value changes the RNG draw count and moves
every seeded `BattleFingerprint`.

### The gap armor exists to fill

`RelicStat` has eight members: `MaxHp`, `Attack`, `Defense`, `Speed`, `CritChance`, `CritDamage`,
`EffectHit`, `EffectResist`. **No relic can grant any of the six.** Six stats are wired end to
end, harness-tested, and reachable by nothing a player can equip or earn.

That is armor's reason to exist beyond "more numbers". Its substat pool is the full live set of
fourteen, and it is the only equipment path to the six.

---

## 2. Three adjustments to the commissioned design

**The chest slot is not called Armor.** One word naming both the system and one of its six slots is
the `Forge` collision the brief warned about, reproduced inside the system that warned about it.
The chest slot is **Plate**.

**Accessory primaries are Crit Chance and Crit Damage.** Both confirmed live in section 1, neither
claimed by the four slots above them.

**The rarity ladder is called Temper, and its rungs are Rough / Honed / Gilded / Radiant.** Every
obvious word is taken: `RelicGrade` owns *Legendary* and *Mythic* and the word *grade*,
`OwnedRelic.Tier` owns *tier*, and `HeroDef.Stars` owns hero rarity - it is an `int`, not an enum,
which is why hero rarity has no member to collide with. *Common* is free but is the pre-rename
label of what is now `RelicGrade.Legendary`, so reusing it points at a word that moved. Temper is
new, is metallurgical, and pairs with the Anvil.

One near-collision, noted and left alone: `Tools/DevHarness/Program.cs` has a synthetic test hero
with id `test_anvil` and name `Anvil` in an inline JSON fixture. It is harness-local, never loaded
from `Assets/Resources/Content`, and never player-visible. Renaming it would touch a fixture that
several checks read; naming the reroll sink around it would be worse.

---

## 3. Slots

Six per hero. `ArmorSlot`, in `Enums.cs`:

| Slot | Primary stat | Mode |
| --- | --- | --- |
| `Weapon` | Attack | multiply |
| `Helm` | Max HP | multiply |
| `Plate` | Defense | multiply |
| `Boots` | Speed | multiply |
| `AccessoryA` | Crit Chance | add |
| `AccessoryB` | Crit Damage | add |

The primary is **guaranteed by the slot** and cannot roll anything else. A Weapon is always an
Attack piece; what varies is how much, and what rides along with it.

Mode follows `RelicStatMode`'s per-stat rule verbatim, and for the same reason: the four large
level-scaled stats multiply, the small stored-as-fractions stats add. A multiplicative +12% on a
0.15 crit chance is +0.018, which is nothing. This is a rule about the stat's storage shape, not
about which system granted the bonus, so armor and relics necessarily agree on it.

One piece per slot per hero. Six pieces, six slots, no stacking, no second Weapon.

---

## 4. Temper

`ArmorTemper`, four rungs. Temper gates **how many substats roll**, and scales the primary.

| Temper | Substats | Primary scale |
| --- | --- | --- |
| `Rough` | 1 | 0.55 |
| `Honed` | 2 | 0.75 |
| `Gilded` | 3 | 1.00 |
| `Radiant` | 4 | 1.30 |

Temper does not change *which* stats can roll, only how many. A Rough Weapon and a Radiant Weapon
both grant Attack; the Radiant one grants more of it and brings three more lines.

Temper is fixed at the moment the piece drops and never improves. There is no upgrade path in this
pass - a better piece is a better drop, not a fed one. That is a deliberate scope cut, not an
oversight: an upgrade track would need its own currency sink, and the Anvil already is one.

---

## 5. Substats

Rolled from the confirmed-live pool, all fourteen:

`MaxHp` `Attack` `Defense` `Speed` `CritChance` `CritDamage` `EffectHit` `EffectResist`
`SkillDamage` `Block` `ArmorBreak` `ControlImmunity` `DamageReduction` `HolyDamage`

Rules:

- **No duplicates within a piece.** Four lines of Attack is not a roll, it is a rounding error with
  a UI.
- **A substat may duplicate the primary.** A Weapon rolling Attack as a substat is fine and reads
  clearly; forbidding it would make the best-in-slot roll depend on which slot you are looking at.
- **Weights are content.** Each entry in `armor.json` carries a `weight`, so the six formerly
  unreachable stats can be made rarer than Attack without a code change.
- **Every roll is a closed range**, `min` to `max`, resolved uniformly. No open-ended scaling.

Nothing on a piece can reference an ability, a status, a trigger, a target mode, or a hook. The
type that describes a bonus (`ArmorStatRoll`) has three fields - stat, mode, value - and there is
nowhere to put an effect. That is the "no unique effects, ever" guarantee enforced by shape, the
way `RelicDef`'s no-numbers guarantee used to be, rather than by convention.

---

## 6. Attunement (the set bonus)

Count the equipped pieces whose own Aspect tag matches **the wearer's** Aspect. Thresholds at 2 and
4. Flat and percentage bonuses on existing stats only - the same `ArmorStatRoll` shape a substat
uses, so a set bonus cannot express anything a substat could not.

A piece's Aspect is rolled at drop and is independent of its slot. Six pieces of six different
aspects give a hero nothing; four matching pieces give the 2- and 4-piece bonus both, because the
thresholds are cumulative rather than exclusive.

Bonuses are **per aspect**, authored in `armor.json`, and are chosen to make the six unreachable
stats reachable:

| Aspect | 2 pieces | 4 pieces |
| --- | --- | --- |
| Ember | +8% Attack | +12% Skill Damage |
| Tide | +8% Effect Hit | +12% Control Immunity |
| Verdant | +8% Max HP | +10% Damage Reduction |
| Gale | +8% Speed | +10% Crit Chance |
| Umbra | +10% Crit Damage | +12% Armor Break |
| Lumen | +8% Defense | +6% Block, +40 Holy Damage |

All placeholders, in the same sense every relic number is a placeholder. The shape is the decision;
the values belong to a balance pass.

Aspect matching uses the wearer's Aspect and no other rule. There is deliberately no
counter-aspect penalty and no off-aspect bonus: a second axis on top of `AspectChart` would make
the wheel argue with itself.

---

## 7. The Anvil

The reroll sink. **Not the Forge** - `DESIGN-BUILD-ORDER` task 6 is "Collect vs Forge", the
idle-income mechanic, and one word cannot name both.

**Currency: Slag.** New, not Shards, not Gold, not Relic Dust. A separate purse on `PlayerProfile`,
a separate save key, and no shared code path with Relic Dust - `SpendRelicDust` stays the choke
point for Dust and only for Dust, so the `RelicDustSpent` event activity keeps meaning exactly what
it means today.

Two operations, both on the inventory screen:

- **Salvage.** Destroys a piece and pays Slag scaled by its Temper. This is where unwanted pieces
  go instead of accumulating. A piece equipped on a hero cannot be salvaged; unequip first.
- **Reroll.** Spends Slag and rerolls **every substat on a piece**, keeping its slot, primary,
  aspect and temper. All-or-nothing: there is no per-line lock, because a lock turns a sink into a
  ratchet and the sink is the point.

Costs are per temper, in `armor.json`. Salvage pays less than reroll costs at every rung, so the
loop consumes pieces rather than printing Slag.

Slag has exactly one use. When a second use appears it will need an argument, not just a price.

---

## 8. Where a piece comes from

Stage clears. A clear that pays its full purse also rolls one armor piece; a clear past the daily
repeat cap rolls nothing, exactly as it pays no Gold and no Relic Dust. Reusing the existing capped
branch rather than adding a source is what keeps armor from handing the uncapped farming loop back
in a new denomination.

Temper is weighted by `stage.GlobalIndex`, so deep stages skew toward Gilded and Radiant without
ever guaranteeing them. Aspect and substats are uniform-random within the content weights.

Armor never drops from summons, and the Deep Recital is deliberately left alone in this pass.

---

## 9. How it reaches the simulation

Through `StatMath`, and through nothing else.

`StatMath.ApplyRelic` already folds a bonus list into an already-built `UnitStats` in place, behind
one entry point - `StatMath.ForHero(def, level, relic)` - called by both
`CombatSimulation.BuildUnits` and the hero-detail preview, so a displayed number and a fought
number cannot drift. Armor reuses that mechanism rather than inventing a parallel one:

```
StatMath.ForHero(def, level, relic, armor)   ->  ForHero(def, level)
                                                 ApplyRelic(stats, relic)
                                                 ApplyArmor(stats, armor)
```

`ApplyArmor` is `ApplyRelic`'s sibling: same in-place signature, same rounding rule (the four
multiplied stats round, the small fractions do not), same tolerance for a null or empty argument
being an exact no-op. Order is relic first, then armor, and it is fixed so it is reproducible; the
two do not commute for multiplicative stats.

**`CombatSimulation.cs` is not modified.** Armor resolves entirely before the simulation starts,
the way a `RelicGrade.Legendary` relic does. There is no armor field on `CombatUnit`, no armor
branch in `ApplyDamageEffect`, no `ArmorHook` enum, and no place for one.

### Deliberately not shared with relics

Armor gets its own `ArmorStat` and `ArmorStatMode` enums rather than reusing `RelicStat` and
`RelicStatMode`. Reusing them would mean adding six members to `RelicStat` to cover the pool, and
`ContentDatabase.ParseRelics` validates against that enum - armor would then be able to break relic
content loading. Separate types, separate save keys, no shared hooks. `RelicStat` is untouched.

### Determinism

Armor changes stat values, not the structure of the RNG stream, with one real edge, and it is the
same edge `StatMath.ApplyRelic` already documents: `DeterministicRng.Chance` short-circuits at
`p <= 0` and `p >= 1` without drawing. A piece that pushes `Block` off zero **adds a draw** at
step 0 of every incoming hit, and one that pushes `EffectHit` across a boundary adds or removes a
draw at the landing gate.

That is not a regression and it is not fixable - it is what it means for these stats to be live. It
matters for exactly one thing: **no harness fingerprint check may equip armor.** Every seeded
fingerprint in the harness runs on an unarmored team and stays byte-identical. The armor checks
build their own profiles.

Rolling a piece is itself seeded, via `DeterministicRng`, so a drop and a reroll are both
reproducible from a seed in a test.

---

## 10. Save

`SaveSerializer.CurrentVersion` 7 -> 8.

Added:

- `slag`, a long on the profile.
- `ownedArmor`, an array of self-describing pieces: `uid`, `name`, `slot`, `aspect`, `temper`,
  the primary roll, and the substat rolls.
- `equippedArmor` on each hero: an array of armor uids, one per slot, empty string for an empty
  slot.

**A piece stores its resolved values, not a roll seed.** A seed would be smaller and would make a
reroll a single integer bump, but it would also mean that retuning `armor.json` silently rerolls
every piece every player owns. Content is JSON here and is expected to move; a stored roll is
immune to that, which is the same instinct that makes a save hold hero ids rather than hero stats.

Upgrade needs no migration: a version 7 save has none of these keys, reads as zero Slag, an empty
armor collection and six empty slots per hero, which is exactly a player who has never seen the
system. Downgrade is refused by the existing version guard, as every bump since 2 has been - version
7 code would silently drop a player's entire armor collection and Slag purse.

`PlayerProfile.NormaliseArmor` runs on load, modelled on `NormaliseRelics`: a hero pointing at a
piece nobody owns is emptied, a piece claimed by two heroes stays with the first, and a piece in
the wrong slot for its own kind is unequipped. None of the three is reachable through `GameSession`
- they exist for a hand-edited or half-written save.

---

## 11. UI

`InventoryScreen` already holds a data-driven RESOURCES card and an OWNED RELICS list with a
hero-picker overlay for assign and unassign. Armor extends it rather than getting its own screen:

- **Slag** becomes a third row in RESOURCES. The card was written so that a third resource is one
  entry; this is that entry.
- An **ARMOR** section lists owned pieces. Each row: name, slot, aspect chip, temper chip, its stat
  lines, and its holder. Buttons: EQUIP (opens the existing hero picker), SALVAGE, and ANVIL
  (reroll).
- The picker overlay is reused unchanged in shape - it is already written against "pick a hero for
  this item".

`HeroDetailScreen` gains a read-only armor block: the six slots, what is in each, and the current
attunement count.

No new art. A piece is a name, two chips and a stat list, exactly as a relic is.

---

## 12. What this does not do

- No unique effects, no hooks, no pipeline entry. Enforced by `ArmorStatRoll` having nowhere to put
  one.
- No upgrade or enhancement track. Temper is fixed at drop.
- No substat locking. See section 7.
- No relic changes. `RelicStat`, `RelicDef`, `OwnedRelic`, `ApplyRelic` and the relic save keys are
  untouched.
- No art dependency, no hero-model rendering, no sprite work.
- No balance pass. Every number in `armor.json` is a placeholder in the same sense every relic
  number is, and `DESIGN-BUILD-ORDER` task 14 owns the retune.
