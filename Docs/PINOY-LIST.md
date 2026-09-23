# Emberfall - waiting on Pinoy

**Split on 2026-09-23, and this file is now the working record behind two lists.** Most of what is
below is not Pinoy's to answer, and his queue should hold only what needs someone who draws.

- **`Docs/PINOY-ART.md`** is what goes to Pinoy: the art queue, ordered by what it unblocks, written
  for someone without the repository open. It covers sections 1, 3, 4 and 5 here, plus the summon
  screen's placeholders and the enemy roster.
- **`Docs/DECISIONS-OPEN.md`** is what goes to Matthias: every decision in section 2, each with a
  proposed answer, the full ledger of sentence-against-kit departures with a verdict per row, and
  replacement card text for every 2f note.

Both stand alone. This file keeps the measurements and the history, and is where a row's evidence
lives when one of those two is questioned.

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

Checked against the tree on 2026-09-23, after the 2-star and 1-star tiers were authored, the
roster reached 94 of 94, the five-star review moved four numbers on three of them, and pass 3 of the
engine gaps work rewrote the eleven sentences in 2g and moved three numbers on Aminah. Section 2b
was rebuilt against the shipped kits when the 3-star tier landed rather than carried forward - the
engine gaps pass had retired nine of its rows and this file had not been told - and the low-star
batch appends to it rather than rebuilding it again.

**Sections 2b and 2c are closed lists**: every hero has a shipped kit, so no further batch can add
a row to either, and 2c's four-clause Energy question is now answered here rather than waiting.
**Sections 2d and 2e are not closed**, because they hold what a MEASUREMENT produced rather than
an authoring batch, and the instruments keep running. 2e holds two talents that cost the hero who
picks them. **2f** is the ability text that still carries a drafting note, one yes or no question
per note, and it shrinks as answers arrive. **2g is the newest**: eleven sentences the engine gaps
pass 3 rewrote when it made the kits do what they say, each one to confirm.

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

`Docs/ENGINE-GAPS.md` is the full record: **25 capabilities, 36 clauses, 32 heroes**, grouped by
what is missing rather than by hero so a repeat is visible. Every row carries the sentence as
written, what the engine lacks, and what the shipped kit does instead. What follows is 33 of those
36 regrouped **by hero**, because that is the order they are easiest to answer in, plus a short
second table of departures that are not engine gaps at all. The other three are the ones the
fidelity pass found - Hale's Fresh Trail, Kael's Rubble per effect removed and Siofra's Prediction
Cost - and each is waiting on an engine primitive rather than on an answer, so they live only there.

**This section was 38 rows and is now 33, and the difference is not a retreat.** Nara's row is the
latest to leave: the content pass after the fidelity pass made her kit say "the first time". It was written
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

### 2d. Numbers on five 5-stars that the measurements moved

Sections 2b and 2c are about clauses the engine could not say or the star contract had no room
for. **The Altheon, Seraphine and Hale rows are different: every one of them is a sentence the
engine says perfectly well, and every one of them was measured doing nothing, or doing something
nobody could have meant.** The Aminah row is the fourth hero and a different kind again: her kit
does what it says, and it was simply too small for a 5-star, which the harness now checks.
They are the first departures on 5-star heroes; every number moved below five stars so far has been
the star band's and is recorded in 2b's preamble rather than row by row.

