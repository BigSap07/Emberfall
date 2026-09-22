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

Checked against the tree on 2026-09-22, after the 3-star tier was authored. Section 2b was
rebuilt against the shipped kits in that pass rather than carried forward: the engine gaps
pass had retired nine of its rows and this file had not been told.

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

The wording was changed in `import/Heroes.json` as well as on the shipped talent, so both say the
same thing. **If the answer is no, both come back together.**

### 2b. Clauses the engine could not say as written

`Docs/ENGINE-GAPS.md` is the full record: **22 capabilities, 29 clauses, 25 heroes**, grouped by
what is missing rather than by hero so a repeat is visible. Every row carries the sentence as
written, what the engine lacks, and what the shipped kit does instead. What follows is the same 29
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

Nothing here was reworded quietly, and nothing here is a number. Every 4-star and 3-star damage
value differs from the original because the star band moved it, which is balance rather than
expressibility.

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

**The three dropped clauses are the ones to read first**: Maera's control immunity, Nadine's hold
and Brio's "cannot miss". All three are in the shipped kit's text as nothing at all rather than as
an approximation, so a player reading any of those heroes gets a shorter ability than the design
describes.

**Two earlier draft flags are withdrawn and need no answer.** Celen's "at round start" and Siofra's
second hook were both marked draft in their batches on the strength of a scan that read 46 of
`PassiveTrigger`'s 52 members and dropped the two that answered them. Both sentences are authored
unchanged now.

### 2c. Three departures the engine could say and a 3-star has no room for

These are not in `Docs/ENGINE-GAPS.md`, because the engine can say all three. They are in the
shipped kit differently because **a 3-star is authored as a basic, an active and one passive and
nothing else**, where a 4-star may carry an extra passive in the overflow list for a described
clause that answers a trigger of its own. That rule is the batch's, not the engine's, so the answer
to any of these could be "give that hero the second passive" rather than "change the engine".

| hero | clause as written | what ships instead | what the faithful shape would cost |
|---|---|---|---|
| **Avera** | "Disrupted targets lose 8 Energy **after an active skill**" | the engine's own `Disrupted`, which takes the 8 off the first Energy gain each round instead | a second passive on `OnEnemyActive`, which is exactly what Finn's Ringing Slug carries |
| **Cal** | "The **next ally attack** on the target also deals 25% damage to an adjacent enemy" | Cal's own shot carries 25% into the bodies beside the target | a link plus a second passive on `OnLinkedUnitHit`, which is what Mika's Loose Parts carries |
| **Petra** | "When a Withheld enemy **targets** an ally, Petra gains 8% Damage Reduction" | it answers the blow landing, so a strike that misses or is blocked does not reach it | nothing in the engine; `OnTargeted` fires on the body being aimed at, not on a watcher |
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

- **63 of 94 heroes have no drawn art.** The 31 that do are the thirty 5-stars plus Celen. The other
  63 carry `visual.spriteStatus: "pending"` and render as an aspect-tinted placeholder mannequin with
  their name under its feet, so the game can say "not drawn yet" separately from "this hero's art is
  broken". By tier that is all 8 one-stars, all 12 two-stars, all 18 three-stars and 25 of the 26
  four-stars.
- **Chapters 3 to 6 are blocked on backdrops.** `DESIGN-WORLD`'s contested-region proposal for
  chapter 3 is moot until they exist.
- **Six orphaned sprite PNGs** sit under `Assets/Resources/Sprites/Units` for heroes deleted in the
  roster swap - `kalyx_emberwright`, `maeve_corrin`, `nyx_serrow`, `orrin_kaal`, `ryn_solvarr` and
  `wren_ilyra` - referenced by nothing. Deleting them needs no answer from Pinoy; it is noted here so
  nobody re-delivers against them.
