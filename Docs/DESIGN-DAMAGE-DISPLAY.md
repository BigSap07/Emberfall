# Emberfall - how an ability's damage should be shown

**A proposal, not a build.** Nothing here changes a number the simulation reads, and nothing here
is UI code. It is about the sentence on a hero's card and which arithmetic that sentence should
carry. Written on 2026-09-22, against the shipped `heroes.json` with all 94 kits authored.

## 1. The reading that looks like a bug

A player who opens two heroes side by side sees this:

| hero | tier | role | the sentence on the card |
|---|---|---|---|
| Iri | 1-star | Mender | "Deals **437%** Attack damage" |
| Arno | 2-star | Vanguard | "Deals **472%** Attack damage" over its two hits |
| Avera | 3-star | Breaker | "Deals **380%** Attack damage" |
| Maera | 4-star | Marksman | "Deals **350%** Attack damage" over its two hits |
| Kael | 5-star | Bulwark | "Deals **110%** Attack damage" |
| Aminah | 5-star | Mender | "Deals **105%** Attack damage" |

Read as written, the game says the rarest hero in the roster hits for a quarter of what the
commonest one does. Nobody will read that as a design statement. They will read it as a bug, file
it, and be right to.

## 2. It is not a bug, and the arithmetic says why

Three separate rules stack into that table, and every one of them was decided deliberately and is
recorded elsewhere. They are repeated here because the display question cannot be answered without
them.

**A lower tier has fewer abilities, so it spends every turn on one of them.** The star contract is
the authoritative kit rule: a 1-star is a basic and nothing else, a 2-star adds one passive, a
3-star adds the active, a 4-star adds a second passive, a 5-star adds a stat-block passive and
three talents. A 5-star's turn is a basic three times out of four and an active the fourth; a
1-star's turn is a basic, every time, for the whole battle.

**A lower tier carries less Attack.** `StatMath.StarMultiplier` is 0.70, 0.85, 1.00, 1.16 and 1.34
from one star to five, and `heroes.json` holds a matching ladder so a non-5-star's whole stat total
is a fixed fraction of its role's 5-star mean. At level 40, Iri builds to an Attack of 511 and Kael
to 1,213.

**So the percentages were set to make the OUTPUT ladder come out right, not the percentages.**
`CheckStarBandByRole` measures each hero's own output over five seeds in one fixed arena and gives
each tier a band against its role's 5-star median: x0.45 to x0.85 at four stars, then x0.30 to
x0.60, x0.18 to x0.36 and x0.10 to x0.20. Every graded hero is inside its band. The 437% is what a
Mender with one ability and 511 Attack has to be priced at to land inside x0.10 to x0.20 of a
5-star Mender.

Follow it through at level 40 in the sweep arena, and the shape is clear:

| hero | Attack | one basic, raw | turns taken over five seeds | own output over five seeds |
|---|---|---|---|---|
| Iri, 1-star | 511 | 2,231 | 9 | 20,673 |
| Moss, 1-star | 591 | 1,762 | 8 | 10,776 |
| Arno, 2-star | 681 | 3,213 | 12 | 22,743 |
| Aminah, 5-star | 1,176 | 1,235 | 35 | 97,650 |
| Kael, 5-star | 1,213 | 1,334 | 36 | 106,380 |

**Iri's swing really is bigger than Kael's.** She takes a quarter of his turns, has no active skill
to spend a full Energy bar on, and dies in round 3 of a fight he is still standing in at round 8.
Per turn she reads 2,297 against his 2,955, which is 78%; per battle she reads 19% of him. The tier
gap is in turns and in the number of abilities, and almost none of it is in the size of a swing.

**This is the finding the display has to respect.** Any presentation that tries to make the
per-swing number rank the tiers will be lying, because the per-swing number does not rank them and
is not supposed to. What the display owes the player is a number that is TRUE and a number that is
COMPARABLE, and those turn out to be two different numbers.

## 3. Three ways to present it

### Option A: one denominator - express every ability against a five-star's Attack

Keep the percentage idiom and change what it is a percentage OF. Today each ability is a share of
its own owner's Attack, and the owner's Attack is exactly what the tier already scaled down, so
the tier is being divided out of the number twice. Multiplying the authored power by
`StarMultiplier(stars) / StarMultiplier(5)` gives one denominator for the whole roster:

| hero | card today | card under A |
|---|---|---|
| Iri, 1-star | 437% | 228% |
| Arno, 2-star | 472% | 299% |
| Avera, 3-star | 380% | 284% |
| Maera, 4-star | 350% | 303% |
| Kael, 5-star | 110% | 110% |

**What it costs:** one multiply at display time, no content change, no new data, and the number
stays a percentage so every existing habit about percentages survives.

**What it does not fix:** the inversion. Iri still reads more than twice Kael. It compresses the
spread from a factor of four to a factor of two and leaves the reader with the same question,
which is the wrong outcome for a change that also makes the percentage mean something the ability
text does not say. A player reading "228% of Attack" on Iri's card and then watching Iri hit for
437% of Iri's Attack has been told something false about the thing they can check.

