# Emberfall - waiting on Pinoy

**What this file is.** Everything the project cannot settle by itself because the answer is Pinoy's.
It is one file so it can be sent as one message, and it is maintained here rather than in
`DESIGN-BUILD-ORDER.md`, which now points at it.

**Four kinds of thing are in here**, and they want different kinds of answer:

1. **Deliveries to identify** - two heroes ship the same battle body, and three profile icons
   arrived under a name no hero has.
2. **Kit text to confirm** - clauses where the shipped kit does not do exactly what the sentence in
   `import/Heroes.json` says, because the engine cannot say it, because the star band had to move
   the number, or because a 3-star has no slot for the shape the sentence needs. None of these was
   quietly reworded.
3. **Strike frames to set by hand** - where a clip's impact frame is a derived guess rather than a
   judgement.
4. **A note for the next painting** - one constraint on future hub art.

Checked against the tree on 2026-09-22, after the 2-star and 1-star tiers were authored, the
roster reached 94 of 94, and the five-star review moved four numbers on three of them. Section 2b
was rebuilt against the shipped kits when the 3-star tier landed rather than carried forward - the
engine gaps pass had retired nine of its rows and this file had not been told - and the low-star
batch appends to it rather than rebuilding it again.

**Sections 2b and 2c are closed lists**: every hero has a shipped kit, so no further batch can add
a row to either, and 2c's four-clause Energy question is now answered here rather than waiting.
**Section 2d is new and is not closed**, because it holds departures a MEASUREMENT produced rather
than an authoring batch, and the instruments keep running.

---

## 1. Deliveries to identify

### 1a. Whose art is in `Celen.zip`

**Celen and Nereia ship the same body.** Of the seven clips they each carry, **four are
byte-identical** - `basic`, `controlled`, `hit_reaction` and `idle` - and the other three differ by
so little that they are plainly the same drawing:

| clip | differing pixels | of | share |
|---|---|---|---|
| `basic2` | 29 | 4,118,400 | 0.0007% |
| `death` | 117 | 4,118,400 | 0.0028% |
| `skill` | 416 | 4,118,400 | 0.0101% |

All seven sheets are 1980x2080 in both deliveries. One of the two is the other one's art under a
different name, and which way round it goes was not a guess to make from the sheets alone.

**The profile icon delivery answers it.** `Celen_icon.png` and `Nereia_icon.png` are two plainly
different characters and are not byte-identical to each other, unlike the battle sheets - so the
icons are the reference the sheets were missing. Celen's badge is an avian character in blue and
white carrying a war fan. Nereia's is a hooded figure with a lantern staff wreathed in blue flame.
**The body both heroes currently ship is Nereia's**: the sheet filed as
`celen_the_open_hand_idle.png` is the hooded lantern-bearer, frame for frame, and nothing avian
appears in either delivery.

**Why it matters where it shows.** Nereia is in the prologue squad and is one of the five bodies the
harness's pinned yardstick fields, so the prologue's first battle is the most visible place a wrong
body would appear. It is now known to be the right one there. Celen is the only 4-star in the
roster with drawn battle art at all, and it is not hers.

**What is left to need:** a sheet set drawn for the character on `Celen_icon.png`. Nereia's sheets
are correct and need nothing.

### 1b. Three profile icons arrived under a name no hero has

The profile icon delivery is 94 files for 94 heroes and 91 of them carry the hero's display name
exactly. Three do not. Each was matched anyway, because the aspect folder and the star folder it
was delivered in leave exactly one hero unclaimed in that cell, and each match was then checked
against the character rather than taken on the folder alone. **They are wired in on that basis,
so a "no" to any of these is a file to re-point, not a file to go and find.**

| delivered as | wired to | how it was resolved |
|---|---|---|
| `Lyda_icon.png`, Gale 3-star | Lydia, `lydia_the_unscheduled` | Spelling. The only free Gale 3-star. |
| `Dalia_icon.png`, Gale 5-star | Dahlia, `dahlia_the_wayward_hymn` | Spelling. The badge is the pale-blue-haired figure with the bell staff, which is Dahlia's shipped battle sheet. |
| `Vespera_icon.png`, Umbra 5-star | Cyrra, `cyrra_the_burden_bearer` | **Not a spelling.** The only free Umbra 5-star, and the badge is a white-winged angel with a staff and a tome, which is Cyrra's race, role and shipped battle sheet. |

