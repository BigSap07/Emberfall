"""Scratch authoring script for batch eight. Not part of the build.

Authors the eleven sub-clauses batch seven listed as blocked, now that the gaps they
needed exist. Surgical rather than a rebuild: each hero's other clauses are left exactly
as batch seven wrote them, so the diff is the eleven and nothing else.
"""
import json, io, collections

P = r'D:\Emberfall\Game\Assets\Resources\Content\heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}

O = collections.OrderedDict
def e(**kw):
    return O(sorted(kw.items(), key=lambda kv: (kv[0] != 'kind', kv[0] != 'target', kv[0])))
def ap(abil, eff):
    return O([('ability', abil), ('effect', eff)])

done = []

# =====================================================================================
# KIVA - "if it gained a Shield or was healed since Kiva's last turn", and the cap
# =====================================================================================
k = H['kiva_the_forsaken_oath']
act = k['active']['effects']
# effects 1 and 2 are the strip and the missing-HP follow-up, both gated on HasShield.
# The gate becomes the clause: shielded OR healed since her last turn. (c1 AND c2) OR c3
# with c2 absent is c1 OR c3, which is the shape the engine's three slots already have.
for i in (1, 2):
    act[i].pop('condition', None); act[i].pop('conditionOn', None)
    act[i]['condition'] = 'SubjectShieldedSinceMyTurn'
    act[i]['conditionOn'] = 'Target'
    act[i]['condition3'] = 'SubjectHealedSinceMyTurn'
    act[i]['conditionOn3'] = 'Target'
# "up to 130% of Attack" - a ceiling in her own currency, which MissingHpCap could not be.
act[2]['attackCap'] = 1.30
done.append('kiva/active: the freshness gate is shielded-or-healed since her turn, and the '
            'missing-HP follow-up is capped at 130% of Attack')

# =====================================================================================
# MEREK - both swap riders, the recorded-progress payout, the round boundary, the timing
# =====================================================================================
m = H['merek_the_sequence_thief']

# "The enemy moved backward gains 15% Skill Damage Down for 2 rounds."
m['active']['effects'].append(
    e(kind='ApplyStatus', target='SwapDelayedUnit', status='SkillDamageDown',
      magnitude=0.15, duration=2, chance=1, bypassResist=True))
done.append('merek/active: the body the swap moved BACK takes Skill Damage Down')

# talent II: the same clause for the other body. The whole talent, which was empty.
m['talents']['talentII'] = O([
    ('id', 'merek_reordered_agenda'), ('name', 'Reordered Agenda'), ('trigger', 'None'),
    ('effects', []),
    ('appends', [ap('active', e(kind='ApplyStatus', target='SwapAdvancedUnit',
                                status='DefenseDown', magnitude=0.10, duration=2,
                                chance=1, bypassResist=True))]),
])
done.append('merek/talentII: the body the swap moved FORWARD takes Defense Down - the talent '
            'was authored as nothing')

# "The first time each round an enemy loses turn progress, Merek records the amount lost.
#  His next Basic Attack deals additional damage equal to 35% of the recorded amount as a
#  percentage of Attack, up to 70%." The record is the engine's now; the counter is the
#  "next Basic Attack" half, taken once a round and spent by the blow.
m['skills']['passive2'] = O([
    ('id', 'merek_unsettled_timing'), ('name', 'Unsettled Timing'), ('trigger', 'OnTurnDelayed'),
    ('limit', 'OncePerRound'), ('oncePerBattle', False),
    ('effects', [e(kind='GainResolve', target='Self', counterIndex=2, maxStacks=1, duration=99)]),
])
m['basic']['effects'].append(
    e(kind='Damage', target='HighestEnergyEnemy', power=0.35, scaleByLastShift=True,
      attackCap=0.70, counterIndex=2, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1,
      conditionOn='Caster'))
