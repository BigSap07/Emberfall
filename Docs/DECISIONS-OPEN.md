# Emberfall: open decisions, each with a proposed answer

**Who this is for.** Matthias, in one sitting. Every item is something that used to wait on the
designer and does not need someone who draws. The art queue is `Docs/PINOY-ART.md`.

**How to use it.** Every item has an ID and a **proposed answer**. Reply with the IDs you overturn;
anything not overturned stands as proposed. Items marked **SHOW PINOY** change who a character is,
so the designer should see the outcome even though he is not deciding it.

**Where it came from.** `Docs/PINOY-LIST.md` (sections 2a to 2g, the working record, which keeps the
measurements), `Docs/ENGINE-GAPS.md`, `Docs/archive/ABILITY-TEXT-REWRITES.md`, the shipped kits in
`heroes.json` and the designer's original sentences in `import/Heroes.json`. Checked against the
tree on 2026-09-23.

**How it is grouped.** By how consequential the decision is, not by which system raised it:

1. **Identity**: decisions that change what a character is. Read all of these. (I1 to I9)
2. **Worth changing**: departures where the shipped kit is weaker, stranger or buggier than it
   needs to be, and a cheap fix exists. (W1 to W6)
3. **Fine as shipped**: the full ledger of every place a written sentence and the built kit
   disagree, with a one-line verdict each. Skim. (L1 to L51)
4. **Text only**: the 30 pinned drafting notes, each answered and each with the replacement card
   text written, plus the nine wording checks. Approve in bulk. (T1 to T29, R1 to R9)
5. **Small housekeeping calls.** (H1 to H4)

---

## 1. Identity (SHOW PINOY on every item here)

### I1. Isolde, Unfinished Sentence: counting debuffs instead of damage-over-time effects

- **Written:** "If the enemy has three or more active damage-over-time effects, this damage instead
  deals 90% of Attack damage and Isolde gains 8 Energy."
- **Built:** the harder strike and the Energy pay when the enemy carries three or more debuffs of any
  kind.
- **Why it moved:** the game has one damage-over-time status, Cinder, and every Cinder on an enemy
  merges into one entry whoever lays it. Three was unreachable in any team.
- **Proposed: keep counting debuffs.**
- **A correction to the framing.** This change did not turn Isolde from a solo threat into a combo
  piece. The rider was always a team clause: Isolde's own Exposed Vein refreshes her Cinder rather
  than adding one, so even as written she supplied one of the three and needed two more from
  allies. What changed is *which* team: as written it asked for other burners, as built it asks for
  any debuffer. Her Revelation and her mark do not count (they are bookkeeping, not debuffs), so she
  still brings one of the three herself.
- **Cost either way.** Keep: she slots into any debuff-heavy line, which is broader than written and
  loses the "burn team" flavour. Revert: needs the engine to keep one Cinder entry per applier, and
  Exposed Vein's refresh rule has to be decided against that; it is engine work for a clause few
  teams would reach.
- **What is actually worth showing the designer:** the whole passive, not just the rider, is dark in
  the hero sweep. Revelation lands on the lowest-HP enemy, which tends to die while her Cinder ticks
  elsewhere. That is a kit question about Inevitable Decay's target, not about this sentence.

### I2. Cyrra, Hidden Reserve: a store that also pays when it runs out

- **Written:** "When Bury the Burden creates a Shield, 50% of healing that exceeded an ally's Max HP
  is stored. When that Shield breaks, the ally is healed for the stored amount."
- **Built:** the store opens with the Ward, keeps gathering overflow while it lasts, and pays when the
  Ward breaks **or when the store runs out**.
- **Proposed: keep.**
- **Why.** The store shares its entry with the one Cyrra's full Veiled Weight already opens, so
  "lose it if the Ward never broke" would also delete the base kit's full-Weight payout. In the sweep,
  none of the breaks found anything stored, so as written the talent would read near zero: a talent
  that does nothing measurable, which is the trap shape of I3 and I4.
- **Cost either way.** Keep: the talent stops being "payback when your Ward is broken" and becomes a
  reliable deferred heal that overlaps her base clause, so its own identity blurs. It is worth about
  +3,175 a battle, and Cyrra already reads x1.37 of the Mender 5-star median, one of the three
  5-stars named as nearest the ceiling. Revert: needs a second, separate store kind in the engine,
  and leaves a talent that pays almost never.