| hero | clause as written | what ships instead | what the measurement said |
|---|---|---|---|
| **Altheon** | Immaculate Record marks the Subject "**for 2 rounds**" and records "up to **120% of Altheon's Attack**" | the Subject lives 3 rounds and the ceiling is 50% of his Attack; the record also takes only Altheon's own blows, which is the sentence's "from Altheon" honoured for the first time | the ceiling needed a single blow of four times his Attack, and his largest effect is 185% and resolves before the record it creates exists. The Subject is also faster than he is, so its clock ticks before his turn and a 2-round Subject contained ONE of his turns. The record closed in none of the five sweep seeds; it closes in four now |
| **Seraphine** | Volume Closed: "the target becomes Unable to Gain Buffs for 1 round. **When that restriction ends**, Footnote is removed and Seraphine gains 15 Energy" | all of it lands in the same firing, on the turn the record reaches its cap | the second beat needed a mark to survive one of her turns, and Contrary Evidence - her own Dispel - was stripping it, on the same trigger that fills the record. It ran zero times in every arena. The Dispel has since been repaired to skip the bookkeeping statuses, so the mark now survives; the departure stands on its own merits rather than on that defect, and the question of whether the two beats should separate again is open |
| **Seraphine** | nothing: Volume Closed says nothing about damage | closing the book also releases the record as damage to the target | a record that reached its cap was thrown away unread, which is what made Cross Reference work against this clause rather than with it |
| **Altheon** | Witness Script deals 120% "and damage increases by **35%** if Altheon has already damaged that enemy this round" | the rider is 35% again, and it resolves BEFORE the 120% blow instead of after it | it shipped as 42%, and the order was what mattered: the condition reads the round's damage list, the 120% blow above it is an entry in that list, so the rider asked a question its own firing had already answered and paid on every basic he ever threw. That flat second blow made his basic x1.55 of the role's per-basic median while his active sat exactly on it, and it is most of why he read x1.51 of the Breaker 5-star median. He reads x1.23 now. Ordered this way the rider fires when the Subject's delayed damage or Fixed in Place reached that body earlier in the round, which is the sentence as written - but a hero with one action a turn reaches it rarely, and it fires zero times in the sweep arena |
| **Aminah** | Marked Diagnosis deals "**105% of Attack**" and heals for "**55% of Attack**"; Restore the Balance deals "**155% of Attack**" | 190%, 129% and 258%, and her smaller heal and her release 25% and 56% | she read x0.50 of the Mender 5-star median, under two of the three Mender 4-stars, on the same stat line as Marrow. Her kit simply does less in the one arena the project measures in: fewer casts over a shorter life, and a Priority that holds healing back. A 5-star below the 4-stars of its own role is what the 5-star check exists to catch, and it took her to 170%, 115% and 230%, x0.86. **The second move is a design rule rather than a measurement**: Lumen and Umbra 5-stars must sit at or above their role's median, because those aspects are the slow ones to fuse and rarity with no payoff is a pool players learn to avoid. Every Damage and Heal number in her basic, active and passives went up by the same x1.12, the smallest step that puts her above Isara, and she reads x1.00 - she is the Mender median now |
| **Caldus** | Answer the Blade "**115%**", Declare the Threat "**180%**", Defined Duty's counter "**75%**", No Uncertain Ground "**45%**" | 121%, 189%, 79% and 47% | the same rarer-aspect rule. He read x0.96 of the Vanguard median; every Damage number in his kit went up by the same x1.05, the smallest step that puts him above Aeron, and he reads x1.00 |
| **Hale** | Field Sign deals "**120% of Attack**" and Closing Radius "**220% of Attack**" | 250% and 570% | he read x0.36 of his role's 5-star median, below every 4-star and both 3-star Marksmen. Both of his numbers are single blows at single targets where his three 5-star peers carry a second damage clause on each ability or hit the whole line; every 4-star and 3-star Marksman had a bigger single-target active than his 220%. He reads x0.79 now |

**Velis is the one rarer-aspect 5-star the rule has not moved, and the reason is Kiva, not Velis.**
He reads x0.91 of the Striker median. The Striker role has four 5-stars, so its median is the mean
of the middle two, and Velis can only reach it by passing Sylven - which raises the median itself by
about 9%. Measured: his smallest lift that clears the rule (x1.30 on every Damage number, reading
x1.01) drops Kiva from x0.79 to x0.72, under the floor every 5-star keeps. Kiva is a starting hero,
so the fix is a Kiva change too, and that is a kit decision rather than a number. The question for
Pinoy is which of the two to move, or whether the Striker role should be an exception.

**The answer that is useful here is different from 2b's.** These are not "the approximation is fine"
questions. Each is *the shipped number is the one I want*, or *the original number is the one I
want and the kit should change around it*, and for Hale in particular the second answer means
giving him a second damage clause rather than restoring 120% and 220%.