**What is needed:** confirmation on Vespera. The first two are near-certain and are flagged only so
nobody is surprised by the rename. Vespera is a different name from Cyrra, so the question worth
asking is whether the hero was renamed on one side and not the other, or whether Vespera is a
character of her own who has landed in Cyrra's slot.

---

## 2. Kit text to confirm

### 2a. One design change, not a transcription

**Kiva III, Unclaimed Victory.** This is the only entry in this file where a number was decided
here rather than transcribed, so it is the one that most needs saying yes or no to.

- **As written:** "When Kiva defeats a Forsaken target, consume all Broken Links. Each one gives her
  a 12% Max HP Shield for 2 rounds."
- **What it paid:** exactly zero, for a rate rather than for a bug. All three Shield clauses gated on
  her holding a Broken Link, and Abandoned Support banks at most one link a round while Severance
  Paid spends one on every basic against a Forsaken body - the same basic whose own Mark banked it.
  She opens every turn at zero, so a kill finds nothing to consume.
- **What it says now:** "When Kiva defeats a Forsaken target, she gains a Shield equal to 12% of her
  Max HP for 2 rounds, and 12% more for each Broken Link she is holding, up to 36%. All Broken Links
  are consumed."
- **What changed:** the first of the three Shield clauses is ungated. The two link-gated clauses are
  untouched, at the same 12% of Max HP and the same thresholds. It pays on the kill its own text
  names rather than on a resource the rest of her kit spends before the kill arrives, and it is
  still worth more to a Kiva who held links than to one who did not. It reads 1118 Shield in its own
  arena against 0.
- **And the "up to 36%" has never been paid, which the text now says out loud.** Measured over the
  four dark arenas that can raise a Forsaken kill, three seeds each, and over the hero sweep's five:
  **every payout is exactly 1118**, which is 12% of her 9320 Max HP at level 40, and there is never
  a second ShieldGain event beside it. The two link-gated clauses are authored, correct and
  reachable in principle; they are inert at the rates the rest of her kit runs at, which is the same
  rate this entry already describes. The talent's own description carries the measurement now rather
  than advertising a number nothing has produced. **Nothing about the kit changed for this** - it is
  the sentence catching up with the arithmetic.

The wording was changed in `import/Heroes.json` as well as on the shipped talent, so both say the
same thing. **If the answer is no, both come back together.**

### 2b. Clauses the engine could not say as written

`Docs/ENGINE-GAPS.md` is the full record: **26 capabilities, 34 clauses, 30 heroes**, grouped by
what is missing rather than by hero so a repeat is visible. Every row carries the sentence as
written, what the engine lacks, and what the shipped kit does instead. What follows is the same 34
regrouped **by hero**, because that is the order they are easiest to answer in, plus a short second
table of departures that are not engine gaps at all.

**This section was 38 rows and is now 29, and the difference is not a retreat.** It was written
before the engine gaps pass, which built eleven capabilities and then put eighteen kits back to the
sentence Pinoy wrote for them. Every row that pass retired has come out of this table, checked one
at a time against what the shipped kit actually does rather than against the old list. Branik's
second passive, Celen's shield extension, Demos's extension cap, Eira's area ward and her
single-target gate, Finn's round-end hook, both of Halwen's, Jory's and Kest's "from it", Leon's
per-buff scaling, Lio's healing bonus, both of Maera's timing rows, Maren's holder tick, Mika's
energy fold, Niko's debuff window, both of Rhys's hit-size rows, Siofra's reversal clock, Tamsin's
skill scope, her per-buff scaling and her named delay subject, and Toren's redirect are all
authored as written now and need no answer.

Nothing here was reworded quietly, and nothing here is a number. Every damage value below five
stars differs from the original because the star band moved it, which is balance rather than
expressibility. That is most visible at the bottom of the roster: the shipped 2-star and 1-star
basics run to about 350% of Attack where the sentence says 105% to 110%, because a hero with no
active skill spends every turn on one ability and carries about half its role's 5-star stat
total. The percentages are the band's, the clauses are yours.

**Three kinds of answer are useful:** *the approximation is fine, keep it*; *the approximation is
wrong, here is a sentence the engine can say*; or *this clause is worth an engine change*.