### I3. Kael III, Lasting Materials: a talent the base kit spends before it can matter

- **Written:** "Rubble no longer expires after 3 rounds. Kael can hold up to 5 Rubble, but Take Down
  the Wall deals 10% less base damage."
- **The trap:** What Breaks Forward spends **all** Rubble on Kael's very next basic attack, so Rubble
  never lives long enough to expire or to stack past what one cast gave. He pays the 10% and gets
  nothing, in every arena it was tried in.
- **Proposed: change the talent, not the base kit.** New talent: **Take Down the Wall grants an extra
  Rubble and Kael can hold more, but deals less damage.** Card text: *"Take Down the Wall gives Kael
  more Rubble and he can hold more of it, but Take Down the Wall deals less damage."* The size of
  the damage cut is set by measurement, so the talent check reads it as a gain or a trade its own
  sentence names.
- **Why the talent side.** Kael is a starter and is in every opening battle, so changing What Breaks
  Forward moves every campaign fingerprint and forces a campaign re-solve. The base kit does what
  its sentence says. And no change to how What Breaks Forward spends would rescue the talent as
  written: Rubble comes in at one a cast, and any spend rule spends it faster than that. The
  talent's real promise is "more Rubble, at a price"; this says that directly, and should be
  buildable from content alone.

### I4. Mireth I, Deeper Erasure: a talent that lands less often than the base kit

- **Written:** "Remove From Record can trigger twice per Focused enemy during its duration, but the
  second trigger deals 30% less bonus damage."
- **The trap:** The Missing Page now lands once in *every* round a marked enemy gains a buff, which
  over a two-round mark is already two full-strength landings. The talent lifts the round limit but
  caps the page at two landings per enemy **for the whole battle** (the engine's count does not
  restart when the mark is laid again), the second one softer. Over a fight she lands fewer, weaker
  pages: 31,145 of her 96,401 a battle lost against an enemy that buffs every turn.
- **Proposed: change the talent.** Drop the per-enemy cap, keep the lifted round limit, keep the
  softer later strikes. Card text: *"The Missing Page is no longer limited to once per round. After
  its first strike on an enemy, later strikes land softer."* This is a content change (remove one
  override). If measurement shows it over-pays against an enemy line that buffs many times a round,
  add a small engine limit of "twice per round" rather than restoring the lifetime cap.
- **Why the talent side.** The base clause is now the designer's own sentence, faithfully. The talent
  was written against a base that fired less, and literally read ("twice during its duration") it
  now buys exactly what the base already gives.

### I5. Niko, Take the Floor: a disruptor who now pulls the blow onto Niko

- **Written:** "forces its next Basic Attack onto a random ally".
- **Built:** the enemy is Provoked, so its next attack lands on Niko.
- **Proposed: keep for now, and ask the engine whether Ruma's False Lead can be generalized.** Niko is
  a 4-star **Stalker**, and pulling a blow onto a Stalker is close to the opposite of the role. If
  "ally" means the enemy's own ally (friendly fire, which fits Flustered and Ruin the Agenda), the
  engine already sends a blow onto the attacker's own ally for Ruma; a version of that which fires
  on the attacker's next basic whoever it aims at would be the faithful shape.
- **Least confident item in this file**: the sentence does not say whose ally, and that decides
  whether this is a small change or a new capability.

### I6. Bren, Take Another Route: a swap that became a plain nuke

- **Written:** "swaps the target's present HP percentage with the highest-HP enemy".
- **Built:** a single blow aimed at the highest-HP enemy. Nothing swaps.
- **Proposed: overturn in content.** Keep the target, and add a second blow that takes **a share of
  that enemy's current HP**, bounded by Bren's Attack, the same shape Demos's Arrears already uses.
  Card text: *"Strikes the enemy with the highest HP, and the more HP it has, the harder a second
  blow lands, up to a limit."*
- **Why.** The swap was the whole ability: an equalizer that drags the healthiest enemy down. As
  built it is a generic 3-star nuke with no idea in it. A current-HP bite keeps the equalizer and
  needs no engine work; the percentage and the bound come out of the star band.

### I7. Seraphine, Volume Closed: closing the book is now a nuke

