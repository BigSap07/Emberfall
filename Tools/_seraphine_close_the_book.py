"""Scratch script for the Seraphine talent-II conflict. Not part of the build.

DESIGN-BUILD-ORDER open decision 4: "Seraphine's Cross Reference works against her own
Volume Closed." Cross Reference appends a LedgerIncrement worth 30% of her Attack to
Contradiction Filed, so the Footnote it hands to the highest-Energy enemy opens at 30% of
her Attack against a ceiling of 80%. Volume Closed fires on LedgerAtCap and, one round
later, strips the Mark and the Ledger. The talent that fills the record faster empties it
sooner, and talent I says the same thing out loud.

TWO THINGS ARE WRONG UNDERNEATH THAT, and the second is why the first one mattered.

1. A RECORD THAT REACHED ITS CEILING WAS THROWN AWAY UNREAD. A Footnote had exactly one
   way to be worth anything - Contradiction Filed's LedgerRelease, which pays the record
   out as damage. Volume Closed's closing group removed the same Mark and the same Ledger
   without releasing them, so filling the record faster moved it toward the bin rather
   than toward the payout. That is the whole of the conflict.

2. THE CLOSING GROUP COULD NEVER RUN AT ALL. It was gated on a MarkAlt that Volume Closed
   writes on the capped body to remember the book is open, and it has to survive one of
   Seraphine's turns. Contrary Evidence - her own passive - is a Dispel of one buff
   whenever a Footnote body gains one, and Dispel takes everything that is not harmful,
   which includes a Mark. So the bookmark was stripped by her own kit.

   And the two halves want opposite boards. Measured in the dark arenas: the record only
   reaches its 80%-of-Attack ceiling in arenas where enemies gain buffs, because Contrary
   Evidence's LedgerIncrement is what fills it - and enemies gaining buffs is exactly what
   makes Contrary Evidence fire and eat the bookmark. In s_soft_big with talent I the
   opening group fires three times and the closing group zero; in an unbuffed arena the
   record never reaches the ceiling at all. Same trigger, same firing, opposite
   requirements.

THE RESOLUTION: the book closes in one firing and files what it read. Volume Closed keeps
every clause it had and stops needing a bookmark that its own sibling passive removes. On
the turn the record reaches its ceiling it suppresses buffs for a round, shifts turn
progress (which is what talent III deepens), takes the Footnote off, RELEASES the record
as damage, and pays Seraphine her 15 Energy. The ordering is what makes it authorable:
LedgerAtCap is still true for the removal and the release, and the BuffSuppression this
firing just applied plus the Mark it just removed are what identify the same body after
the release has zeroed the record and made LedgerAtCap false.

Both clauses can now matter, and in different directions rather than against each other.
Cross Reference makes the payout arrive sooner and start 30% of her Attack higher; Volume
Closed keeps the suppression, the turn progress and the 15 Energy, and now also files the
contradiction it spent two rounds recording.

THE DEPARTURE. Pinoy writes it in two beats - "the target becomes Unable to Gain Buffs for
1 round. When that restriction ends, Footnote is removed and Seraphine gains 15 Energy" -
and the removal and the Energy now arrive with the suppression rather than after it. The
release is not in his sentence at all. Docs/PINOY-LIST.md carries the row.
"""
import collections
import io
import json

O = collections.OrderedDict

P = r'Assets/Resources/Content/heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=O)
sera = [h for h in d['heroes'] if h['id'] == 'seraphine_the_closed_volume'][0]

p2 = sera['passive2']
assert p2['id'] == 'seraphine_volume_closed', p2['id']
assert len(p2['effects']) == 7, 'Volume Closed is no longer the two-beat shape this rewrites'
# Talent III deepens the TurnShift and names it by index. It stays at index 1.
assert sera['talents']['talentIII']['overrides'][0]['effectIndex'] == 1


def capped(extra=None):
    """The gate for the effects that run while the record is still at its ceiling."""
    o = O([('condition', 'LedgerAtCap'), ('conditionOn', 'Target')])
    if extra:
        o.update(extra)
    return o


def just_closed(subject):
    """The gate for the effects that run AFTER the release has zeroed the record: this
    firing's own BuffSuppression, on a body this firing has just taken the Footnote off."""
    return O([
        ('condition', 'HasStatus'),
        ('conditionStatus', 'BuffSuppression'),
        ('conditionOn', subject),
        ('condition2', 'LacksMyMark'),
        ('conditionStatus2', 'Mark'),
        ('conditionOn2', subject),
    ])


suppress = O([('kind', 'ApplyStatus'), ('target', 'AllEnemies')])
suppress.update(capped())
suppress.update(O([('bypassResist', True), ('duration', 1), ('status', 'BuffSuppression')]))

shift = O([('kind', 'TurnShift'), ('target', 'AllEnemies'), ('magnitude', 0.0)])
shift.update(capped())

take_mark = O([('kind', 'RemoveStatus'), ('target', 'AllEnemies'), ('hits', 1), ('status', 'Mark')])
take_mark.update(capped())

release = O([('kind', 'LedgerRelease'), ('target', 'AllEnemies'), ('power', 1.0)])
release.update(capped())

take_ledger = O([('kind', 'RemoveStatus'), ('target', 'AllEnemies'), ('hits', 1), ('status', 'Ledger')])
take_ledger.update(just_closed('Target'))

pay = O([('kind', 'EnergyGain'), ('target', 'Self'), ('flat', 15)])
pay.update(just_closed('LastTarget'))

p2['effects'] = [suppress, shift, take_mark, release, take_ledger, pay]
p2['description'] = (
    "When a Footnote record reaches its cap, the target cannot gain buffs for 1 round, loses turn "
    "progress, has Footnote removed and takes damage equal to the whole record, and Seraphine gains "
    "15 Energy. Pinoy writes it in two beats, with the removal and the Energy arriving when the "
    "restriction ends, and says nothing about the record being paid out. Both departures are the "
    "same repair: the second beat was gated on a Mark that Contrary Evidence - Seraphine's own "
    "Dispel - strips on the same trigger that fills the record, so it could never run, and a "
    "record that reached its cap was thrown away unread. Draft, Pinoy to confirm, see "
    "Docs/PINOY-LIST.md section 2d.")

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('seraphine: Volume Closed closes in one firing and files the record it read')