| hero | clause as written | what ships instead |
|---|---|---|
| **Adric** | "When an enemy with Lost Route **misses**, Adric gains 8 Energy" | the 8 Energy pays only when the missed blow was aimed at Adric himself; the engine raises its miss window on the body that dodged |
| **Aster** | "Fracture causes the **next critical hit** to deal 15% more damage" | Fracture raises damage taken by 15% from every blow, for its whole 2 rounds |
| **Avera** | "has a **50% chance** to delay each by 15% turn progress" | the delay is certain |
| **Brio** | "Attacks against Accounted **cannot miss**" | carried in the text only; the Mark does the rest of Accounted's work |
| **Bren** | "swaps the target's present HP percentage with the **highest-HP enemy**" | the blow is aimed at the highest-HP enemy instead, so it goes where the swap would have sent the health |
| **Cal** | "When **Offset splash** defeats an enemy, Cal gains a guaranteed Critical Hit" | any defeat Cal lands on an Offset enemy pays it; nothing records which effect of a cast landed a blow |
| **Celen** | "transfer 15 Energy from the highest-Energy ally to the lowest **if their difference is at least 30**" | the pair is ungated, and self-cancels when the fullest and emptiest bars are the same body |
| **Clara** | "When Clara removes a **control debuff**, the ally gains 12 Energy" | the 12 Energy pays for any debuff she strips |
| **Demos** | "At turn end, Arrears loses 4% current HP" | an `attackCap` was added that the sentence does not mention, holding the clause at the relationship it was written with |
| **Eira** | "moves the target **to the back** if another enemy is alive" | a position swap, which displaces it but not reliably to the back |
| **Finn** | "have a **35% chance** to be delayed by 15% turn progress" | the delay is certain |
| **Iolan** | "removes one target buff; **if none is removed**, applies 10% Effect Hit Down" | the rider lands whether or not the Dispel found anything |
| **Jory** | "If the **same enemy hits Jory twice** in a round, he counterattacks" | limited to once a round, so the first enemy to strike him is counterattacked |
| **Leon** | "assigns Case Number ..., **revealing current HP, Energy, and buffs to allies**" | carried in the text only; the Mark does the mechanical half |
| **Linnet** | "Crossed Line: the target's **first debuff each round** goes to a random enemy" | one debuff already on that body is rerouted at round start instead of one being caught as it lands |
| **Linnet** | "**When Crossed Line redirects a debuff**, Linnet gains 10 Energy" | the 10 Energy pays whether or not the reroute found anything to move |
| **Lio** | "**control debuffs** also grant 10 Energy" | the 10 Energy pays for any debuff Lio strips |
| **Lydia** | "Whenever Lydia **acts before the slowest enemy**, she gains 10% Damage Reduction" | the Damage Reduction goes up at the start of every one of her turns, ungated |
| **Maera** | "and **she cannot be controlled**" | **the clause is dropped.** The guaranteed critical half of the passive is authored and the immunity half is not |
| **Mika** | "moves each effect to **an unmarked enemy**" | moves to the highest-HP enemy, the answer Demos's No Appeal already gives to the same question |
| **Nadine** | "Registered targets **cannot become Concealed or Untargetable**" | Proper Place strips the concealment the moment it lands instead of preventing it |
| **Nadine** | "marks the target position for 2 rounds. **It cannot move**" | **the clause is dropped.** Hold the Line keeps its damage rider |
| **Niko** | "forces its next Basic Attack onto **a random ally**" | Provoked, so the blow lands on Niko |
| **Oswin** | "**Allies who lose a debuff to Oswin** gain 8% Defense for 2 rounds" | every ally gains it after the cast, whether or not a debuff left them |
| **Quin** | "Delivered targets take 8% more damage from the **next** active skill" | Delivered carries the skill-scoped 8% for its whole 2 rounds |
| **Quin** | "removes Delivered to give Quin **12 Energy per target**" | a flat 12 Energy for the cast, whatever it reached |
| **Quin** | "Quin's **first active skill each battle costs 15 less Energy**" | a battle-start passive that banks 15 Energy instead |
| **Rhys** | "**swaps HP percentage** with the lowest-HP ally if they are below 50%" | Rhys pays 20% of what the ally is missing out of his own bar, clamped to leave him at 1 HP. There is no swap in the engine |
| **Selka** | "Overdue **cannot be cleansed** during the round it was applied" | Overdue is written back onto its carrier at every round start, so it cannot be shaken off at all rather than for one round |
| **Sena** | "Sena gains **10% Critical Chance** against slowed enemies" | the advantage is paid as damage - a rider worth 85% of Attack against a slowed enemy. `StatusKind` has no Critical Chance entry in either direction |
| **Arno** | "**When an enemy Shield is removed**, Arno gains 8% Attack for 2 rounds" | it answers a strike landing on an enemy whose Shield is still standing, which is the moment before the break. A break is only audible to the side that lost it |
| **Kora** | "Kora deals **10% more damage** to enemies that have not acted this round" | an added blow worth 38% of Attack against an enemy that has not acted, rather than a modifier on the one she is already landing |
| **Nara** | "**The first time** an ally is healed below 35% HP, they gain 6% Damage Reduction" | every heal that leaves an ally under 35% pays it. A once-per-battle limit is spent when the trigger arrives, so it would be gone on the battle's first heal whatever that ally's health |
| **Yara** | "When Yara **attacks a different target than last turn**, she gains 10 Energy" | she is forbidden the body she just hit, so every shot is a new bearing, and the reward is paid on all of them. Nothing lets a hero hear its own change of target |