- **Written:** two beats: the target cannot gain buffs for 1 round, and **when that restriction
  ends**, Footnote is removed and she gains 15 Energy. Nothing about damage.
- **Built:** one beat, and closing the book **also releases the whole record as damage**.
- **Proposed: keep both.** The second beat could never run (her own Dispel was stripping the mark it
  waited on), and a record that reached its cap used to be thrown away unread, which made Cross
  Reference work against this clause. The Dispel has since been repaired, so the two beats *could*
  separate again, but all that buys is Energy one round later.
- **Show Pinoy** because the payout is new design, not a restoration: her capstone is now a damage
  event rather than a lockout.

### I8. Hale, Field Sign and Closing Radius: a bigger single-target hunter

- **Written:** Field Sign 120% and Quarry makes Hale **ignore 10% Defense**; Closing Radius 220%.
- **Built:** 250% and 570%; Quarry makes Hale **hit 10% harder** (the engine has no per-attacker
  armour ignore, so it is a Vulnerable that only Hale's blows read).
- **Proposed: keep.** He read x0.36 of his role's 5-star median, below every 4-star Marksman, because
  both his abilities are single blows at single targets where his three 5-star peers carry second
  damage clauses or hit the whole line. He reads x0.79 now, tied with Kiva as the lowest graded
  5-star. The alternative the designer could prefer is a second damage clause at the original
  numbers; say so if you want that tried.

### I9. Kiva III, Unclaimed Victory: the first Ward is ungated

- **Written:** "When Kiva defeats a Forsaken target, consume all Broken Links. Each one gives her a
  12% Max HP Shield for 2 rounds."
- **Built:** the kill always pays one Ward, and each Broken Link held adds another, up to a limit.
- **Proposed: keep.** As written it paid exactly zero: Severance Paid spends every Broken Link on the
  next basic, so a kill never finds one. The ungated Ward pays on the kill the talent names.
- **The wording, owed since the five-star review.** The card currently carries a measurement, which
  the style rules forbid. Replacement:

  > *When Kiva defeats a Forsaken enemy, she gains a Ward drawn from her Max HP, larger for each
  > Broken Link she holds, up to a limit. Every Broken Link is spent.*

  It says what the kit does and stops. The link-gated part is real and reachable, just rare, so it
  stays on the card without a number and without a caveat. The same sentence should replace the
  measurement note carried in `import/Heroes.json`.

---

## 2. Worth changing

### W1. Build the two cheap engine primitives that close five rows at once

- **G1**, an amount scaled by how many bodies the previous effect changed, restores **Quin's 12 Energy
  per target** (L26) and **Kael's one Rubble per buff removed** (L35).
- **G2**, a moved or inverted status recorded as a removal, restores **Oswin's Guard Up only for the
  allies who lost a debuff** (L24), **Linnet's Energy only when a debuff actually moved** (L16) and
  **Siofra's Energy drain only after an actual inversion** (L36).
- **Proposed: build both.** Each is sized small in `Docs/ENGINE-GAPS.md`, and together they put five
  kits back to the sentence. Siofra is the one that matters most: 9 of 11 of her basic-attack drains
  follow a basic that inverted nothing, so she is draining on a condition her card does not name.

### W2. Hale, Tracking Habit: move Quarry at round end, as written

- **Written:** "At the end of a round in which Quarry was not defeated, move Quarry to the enemy that
  dealt the most damage that round."
- **Built:** at the start of each of Hale's turns. The drafting note says the start of his turn is
  "the engine's nearest hook". **That is stale**: a round-end trigger was built when the engine gaps
  pass closed group 14.
- **Proposed: overturn, move it to round end.** It is also probably a hidden defect as built. "The
  enemy that dealt the most damage" reads damage dealt **this round**, and Hale outpaces every
  campaign enemy but the Grave Moth. At the start of his turn, early in a round, most enemies have
  dealt nothing yet, and a tie goes to the front slot, so Quarry likely drifts to the front enemy
  rather than the dangerous one. At round end the whole round's damage is known. **Measure before
  and after**: this item is read from the code, not from a run. It is the only one of the fifty-one
  not recorded in `Docs/PINOY-LIST.md` sections 2a to 2d or in `Docs/ENGINE-GAPS.md`.

### W3. Nadine, Hold the Line: the card still promises the dropped clause

- **Written:** "marks the target position for 2 rounds. It cannot move and takes 12% more damage from
  Nadine."
- **Built:** the damage rider only. The hold was dropped because the game has no movement system.
- **The card says** "holds it in place", which the kit does not do. Maera's card already leaves her
  dropped clause out and Brio's is T16; Nadine's was missed.
- **Proposed: accept the drop and fix the card** to *"Strikes the front enemy and marks it; while the
  mark lasts, it takes more damage from Nadine."*

### W4. Nysera II, False Target: a talent that can hit its own hero

- **Written:** "When the Linked effect redirects an attack away from Nysera, the redirected damage is
  increased by 25% of Attack and the enemy that was originally targeted loses 10 Energy."
- **Built:** the extra damage is aimed at the body the redirected share came off, and when the blow
  was aimed at Nysera that body is Nysera. Only the Energy half works. It has no card text, because
  honest text would print a defect.
- **Proposed: repair it as written**: add the damage to the redirected blow. Then write the card.

### W5. Celen shows Nereia's body until her own art arrives

- **Proposed: switch Celen to the placeholder mannequin now.** A mannequin says "not drawn yet"; the
  wrong character in the right place says "broken", and a player who owns both sees the same body
  twice. A data change, undone when her sheets land (`Docs/PINOY-ART.md` item 1).

### W6. The first three summons ignore the published rates

- **The finding:** an account's first three pulls draw uniformly from every unowned hero of any tier
  and ignore the summon weights. The Rift's odds line now shows real rates (20/30/30/17/3), so the
  line is wrong for exactly the three pulls a new player makes first.
- **Proposed: keep the guarantee and say it on the Rift**: "your first three summons are always a
  hero you do not own". The guarantee is the better new-player experience; the fix is the words.

---

## 3. Fine as shipped: the full ledger

**The count.** The task that produced this file said "roughly fifty". **Fifty-one are recorded**: the
36 engine-gap clauses (`Docs/ENGINE-GAPS.md`, 33 of them regrouped by hero in `Docs/PINOY-LIST.md`
2b), the 7 star-contract departures (2c), the 6 measured 5-star rows (2d), Kiva III (2a), and Hale's
Tracking Habit, which only its drafting note records. Isolde's and Cyrra's rewrites (2g) are two
more, taken above as I1 and I2, and Nadine's card (W3) is one more that nothing had recorded.

**Verdicts:** 43 keep (one of them, L22, with a card fix), 2 overturn now (L5 and L51), 5 restore
when W1's primitives land, 1 open (L23). "Keep" means the resolution in favour of the kit was right.

| ID | hero | written | built | verdict |
|---|---|---|---|---|
| L1 | Adric | Energy when a Lost Route enemy misses anyone | only when it misses Adric | Keep. A miss is only heard by the body that dodged. |
| L2 | Aster | Fracture boosts the next critical hit | Fracture boosts every blow for its duration | Keep. Stronger and band-checked; the per-hit charge (G3) touches the damage funnel. |
| L3 | Avera | 50% chance to delay | the delay is certain | Keep. |
| L4 | Brio | attacks against Accounted cannot miss | dropped | Keep the drop. No campaign enemy can make Brio's side miss; only a Proving line with Adric could. |
| L5 | Bren | swaps HP percentages with the highest-HP enemy | a blow on the highest-HP enemy | **Overturn, see I6.** |
| L6 | Cal | a kill by the splash grants a guaranteed critical | any kill on an Offset enemy | Keep. |
| L7 | Celen | Energy transfer only if the gap is at least 30 | ungated | Keep. |
| L8 | Clara | Energy for removing a control debuff | for any debuff | Keep. No campaign enemy applies control, so as written it would almost never pay. |
| L9 | Demos | Arrears loses 4% current HP a turn | the same, with a cap from Demos's Attack | Keep. Uncapped he reads x3.40 of his role's median. |
| L10 | Eira | moves the target to the back | a swap with a random position | Keep. Position means little here. |
| L11 | Finn | 35% chance to delay | certain | Keep. |
| L12 | Iolan | the rider lands only if the Dispel found nothing | lands either way | Keep. |
| L13 | Jory | counters an enemy that hits him twice in a round | counters the first enemy each round | Keep. More reliable, band-checked. |
| L14 | Leon | reveals HP, Energy and buffs to allies | text only | Keep. The player already sees everything. |
| L15 | Linnet | the first debuff to land each round is rerouted | one debuff already on the body moves at round start | Keep. |
| L16 | Linnet | Energy when a debuff is redirected | Energy every round start | Keep now; restore with G2 (W1). |
| L17 | Lio | Energy for a control debuff removed | for any debuff | Keep, as L8. |
| L18 | Lydia | Bulwark when she acts before the slowest enemy | Bulwark every turn | Keep. |
| L19 | Maera | "and she cannot be controlled" | dropped | Keep the drop. No campaign enemy stuns or silences; the immunity would buy nothing outside the Proving. |
| L20 | Mika | Loose Parts moves to an unmarked enemy | to the highest-HP enemy | Keep. |
| L21 | Nadine | Registered enemies cannot become Concealed | concealment is stripped as it lands | Keep. |
| L22 | Nadine | the held target cannot move | dropped | Keep the drop; **fix the card, see W3.** |
| L23 | Niko | forces the next attack onto a random ally | Provoked onto Niko | **Open, see I5.** |
| L24 | Oswin | Guard Up for allies who lost a debuff | for every ally | Keep now; restore with G2 (W1). |
| L25 | Quin | Delivered boosts the next active skill | every active skill for its duration | Keep. |
| L26 | Quin | 12 Energy per Delivered target | 12 flat | Keep now; restore with G1 (W1). This one mattered to his loop. |
| L27 | Quin | first active costs 15 less | starts with 15 Energy | Keep. Same first cast. |
| L28 | Rhys | swaps HP percentage with a wounded ally | gives the ally a share of their missing HP out of his own bar | Keep. The sacrifice survives. |
| L29 | Selka | Overdue cannot be cleansed in its first round | Overdue is rewritten every round start | Keep, noting it never runs out once applied. |
| L30 | Sena | Critical Chance against slowed enemies | an extra blow against an enemy in Mire | Keep. |
| L31 | Arno | Attack Up when an enemy Ward breaks | Attack Up when he strikes an enemy still holding one | Keep. |
| L32 | Kora | more damage to enemies that have not acted | an extra blow against them | Keep. |
| L33 | Yara | Energy for changing target | cannot repeat a target, Attack Up every shot | Keep. She cannot finish a target on consecutive basics, a small real cost. |
| L34 | Hale | first attack on a new Quarry gains Crit Damage | Crit Damage Up every turn | Keep until the per-hit charge exists; the card is honest. |
| L35 | Kael | one Rubble per buff removed | one Rubble per cast | Keep now; restore with G1 (W1). |
| L36 | Siofra | Energy drain after a buff is inverted | drain after every basic | Keep now; **restore with G2 (W1)**, the most off-sentence of the five. |
| L37 | Piri | Piri gains 15 Energy on a kill | the emptiest ally gains it | Keep. Already decided: the star contract does not move. |
| L38 | Wynn | Wynn gains 5 Energy | the hit ally gains it | Keep, as L37. |
| L39 | Orenna | Orenna gains 5 Energy a swing | the emptiest ally gains it | Keep, as L37. |
| L40 | Yara | paid in Energy | paid in Attack Up | Keep, as L37. |
| L41 | Avera | Disrupted drains after an active skill | drains the first Energy gain each round | Keep. The faithful shape needs a second passive, which a 3-star does not carry. |
| L42 | Cal | the next ally attack ricochets | Cal's own shot ricochets | Keep, as L41. It turns a setup piece into a self-sufficient splasher; mild. |
| L43 | Petra | answers a Withheld enemy targeting an ally | answers its blow landing | Keep. |
| L44 | Altheon | Subject 2 rounds, record ceiling 120% of Attack | 3 rounds, 50%, his own blows only | Keep. As written the record closed in none of five runs. |
| L45 | Seraphine | Volume Closed in two beats | one beat | Keep, see I7. |
| L46 | Seraphine | nothing about damage | the record is released as damage | Keep, see I7. |
| L47 | Altheon | Witness Script rider 35%, after the blow | 35%, resolving before the blow | Keep. After the blow it paid on every basic. |
| L48 | Aminah | 105%, 55%, 155% | 170%, 115%, 230% | Keep. She read below two 4-star Menders. |
| L49 | Hale | 120%, 220%, Defense ignore | 250%, 570%, hits harder | Keep, see I8. |
| L50 | Kiva | Wards only from Broken Links | the first Ward is ungated | Keep, see I9. |
| L51 | Hale | Quarry moves at round end | at the start of his turn | **Overturn, see W2.** |

**Three standing design notes, none needing an answer to ship:**

- **Velis III, Final Denial.** Its Heal Suppression also stops the heal Hollowed would convert, so it
  can cost Velis her own payoff. Measured at -341 a battle, inside the noise. **Proposed: leave it.**
  If the designer meant it to deny the enemy's healer, that is a new talent, not a fix.
- **Altheon III, Final Record** spends the same Records Preserved Moment uses. It is a thin gain, about
  1% over 150 seeds. **Proposed: leave it.**
- **Kiva's Broken Links** are banked and spent in the same breath, so Severance Paid's links rarely
  last to a kill (I9). Not a defect; the kit simply runs hot on them.

---

## 4. Text only

### 4a. The 30 pinned drafting notes (`Docs/PINOY-LIST.md` 2f)

Twenty-nine questions over thirty ability ids (Demos has two). **Proposed: yes to all except T11
and T25**, which follow I6 and W2. **Yes** means the drafting note comes off and the card text below
goes on, and the id comes out of the harness's pinned list in the same commit. Every sentence below
follows `Docs/ABILITY-TEXT-STYLE.md`: no numbers, in-battle status names, forty words at most.

| ID | ability | answer | card text |
|---|---|---|---|
| T1 | Arno, Rubble Resolve | Yes | After Arno's basic attack, if the enemy he struck still holds a Ward, he gains Attack Up. |
| T2 | Yara, Second Bearing | Yes | Yara never aims at the same enemy twice in a row, and gains Attack Up after each basic attack. |
| T3 | Kora, First Opening | Yes | After Kora's basic attack, if the enemy she struck has not acted this round, she strikes it again. |
| T4 | Sena, Unfixed Aim | Yes | After Sena's basic attack, if the enemy she struck is in Mire, she strikes it again. |
| T5 | Adric, No Return Mark | Yes | When an enemy with Lost Route misses Adric, he gains Energy. |
| T6 | Aster, Scoring Point | Yes | Strikes the enemy with the highest Attack and applies Fracture. A Fractured enemy takes more damage from every blow. |
| T7 | Quin, Stamped Parcel | Yes | Strikes the front enemy and applies Delivered. A Delivered enemy takes more damage from active skills. |
| T8 | Quin, Return to Sender | Yes | Strikes every Delivered enemy and takes Delivered back off them, and Quin gains Energy. |
| T9 | Quin, Urgent Matter | Yes | At the start of battle, Quin gains Energy. |
| T10 | Selka, No Extension | Yes | At the start of each round, Overdue is written back onto the enemy carrying it, so it cannot be shaken off. |
| T11 | Bren, Take Another Route | **No, see I6** | If I6 is approved: Strikes the enemy with the highest HP, and the more HP it has, the harder a second blow lands, up to a limit. If I6 is overturned: Strikes the enemy with the highest HP. |
| T12 | Cal, Ricochet Mark | Yes | Strikes the enemy with the highest Attack and applies Offset, then a ricochet strikes it and the enemies beside it. |
| T13 | Cal, New Sightline | Yes | When Cal defeats an Offset enemy, his next attack is a guaranteed Critical. |
| T14 | Linnet, Open Circuit | Yes | At the start of each round, a debuff on the Crossed Line enemy moves to a random enemy, and Linnet gains Energy. |
| T15 | Oswin, Useful Scar | Yes | After Oswin casts Reposition the Hurt, every ally gains Guard Up. |
| T16 | Brio, Recorded Shot | Yes | Strikes the front enemy and applies Accounted. |
| T17 | Avera, Offbeat Note | Yes | Strikes the front enemy and applies Disrupted. The first time each round a Disrupted enemy would gain Energy, it loses Energy instead. |
| T18 | Avera, Break the Measure | Yes | Strikes every Disrupted enemy and delays each of them. |
| T19 | Lydia, No Fixed Departure | Yes | At the start of each of Lydia's turns, she gains Bulwark until her next turn. |
| T20 | Petra, Private Address | Yes | When a Withheld enemy strikes an ally, Petra gains Bulwark. |
| T21 | Clara, Known Condition | Yes | When Clara takes a debuff off an ally, that ally gains Energy. |
| T22 | Demos, Notice of Loss, and its Arrears tick | Yes | Notice of Loss: Strikes the front enemy and applies Arrears. As its own turn ends, an enemy in Arrears loses a share of its current HP, up to a limit set by Demos's Attack. The tick: As its own turn ends, an enemy in Arrears loses a share of its current HP, up to a limit set by Demos's Attack. |
| T23 | Hale, Field Sign | Yes | Strikes the enemy with the lowest HP and marks it as Quarry. Hale hits a Quarry harder. |
| T24 | Hale, Closing Radius | Yes | Strikes the Quarry. If an ally has struck it since Hale's last turn, a second blow lands, harder the more wounded it is, up to a limit. |
| T25 | Hale, Tracking Habit | **No, see W2** | If W2 is approved: At the end of each round, if Quarry still stands, it moves to the enemy that dealt the most damage that round, and Hale hits it harder. If W2 is overturned: At the start of each of Hale's turns, Quarry moves to the enemy that has dealt the most damage this round, and Hale hits it harder. |
| T26 | Kiva, Unclaimed Victory | Yes | When Kiva defeats a Forsaken enemy, she gains a Ward drawn from her Max HP, larger for each Broken Link she holds, up to a limit. Every Broken Link is spent. |
| T27 | Seraphine, Volume Closed | Yes | When a Footnote record reaches its limit, the enemy takes the whole record as damage, loses Footnote and is Nullified, and Seraphine gains Energy. A Nullified enemy cannot gain buffs. |
| T28 | Altheon, Witness Script | Yes | Strikes the enemy with the highest Attack, harder if Altheon has already damaged it this round. If it is the Subject and holds more HP than when he last noted it, he strikes it again. |
| T29 | Altheon, Immaculate Record | Yes | Strikes every enemy and marks the enemy with the highest Attack as the Subject. The first time each round Altheon damages the Subject, a share of it is recorded, up to a limit. When the Subject expires, it takes the whole record as damage. |

Notes on three of them. **T10**: Overdue never runs out once applied, which the card's "cannot be
shaken off" covers. **T22**: the percentage is the designer's; only the cap is new, and the card now
says it. **T27**: the draft also said the target loses turn progress, which only the No Revision
talent adds, so it is left out.

### 4b. The nine wording checks from engine gaps pass 3 (`Docs/PINOY-LIST.md` 2g)

Each is the designer's own sentence, now said by the kit. **Proposed: yes to all nine.** Isolde's and
Cyrra's, the two that changed meaning, are I1 and I2 above.

| ID | ability | proposed |
|---|---|---|
| R1 | Marrow, The Cost Is Named | Yes. The heal now lands as the ally's turn opens, as Vow Kept says. |
| R2 | Nereia, Deeper Reservoir | Yes. |
| R3 | Nereia, Shared Reserve | Yes. The Ward is now half of what Reserve released, as written. |
| R4 | Aeron, Unpaid Action | Yes. |
| R5 | Velis, Hollow the Wound | Yes. Hollowed converts one heal, as written. |
| R6 | Velis, No Second Breath | Yes. |
| R7 | Velis, What Was Taken | Yes. |
| R8 | Velis, Starve the Light | Yes. |
| R9 | Mireth, The Missing Page | Yes. The arriving buff is removed at once, as Remove From Record says. |

---

## 5. Small housekeeping calls

- **H1.** `Docs/PINOY-LIST.md` 2f says "except these 30" and lists 29. Both are right: 29 questions,
  30 ids. **Proposed:** say so in the heading when it is next edited.
- **H2.** Six orphaned sprite files for heroes removed in the roster swap are referenced by nothing.
  **Proposed: delete them.**
- **H3.** Every approved T item removes its id from the harness's pinned list in the same commit;
  the harness fails if one half lands without the other.
- **H4.** When I3, I4, I6 or W2 lands, the new card text goes into `heroes.json` and the designer's
  source file together, and the talent or star-band check is rerun before it counts as done.