done.append('merek/passive1: the recorded turn progress is readable, so the next Basic Attack '
            'pays 35% of it against Attack, capped at 70%')

# "At the start of each round" is the round's own boundary, not his turn.
m['passive2']['trigger'] = 'RoundStart'
done.append('merek/passive2: First Claim is designated at the start of the ROUND')

# "it is delayed by 10% turn progress AFTER the skill resolves"
m['passive1']['trigger'] = 'OnEnemyActiveResolved'
done.append('merek/basic: Anticipated answers the far end of the cast, which is what its text says')

# =====================================================================================
# ORIS - the talent that gated on absolute Energy, and the one that needed a cadence
# =====================================================================================
o = H['oris_the_unwritten_path']

# "more than 70" and "below 30" are EnergyAtLeast 71 and EnergyBelow 29: both conditions
# are inclusive at the boundary, matching HpBelow/HpAbove one file over.
o['talents']['talentII'] = O([
    ('id', 'oris_change_the_key'), ('name', 'Change the Key'), ('trigger', 'None'),
    ('effects', []),
    ('appends', [
        ap('active', e(kind='EnergyGain', target='AllEnemies', flat=-15,
                       condition='EnergyAtLeast', conditionValue=71)),
        ap('active', e(kind='EnergyGain', target='Self', flat=10,
                       condition='EnergyBelow', conditionValue=29, conditionOn='Target')),
    ]),
])
done.append('oris/talentII: both halves gate on absolute Energy - the talent was authored as nothing')

# "This can occur once every 2 rounds" is a cadence, which a passive carries and an effect
# does not. The talent appends the marker its own trigger reads, and carries the limit.
o['talents']['talentIII'] = O([
    ('id', 'oris_open_ending'), ('name', 'Open Ending'), ('trigger', 'AfterBasicAttack'),
    ('limit', 'EveryNRounds'), ('limitRounds', 2), ('oncePerBattle', False),
    ('effects', [
        e(kind='Damage', target='LastTarget', power=0.90, counterIndex=3, consumesResolve=True,
          condition='StatusStacksAtLeast', conditionCounterIndex=3, conditionValue=1,
          conditionOn='Caster'),
    ]),
    ('appends', [
        ap('basic', e(kind='GainResolve', target='Self', counterIndex=3, maxStacks=1, duration=2,
                      condition='HasStatus', conditionStatus='MarkAlt', conditionOn='Caster')),
    ]),
])
done.append('oris/talentIII: the extra blow now carries its once-every-2-rounds cadence, on a '
            'passive limit rather than on an effect')

# =====================================================================================
# SYLVEN - the healing reduction and the resized transfer
# =====================================================================================
s = H['sylven_the_unfading_blade']

# "When Wounds Remember removes a matching effect, the target receives 20% Healing
# Reduction for 2 rounds" - same gate as the removal it answers.
s['talents']['talentIII'] = O([
    ('id', 'sylven_nothing_heals_cleanly'), ('name', 'Nothing Heals Cleanly'), ('trigger', 'None'),
    ('effects', []),
    ('appends', [ap('passive2', e(kind='ApplyStatus', target='BroadcastSubject',
                                  status='HealingReduction', magnitude=0.20, duration=2,
                                  chance=1, bypassResist=True,
                                  condition='StoredStatusMatches', conditionStatus='Ledger',
                                  conditionOn='BroadcastSubject'))]),
])
done.append('sylven/talentIII: 20% Healing Reduction, the partial sibling of HealSuppression')

# "Transferred Bleed deals 20% more damage but cannot be extended." The second half was
# built last batch; this is the first.
for eff in s['passive1']['effects']:
    if eff.get('status') == 'Burn':
        eff['transferMagnitude'] = 1.2
done.append('sylven/passive1: the transferred Bleed is resized by 20% as it lands')

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print("authored the eleven sub-clauses batch seven left blocked:\n")
for line in done:
    print("  - " + line)