**The three dropped clauses are the ones to read first**: Maera's control immunity, Nadine's hold
and Brio's "cannot miss". All three are in the shipped kit's text as nothing at all rather than as
an approximation, so a player reading any of those heroes gets a shorter ability than the design
describes.

**Two earlier draft flags are withdrawn and need no answer.** Celen's "at round start" and Siofra's
second hook were both marked draft in their batches on the strength of a scan that read 46 of
`PassiveTrigger`'s 52 members and dropped the two that answered them. Both sentences are authored
unchanged now.

### 2c. Seven departures the engine could say and the star contract has no room for

These are not in `Docs/ENGINE-GAPS.md`, because the engine can say all of them. They are in the
shipped kit differently because of what the star contract gives each tier, not because of what the
engine can express, so the answer to any of these could be "change the contract for that hero"
rather than "change the engine".

**Four of the seven were one question and it is now CLOSED, so they need no answer.** A 1-star is a
basic and nothing else and a 2-star is a basic and one passive, so **neither tier has an active
skill** - and Energy is spent on nothing else. Four of the low-star designs pay the hero Energy:
Piri 15 on a kill, Yara 10 for a new bearing, Wynn 5 when a buffed ally is hit, and Orenna 5 on
every swing. Written as they stand, all four are clauses a player reads on the card and that can
never do anything. Each shipped kit keeps the amount and moves where it lands - to the ally with
the emptiest bar, or to the ally who was hit - except Yara's, which is paid in Attack because two
heroes handing Energy to an ally was already one more than the tier needed.

**The decision: the star contract does not move, and the redirect stands.** One star stays a basic
and nothing else, two stars stays a basic and one passive, and the Energy those four clauses pay
goes to an ally who can spend it. The reason is that the alternative was never about these four
heroes: giving the low tiers an active skill changes the contract `validate_heroes.py` enforces at
import time for all twenty of them, moves both tiers' star-band rungs - a hero with an active does
not spend every turn on one ability, which is half of why the low-star basics run to about 350% of
Attack against Pinoy's 105% to 110% - and re-opens the two ceilings the band read off the tiers
above. Four clauses on four heroes is not a reason to re-derive a rung. The four rows below are the
record of what the kits do, not a question.

| hero | clause as written | what ships instead | what the faithful shape would have cost |
|---|---|---|---|
| **Piri** | "When Piri defeats an enemy, **she gains 15 Energy**" | the ally with the emptiest bar gains the 15 | an active skill at two stars, for the whole tier |
| **Wynn** | "When an ally with Wynn's Defense buff is hit, **Wynn gains 5 Energy**" | the ally who was hit gains the 5 | the same |
| **Orenna** | "Deals 110% Attack damage and **gains 5 Energy**" | the ally with the emptiest bar gains the 5 | an active skill at one star, for the whole tier |
| **Yara** | "she gains **10 Energy**" | 10% Attack for 1 round instead | the same, plus group 33 for the gate it hangs on |

**The other three are the older question, one tier up, and they are still open.** A 3-star is
authored as a basic, an active
and one passive and nothing else, where a 4-star may carry an extra passive in the overflow list
for a described clause that answers a trigger of its own.