**A sixth reading is not in the table because nothing was changed for it, and an earlier version
of this file had its sign wrong.** Altheon's Final Record pays 20% of Attack per Record and then
consumes every Record, exactly as written, while Preserved Moment is spending those same Records as
a standing +5% on every blow he lands on a marked body. This file used to say the talent loses on
that trade, at 103,377 against 107,349. That was one seed of the arena built to raise it, and one
seed is one crit draw: the talent fires in every seed, and over 150 seeds of the same arena it is
worth +1,025 a battle, about 1%, more than four standard errors clear of zero. Preserved
Moment rebuilds its three Records in three basics, so the consumption costs about what the three
blows pay. **So it is a gain, and a thin one.** The question left is only whether the two halves of
Record were meant to compete at all; nothing needs answering for the talent to be worth picking.

One more thing sits underneath the Altheon rows and is not a question for Pinoy, only a note:
Immaculate Record's own ceiling arithmetic assumed a blow lands for as much as it is priced at.
Defence takes a share of every blow before the record sees it, so a clause that records a fraction
of damage and ceilings against raw Attack is reading two different currencies. Nothing else in the
roster is shaped that way today.

### 2e. Two talents that cost their own hero, and cannot be mended from content

Every authored talent was measured against the same hero with nothing picked, over 40 seeds, in
the sweep arena and in the arena built to raise it. Six cost their hero output. Four buy something
the output total cannot see, or pay a price their own sentence names, and are fine as written:
Nysera's Deeper Absence strips a buff (a taunting tank's Provoke, in the sweep), Rheia's Endless
Revision trades casts for bigger ones, Rheia's Perfect Reversal pays Energy for every strike it
keeps Revision for, and Marrow's Shared Oath spreads a smaller Promise over a third ally. Kiva's
Unclaimed Victory and Mireth's No Record Remains were on that list and have left it: once
Kiva's and Mireth's buff listeners heard real buffs only, the first reads inside the noise and the
second gains in its own room. **These two buy nothing, and each is a conflict between the talent and the kit it hangs on**,
so the smallest repair is a change to the kit rather than to a number.

| hero | talent as written | what it costs | why nothing comes back |
|---|---|---|---|
| **Kael** | Lasting Materials: "Rubble no longer expires after 3 rounds. Kael can hold up to 5 Rubble, but Take Down the Wall deals 10% less base damage." | 1,726 of his 23,781 output a battle, t -36.6: the 10% cut, exactly | What Breaks Forward consumes all Rubble on his very next Basic Attack, so Rubble never lives long enough to expire and never stacks past what one cast gave. The retention and the cap of 5 have nothing to act on. It loses in every arena it was tried in, including one where his Dispel has four long buffs to take. Unusable Cover now spends Rubble only when it moves a Ward, as its own sentence says, which keeps a Rubble in hand a little longer and changes nothing here: it is still gone at the next basic. The question: should What Breaks Forward stop consuming all of it, or should the talent buy something What Breaks Forward cannot spend first? |
| **Mireth** | Deeper Erasure: "Remove From Record can trigger twice per Focused enemy during its duration, but the second trigger deals 30% less bonus damage." | against one enemy that buffs every turn, 31,145 of her 96,401 a battle, t -92; the sweep no longer raises The Missing Page, so it is read in that room now | The Missing Page now lands in every round a Focused enemy gains a buff: its once-a-round limit is spent by a landing rather than by the first buff anywhere, which is what "the first time each round" says. Over Focus's two rounds that is already two landings at full strength. The talent lifts the round limit and caps the clause at two landings per enemy, and the engine's count of those cannot start again when Focus is laid again, so over a fight it is fewer landings, the second one softer. The question: now that the base clause lands every round, should Deeper Erasure take the 30% cut off, or buy a third landing? |

**Velis's Final Denial was the second row, and it no longer costs measurably.** Once No Second
Breath paid its Energy only for enemies below half HP, as its sentence says, the reading fell to
-341 a battle, t -1.4, inside the noise. The question it carried still stands as a design question:
was Final Denial meant to deny the enemy's healer rather than the healing Velis herself feeds on?
It is no longer a trap by the numbers, and `CheckTalentsAreWorthPicking` has taken it off the list.

