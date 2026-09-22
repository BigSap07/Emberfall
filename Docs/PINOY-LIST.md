# Emberfall - waiting on Pinoy

**What this file is.** Everything the project cannot settle by itself because the answer is Pinoy's.
It is one file so it can be sent as one message, and it is maintained here rather than in
`DESIGN-BUILD-ORDER.md`, which now points at it.

**Four kinds of thing are in here**, and they want different kinds of answer:

1. **A delivery to identify** - two heroes currently ship the same body.
2. **Kit text to confirm** - clauses where the shipped kit does not do exactly what the sentence in
   `import/Heroes.json` says, either because the engine cannot say it or because the number had to
   change. None of these was quietly reworded.
3. **Strike frames to set by hand** - where a clip's impact frame is a derived guess rather than a
   judgement.
4. **A note for the next painting** - one constraint on future hub art.

Checked against the tree on 2026-09-22.

---

## 1. Whose art is in `Celen.zip`

**Celen and Nereia ship the same body.** Of the seven clips they each carry, **four are
byte-identical** - `basic`, `controlled`, `hit_reaction` and `idle` - and the other three differ by
so little that they are plainly the same drawing:

| clip | differing pixels | of | share |
|---|---|---|---|
| `basic2` | 29 | 4,118,400 | 0.0007% |
| `death` | 117 | 4,118,400 | 0.0028% |
| `skill` | 416 | 4,118,400 | 0.0101% |

All seven sheets are 1980x2080 in both deliveries. One of the two is the other one's art under a
different name, and **which way round it goes is Pinoy's answer, not a guess to make here.**

**Why it matters where it shows.** Nereia is in the prologue squad and is one of the five bodies the
harness's pinned yardstick fields, so the prologue's first battle is the most visible place a wrong
body would appear. Celen is the only 4-star in the roster with drawn art at all.

**What is needed:** which hero the delivery was drawn for, and a sheet set for the other one.

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

`Docs/ENGINE-GAPS.md` is the full record: **22 capabilities, 39 clauses, 20 heroes**, grouped by
what is missing rather than by hero so a repeat is visible. Every row carries the sentence as
written, what the engine lacks, and what the shipped kit does instead. What follows is the same 39
regrouped **by hero**, because that is the order they are easiest to answer in.

Nothing here was reworded quietly, and nothing here is a number. The 4-star values all differ from
the originals because the star band moved them, which is balance rather than expressibility.

**Three kinds of answer are useful:** *the approximation is fine, keep it*; *the approximation is
wrong, here is a sentence the engine can say*; or *this clause is worth an engine change*.