| hero | clause as written | what ships instead | what the faithful shape would cost |
|---|---|---|---|
| **Avera** | "Disrupted targets lose 8 Energy **after an active skill**" | the engine's own `Disrupted`, which takes the 8 off the first Energy gain each round instead | a second passive on `OnEnemyActive`, which is exactly what Finn's Ringing Slug carries |
| **Cal** | "The **next ally attack** on the target also deals 25% damage to an adjacent enemy" | Cal's own shot carries 25% into the bodies beside the target | a link plus a second passive on `OnLinkedUnitHit`, which is what Mika's Loose Parts carries |
| **Petra** | "When a Withheld enemy **targets** an ally, Petra gains 8% Damage Reduction" | it answers the blow landing, so a strike that misses or is blocked does not reach it | nothing in the engine; `OnTargeted` fires on the body being aimed at, not on a watcher |

### 2d. Four numbers on three 5-stars that the measurements moved

Sections 2b and 2c are about clauses the engine could not say or the star contract had no room
for. **These four are different: every one of them is a sentence the engine says perfectly well,
and every one of them was measured doing nothing, or doing something nobody could have meant.**
They are the first departures on 5-star heroes; every number moved below five stars so far has been
the star band's and is recorded in 2b's preamble rather than row by row.

| hero | clause as written | what ships instead | what the measurement said |
|---|---|---|---|
| **Altheon** | Immaculate Record marks the Subject "**for 2 rounds**" and records "up to **120% of Altheon's Attack**" | the Subject lives 3 rounds and the ceiling is 50% of his Attack; the record also takes only Altheon's own blows, which is the sentence's "from Altheon" honoured for the first time | the ceiling needed a single blow of four times his Attack, and his largest effect is 185% and resolves before the record it creates exists. The Subject is also faster than he is, so its clock ticks before his turn and a 2-round Subject contained ONE of his turns. The record closed in none of the five sweep seeds; it closes in four now |
| **Seraphine** | Volume Closed: "the target becomes Unable to Gain Buffs for 1 round. **When that restriction ends**, Footnote is removed and Seraphine gains 15 Energy" | all of it lands in the same firing, on the turn the record reaches its cap | the second beat needed a mark to survive one of her turns, and Contrary Evidence - her own Dispel - strips it, on the same trigger that fills the record. It ran zero times in every arena |
| **Seraphine** | nothing: Volume Closed says nothing about damage | closing the book also releases the record as damage to the target | a record that reached its cap was thrown away unread, which is what made Cross Reference work against this clause rather than with it |
| **Hale** | Field Sign deals "**120% of Attack**" and Closing Radius "**220% of Attack**" | 250% and 570% | he read x0.36 of his role's 5-star median, below every 4-star and both 3-star Marksmen. Both of his numbers are single blows at single targets where his three 5-star peers carry a second damage clause on each ability or hit the whole line; every 4-star and 3-star Marksman had a bigger single-target active than his 220%. He reads x0.79 now |

**The answer that is useful here is different from 2b's.** These are not "the approximation is fine"
questions. Each is *the shipped number is the one I want*, or *the original number is the one I
want and the kit should change around it*, and for Hale in particular the second answer means
giving him a second damage clause rather than restoring 120% and 220%.

One more thing sits underneath the Altheon row and is not a question for Pinoy, only a note:
Immaculate Record's own ceiling arithmetic assumed a blow lands for as much as it is priced at.
Defence takes a share of every blow before the record sees it, so a clause that records a fraction
of damage and ceilings against raw Attack is reading two different currencies. Nothing else in the
roster is shaped that way today.

---

## 3. Strike frames

`impactFraction` says when in a clip the blow lands, and the game hangs the damage number, the
flash and the hit reaction off it. It is **derived** by default - peak horizontal extent per frame -
which is right for a side swing and wrong for an overhead one, where the raised weapon is widest at
the top of the windup. A frame set by judgement instead reads `impactSetBy: "hand"` in that clip's
sidecar.

`python Tools/impact_review.py` writes one contact sheet per hero showing each attack clip across
its length with the chosen frame boxed, which is the artefact to confirm or correct without opening
the project.

**Read from the tree, not from memory.** Of the 31 heroes with drawn art:

**One hero has every attack clip hand-set:** Hale.

**Ten have some hand-set and some still derived:**