### 2f. Ability text waiting on an answer

Every basic, active, passive and talent now carries a player-facing sentence with no numbers in
it, written to `Docs/ABILITY-TEXT-STYLE.md`, **except these 29 ids, asked as 28 questions** (Demos's
two share one). Each still carries a drafting
note because the sentence Pinoy wrote and the kit that ships disagree. Most repeat a row of 2a,
2b, 2c or 2d, asked here as the one question the text needs answered.

**Yes** means the text is rewritten to say what the kit does and the note comes off. **No** means
the kit changes, which is balance or engine work rather than a text edit. **The recommendation is
yes throughout**, because the built kit is what players fight. The harness pins each of these ids
in `AbilityTextPinnedDraftingNotes` and fails if one is answered and not unpinned. Nara's Carry On
was the third and has left the list the other way: the kit now says "the first time", as her
sentence does, and the text says what it does. **Hale's Tracking Habit has left the same way**: the
kit now moves Quarry at the end of the round, as his sentence does, rather than at the start of his
turn, and its card says so.

1. **Arno, Rubble Resolve**: Attack Up when Arno strikes an enemy whose Ward is still standing, not when a Ward breaks? Recommend yes.
2. **Yara, Second Bearing**: Yara can never aim at the same enemy twice running and gains Attack Up on every shot, rather than Energy for choosing to switch? Recommend yes.
3. **Kora, First Opening**: an extra blow against an enemy that has not acted yet, rather than a damage bonus? Recommend yes.
4. **Sena, Unfixed Aim**: an extra blow against an enemy in Mire, rather than Critical Chance? Recommend yes.
5. **Adric, No Return Mark**: Energy only when a Lost Route enemy misses Adric himself, not any ally? Recommend yes.
6. **Aster, Scoring Point**: Fracture raises every blow's damage for its whole duration, not only the next critical hit? Recommend yes.
7. **Quin, Stamped Parcel**: Delivered raises active-skill damage for its whole duration, not only the next skill? Recommend yes.
8. **Quin, Return to Sender**: one Energy payment per cast, not one per target? Recommend yes.
9. **Quin, Urgent Matter**: Energy banked at battle start, rather than a cheaper first active skill? Recommend yes.
10. **Selka, No Extension**: Overdue rewritten at every round start so it cannot be shaken off at all, rather than uncleansable for one round? Recommend yes.
11. **Bren, Take Another Route**: the blow lands on the highest-HP enemy and nothing swaps HP? Recommend yes.
12. **Cal, Ricochet Mark**: Cal's own shot carries the ricochet, not the next ally attack? Recommend yes.
13. **Cal, New Sightline**: any kill Cal lands on an Offset enemy pays, not only a kill by the splash? Recommend yes.
14. **Linnet, Open Circuit**: at round start one debuff already on the Crossed Line enemy moves, and the Energy pays whether or not anything moved? Recommend yes.
15. **Oswin, Useful Scar**: every ally gains Guard Up after the cast, whether or not a debuff left them? Recommend yes.
16. **Brio, Recorded Shot**: drop "attacks against Accounted cannot miss" from the text, since nothing in the kit does it? Recommend yes.
17. **Avera, Offbeat Note**: Disrupted takes Energy off the first gain each round, not after an active skill? Recommend yes.
18. **Avera, Break the Measure**: the delay is certain, not a chance? Recommend yes.
19. **Lydia, No Fixed Departure**: Bulwark at the start of every one of her turns, with no gate on the slowest enemy? Recommend yes.
20. **Petra, Private Address**: it answers a Withheld enemy's blow landing on an ally, not the enemy targeting one? Recommend yes.
21. **Clara, Known Condition**: Energy for any debuff Clara strips, not only a control debuff? Recommend yes.
22. **Demos, Notice of Loss and its Arrears tick**: keep the cap on the Arrears tick that the sentence does not mention? Recommend yes.
23. **Hale, Field Sign**: 250% and a Quarry that makes Hale hit harder, rather than 120% and Defense ignore (2d)? Recommend yes.
24. **Hale, Closing Radius**: 570% rather than 220% (2d)? Recommend yes.
25. **Kiva, Unclaimed Victory**: the ungated first Ward of 2a, with the text saying what it pays rather than what was measured? Recommend yes.
26. **Seraphine, Volume Closed**: everything lands in one firing, and the record is paid out as damage (2d)? Recommend yes. The draft also says the target loses turn progress, which only the No Revision talent adds, so the player text will leave that out.
27. **Altheon, Witness Script**: the rider at 35%, resolving before the blow it rides (2d)? Recommend yes.
28. **Altheon, Immaculate Record**: a 3-round Subject, a 50% ceiling, and only Altheon's own blows recorded (2d)? Recommend yes.

