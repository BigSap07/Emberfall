# Emberfall - the relic/aspect hook conflict

DESIGN-BUILD-ORDER names four collisions between DESIGN-RELICS and DESIGN-ASPECTS: two relics
and two aspect identities both reaching for the same thing in the effect pipeline, or for the
same piece of content. DESIGN-ASPECTS section 6 argues a direction for resolving them (aspects
keep the hook, relics move off it) but that is an argument made inside a document about
something else, not a decision anyone has made. This document exists to make the decision
possible in one sitting: what each side actually claims, what the code actually does, what each
resolution actually costs, and where the proposed fix does not survive contact with the code as
written. It does not choose. Nothing here is a new relic, a new aspect rule, or a new task.

Two of the four are genuine hook collisions: two systems wanting the same branch in
`CombatSimulation.cs`, where giving it to one makes the other impossible as written. The other
two are not hook collisions at all; they are two documents disagreeing about the same piece of
authored content (a passive's numbers, a hero's aspect string). They are handled separately below
because the shape of the decision is different: a hook collision is resolved by choosing who owns
a branch of code, a content collision is resolved by choosing what a fact about the game is.

## At a glance

| # | Collision | Kind | Hook or content |
|---|---|---|---|
| 1 | Tide vs Tidewrought Metronome | Hook | `AddEnergy`'s clamp to `EnergyMax` |
| 2 | Verdant vs Green Hour Vessel | Hook | `ApplyHealValue`'s early return on `missing <= 0` |
| 3 | Kalyx's Kindling vs Splitting Prism | Content | `PassiveDef.BonusVsStatus` / `BonusMultiplier` |
| 4 | Orrin's aspect vs Warden's Debt | Content | Orrin's `"aspect"` string in `heroes.json` |

Only these four. The other two relics, Stormlance Oath (Ryn, retargets `LowestHpEnemy` to
`HighestHpEnemy`) and Verdict Sigil (Nyx, widens which triggers fire `AfterBasicAttack`), touch no
hook any aspect identity claims. Whichever way all four collisions resolve, those two relics are
unaffected and can be built on their own schedule.

---

## The two hook collisions

### 1. The energy clamp: Tide vs Tidewrought Metronome

**Tidewrought Metronome** (DESIGN-RELICS): *"`EnergyGain` the wearer grants is not clamped at
100; the excess banks toward the next cast."* Named wearer: Maeve. Stated effect: "Tidal Cadence
plus Rising Chorus wastes energy constantly at cap. Banked, the line can cast on consecutive
rounds. Pure tempo, zero stats."

**Tide** (DESIGN-ASPECTS): *"Tide never discards energy. Overflow past `EnergyMax` is handed to
the living ally furthest from casting instead of being thrown away."* Same clamp, different shape
of fix: Metronome banks the excess for the same caster to spend later; Tide redirects it to a
different ally immediately. These are not the same mechanic wearing different words. One is a
personal reserve, the other is a hand-off.

**What the code actually does.** `AddEnergy(CombatUnit unit, int amount)` is one shared method
with no notion of who granted the energy:

```csharp
private void AddEnergy(CombatUnit unit, int amount)
{
    if (!unit.Alive || amount == 0) return;
    int before = unit.Energy;
    unit.Energy += amount;
    if (unit.Energy > unit.EnergyMax) unit.Energy = unit.EnergyMax;
    if (unit.Energy < 0) unit.Energy = 0;
    if (unit.Energy != before) EmitEnergy(unit);
}
```

It is called from three places, and only one of them is what either document means by "grants":
the `EnergyGain` effect a caster's ability applies to a target. The other two are a unit's own
energy regen, with no caster at all: `EnergyPerBasicAttack` after its own basic attack, and
`EnergyOnDamaged` when it takes a hit. Neither Tide's redirect nor Metronome's bank can be built
as specifically "energy the wearer grants" without first adding a source to this method, since it
cannot currently tell "Maeve granted this via Tidal Cadence" apart from "this unit's own basic
attack refilled itself." That plumbing is small (the caller at the `EnergyGain` site already has
`source` in scope) but it is real, unwritten work, and it is needed regardless of which side wins
this collision. DESIGN-ASPECTS' own wording for Tide, read literally, does not make this
distinction either: "Tide never discards energy" does not say "energy Tide grants," so as written
it would also redirect a Tide unit's own basic-attack and on-damage regen, which is probably not
intended but is not ruled out by the text either.