| hero | hand-set | still derived |
|---|---|---|
| Brenna | basic | basic2, skill |
| Caldus | basic2 | basic, skill |
| Darian | basic, skill | basic2 |
| Edrin | basic | basic2, skill |
| Kael | basic, skill | basic2 |
| Kiva | skill | basic, basic2 |
| Mireth | basic, basic2 | skill |
| Nereia | skill | basic, basic2 |
| Seraphine | basic | basic2, skill |
| Sylven | skill | basic, basic2 |

**Twenty have no hand-set attack clip at all:** Aeron, Altheon, Aminah, Arden, Celen, Cyrra, Dahlia,
Elyra, Isara, Isolde, Marrow, Merek, Nysera, Oris, Rheia, Rovan, Talren, Vaelis, Velis, Zella.

That is **69 of 85 attack clips still on a derived fraction.** They are not wrong by default; the
derived number happens to be close on a side swing. What is wanted is a pass over the contact sheets
naming the ones that read wrong, starting with the twenty above that have had no pass at all.

**One clip is missing rather than mistimed.** Rheia's delivery included an `idle2` where `skill`
should be, so her active plays with no animation and the idle continues. It is silent rather than an
error, and it needs a skill sheet.

---

## 4. The next hub painting: keep the right edge clear

The delivered `hub.png` puts the Proving's gatehouse hard against the right edge, roughly **x 0.82
to 0.97** of the painting. The Hub's HUD frame runs a rail of four icon buttons down that edge, in a
column about 140 logical units wide - about 7% of the width - from 106 to 580 down from the top.

**No marker and no label is covered**: `HubMarkerClamp` reserves the rail's column and slides the
Proving's plate left while its tail stays on the gate's door. But the rail's discs sit on painted
architecture at every supported aspect, which is what the Hub's layer 0 rule exists to prevent.

**For anything painted from here on: leave the outer 7% of both the left and right edges as
landscape rather than structure.** The left edge already satisfies this in `hub.png` - rock face,
nothing built until the barracks at about x 0.08 - which is why moving the rail there is one of the
three ways out of the current conflict. The other two are repainting the right 7%, or accepting it.
The full measurement is in `DESIGN-HUB-DELIVERY`, section 3a.

---

## 5. Standing art backlog

Not a question, listed so it is in one place with everything else.

- **63 of 94 heroes have no drawn BATTLE art**, and that is now the only thing missing from the
  roster: every one of the 94 has a shipped kit and all 94 profile badges are wired. The 31 heroes
  that have battle sheets are the thirty 5-stars plus Celen. The other 63 carry
  `visual.spriteStatus: "pending"` and render as an aspect-tinted placeholder mannequin with their
  name under its feet, so the game can say "not drawn yet" separately from "this hero's art is
  broken". By tier that is all 8 one-stars, all 12 two-stars, all 18 three-stars and 25 of the 26
  four-stars. The clip set `SpriteAnimator` plays is `idle`, `basic`, `basic2`, `skill`,
  `hit_reaction`, `death` and `controlled`; a clip a hero never shipped falls back to idle rather
  than breaking, and the 31 drawn heroes do not all carry all seven today.
- **Chapters 3 to 6 are blocked on backdrops.** `DESIGN-WORLD`'s contested-region proposal for
  chapter 3 is moot until they exist.
- **Optional: rarity-coloured frame variants for the badges** - gold, purple, blue, green and grey,
  matching the five star tiers. The badges as delivered all carry the SAME gold frame, so the game
  cannot let the frame carry rarity and instead reads it off a coloured plate drawn behind the
  badge plus the star row beside it. That works, but rarity in the frame itself is the stronger
  read and it is what this genre's players expect. Five recolours of the existing frame would drop
  straight into the override slots with no code change - see `DESIGN-EMBERFORGE` for how a PNG at
  `Resources/Sprites/Hud` wins over the generated kit. Not a blocker, and not worth redrawing any
  badge art for; only worth doing if the frame is a separate layer in the source files already.

- **Six orphaned sprite PNGs** sit under `Assets/Resources/Sprites/Units` for heroes deleted in the
  roster swap - `kalyx_emberwright`, `maeve_corrin`, `nyx_serrow`, `orrin_kaal`, `ryn_solvarr` and
  `wren_ilyra` - referenced by nothing. Deleting them needs no answer from Pinoy; it is noted here so
  nobody re-delivers against them.