**Nysera's False Target has its text now, because its kit is repaired.** It used to aim its extra
damage at the body the redirected share came off, which for a blow aimed at Nysera is Nysera, so the
talent struck its own hero every time it fired. The damage now lands on the enemy her Link turns the
blow onto, which is what Pinoy's sentence says, and the card reads: *When her Link turns an attack
away from Nysera, the enemy it lands on takes extra damage and the attacker loses Energy.* Nothing is
pinned as unwritten any more.

### 2g. Eleven sentences pass 3 of the engine gaps work rewrote

The engine gaps pass 3 made nine clauses do what Pinoy's sentences say, where before they did
something close to it. Every sentence below is the player text that ships now, in the house style of
`Docs/ABILITY-TEXT-STYLE.md`, and each changed because the kit under it changed. **Nine are the
sentence Pinoy wrote, said at last, and the question is only whether the wording reads right.**
Two are not: Cyrra's store also pays when it runs out, and Isolde's rider counts debuffs. Those two
are real questions and say why.

| hero | ability | the text now | against Pinoy's sentence | the question |
|---|---|---|---|---|
| **Marrow** | The Cost Is Named | When an ally holding Promise starts their next turn, Promise is spent: it heals them for a share of their Max HP and removes a debuff. | Now what Vow Kept says: the heal lands as that ally's next turn opens. It used to land as their previous turn ended. | Reads right? Recommend yes. |
| **Nereia** | Deeper Reservoir | Relief Circuit's Reserve can hold more, and when Reserve pays out healing, Nereia gains Energy. | Now what the talent says: the Energy comes when Reserve is spent on healing, and a Reserve that ends empty pays nothing. | Reads right? Recommend yes. |
| **Nereia** | Shared Reserve | When Reserve pays out, the lowest-HP ally also gains a Ward worth a share of what it held. | Now what the talent says: a Ward worth half of what Reserve released. It used to be a share of Nereia's own Max HP whatever Reserve held. | Reads right? Recommend yes. |
| **Cyrra** | Hidden Reserve | Bury the Burden also stores a share of the healing it gives beyond each warded ally's Max HP, and any overflow after it. When that Ward breaks, or the store runs out, the ally is healed with what was stored. | The store now starts with Bury the Burden's Ward and pays when that Ward breaks, as written. It also keeps gathering overflow while it lasts, and pays out when it runs out if the Ward never broke, because it shares a store with the one her full Veiled Weight already opens. | Should an unbroken Ward's store be lost instead of paid? Recommend no: the store is shared, and losing it would take the full-Weight payout with it. |
| **Aeron** | Unpaid Action | The first enemy each round to repay a heavy Turn Debt is Silenced once its turn ends. | Now what the talent says: only a large repayment counts, and the Silence covers the enemy's next turn rather than the one it was already taking. | Reads right? Recommend yes. |
| **Velis** | Hollow the Wound | Strikes the enemy with the lowest HP. If it is badly hurt, it becomes Hollowed: the next heal it receives is partly turned into damage against it instead. Hollowed does this once. | Now what the active says: Hollowed converts one heal and is spent. It used to convert every heal the enemy took. | Reads right? Recommend yes. |
| **Velis** | No Second Breath | Whenever an enemy below half HP is healed, Velis gains Energy, more if it is Hollowed. | The Remnant has moved to What Was Taken, where the sentence puts it. | Reads right? Recommend yes. |
| **Velis** | What Was Taken | Whenever Hollowed turns healing into damage, Velis stores a Remnant, up to a limit. | A new line of text for the half of What Was Taken that stores Remnants, which used to ride on any heal of a Hollowed enemy. | Reads right? Recommend yes. |
| **Velis** | Starve the Light | When Hollowed turns healing into damage, that enemy loses Energy. If that empties its bar, Velis gains Energy. | Now what the talent says, including the bonus for emptying the bar, which the kit did not have. | Reads right? Recommend yes. |
| **Isolde** | Unfinished Sentence | The first time each round Isolde's damage over time hurts a Revealed enemy, she strikes it. If that enemy is carrying several debuffs, she strikes harder and gains Energy. | The sentence asks for several damage-over-time effects. Cinder is the only one the game has, and every Cinder on an enemy merges into one whoever lays it, so no team could ever reach the harder strike. It counts debuffs of any kind now, and keeps the idea: a team that piles effects onto one enemy is rewarded. | Is counting debuffs right, or should Isolde's own Cinder stack instead? Recommend debuffs: stacking would break Exposed Vein, which refreshes her Cinder rather than adding another. |
| **Mireth** | The Missing Page | The first time each round an enemy Mireth has marked gains a buff, she strips it, strikes the enemy and stores a Page, up to a limit. With the Pages full, she spends them all to strike it harder and apply Attack Down. | Now what Remove From Record says: the buff that arrived is removed at once. That also gives Absence Has Weight something to count, which it had not had. | Reads right? Recommend yes. |

