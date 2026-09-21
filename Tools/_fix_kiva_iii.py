"""Scratch script for the Kiva III repair. Not part of the build.

Unclaimed Victory paid nothing. Its three clauses were all gated on holding a Broken
Link, and repairing Severance Paid turned the spend on: Abandoned Support banks at most
one link a round and Severance Paid consumes one on every basic against a Forsaken body
- the same basic whose own Mark banked it - so she opens every turn at zero and a kill
finds nothing to consume. chore/four-star-prep looked at it, left it alone and said the
number was for this pass to decide.

The decision: the first Shield is ungated, the two link-gated ones stay exactly as they
were, and the talent reads 12 / 24 / 36% of her Max HP. It pays on the kill the text
names rather than on a resource the same kit spends before the kill arrives, and it is
still worth more to a Kiva who held links than to one who did not.
"""
import json, io, collections

O = collections.OrderedDict

NEW_TEXT = ("When Kiva defeats a Forsaken target, she gains a Shield equal to 12% of her Max HP "
            "for 2 rounds, and 12% more for each Broken Link she is holding, up to 36%. All "
            "Broken Links are consumed.")


def shield(stacks):
    """One clause of the payout. stacks 0 is the ungated one."""
    o = O([('kind', 'Shield'), ('target', 'Self'),
           ('condition', 'HasMyMark'), ('conditionOn', 'Defeated')])
    if stacks > 0:
        o['condition2'] = 'StatusStacksAtLeast'
        o['conditionOn2'] = 'Caster'
        o['conditionStatus2'] = 'Resolve'
        o['conditionValue2'] = stacks
    o['duration'] = 2
    o['selfHpRatio'] = 0.12
    return o


P = r'Assets/Resources/Content/heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
kiva = [h for h in d['heroes'] if h['id'] == 'kiva_the_forsaken_oath'][0]
t = kiva['talents']['talentIII']
assert t['id'] == 'kiva_unclaimed_victory', t['id']

rebuilt = O()
for k, v in t.items():
    if k == 'effects':
        rebuilt['description'] = NEW_TEXT
        rebuilt['effects'] = [
            shield(0), shield(1), shield(2),
            O([('kind', 'RemoveStatus'), ('target', 'Self'),
               ('condition', 'HasMyMark'), ('conditionOn', 'Defeated'),
               ('hits', 10), ('status', 'Resolve')]),
        ]
    else:
        rebuilt[k] = v
kiva['talents']['talentIII'] = rebuilt
io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')

# The text authority is import/Heroes.json, which is where every other clause in the
# roster was followed back to. It moves with the code, flagged for Pinoy.
I = r'import/Heroes.json'
imp = json.loads(io.open(I, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
ik = [h for h in imp['heroes'] if h['id'] == 'kiva_the_forsaken_oath'][0]
ik['talents']['talentIII']['description'] = NEW_TEXT
io.open(I, 'w', encoding='utf-8', newline='').write(json.dumps(imp, indent=2, ensure_ascii=False) + '\n')

print('kiva III: ungated 12% plus the two link-gated 12%s, 12/24/36, text updated in both files')
