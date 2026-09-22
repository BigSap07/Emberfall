"""Scratch script for the Hale repair and retune. Not part of the build.

DESIGN-BUILD-ORDER open decision 6 read "Hale reads x0.36 of the Marksman median" and
asked the right question: "Either his kit is under-tuned or the sweep arena cannot raise
what he is built for, and the two want telling apart." Telling them apart is arithmetic,
and the answer is BOTH, in a measurable ratio.

WHAT THE SWEEP MEASURES OF HIM. 30,071 over five seeds, from exactly two clauses:
hale_field_sign 20,615 over 14 basics and hale_closing_radius 9,456 over 3 landing casts.
Nothing else in his kit ever deals a point.

  - hale_no_safe_distance answers a Quarry target gaining a SHIELD. There is no Shield
    anywhere in enemies.json - the word does not appear in the file - so that clause is
    dark against every enemy in the shipped game, not only in this arena. It can only
    fire in the Proving and in a friend duel, against a player line.
  - The active's missing-HP rider needs an ally to have struck the Quarry since his last
    turn. The sweep's fillers aim at FrontEnemy and his basic aims at LowestHpEnemy, so
    the two coincide rarely; it fired zero times across the five seeds.
  - Two of his five casts landed on nobody. His Quarry died, Tracking Habit had no Mark
    left to move, and EnemyWithMyMark resolved to nothing, so 100 Energy bought a cast
    with no damage event at all. That case is Pinoy's - talent III is the clause that
    re-marks on a defeat - and it is left as authored.

SO: HE TAKES THE FEWEST TURNS IN HIS ROLE AND HE IS PRICED THE LOWEST PER TURN. 19 turns
over the five seeds against Talren's 27, Edrin's 24 and Nysera's 34, because he has no
survival clause and Nysera has concealment. Per turn he reads 1,583 against a role
per-turn median of about 2,875. Roughly half of the gap is the arena refusing to raise
what he is built for, and roughly half is the kit. The arena half is not ours to fix; the
kit half is.

THREE THINGS MOVE.

1. THE QUARRY RIDER FOLLOWS THE QUARRY. Tracking Habit is a TransferStatus that moves the
   Mark to the highest-damage enemy and an ApplyStatus that puts Vulnerable there. When
   the Quarry is dead there is no Mark to move, and the Vulnerable landed anyway - so
   Quarry's own rider ("Hale's attacks against it ignore 10% Defense", authored as a
   source-keyed Vulnerable) sat on a body that was not the Quarry. The Vulnerable is
   gated on the body carrying his Mark now, which is the same body the transfer either
   reached or did not.

2. THE RIDER'S CEILING IS IN THE CASTER'S CURRENCY. The active's second effect is
   "additional damage equal to 12% of its missing HP, up to 120% of Attack". It was
   authored with missingHpCap 0.15, which ceilings against the TARGET's Max HP, and with
   a stray power of 1.0 the sentence does not mention. Against the sweep's 1,257,000-HP
   warden that ceiling is 188,550, which is no ceiling at all - the exact trap
   Docs/ENGINE-GAPS.md closes on, and the one that read Arden at seven and a half times
   the roster median. It is attackCap 1.2 and power 0.0 now, which is Kiva's identical
   clause's shape and is what the sentence says. The clause has never fired without
   talent II, so this bounds something nothing has measured rather than taking anything
   away.

3. THE TWO NUMBERS HE ACTUALLY LIVES ON GO UP. 120% and 220% are Pinoy's and they are the
   smallest in the role by a wide margin once you count clauses rather than percentages:
   Nysera's basic is 120% plus a 70% rider, Talren's is 120% plus a 40% echo and two
   passives that deal damage, and Edrin's 220% active hits ALL enemies. Hale's basic is
   one blow and his active is one blow at one body, and every 4-star and 3-star Marksman
   carries a bigger single-target active than his 220% - Maera 540%, Leon 490%, Brio
   480%, Cal 457%. They go to 250% and 570%.

MEASURED AFTER: 65,851 over the same five seeds, which is x0.79 of the Marksman 5-star
median. He is above all three authored 4-star Marksmen (x0.51 to x0.62) and below all
three of his 5-star peers. The median itself does not move: it is the mean of Talren's
77,618 and Nysera's 88,333, and 65,851 sits under both, so no other Marksman's ratio
changes by a point.

THE DEPARTURE. 120% and 220% are Pinoy's numbers. Docs/PINOY-LIST.md carries the row.
"""
import collections
import io
import json

O = collections.OrderedDict

BASIC_POWER = 2.5
ACTIVE_POWER = 5.7

P = r'Assets/Resources/Content/heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=O)
hale = [h for h in d['heroes'] if h['id'] == 'hale_the_relentless_hunter'][0]

basic = hale['basic']
assert basic['id'] == 'hale_field_sign', basic['id']
assert basic['effects'][0]['power'] == 1.2
basic['effects'][0]['power'] = BASIC_POWER
basic['description'] = (
    "Deals 250% Attack damage to the enemy with the lowest HP and applies Quarry for 2 rounds. "
    "Quarry makes Hale's attacks against that enemy hit 10% harder. Pinoy writes the damage as "
    "120% and Quarry as ignoring 10% Defense; the percentage is the star band's and the engine has "
    "no per-attacker armour ignore, so Quarry is a source-keyed Vulnerable of the same size. "
    "Draft, Pinoy to confirm, see Docs/PINOY-LIST.md section 2d.")

active = hale['active']
assert active['id'] == 'hale_closing_radius', active['id']
assert active['effects'][0]['power'] == 2.2
active['effects'][0]['power'] = ACTIVE_POWER

rider = active['effects'][1]
assert rider['targetMissingHpRatio'] == 0.12 and rider['missingHpCap'] == 0.15
rebuilt = O()
for k, v in rider.items():
    if k == 'missingHpCap':
        rebuilt['attackCap'] = 1.2
        continue
    rebuilt[k] = 0.0 if k == 'power' else v
active['effects'][1] = rebuilt
active['description'] = (
    "Deals 570% Attack damage to the enemy with Quarry. If any ally attacked it since Hale's last "
    "turn, deals additional damage equal to 12% of its missing HP, up to 120% of Attack. Pinoy "
    "writes the first number as 220%; the percentage is the star band's. Draft, Pinoy to confirm, "
    "see Docs/PINOY-LIST.md section 2d.")

habit = hale['passive1']
assert habit['id'] == 'hale_tracking_habit', habit['id']
transfer, vulnerable = habit['effects'][0], habit['effects'][1]
assert transfer['kind'] == 'TransferStatus' and vulnerable['status'] == 'Vulnerable'
rebuilt = O()
for k, v in vulnerable.items():
    rebuilt[k] = v
    if k == 'target':
        rebuilt['condition'] = 'HasMyMark'
        rebuilt['conditionOn'] = 'Target'
habit['effects'][1] = rebuilt
habit['description'] = (
    "At the start of each of Hale's turns, Quarry moves to the enemy that has dealt the most "
    "damage, and that enemy takes 10% more damage from Hale for 2 rounds. Both halves answer the "
    "same body: when Quarry was defeated there is no Mark to move and the rider does not land "
    "either. Pinoy writes the move as happening at the end of a round in which Quarry was not "
    "defeated; the engine's nearest hook is the start of his own turn.")

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('hale: basic 250%, active 570%, the rider capped in Attack, the Quarry rider follows Quarry')