### Option B: flat damage at the hero's current stats

Show what the ability will actually hit for, computed from the hero's live `UnitStats` the way the
detail screen already computes Power, with the percentage kept as a smaller second line for
players doing build arithmetic.

| hero | card under B |
|---|---|
| Iri, 1-star | "Deals 2,231 damage (437% Attack)" |
| Kael, 5-star | "Deals 1,334 damage (110% Attack)" |
| Hale, 5-star | "Deals 3,490 damage (250% Attack)" |

**What it costs:** the number moves whenever a level, a relic or a piece of armour moves, which is
a feature rather than a cost, and it is the same `StatMath.ForHero` call the card already makes for
the stat block, so the card is already holding everything it needs. There is one honest caveat to
carry in the text: it is the damage before the target's Defence, so it is a hero's output rather
than a promise about a hit.

**What it does not fix:** the inversion, again. It is the same ordering in different units. What it
DOES fix is the lie: 2,231 against 1,334 is a true statement about two heroes that a player can
verify in a fight, where "437% against 110%" is a true statement about two different denominators
that a player cannot.

### Option C: damage per round, which is the number that ranks the tiers

The thing a lower tier is actually missing is turns' worth of ability, so show a round rather than
a swing. Energy is 28 a basic against a cost of 100, so a hero with an active spends three basics
charging it and casts on the fourth: a round of a 5-star is `(3 x basic + active) / 4`, and a round
of a 1-star or a 2-star is its basic, because that is the whole of what it can do.

| hero | per swing | per round |
|---|---|---|
| Iri, 1-star | 2,231 | 2,231 |
| Moss, 1-star | 1,762 | 1,762 |
| Kael, 5-star | 1,334 | 4,184 |
| Hale, 5-star | 3,490 | 4,607 |

Kael's round is three basics and one Take Down the Wall, which is 210% of Attack into every enemy,
so against a full line it is worth about ten basics on its own. **This is the only one of the three
that puts the tiers in the order the game actually plays them in**, and it is the same quantity
`CheckStarBandByRole` grades a hero on, one round instead of five battles.

**What it costs:** it is a model rather than a reading. It assumes a full Energy bar, it has to
decide how many enemies an `AllEnemies` ability reaches, and it says nothing about a hero whose
value is a Shield, a cleanse or a turn shift. Every one of those is a judgement the card would be
making on the player's behalf, and a wrong one is worse than no number at all. It also cannot be
computed honestly for the half of the roster whose kit is not damage.

## 4. Recommendation

**Ship B, keep the percentage beside it, and do not ship C on the card.**

1. **The ability line becomes flat damage at the hero's current stats, with the authored percentage
   as a smaller second line.** It is true, it is checkable in a fight, it moves with gear and level
   the way a player expects a damage number to move, and it needs no content change and no new
   model. The percentage stays because it is the only form in which a player can reason about
   relics and Attack buffs.

2. **Reject A.** It buys a smaller spread by making the card say something the ability does not do,
   and it does not answer the question that made this a problem.

3. **Take C's idea somewhere other than the ability line.** The reason a 5-star is better than a
   1-star is that it has more to do with a turn, and the card should say that in words rather than
   in a derived number: a kit summary that reads "basic, active, two passives and three talents"
   against "basic only" tells a player the truth about the gap in a form that cannot be wrong. If a
   single comparable figure is wanted later, the honest place for it is the hero list's sort order
   rather than the card, and the honest source for it is a measurement rather than a formula: the
   sweep already produces exactly this number for all 94 heroes, five seeds each.

4. **Whatever ships, the star tier has to be legible next to the number.** The badge plate and the
   star row already carry rarity, and the ability line is the one place on the card where a bigger
   number currently means a rarer hero is worse. A flat number does not remove that inversion, it
   only stops it being a lie, and the kit summary in point 3 is what explains it.

## 5. What this proposal deliberately does not do

- **It changes no balance.** Every authored percentage stays exactly where the star band put it.
  Nothing here is a reason to retune a tier, and a tier retuned to make its card read better would
  fail `CheckStarBandByRole` on the way out.
- **It writes no UI.** `HeroDetailScreen` does not read `description` at all: its ability cards
  carry a kind and a name. `AbilityDef.Description` and `PassiveDef.Description` are read from the
  JSON and displayed nowhere. Whoever builds the ability line owns the question of where the text
  and the computed number meet, and that is a screen decision this document has no business
  making. *(Correction made when the recommendation was built: the sentence here originally said
  the screen "parses `description` today and does not render it", which was never true of the
  code. The screen that answered the question above reads the effect DATA and still shows no
  prose - see `AbilityMath` in `Assets/Scripts/Core/Progression` and `AbilityText` in
  `Assets/Scripts/UI`.)*
- **It does not touch the ability text itself.** The sentences in `heroes.json` are Pinoy's, or are
  departures recorded against his in `Docs/PINOY-LIST.md`. A display layer that needs the sentence
  to change is a display layer that has got the split wrong.
