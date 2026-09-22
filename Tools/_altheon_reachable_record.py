"""Scratch script for the Altheon III reachability repair. Not part of the build.

DESIGN-BUILD-ORDER open decision 5 read "Altheon III is unreachable in the shipped
roster", and the reason it gave was that capping the Ledger takes a single blow of about
5,700 on the holder when the biggest single damage effect on any 5-star is about 3,600.
That is the symptom. The cause is arithmetic and it is worse than a missing blow: no
roster can land it.

THE THREE READINGS, all taken at level 40 in the hero-sweep arena, where Altheon builds
to Attack 1,433 and an Effective Attack of 1,548 under his own 8% AttackUp.

1. The cap was 120% of his Attack, which is 1,857. Immaculate Record banks 30% of ONE
   blow a round, so closing it needs a single blow of 6,190, which is 4.0 times his own
   Attack. His largest authored effect is the active's 185%, and that effect resolves
   BEFORE the same cast applies the Ledger, so the largest blow that can ever be
   recorded is his basic's 120%. Measured, that blow lands 1,577 and records 473.

2. The Subject is faster than he is, and that halves the window. He marks the
   highest-Attack enemy; in this arena that is the hitter at Speed 160 against his 123,
   so the Subject's own turn - where its status clock ticks down - comes before his turn
   every round. A Subject that lives "2 rounds" therefore contains exactly ONE of
   Altheon's turns, so the record gets one blow rather than the two the sentence's
   accumulation implies. One blow is 473, which is 0.31 of his Attack against a cap of
   1.20.

3. The one blow it did record was usually not his. `ledgerBankLimit` of 1 with no
   `ledgerBankFromCaster` spends the round's single recording on whichever hit lands
   first, and the Subject is the enemy the whole line is aiming at, so that is an ally's
   smaller blow. Pinoy's sentence is "the first time the Subject takes damage FROM
   ALTHEON each round", which is both the faithful reading and the larger one.

WHAT MOVES. Three fields on one effect.

  ledgerBankFromCaster: true   - reading 3, and it is Pinoy's sentence restored.
  duration: 2 -> 3             - reading 2. Three rounds of Subject is two of his turns,
                                 which is the two-blow accumulation the clause describes.
                                 The Mark moves with it; the Subject is one thing.
  storedCap: 1.2 -> 0.5        - reading 1. Two recorded blows are 473 + 346 = 819
                                 against a cap of 0.5 x 1,548 = 774.

MEASURED AFTER. Seed 1: the active applies the Subject in round 3, round 4 records 473,
round 5 records 346 and closes the book, Fixed in Place fires, and the record
auto-releases its full 774 when the Subject expires in round 6. The record caps in 4 of
the 5 sweep seeds, where it capped in none before, which lights altheon_the_first_witness
III (it waits on OnLedgerCapped) and his passive2 with it. His sweep output moves from
115,051 to 121,869; the Breaker 5-star median is Mireth's 80,728 and does not move,
because Altheon was already the highest reading in the role.

THE DEPARTURE. "120% of Altheon's Attack" and "for 2 rounds" are both Pinoy's numbers and
both move. Docs/PINOY-LIST.md carries the row.
"""
import collections
import io
import json

O = collections.OrderedDict

P = r'Assets/Resources/Content/heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=O)
altheon = [h for h in d['heroes'] if h['id'] == 'altheon_the_first_witness'][0]

eff = altheon['active']['effects']
mark, ledger = eff[1], eff[2]
assert altheon['active']['id'] == 'altheon_immaculate_record', altheon['active']['id']
assert mark['status'] == 'Mark' and ledger['status'] == 'Ledger'
assert ledger['storedCap'] == 1.2 and ledger['ledgerBankLimit'] == 1

mark['duration'] = 3
ledger['duration'] = 3
ledger['storedCap'] = 0.5

# Rebuilt rather than assigned, so the new key sits beside the other ledger* fields
# instead of at the end of the effect.
rebuilt = O()
for k, v in ledger.items():
    rebuilt[k] = v
    if k == 'ledgerBankLimit':
        rebuilt['ledgerBankFromCaster'] = True
eff[2] = rebuilt

altheon['active']['description'] = (
    "Deals 185% Attack damage to all enemies and marks the enemy with the highest Attack as the "
    "Subject for 3 rounds. The first time the Subject takes damage from Altheon each round, record "
    "30% of that damage, up to 50% of Altheon's Attack. When the Subject expires it takes damage "
    "equal to the whole record. Pinoy writes the Subject as 2 rounds and the ceiling as 120% of "
    "Attack; at those numbers the ceiling needs a single blow of four times Altheon's Attack and "
    "the Subject outruns him, so the record got one blow a round and never closed. Draft, Pinoy to "
    "confirm, see Docs/PINOY-LIST.md section 2d.")

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('altheon: Subject 3 rounds, record capped at 50% of Attack, recorded from Altheon only')
