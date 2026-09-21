"""Scratch fix: two batch-two clauses that were approximated against a trigger list that was
read wrong. Not part of the build.

The batch-two report named seven places where Pinoy's text could not be expressed. Two of
them were not true, and the cause was a bad reading rather than a bad engine:
PassiveTrigger has fifty-two members and the scan that produced that report found forty-six
of them, dropping every member whose declaration line did not match the pattern it used.
`RoundStart` and `AfterActiveCast` were both in the dropped set.

CELEN, NOTHING HOARDED. Authored on `TurnStart` with the text reworded to "at the start of
his turn", because the report said no round-start hook existed. `RoundStart` exists and its
own summary is exactly the distinction that was claimed to be missing: "The start of a
round, raised on every living unit before any of them acts. TurnStart is per-unit and fires
when that unit's own turn comes up, which is a different moment and a different number of
times." Pinoy's wording goes back.

SIOFRA, PREDICTION COST. The rule is "whenever Siofra changes a buff into a debuff", and
both her abilities do. It was authored as one passive on `AfterBasicAttack` plus a copy of
the drain inlined into the active, because the report said there was no hook after an
active. `AfterActiveCast` exists and describes itself as "the mirror of AfterBasicAttack".
The inline copy comes out of the ability and the rule becomes one rule on two hooks, which
is the overflow-passive shape the rest of this tier already uses.

Neither is an engine change. Both remove a row from Docs/ENGINE-GAPS.md before it is
written.
"""
import json, io, collections

P = r'Assets/Resources/Content/heroes.json'
O = collections.OrderedDict

d = json.loads(io.open(P, encoding='utf-8').read(),
               object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}

# ---- CELEN -----------------------------------------------------------------------------
celen = H['celen_the_open_hand']
p = celen['passive1']
assert p['trigger'] == 'TurnStart', p['trigger']
p['trigger'] = 'RoundStart'
p['description'] = ('At round start, Celen transfers 18 Energy from the highest-Energy '
                    'ally to the lowest.')
print('celen  passive1 TurnStart -> RoundStart')

# ---- SIOFRA ----------------------------------------------------------------------------
siofra = H['siofra_the_turncoat_oracle']
active = siofra['active']
before = len(active['effects'])
active['effects'] = [e for e in active['effects']
                     if not (e.get('kind') == 'EnergyGain' and e.get('flat') == -10)]
assert len(active['effects']) == before - 1
active['description'] = ('Deals 190% Attack damage to all enemies and reverses one Attack, '
                         'Defense or Speed modifier on each.')

siofra['passive1']['description'] = ('Whenever Siofra changes a buff into a debuff, that '
                                     'target loses 10 Energy, once per round.')

# The same rule on the other hook. AfterActiveCast is raised on the caster once the ability
# has resolved and the cast state is cleared, so the reversal it answers has already landed.
overflow = O([
    ('id', 'siofra_prediction_cost_active'),
    ('name', 'Prediction Cost'),
    ('description', 'The same cost after Forecast Reversed: every enemy whose modifier was '
                    'turned over loses 10 Energy.'),
    ('trigger', 'AfterActiveCast'),
    ('oncePerBattle', False),
    ('effects', [O([('kind', 'EnergyGain'), ('target', 'AllEnemies'), ('flat', -10)])]),
])
siofra['passives'] = [overflow]
print('siofra active drain -> overflow passive on AfterActiveCast')

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