Once that plumbing exists, the two mechanics are still different shapes of work: Tide's redirect
needs a lookup across the whole roster for "the living ally furthest from casting," a targeting
operation with no existing equivalent; Metronome's bank needs an entirely new stored value on the
unit (there is no banked-energy field anywhere in `CombatUnit` today) that persists and is spent
on the wearer's own next cast.

**If aspects keep it.** Metronome is a no-op on its intended wearer, since Maeve is the one Tide
hero DESIGN-ASPECTS counts. DESIGN-RELICS drops from six relics to five, or Metronome needs
relocating. DESIGN-ASPECTS needs no rewrite; Tide's rule as stated already covers this, though the
basic-attack/on-damage ambiguity above should be closed while someone's already in that
paragraph.

**If relics keep it.** Tide loses the only mechanic DESIGN-ASPECTS gives it. Unlike Ember, which
still has Burn-stacking if Kindling is cut, or Umbra, Gale and Lumen, which each have more than
one named entry point, Tide's entire identity in section 1 and section 2 is this one hook. Losing
it does not narrow Tide, it empties it; DESIGN-ASPECTS would need to find or invent a second Tide
mechanic before the aspect means anything at all. This is a bigger cost to the losing side than
the mirror case above, not a symmetric one.

**Whether the proposed relocation survives.** DESIGN-ASPECTS section 6 suggests: *"Metronome
becomes a hook on `EnergyGain` cost rather than cap."* Read against the code, `EnergyCost` is a
flat number on an ability (`unit.Energy >= unit.Active.EnergyCost` gates whether a cast fires). A
relic that reduces that number is a numeric discount on how much a cast costs, which is exactly
what DESIGN-RELICS' own founding rule forbids in its own words: *"if a relic cannot be written as
one sentence without a number, it is a stat stick and should be cut."* "Costs 20 less energy to
cast" is that sentence with a number in it. If "EnergyGain cost" means something other than the
`EnergyCost` number, the six words proposing it do not say what, and nothing in DESIGN-RELICS'
own hook inventory names a second thing by that name. As written, this relocation either fails
the standard it has to answer to or points at a hook that does not exist. It needs a human to
say which, not an assumption that it already works.

### 2. The heal clamp: Verdant vs Green Hour Vessel

**Green Hour Vessel** (DESIGN-RELICS): *"healing above a target's missing HP becomes a ward
instead of being discarded."* Named wearer: Wren. Stated effect: *"Verdant Tide on a healthy line
is currently worth nothing; with this it is a team-wide ward, and Wren stops being a reactive
button."*

**Verdant** (DESIGN-ASPECTS): *"Healing and warding with nowhere to go becomes `Regen` on the
recipient rather than being clamped away."* Section 2 names two entry points: `ApplyHealValue`'s
early return, and, in the document's own words, *"`EffectKind.Shield` in `Sim.ApplyEffect` is the
second entry point."*

**What the code actually does, and the finding that matters most in this document.** The heal
clamp is real:

```csharp
private void ApplyHealValue(int sourceId, CombatUnit target, int amount, string vfx)
{
    if (!target.Alive || amount <= 0) return;
    float missing = target.MaxHp - target.Hp;
    if (missing <= 0f) return;
    int healed = (int)Math.Min(missing, amount);
    ...
```

`missing <= 0` discards the rest silently. Both documents want this exact branch: DESIGN-RELICS
to redirect it to a ward, DESIGN-ASPECTS to redirect it to `Regen`. That part of the collision is
exactly as described. But the Shield side is not. Here is the entire `Shield` handler:

```csharp
case EffectKind.Shield:
{
    if (!target.Alive) break;
    float amount = source.EffectiveAttack * effect.Power + effect.Flat + source.MaxHp * effect.SelfHpRatio;
    int value = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
    if (value <= 0) break;
    target.Shield += value;
    ...
```

`target.Shield += value` is unconditional. There is no cap, no `missing`-style ceiling, nothing
discarded, ever. A target with 900 shield already on it gets the new value added on top of the
900. DESIGN-ASPECTS describes Verdant as claiming a Shield-side discard as its "second entry
point," and there is no such discard to claim. This is not a small imprecision: it means DESIGN-
ASPECTS section 6's own proposed fix for this collision, *"Green Hour Vessel moves to the `Shield`
path Verdant doesn't take,"* is not available, for two independent reasons. First, by DESIGN-
ASPECTS' own section 2 text two sections earlier in the same document, Verdant already claims
that path, so it is not free to hand to anyone. Second, and more basically, there is nothing there
to move to: Green Hour Vessel converts wasted healing into a *new* ward, it does not intercept a
wasted ward, so "the Shield path" was never the site of this collision in the first place. The
actual, only collision here is on `ApplyHealValue`, and the suggested relocation does not touch
it.

