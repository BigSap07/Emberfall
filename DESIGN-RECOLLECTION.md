# Emberfall - recollection

Heroes have no per-hero progression beyond level and rarity. Gold has no sink once a roster hits
its level cap. A cleared stage has no reason to be fought again. This document specifies
recollection, the system that answers all three at once. It is the equipment layer: a per-hero,
flat-stat progression track paid for in gold and a new material earned by replaying stages you
have already cleared.

The premise above is decided and is not re-argued here. What follows is the design and the reading
of the code it has to sit on.

**The one constraint that shapes everything below.** DESIGN-RELICS exists to give relics the
effect pipeline's rule hooks, and its opening argument is that a relic is a different rule, never
a bigger number, and that nothing in it may be written as a percentage. DESIGN-COMBAT-STATS then
showed that the pipeline has a fixed number of hooks and that two systems already fight over four
of them (see DESIGN-HOOK-CONFLICT). Recollection is not allowed to become a third claimant.
Recollection owns numbers, never rules. It touches `StatMath`, never `CombatSimulation`. If a
future idea for recollection wants a hook in the effect pipeline, it is not recollection, it is a
relic, and it belongs in that document instead.

---

## 1. What the fiction already decided

DESIGN-WORLD is specific about what the Cinder is and is not. It is a fragment of the First Light
that took no side among the aspects, and what it can do is remember people whose pattern imprinted
on the First Light while it still held. It cannot create, and it cannot remember relics at all,
because a relic returns nothing and the Cinder only ever returns people. Summoning is the Cinder
recalling a pattern into form.

Three consequences follow, and none of them are invented, they are read off that paragraph.

**Recollection is the same act, done again, on a recall that already happened.** Summoning is the
first recall. Recollection is the Keeper spending further attention on a pattern already standing
in front of them, so that the recall holds more completely. It is a matter of degree, not a second
kind of magic.

**It cannot be transferable.** A recollection is not an object handed to a hero, it is how well
that specific pattern was recalled. There is nothing to hand to a different hero, and nothing to
move between two copies of the same hero either, which matters because it is not hypothetical:
`HeroInstance.Copies` (`Assets/Scripts/Core/Model/PlayerProfile.cs:14`) already shows that a
duplicate pull does not create a second instance, it increments a counter on the one instance that
exists. One hero, one pattern, one recall. Recollection state belongs on `HeroInstance`, keyed by
`Uid`, full stop. There is no version of this system where it is un-equipped, sold, or moved.

**It cannot be refunded.** Deepening is one-directional in the fiction the same way it would be for
an actual memory: you do not make a recollection shallower again on purpose. A respec button has no
fictional basis here, and it also is not free to build (it would need a return currency, a
confirmation flow, and a reason a player would trust it). Recollection is spent forward only. If a
tier's stat weighting turns out to be wrong, that is a balance patch to future tiers, not a refund
of past ones.

**Acquisition has to be the Keeper's own repeated attention, not loot.** Relics are found: DESIGN-
WORLD calls them objects recovered where the world's memory disagrees with the world itself, sitting
outside the Cinder's reach entirely. Recollection is the opposite motion, it is the Keeper looking
again at people who already stood somewhere. Refighting a stage is the Keeper attending to their own
roster in a place they have already been. That is the fictional justification for tying the new
material to stage replays specifically, rather than to a shop, an idle trickle, or a login reward.

The new material is named **Motes**. The Cinder is already described as a fragment of the First
Light; a Mote is a smaller fragment again, of the Cinder's own attention, spent one recollection at
a time. This keeps it distinct from Shards (summon currency, already spent and refunded elsewhere
in `GameSession`) and from Gold.

---

## 2. Acquisition: the code allows this, and it does not fight anything

This was the largest open question and it resolves cleanly. `StageDef` (`Assets/Scripts/Core/Data/
Definitions.cs:227-271`) is fully authored, not computed. Every reward field on it, `RewardGold`,
`RewardShards`, `RewardAccountXp`, `FirstClearGold`, `FirstClearShards`, `RecommendedPower`, is a
literal read straight off the stage's JSON object in `FromJson`, with a hardcoded fallback if the
key is missing. `campaign.json` (`Assets/Resources/Content/campaign.json`) carries one JSON object
per stage with exactly those keys and nothing that looks generated. `GameSession.GrantBattleResult`
(`Assets/Scripts/Core/Session/GameSession.cs:212-234`) reads those fields straight onto
`Profile.Gold` and `Profile.Shards` on every victory, with the `FirstClearGold`/`FirstClearShards`
bonus added only the first time. `EnemySpawn` (`Definitions.cs:200-224`) carries only `id` and
`level`, nothing resembling a loot table. There is no drop system anywhere in this project, per-
stage or per-enemy, computed or authored, to collide with.

