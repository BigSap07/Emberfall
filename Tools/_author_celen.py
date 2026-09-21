"""Scratch authoring script for Celen. Not part of the build.

Celen was deliberately left out of the first 4-star batch, and that branch report said
why: two of his three clauses name an ally pick the engine did not have. Both exist now -
HighestEnergyAlly and LargestShieldAlly - and they are the only engine addition in this
batch.

Same shape as the first batch: basic + active + passive1, per the 4-star star contract.
No talents and no stat-block passive, because those are what a 5-star has and a 4-star
does not.

TWO PLACES WHERE THE TEXT AND THE ENGINE DO NOT MEET, both flagged in the branch report as
draft for Pinoy rather than quietly reworded:

1. "At round start" has no hook. This engine has no round-start broadcast at all -
   PassiveTrigger has BattleStart and TurnStart and nothing between them - and Celen is the
   only description in import/Heroes.json that asks for one. TurnStart is the nearest thing
   that exists, and at Speed 97 he takes one turn a round, so the two coincide in practice
   without being the same rule.

2. "if their difference is at least 30" is not expressible. EffectCondition has EnergyAtLeast
   and EnergyBelow and both read ONE subject; ConditionSubject can name the caster, the
   target, the broadcast subject and the last attacker, and none of those is "the other ally
   this effect is about". Gating the drain and the grant on two different subjects is worse
   than not gating at all, because the two halves can then disagree and Energy is destroyed
   or created out of nothing. So the pair is ungated, and it self-cancels in the case the
   threshold was written to avoid: when the fullest and emptiest bars are the same body, the
   -15 and the +15 land on it and net zero.

THE TRANSFER GOES THROUGH CELEN, and that is the shape of ShieldTransfer rather than a
choice. It moves a pool between the CASTER and its TARGET; there is no donor-and-recipient
pair of fields, so "from the largest ally Shield to the lowest-HP ally" is two hops - in to
Celen, out to the ally - and adding a secondaryTarget to ShieldTransfer would have been a
second engine change in a batch allowed one.
"""
import json, io, collections

P = r'Assets/Resources/Content/heroes.json'
O = collections.OrderedDict


def e(**kw):
    return O(sorted(kw.items(), key=lambda kv: (kv[0] != 'kind', kv[0] != 'target', kv[0])))


def ability(aid, name, desc, effects, energy=None):
    o = O([('id', aid), ('name', name)])
    if energy is not None:
        o['energyCost'] = energy
    o['description'] = desc
    o['effects'] = effects
    return o


def passive(pid, name, desc, trigger, effects, **kw):
    o = O([('id', pid), ('name', name), ('description', desc), ('trigger', trigger)])
    for k, v in kw.items():
        o[k] = v
    o['effects'] = effects
    return o


d = json.loads(io.open(P, encoding='utf-8').read(),
               object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}


def install(hid, basic, active, passive1, overflow=None):
    h = H[hid]
    for key in ('basic', 'active', 'passive', 'passive1', 'passive2', 'skills', 'passives',
                'talents'):
        h.pop(key, None)
    h['basic'] = basic
    h['active'] = active
    h['passive1'] = passive1
    if overflow:
        h['passives'] = overflow


# =====================================================================================
# CELEN - Warden, Tide. Nothing stays in one hand: Energy downhill, wards sideways.
# =====================================================================================
install(
    'celen_the_open_hand',
    ability('celen_offered_strike', 'Offered Strike',
            'Deals 150% Attack damage and gives the lowest-Energy ally 22 Energy.',
            [e(kind='Damage', target='FrontEnemy', power=1.5),
             e(kind='EnergyGain', target='LowestEnergyAlly', flat=22)]),
    ability('celen_distribution', 'Distribution',
            'Deals 245% Attack damage to all enemies, then transfers the largest ally Shield '
            'to the lowest-HP ally and extends it 1 round.',
            [e(kind='Damage', target='AllEnemies', power=2.45),
             # In to Celen, then out to the ally. ShieldTransfer moves a pool between the
             # caster and its target and has no third field, so the donor and the recipient
             # cannot both be named in one effect.
             e(kind='ShieldTransfer', target='LargestShieldAlly', magnitude=1.0,
               transferFromTarget=True),
             e(kind='ShieldTransfer', target='LowestHpAlly', magnitude=1.0),
             e(kind='ExtendStatus', target='LowestHpAlly', status='Shield', duration=1)],
            energy=100),
    passive('celen_nothing_hoarded', 'Nothing Hoarded',
            'At the start of his turn, Celen transfers 18 Energy from the highest-Energy ally '
            'to the lowest.',
            'TurnStart',
            [e(kind='EnergyGain', target='HighestEnergyAlly', flat=-18),
             e(kind='EnergyGain', target='LowestEnergyAlly', flat=18)],
            oncePerBattle=False))

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('authored celen_the_open_hand')