**Two changes on this pass changed no sentence**, and are recorded here so nobody looks for them.
Rheia's Rewrite the Outcome now grants one Revision for each enemy it reached, as her sentence
counts, rather than a flat amount that overpaid once fewer enemies stood. Arden's Deeper Measure
now raises the limit on Find the Measure's Max HP damage, as its sentence always said; the game had
been dropping that part.

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
derived number happens to be close on a side swing. **They matter more than they did**: the battle
used to hold every hit until the attacker's whole clip had finished, so a wrong strike frame was
hidden behind a late number. The hit now lands on the frame this value names, and 24 `basic2`
clips play in battle for the first time, so a misjudged frame shows as a number appearing mid-windup. What is wanted is a pass over the contact sheets
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
  four-stars. Of the 31 drawn heroes, **22 carry all seven clips** and 9 are short of one or two;
  section 5a is the per-hero list.
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

### 5a. Battle clips per drawn hero: what plays, and what is still missing

**Every delivered clip now plays in battle.** Until this pass `basic2` and `controlled` were
imported beside every other sheet and nothing in the battle asked for them: 24 `basic2` and 25
`controlled` sheets sat unused. The harness check `CheckEveryDeliveredSheetIsPlayed` now fails if a
sheet is delivered under a clip name no battle beat requests, if a sheet is missing its `.json`
sidecar, or if a clip the battle claims to play is asked for by no code, so a delivery cannot sit
unused again without the build saying so.

**When each clip plays**, all of it read from the fight record, none of it changing the fight:

| clip | plays when |
|---|---|
| `idle` | resting, whenever nothing below applies |
| `basic` / `basic2` | a basic attack. A hero's basics alternate: first, third, fifth play `basic`, second and fourth play `basic2` |
| `skill` | the hero casts their active |
| `hit_reaction` | the hero takes damage, unless they are still mid-way through their own basic or skill, which a hit never cuts short |
| `death` | the hero dies, held on its last frame |
| `controlled` | the resting loop, in place of `idle`, while the hero is Stunned. Silence does not count, because a silenced hero still acts |

**A hero missing a clip plays what it played before**: no `basic2` means every basic plays
`basic`, no `controlled` means a stunned hero rests on `idle`, and no `skill` keeps the resting loop
through the cast. The 63 heroes on the placeholder mannequin have no clips and see none of this.