| hero | clause as written | what ships instead |
|---|---|---|
| **Branik** | "The first time each round a Cracked enemy attacks, Branik gains 8% Damage Reduction" | a second passive on `OnDamaged` carrying the identical clause, because the ally trigger excludes him |
| **Celen** | "transfer 15 Energy from the highest-Energy ally to the lowest **if their difference is at least 30**" | the pair is ungated, and self-cancels when the fullest and emptiest bars are the same body |
| **Celen** | "transfers the largest ally Shield to the lowest-HP ally and **extends it 1 round**" | the extension is authored and lands, but against a 99-round entry it is nominal |
| **Demos** | "extends Arrears by 1 round, **up to 3 rounds**" | the cap is dropped; the clause extends without limit |
| **Demos** | "At turn end, Arrears loses 4% current HP" | an `attackCap` was added that the sentence does not mention, holding the clause at the relationship it was written with |
| **Eira** | "it reduces the first **area** hit **each round** by 20%" | one Guard charge for the whole 2-round duration, answering whichever hit lands first, area or not |
| **Eira** | "When an ally with Mobile Cover is **singled out**" | gated on the victim carrying the ward, so "singled out" becomes "hit" |
| **Eira** | "moves the target **to the back** if another enemy is alive" | a position swap, which displaces it but not reliably to the back |
| **Finn** | "**At round end**, Ringing enemies have a 35% chance to be delayed" | fires on a Ringing enemy's own turn ending, once a round |
| **Finn** | "have a **35% chance** to be delayed by 15% turn progress" | the delay is certain and the passive is limited to once a round instead |
| **Halwen** | "**When Reserve prevents damage**, its holder recovers 4% Max HP" | fires once a round on an ally being damaged, healing the victim whether or not Reserve answered the blow |
| **Halwen** | "its holder recovers 4% Max HP" - the holder can be Halwen | a second passive on `OnDamaged` carrying the identical clause |
| **Iolan** | "removes one target buff; **if none is removed**, applies 10% Effect Hit Down" | the rider lands whether or not the Dispel found anything |
| **Jory** | "Taunts the front enemy for 2 rounds; **it** deals 15% less damage to Jory" | 15% damage reduction on Jory for the Taunt's duration, which reduces damage from everybody |
| **Jory** | "If the **same enemy hits Jory twice** in a round, he counterattacks" | limited to once a round, so the first enemy to strike him is counterattacked |
| **Kest** | "takes 10% less **from it**" | 10% damage reduction, which reduces damage from everybody |
| **Leon** | "Damage increases **10% per displayed buff**, up to 40%" | the whole 40% lands against a body carrying any buff at all |
| **Leon** | "assigns Case Number ..., **revealing current HP, Energy, and buffs to allies**" | carried in the text only; the Mark does the mechanical half |
| **Lio** | "Diagnosed allies **receive 18% more healing**" | Diagnosed is a Mark and Lio's own healing reads it, so the extra comes from him rather than from every source |
| **Lio** | "When Lio removes a debuff, **that ally** recovers 5% missing HP; control debuffs also grant 10 Energy" | the heal moves into the ability where the cleansed body can be seen; the Energy pays the lowest-HP ally, which is that body by construction |
| **Maera** | "if the target **attacked an ally** since Maera's last turn, deals 25% additional" | "has acted this round", which in this engine's turn shape means it has attacked |
| **Maera** | "the enemy that **most recently damaged an ally**" | the enemy that has dealt the most damage so far |
| **Maera** | "and **she cannot be controlled**" | **the clause is dropped.** The guaranteed critical half of the passive is authored and the immunity half is not |
| **Maren** | "Recovery heals **3% Max HP** each turn" | the tick is written in Maren's Attack at the value that comes to about 3% of an ally's bar at the level the number was set against |
| **Mika** | "moves each effect to **an unmarked enemy**" | moves to the highest-HP enemy, the answer Demos's No Appeal already gives to the same question |
| **Mika** | "When an ally **gains Energy** from Loose Parts, Mika gains a Shield" | folded into the one passive that hands the Energy out, so the grant and the ward are one firing |
| **Nadine** | "Registered targets **cannot become Concealed or Untargetable**" | Proper Place strips the concealment the moment it lands instead of preventing it |
| **Nadine** | "marks the target position for 2 rounds. **It cannot move**" | **the clause is dropped.** Hold the Line keeps its damage rider |
| **Niko** | "When a Flustered enemy **attempts a debuff**" | fires when an enemy casts an active, which is when the debuffs come |
| **Niko** | "forces its next Basic Attack onto **a random ally**" | Provoked, so the blow lands on Niko |
| **Rhys** | "When an ally takes a hit **exceeding 25% Max HP**, Rhys takes 30% of it instead" | the guard goes up on the first strike of each round whatever its size, and covers that ally for the rest of the round |
| **Rhys** | "Rhys takes 30% of it instead, **once per round**" | the ward is applied once a round and then answers every hit for that round |
| **Rhys** | "**swaps HP percentage** with the lowest-HP ally if they are below 50%" | Rhys pays 45% of what the ally is missing out of his own bar, clamped to leave him at 1 HP. There is no swap in the engine |
| **Siofra** | "changes one target buff into its matching debuff **for 2 rounds**" | the reversal lasts as long as the buff had left, and the text says so |
| **Tamsin** | "Targets above 50% turn progress ... **take 15% more Skill Damage**" | only Tamsin's own blows land harder |
| **Tamsin** | "removes 6% turn progress **for each target buff**, up to 18%" | 6% flat, and 18% against a body carrying any buff at all |
| **Tamsin** | "**The first enemy delayed** each round gains 10% Defense Down" | the whole enemy line takes it, once a round |
| **Toren** | "redirects 25% of **that hit**" | the ward goes up for the rest of the round and answers the next one |

**The two dropped clauses are the ones to read first**: Maera's control immunity and Nadine's hold.
Both are in the shipped kit's text as nothing at all rather than as an approximation, so a player
reading either hero gets a shorter ability than the design describes.

**Two earlier draft flags are withdrawn and need no answer.** Celen's "at round start" and Siofra's
second hook were both marked draft in their batches on the strength of a scan that read 46 of
`PassiveTrigger`'s 52 members and dropped the two that answered them. Both sentences are authored
unchanged now.

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