Adding a per-stage material drop is therefore a content change, not an architecture change: a
`"motes"` key on each stage object in `campaign.json`, a `RewardMotes` field on `StageDef` read the
same way `RewardGold` is, and one more line in `GrantBattleResult` crediting `Profile.Motes`. Unlike
Gold and Shards, Motes should **not** get a first-clear bonus. The entire point is to make repeat
clears worth doing, so the reward has to be the same on clear one hundred as on clear one, scaled by
stage depth rather than by novelty.

**This is where recollection collides with a task that already exists, not with a design.** Build
order task 5 adds a repeat cap (`dailyResetUnix`, `stageClearsToday`) specifically because repeat
clears currently have no throttle: a player can already refight stage 1 forever for Gold and Shards
at whatever rate their team can clear it. Motes ride the same unthrottled loop until task 5 lands.
Recollection does not need to build a repeat cap, task 5 already owns one, but recollection cannot
be the reason that cap gets built either. It should be assumed present, not requested.

---

## 3. The stat pool: one track, not four

A hero gets one recollection tier number, not four independently-allocated stat tracks. Each tier
grants a fixed flat bundle, some HP, some Attack, some Defense, some Speed, authored once in
`BalanceConfig`, identical for every hero at that tier. This is a real design choice with a real
alternative rejected below (section 8), and the reasoning is worth stating plainly:

**Flat and identical across rarity is the point, not an oversight.** `StatMath.RarityMultiplier`
(`Assets/Scripts/Core/Progression/StatMath.cs:29-37`) already gives Epic a 1.34x and Rare a 1.16x
multiplier on every base stat. That gap is rarity's whole job. If recollection's flat bonus were
itself scaled by rarity, it would just be rarity again wearing a second name, and a Common hero
would never be able to close the gap no matter how much a player invested in one. DESIGN-COMBAT-
STATS already established the phrase for this shape of stat when it specified Effect Hit and
Effect Resist as "flat across level and rarity." Recollection reuses that exact standard on purpose.
A fully recollected Common hero and a fully recollected Epic hero gain the same flat amount; the
Epic is still stronger, because rarity still multiplies the base the flat amount is added to
afterward (section 4), but recollection itself does not care what rarity it is deepening.

One combined tier also keeps the save shape and the UI to one number instead of four (sections 5
and 6).

---

## 4. Where the flat stats enter StatMath.Build, and why it has to be after

`StatMath.Build` (`StatMath.cs:50-62`) is the whole formula:

```
scale = 1 + def.Growth * (level - 1)
MaxHp = Round(def.BaseHp * scale * rarityMultiplier)
Attack = Round(def.BaseAttack * scale * rarityMultiplier)
Defense = Round(def.BaseDefense * scale * rarityMultiplier)
Speed = Round(def.BaseSpeed + (level - 1) * 0.35)
```

There are two places a flat recollection bonus could enter: folded into the base before `scale` and
`rarityMultiplier` are applied, or added on afterward, once, unscaled.

**Before is wrong, and it stops being flat the moment it lands.** A bonus added to `def.BaseHp`
before the multiply gets re-multiplied by `scale` and `rarityMultiplier` on every read. That means
the same recollection tier is worth more on a level 60 hero than a level 1 hero, and more on an
Epic than a Common, exactly the coupling section 3 just argued against. It also breaks the
player-facing promise: a stat panel that says a tier grants "+40 Attack" would be lying, since the
number actually delivered would move with level and rarity. And it compounds forever: a hero's own
future level-ups would keep inflating a bonus the player already paid for, for free, which is not a
reward, it is an accounting bug wearing a reward's clothes.

**After is correct.** The bonus is added once, to the already-`Round`ed, already-scaled,
already-rarity-multiplied result, the same way `BaseSpeed`'s flat `+ (level - 1) * 0.35` sits
outside the rarity multiply today. This is not a new pattern, `Speed` already proves the codebase is
comfortable adding an unscaled flat term after the rest of the formula resolves. Recollection's
bonus is four more lines of exactly that shape, one per stat.