**The strike frame is the beat.** An attack clip holds the fight until its strike frame, the hit
lands there, and the rest of the clip plays out while the damage numbers, hit reactions and status
tags of that strike appear. The next action waits for the follow-through to finish. Every delivered
attack clip is 2.0 to 2.4 seconds, so the beat stretches to the clip rather than the clip being cut
to the beat.

**`controlled` plays now, rarely.** Stun is the only status that takes a turn away, and the only
Stun in content is Corin's Weight of Time, which stuns a target whose Settling is at its limit of
three. Until defect pass 3 the limit could not be reached at any fight length: each stack of Settling
kept its own two-round clock and he added one per basic, so the oldest expired as the newest landed,
and 1,440 simulated battles across six lineups built to favour him produced no Stun and no stack
above two. Settling now lasts two rounds from its latest stack, and Weight of Time settles it one
step deeper before judging the limit, which is Pinoy's "8% each round, up to 24%" counted for the
round Corin casts in as well. Over the same 1,440 battles a Stun lands in 41, about 3 in 100; in
1,440 hero-against-hero duels, Corin on either side, it landed on a hero 45 times. **A hero's
`controlled` sheet plays only when that hero is stunned, which needs Corin fighting on the other
side**: the Proving or a friend duel. Everywhere else he stuns enemies.

| hero | delivered | newly playing | still missing |
|---|---|---|---|
| Aeron | 5 of 7 | none | `basic2`, `controlled` |
| Altheon | 7 of 7 | `basic2`, `controlled` | none |
| Aminah | 7 of 7 | `basic2`, `controlled` | none |
| Arden | 7 of 7 | `basic2`, `controlled` | none |
| Brenna | 7 of 7 | `basic2`, `controlled` | none |
| Caldus | 7 of 7 | `basic2`, `controlled` | none |
| Celen | 7 of 7, but Nereia's body (section 1a) | `basic2`, `controlled` | a set drawn for Celen |
| Cyrra | 7 of 7 | `basic2`, `controlled` | none |
| Dahlia | 5 of 7 | none | `basic2`, `controlled` |
| Darian | 7 of 7 | `basic2`, `controlled` | none |
| Edrin | 7 of 7 | `basic2`, `controlled` | none |
| Elyra | 7 of 7 | `basic2`, `controlled` | none |
| Hale | 7 of 7 | `basic2`, `controlled` | none |
| Isara | 7 of 7 | `basic2`, `controlled` | none |
| Isolde | 7 of 7 | `basic2`, `controlled` | none |
| Kael | 7 of 7 | `basic2`, `controlled` | none |
| Kiva | 7 of 7 | `basic2`, `controlled` | none |
| Marrow | 7 of 7 | `basic2`, `controlled` | none |
| Merek | 5 of 7 | none | `basic2`, `controlled` |
| Mireth | 6 of 7 | `basic2` | `controlled` |
| Nereia | 7 of 7 | `basic2`, `controlled` | none |
| Nysera | 7 of 7 | `basic2`, `controlled` | none |
| Oris | 5 of 7 | none | `basic2`, `controlled` |
| Rheia | 6 of 7, plus an `idle2` that nothing plays | `basic2`, `controlled` | `skill` (section 3) |
| Rovan | 7 of 7 | `basic2`, `controlled` | none |
| Seraphine | 7 of 7 | `basic2`, `controlled` | none |
| Sylven | 7 of 7 | `basic2`, `controlled` | none |
| Talren | 6 of 7 | `controlled` | `basic2` |
| Vaelis | 6 of 7 | `controlled` | `basic2` |
| Velis | 7 of 7 | `basic2`, `controlled` | none |
| Zella | 5 of 7 | none | `basic2`, `controlled` |

**What is still wanted, in one line each:** `basic2` for Aeron, Dahlia, Merek, Oris, Talren, Vaelis
and Zella; `controlled` for Aeron, Dahlia, Merek, Mireth, Oris and Zella; `skill` for Rheia; a whole
set for Celen; and all seven clips for each of the 63 heroes on the mannequin. Rheia's `idle2` is
pinned in the harness as delivered and deliberately unplayed, so it is not mistaken for an unused
clip; it can be deleted once her `skill` arrives.