**If aspects keep it.** Green Hour Vessel is a no-op on Wren, DESIGN-RELICS' own Verdant hero,
the same shape of collision as Metronome and Maeve. DESIGN-ASPECTS needs one correction regardless
of who wins this collision: drop the false claim that Shield is a second entry point, since
current code discards nothing there for Verdant to bank. DESIGN-RELICS loses a relic whose entire
premise (turn a discard into a reward) is intrinsically the heal-clamp hook; nothing else in its
own inventory (`effect.Target`, `effect.Hits`, `effect.Chance`, `PassiveTrigger`,
`AspectChart.Multiplier`) produces "unwanted healing becomes something useful" as a side effect,
so this relic may be harder to relocate at all than Metronome, not just costly to relocate.

**If relics keep it.** Once the false Shield claim is corrected, Verdant is in the same position
Tide is in: one real hook, contested, no fallback. DESIGN-ASPECTS would need a genuinely new
mechanism for Verdant, not a reduced one.

---

## The two content collisions

These are not fights over a shared branch of code. Kindling and Splitting Prism touch different
methods; Orrin's aspect string and Warden's Debt's targeting rule touch different systems
entirely. What they share is that two documents have made incompatible claims about the same fact
(what Kindling's numbers should be, what colour Orrin is), and one relic's own stated justification
depends on the answer.

### 3. Kalyx's Kindling vs Splitting Prism's stated cost

Kindling is real, exactly as both documents describe it:

```json
"passive": {
  "id": "kalyx_passive", "name": "Kindling", "trigger": "None",
  "bonusVsStatus": "Burn", "bonusMultiplier": 0.30,
  "description": "Kalyx deals 30% more damage to anything already burning.",
  "effects": []
}
```

Read only in `ApplyDamageEffect`, and only ever non-default for Kalyx; no other hero or enemy sets
these fields. DESIGN-ASPECTS calls this exactly what its own no-percentage rule forbids and cuts
it, replacing Kalyx's identity with Ember's Burn-stacking rule. DESIGN-RELICS' Splitting Prism
names Kindling as its own downside: retargeting `AllEnemies` Damage to `FrontEnemy` with `Hits`
multiplied stops Burn from spreading across the whole line, so *"Kindling (+30% versus burning)
has almost nothing to feed on. The relic asks you to rebuild him."* If Kindling does not exist,
that sentence describes nothing. The relic's upside (a board-wipe becomes a drill) is unchanged;
its only stated cost evaporates.

**If Kindling is cut.** `PassiveDef.BonusVsStatus` and `BonusMultiplier` become dead fields, read
by code that never finds a non-default value; not a bug, just orphaned plumbing worth noting for
later cleanup. Splitting Prism needs a new downside clause in DESIGN-RELICS, one paragraph. A real
one is available under Ember's actual replacement identity: without spread, fewer simultaneous
Burn stacks exist to tick, which is a true cost under Burn-stacking the way the current text is a
true cost under Kindling. Whether that is worth writing in, or whether Splitting Prism should be
cut alongside Kindling, is the actual decision here.

**If Kindling stays.** DESIGN-ASPECTS keeps a hero whose passive is a percentage bonus its own
constraint names as the thing it exists to forbid, stated in its own words: *"precisely the thing
the no-percentage constraint forbids, and it is the current Ember identity. It has to go."*
Keeping it is not a rewrite, it is DESIGN-ASPECTS carrying an unresolved exception to its own
founding rule for exactly one hero.

### 4. Orrin's aspect vs Warden's Debt's premise

Warden's Debt (DESIGN-RELICS): redirects `Shield` effects targeting `Self` to the lowest-HP ally
instead. Named wearer: Orrin. Its entire justification depends on him being an Ember tank whose
own passive rewards being low on health: *"Cinderguard's ward leaves the tank and lands on Ryn or
Kalyx. Orrin becomes a protector who protects nobody but the line, and drops below half health
faster, which is where Emberskin wants him. Deliberately double-edged."*

Orrin's actual kit, confirmed in `heroes.json`: `Shield` on `Self` (22% of his own max HP),
`Taunt` on `Self`, `DefenseUp` 45% at half HP. DESIGN-ASPECTS section 3 reads this as *"the Lumen
rule, verbatim, three times over"* and recommends relabelling his `"aspect"` string from `"Ember"`
to `"Lumen"`, a one-string content change that touches no ability. DESIGN-WORLD section 3 argues
the same relabel independently, on the fiction (his lore reads as "everything should have a
knowable place," Lumen's belief, not Ember's "nothing changes without destruction") and does not
mention Warden's Debt at all; the two documents arrive at the same content change from different
directions without either one noticing the relic.

**The actual tension.** This is not a hook fight; `effect.Target` on `Self`, the hook Warden's
Debt uses, is not claimed by any aspect identity. The collision is that Warden's Debt's flavor
text is written for an Ember hero whose self-shielding is incidental to his kit, and removing it
is a real sacrifice, "double-edged," precisely because it costs him something outside his
identity. If Orrin becomes Lumen, self-shielding and interception stop being incidental and become
the definition of what his colour means. Taking that away from him no longer costs him a bonus; it
removes the one thing his colour is. A relic that was "double-edged" on an Ember tank reads as
"disables what this hero's colour is" on a Lumen one, and DESIGN-RELICS' own standard (a relic
should bend a rule, not erase it) is not obviously still met.

**If Orrin relabels.** One string in `heroes.json`, confirmed trivial in code. Warden's Debt needs
more than a downside rewrite; its whole justification needs re-arguing, or the relic needs a
different wearer. Two consequences that follow from the relabel itself, independent of Warden's
Debt, and already named in DESIGN-WORLD: `AspectChart`'s Lumen/Umbra pair, currently inert because
no unit in the game is Lumen, activates for the first time, and the first thing it does is give
every Umbra enemy (`hollow_husk`, `sundered_colossus`) a live advantage against the player's tank,
in the first stage of the campaign and the last. That is a real difficulty change, not a cost this
document is inventing, and it lands whether or not Warden's Debt is ever resolved.

**If Orrin stays Ember.** Warden's Debt's text is valid as written, no rewrite needed. DESIGN-
ASPECTS' own section 5 finding stands unaddressed: Lumen has no hero, Ember has a duplicate that
does not fit the identity DESIGN-ASPECTS defines for it. DESIGN-ASPECTS already anticipates this
exact outcome as workable: Lumen is called the least trustworthy of the six entries and *"if one
of the six has to be cut to ship, cut this one."* Keeping Orrin Ember does not block building the
other five identities. It does mean Lumen stays unbuilt for as long as no hero is found for it,
and Umbra's advantage against a Lumen target never fires in this game under the current roster.

---

## Sequencing

DESIGN-ASPECTS section 6 states, without arguing it further: *"do not build relics and aspects in
the same pass and expect either spec to survive."* Whether that holds, and in which direction,
depends on how the four collisions above resolve.

**If aspects keep both hooks (Tide keeps the energy clamp, Verdant keeps the heal clamp), build
Aspects first.** Two of the four relics collision here (Metronome, Green Hour Vessel) need full
rewriting once Aspects lands, and the other two collisions (Kindling, Orrin) are entangled with
decisions that are themselves part of the Aspects work, not independent of it: whether Kindling
survives is an Ember-identity question, whether Orrin relabels is a Lumen-identity question.
Writing Splitting Prism's or Warden's Debt's replacement text before those questions are settled
means writing it twice. Only Stormlance Oath and Verdict Sigil, the two relics untouched by any of
this, can be built in either order without cost.

**If relics keep one or both hooks, the pressure reverses for that hook, not for the whole
decision.** A relic that keeps its hook needs nothing from Aspects and can be built immediately;
Relics does not depend on Aspects the way Aspects depends on Relics resolving first once it has
lost a hook. But the aspect that lost its hook is not simply narrower, per sections 1 and 2 above:
Tide and Verdant each have exactly one named mechanism today, so either one losing its hook leaves
that aspect with no identity at all until a replacement is designed. In that direction, Relics can
proceed on its current schedule, but Aspects cannot be finished as specified; it needs a design
round of its own first, for whichever of Tide or Verdant lost.

**The content collisions do not gate sequencing the same way.** Kindling staying or going, and
Orrin relabelling or not, each have a valid, buildable state on both sides (Splitting Prism and
Warden's Debt as currently written are not broken by inaction, only made to describe a fiction
that will stop matching once the other document's change lands). They do not block either Relics
or Aspects from being built; they determine whether specific relic entries need rewriting
afterward, and how much.

Four collisions, two of them decided by which system owns a branch of `CombatSimulation.cs`, two
of them decided by what two documents want a hero to be. This document states the cost of each
answer. It does not supply one.