**The real cost is the method signature, not the math.** `StatMath.ForHero(HeroDef def, int level)`
and `StatMath.ForEnemy(EnemyDef def, int level)` both call the same private `Build`, and `Build` has
no idea a `HeroInstance` exists, it only ever sees a `UnitDef` and a level. Recollection tier lives
on `HeroInstance`, not on `HeroDef` and not on level, so `ForHero` needs a new parameter (the tier,
or the resolved flat bundle) that `ForEnemy` must never receive. `Build` itself needs an optional
additive parameter that defaults to none, because it is the shared method behind both call sites,
and an enemy silently inheriting a player's recollection math because a default was left non-zero
would be exactly the kind of bug that is invisible until someone reads the diff. This is a small,
mechanical change, but it is the one place in this document where "flat stats only" is not free,
it moves a formula that is currently keyed on two inputs to one keyed on three.

---

## 5. PowerScore: recollection cannot take the exit Effect Hit took

DESIGN-COMBAT-STATS deliberately kept Effect Hit and Effect Resist out of `PowerScore`
(`StatMath.cs:69-79`), specifically to avoid retuning the twelve `recommendedPower` values and
`CheckStageWinnability` in the same pass that added the stats. That exit exists because Effect Hit
and Effect Resist are new terms, entirely absent from the existing formula; leaving them out is
just not writing a new line.

Recollection cannot use the same exit, because it is not a new term. `PowerScore` already weights
`MaxHp`, `Attack`, `Defense`, and `Speed`, which are precisely the four stats recollection adds to.
There is no version of "recollection stays out of Power" that does not also mean "Power stops
reflecting a stat the player can see boosted on the exact same screen." That is a worse problem
than the one Combat-Stats avoided: not a missing term, but a number on `HeroDetailScreen` that lies
to the player about their own investment, or a second, shadow `UnitStats` computed without
recollection purely to feed `Power`, which is the kind of parallel-path complexity this project's
own combat sim was built specifically to avoid. Recollection is forced into `PowerScore`. There is
no clean way out of it.

**What that does to the twelve `recommendedPower` values.** They currently run 2600 to 6400 across
`ch1_1` through `ch1_12`. Motes only exist as a reward for clearing a stage, so a player cannot
accumulate recollection-driven Power ahead of a stage they have not reached, `CheckStageWinnability`
and `CheckUnderlevelledLoses` are checking teams at or below the frontier of what they have already
cleared, and recollection cannot move that frontier forward, only deepen it behind the player. The
low end of the winnability floor is safe. The risk is the opposite direction: `recommendedPower` on
stages a player has already cleared becomes a number recollection can blow straight through, so a
"you are above the recommended power for this stage" reading stops meaning "this will be easy" and
starts meaning "this reflects investment, not just level and rarity." That is a legibility
question, not a correctness one, since a higher Power team failing to also be a stronger team was
never a risk here, everything recollection adds is real combat stat.

---

## 6. Save cost: recollection is not the second name on the list, or the third

`SaveSerializer.CurrentVersion` (`Assets/Scripts/Core/Save/SaveSerializer.cs:15`) is `1` today.
Reading the actual queue rather than assuming a number:

- Build order task 5 (repeat cap) already takes it to 2.
- Build order task 8 (team size 3 to 5) bumps it again, specifically to protect the downgrade path
  where a 5-entry `Team` array read by 3-slot code would silently truncate two heroes with no
  error.
- DESIGN-RELICS already flagged itself as riding whichever of those lands first, or claiming 2 to 3
  on its own if it ships alone.

Recollection is a fourth thing wanting the same integer, behind repeat cap and team size for
certain, and possibly behind relics too depending on ship order. No version number is picked here.
The honest statement is that recollection sequences behind all of the above, and whichever document
ships last should be the one that states the actual number, the same way DESIGN-RELICS already
declined to pick one.

**New save-shaped state, once it is recollection's turn.** `PlayerProfile.Motes` (a `long`,
alongside `Gold` and `Shards`, `Assets/Scripts/Core/Model/PlayerProfile.cs:41-42`) and one new int
on `HeroInstance` for the combined tier (alongside `Level` and `Copies`, `PlayerProfile.cs:11-15`).
Both follow the exact pattern `SaveSerializer` already uses for every other field: written with a
default if absent, so an old save loads into a fresh install with `Motes = 0` and every hero at
tier 0, no migration code, the same graceful-degradation `SaveSerializer` already gives an unknown
`defId`.

---

## 7. UI: HeroDetailScreen has vertical room and no horizontal room

`HeroDetailScreen` (`Assets/Scripts/UI/Screens/HeroDetailScreen.cs`) is explicitly commented as "the
only place gold turns into power," which makes it the obviously correct home for recollection, and
also the place that comment stops being true the day recollection ships (a one-line fix, noted here
so it is not forgotten).

**Vertically, there is room.** The detail content lives in a `ScrollRect`
(`UIBuilder.ScrollList("Detail", ...)`, `HeroDetailScreen.cs:65`), and the Stats card
(`BuildStatsCard`, `HeroDetailScreen.cs:127-151`) is one card among others inside it. A Recollection
card, built the same way, sized the same way, sits below Stats and above the ability cards, or
after them, without needing to fight anything for space. This is cheap.

**Horizontally, and specifically at the action bar, there is not.** Two things are load-bearing:

First, `AttachDelta` (`HeroDetailScreen.cs:154-160`) hangs exactly one "+n" preview per stat row,
currently used to preview the next level's gain. Recollection's own bonus needs to be visible on the
same four rows (HP, Attack, Defense, Speed), and folding it silently into "now" defeats the purpose
of a system whose entire pitch is visible investment. Two different "+something" sources wanting the
same slot on the same row is a real layout conflict, not a drop-in.

Second, `BuildActionBar` (`HeroDetailScreen.cs:162-185`) is a fixed 200-tall bottom strip. When a
hero is owned, it already shows two full-width buttons, `LEVEL UP` at 640 wide and `x10` at 340
wide, which is 980 of the 1080-ish usable width before padding, matching the single `OPEN THE RIFT`
button (980 wide) that replaces them when the hero is locked. There is no third slot waiting to be
filled. A `RECOLLECT` action belongs on the new card itself, next to its own tier readout, not
squeezed into the bottom bar, which is specifically the gold-for-level bar and should stay that way.

**This screen is also mid-renovation already, independent of recollection.** Task 8 changes
`_slotButtons` from 3 to 5 entries and task 9 re-lays the whole screen out for landscape. Any real
estate estimate against today's portrait, 3-slot layout is an estimate against a screen that will
not exist by the time recollection could ship. The cost above is directionally right, the pixels are
not.

---

## 8. Rejected

**Per-stat allocation (player chooses where each tier's points go).** More expressive, and also a
four-times-larger save shape, a stat-picker UI the current screen has no room for even before
recollection's own additions, and a source of build-comparison complexity the project has
deliberately avoided everywhere else (one relic slot, one recollection tier, one team). Rejected in
favour of the fixed bundle in section 3.

**Rolled substats with randomness (a gear-roll model).** The combat sim is deterministic by design,
`CombatSimulation.Run()` resolves a fight up front from a seed, and the wider project has no other
system that hides an outcome behind a roll the player did not choose to gate (Effect Hit and Effect
Resist are a resolution roll during combat, not an acquisition roll). Introducing RNG into how much
a piece of progression is worth, after the fact, would be the first system in this project to do
that, and there is no argument here that it should be.

**A percentage bonus per tier ("recollection grants +2% Attack per tier").** This is exactly the
shape DESIGN-RELICS bans in its opening argument and exactly the shape DESIGN-ASPECTS names and
rejects for every one of its six identities. Recollection is not exempt from a rule this project has
applied twice already just because it is a different document.

**Recollection material dropping from specific enemies rather than the stage as a whole.** Nothing
in `EnemySpawn` carries any per-unit loot concept today, and adding one would mean reading combat
outcomes (which enemy died, which enemy was even present) back out of the fight to attribute a
drop, when the stage-level reward already answers "did the player win" without needing to. Rejected
for cost, not principle, a future document is free to revisit this once relics or aspects give the
sim a reason to report per-unit outcomes for some other purpose.

**A refund or respec.** Argued against on fictional grounds in section 1. Also argued against on
cost grounds: a refund needs a return currency (Motes back, Gold back, or a soft currency invented
solely to make refunds feel fair), a confirmation flow, and a story for why the Keeper's recall can
suddenly reverse when nothing else about this system reverses. Not built.

---

## 9. Contradictions with existing docs

**DESIGN-BUILD-ORDER task 14 ("Full twelve-stage retune. Once, last.").** That document is explicit
that task 14 is the only step that produces a valid win-rate table, and that nothing tuned before it
should be read as final. Recollection adds Power to rosters via `PowerScore` (section 5) and adds a
new reward currency whose per-stage values need authoring against the same economy task 14 tunes.
Shipping recollection before task 14 would mean task 14 is retuning against a roster shape that
changes again immediately afterward, which is the same contradiction DESIGN-WORLD already named
between Aspects and build order's task 9. The resolution is the same: recollection sequences after
14, not before it, and "once, last" stays true because recollection is not part of the once.

**DESIGN-RELICS' own save-version arithmetic ("2 to 3, if relics ship alone").** Not wrong, just no
longer complete. That line was written when repeat cap and relics were the only two systems in the
queue for `SaveSerializer.CurrentVersion`. Recollection is a third, and depending on ship order
possibly a fourth behind team size. This does not need fixing now, DESIGN-RELICS should not be
edited for a system that does not exist yet, but whichever of these three documents ships last is
the one that owes the real number, and none of them owes it today.

**The `HeroDetailScreen` code comment.** "The only place gold turns into power" is accurate today
and becomes false the moment recollection ships on the same screen. Noted in section 7. A one-line
comment fix at implementation time, not a design contradiction, but it is the kind of small
inaccuracy that outlives the reason for it if nobody names it.

No contradiction was found with DESIGN-ASPECTS, DESIGN-COMBAT-STATS' pipeline-hook argument, or
DESIGN-HOOK-CONFLICT. Recollection touches none of the hooks any of those three documents claim,
which is the entire point of the constraint stated in the introduction.

---

## 10. At team size 5

Recollection is per-hero, so its total cost scales with roster depth, not with team slot count
directly, but team size 5 changes both sides of that ledger.

**Cost side.** A player fully recollecting a five-hero deployed team pays for five heroes' worth of
tiers instead of three, roughly 67% more total Gold and Motes to bring a full team to the same tier,
before counting bench heroes at all. The tier-cost curve (section 6) needs to be authored with that
multiplier in mind, not tuned against a 3-hero assumption and left alone.

**Income side, and this is the one that could actually hurt.** Build order task 10 refills
`campaign.json` with enemy counts that ramp toward five per side. A harder, longer fight at 5v5
costs more real time per clear than the same stage did at 3v3, while `RewardMotes` (section 2) is a
flat per-stage number that does not automatically rise to match. Left unaddressed, Motes-per-minute
would fall exactly when team size 5 increases the total Motes a full team needs, a squeeze in both
directions at once. This is not a reason to change the acquisition model, it is a reason the actual
`RewardMotes` values cannot be authored until the 5v5 enemy refill and the task 14 retune have
already happened, which section 9 already puts recollection behind for other reasons. One more
argument for the same ordering, not a new one.

Nothing about the fiction, the transferability argument, or the single-tier design in section 3
changes at team size 5. Those are per-hero and per-pattern, not per-team.

---

## 11. What could not be verified in this pass

**DevHarness checks beyond the three DESIGN-COMBAT-STATS already named.** `CheckStageWinnability`,
`CheckUnderlevelledLoses`, and `RunCampaignBalancePass` are discussed here on the strength of that
document's own audit of them, not from an independent read of `DevHarness` in this pass. If other
harness checks assume `PowerScore` is a pure function of `HeroDef` and level, they would also need
attention once recollection is real, and this document does not have a complete list of which ones
those are.

**Whether an unrecognised JSON key is tolerated everywhere it would need to be.** Every `FromJson`
method read in this pass (`StageDef`, `EnemySpawn`) uses `Get*` accessors with defaults, which is
consistent with unknown keys being ignored safely, but this was not checked against every content
loader in the project, only the ones a stage-level material drop would touch.

**Whether any other screen reads `HeroInstance` stats in a way that assumes exactly four fields.**
`HeroDetailScreen` was read in full. Other UI (`Widgets.cs`, banner or team-select screens) that
also render hero stats were not audited here for the same signature change discussed in section 4.

---

## 12. Where this sits

Not a numbered task in DESIGN-BUILD-ORDER, and it should not become one yet. That document's own
task 14 is described as the last task, and sections 5, 6, 7, and 9 of this document all land on the
same conclusion independently: recollection has to sequence after task 14, not inside the fourteen.

It is also worth saying plainly rather than leaving implied: the game currently has one chapter,
twelve stages, and chapters 2 through 6 do not exist yet. A system whose entire second purpose is
giving a cleared stage a reason to be replayed is worth relatively little against twelve stages of
content, most of which a returning player will simply outlevel. Recollection is a system for a game
with a campaign long enough to want deepening instead of just clearing, and that game is not built
yet. Writing the design now, while the fiction and the code are fresh, is worth doing. Scheduling it
is not, until there is more campaign for it to give a reason to replay.
