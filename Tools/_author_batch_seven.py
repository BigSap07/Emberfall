"""Scratch authoring script for batch seven. Not part of the build.

Writes Kiva, Sylven, Merek and Oris into Assets/Resources/Content/heroes.json, and
re-authors the two clauses Zella's shipped passive2 has been missing since batch five.
Stat blocks, visuals and every other hero are left exactly as they are.

Passive slot order matters and is used deliberately. UnitDef.ReadPassives appends in
this order, and TalentOverrideDef / TalentAppendDef can only address the first two:

    Passives[0] = flat "passive"           -> addressable as "passive1"
    Passives[1] = flat "passive1"          -> addressable as "passive2"
    Passives[2] = flat "passive2"          -> not addressable
    Passives[3] = skills.passive1          -> not addressable
    Passives[4] = skills.passive2          -> not addressable

So anything a talent has to append to lives in flat "passive1".

TEN SUB-CLAUSES ARE DELIBERATELY NOT AUTHORED, each blocked on a gap left unbuilt by
decision. They are listed at the foot of this file rather than approximated into the
content.
"""
import json, io, collections

P = r'D:\Emberfall\Game\Assets\Resources\Content\heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}

O = collections.OrderedDict
def e(**kw):
    return O(sorted(kw.items(), key=lambda kv: (kv[0] != 'kind', kv[0] != 'target', kv[0])))

def stats(hid, name, *effs, **kw):
    o = O([('id', hid), ('name', name), ('trigger', 'BattleStart')])
    o.update(kw)
    o['effects'] = list(effs)
    return o

def ability(aid, name, effs, energy=None):
    o = O([('id', aid), ('name', name)])
    if energy is not None: o['energyCost'] = energy
    o['effects'] = effs
    return o

def passive(pid, name, trigger, effs, **kw):
    o = O([('id', pid), ('name', name), ('trigger', trigger)])
    o.update(kw)
    o['oncePerBattle'] = kw.pop('oncePerBattle', False)
    o['effects'] = effs
    return o

def talent(tid, name, effects=None, overrides=None, appends=None, **kw):
    o = O([('id', tid), ('name', name)])
    o['trigger'] = kw.pop('trigger', 'None')
    o.update(kw)
    o['effects'] = effects or []
    if overrides: o['overrides'] = overrides
    if appends: o['appends'] = appends
    return o

def ap(abil, eff):
    return O([('ability', abil), ('effect', eff)])

def ov(abil, idx, field, delta):
    return O([('ability', abil), ('effectIndex', idx), ('field', field), ('delta', delta)])

# =====================================================================================
# ZELLA - the two clauses her shipped passive2 has never carried
# =====================================================================================
# "transfer its Residue and recorded action history to the highest-HP enemy FOR 2 ROUNDS.
#  Transferred Residue CANNOT BE EXTENDED." Both halves were absent from the content
# because TransferStatus had no field for either. Residue is the Burn; the Mark carries
# the recorded action history, and the description does not say that one cannot be
# extended, so only the Burn is stamped.
z = H['zella_the_slow_argument']
z['passive2'] = passive('zella_nothing_stays_settled', 'Nothing Stays Settled', 'OnEnemyDefeated', [
    e(kind='TransferStatus', target='DefeatedUnit', status='Burn',
      secondaryTarget='HighestHpEnemy', transferDuration=2, preventExtend=True),
    e(kind='TransferStatus', target='DefeatedUnit', status='Mark',
      secondaryTarget='HighestHpEnemy', transferDuration=2),
])

# =====================================================================================
# KIVA THE FORSAKEN OATH - Striker, Ember
# =====================================================================================
k = H['kiva_the_forsaken_oath']
k['passive'] = stats('kiva_abandoned_support_stats', 'Abandoned Support',
    e(kind='ApplyStatus', target='Self', status='AttackUp', magnitude=0.10, duration=99),
    e(kind='ApplyStatus', target='Self', status='SpeedUp',  magnitude=0.10, duration=99),
    e(kind='ApplyStatus', target='Self', status='Focus',    magnitude=0.08, duration=99))

# Forsaken is a Mark from Kiva and nothing else - every other clause of hers reads it as
# a marker, and the rider it used to carry was taken out of the description because no
# enemy in content can protect another one.
k['basic'] = ability('kiva_cut_the_escort', 'Cut the Escort', [
    e(kind='Damage', target='LowestHpEnemy', power=1.25),
    e(kind='ApplyStatus', target='LowestHpEnemy', status='Mark', duration=2, chance=1),
])

# The active names "a Forsaken enemy" - EnemyWithMyMark. The shield strip and the
# missing-HP follow-up are gated on the body actually carrying a Shield.
k['active'] = ability('kiva_sever_the_return', 'Sever the Return', [
    e(kind='Damage', target='EnemyWithMyMark', power=2.45),
    e(kind='RemoveStatus', target='EnemyWithMyMark', status='Shield', hits=1,
      condition='HasShield', conditionOn='Target'),
    e(kind='Damage', target='EnemyWithMyMark', power=0.0, targetMissingHpRatio=0.10,
      condition='HasShield', conditionOn='Target'),
    # Severance Paid's own clause, on the active as well as on the basic: "when Kiva
    # ATTACKS a Forsaken target" is both of her attacks, and a passive on
    # AfterBasicAttack reaches only one of them.
    e(kind='ApplyStatus', target='EnemyWithMyMark', status='HealSuppression', duration=1,
      chance=1, bypassResist=True, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionValue=1, conditionOn='Caster',
      condition2='LacksStatus', conditionStatus2='HealSuppression', conditionOn2='Target'),
    e(kind='EnergyGain', target='EnemyWithMyMark', flat=-15, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionValue=1, conditionOn='Caster',
      condition2='HasStatus', conditionStatus2='HealSuppression', conditionOn2='Target'),
], energy=100)

# passive1 = the Broken Link store, in the slot talents can append to.
k['passive1'] = passive('kiva_broken_links', 'Abandoned Support', 'OnEnemyBuffGained', [
    e(kind='GainResolve', target='Self', maxStacks=2, duration=99,
      condition='HasMyMark', conditionOn='BroadcastSubject'),
], limit='OncePerRound')

# passive2 = Severance Paid's basic-attack half. perTargetLimit with perTargetPerRound is
# "each target can be affected once per round" exactly.
k['passive2'] = passive('kiva_severance_paid', 'Severance Paid', 'AfterBasicAttack', [
    e(kind='ApplyStatus', target='LastTarget', status='HealSuppression', duration=1,
      chance=1, bypassResist=True, consumesResolve=True,
      condition='HasMyMark', conditionOn='LastTarget',
      condition2='StatusStacksAtLeast', conditionValue2=1, conditionOn2='Caster'),
    e(kind='EnergyGain', target='LastTarget', flat=-15, consumesResolve=True,
      condition='HasMyMark', conditionOn='LastTarget',
      condition2='HasStatus', conditionStatus2='HealSuppression', conditionOn2='LastTarget'),
], perTargetLimit=1, perTargetPerRound=True)

k['talents'] = O([
    # "Prevents the target from gaining Block" is authored as Block held at nothing for
    # Forsaken's duration, which is the same board state by the only route the engine has.
    ('talentI', talent('kiva_no_safe_escort', 'No Safe Escort', appends=[
        ap('basic', e(kind='ApplyStatus', target='LowestHpEnemy', status='BlockDown',
                      magnitude=1.0, duration=2, chance=1, bypassResist=True)),
    ])),
    # OnKill can read the corpse now, which is what this talent needs: it fires only when
    # the body Kiva just killed was the Forsaken one.
    ('talentII', talent('kiva_cut_the_last_line', 'Cut the Last Line', trigger='OnKill',
        oncePerBattle=False, effects=[
        e(kind='ApplyStatus', target='HighestHpEnemy', status='Mark', duration=2, chance=1,
          condition='HasMyMark', conditionOn='Defeated'),
    ])),
    # Two gated Shields, one per Broken Link, then the counter is emptied - the unroll
    # Kael's passive2 and Elyra's passive2 both use.
    ('talentIII', talent('kiva_unclaimed_victory', 'Unclaimed Victory', trigger='OnKill',
        oncePerBattle=False, effects=[
        e(kind='Shield', target='Self', selfHpRatio=0.12, duration=2,
          condition='HasMyMark', conditionOn='Defeated',
          condition2='StatusStacksAtLeast', conditionValue2=1, conditionOn2='Caster'),
        e(kind='Shield', target='Self', selfHpRatio=0.12, duration=2,
          condition='HasMyMark', conditionOn='Defeated',
          condition2='StatusStacksAtLeast', conditionValue2=2, conditionOn2='Caster'),
        e(kind='RemoveStatus', target='Self', status='Resolve', hits=10,
          condition='HasMyMark', conditionOn='Defeated'),
    ])),
])

# =====================================================================================
# SYLVEN THE UNFADING BLADE - Striker, Verdant
# =====================================================================================
s = H['sylven_the_unfading_blade']
s['passive'] = stats('sylven_persistent_edge_stats', 'Persistent Edge',
    e(kind='ApplyStatus', target='Self', status='AttackUp', magnitude=0.10, duration=99),
    e(kind='ApplyStatus', target='Self', status='SpeedUp',  magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='Focus',    magnitude=0.08, duration=99))

# Bleed is a Burn carrying a per-tick record. doTRecord stamps TickRecord on the entry and
# TickDamageOverTime takes the record BEFORE the tick lands, which is the clause.
s['basic'] = ability('sylven_open_wound', 'Open Wound', [
    e(kind='Damage', target='FrontEnemy', power=1.20),
    # bypassResist, the same way Cyrra's and Isolde's Burns are authored: a harmful status
    # written at a full chance still rolls against Effect Resist without it, and the harness
    # measures every enemy in the campaign to say so.
    e(kind='ApplyStatus', target='FrontEnemy', status='Burn', magnitude=0.35, duration=2,
      chance=1, bypassResist=True, doTRecord='LastPositiveStatus'),
])

# Scar Record is a Ledger that auto-releases as damage to its holder when it expires, so
# "when Scar Record expires it deals the stored damage to its target" is the mechanism
# rather than a second effect. The deposit reads what the Bleed removal was worth.
s['active'] = ability('sylven_collect_the_wound', 'Collect the Wound', [
    e(kind='Damage', target='HighestPendingDoTEnemy', power=2.15),
    e(kind='ApplyStatus', target='HighestPendingDoTEnemy', status='Ledger', duration=3,
      chance=1, storedCap=3.0, ledgerAutoRelease=True),
    e(kind='RecordFact', target='HighestPendingDoTEnemy', status='Ledger',
      record='LastPositiveStatus'),
    e(kind='RemoveStatus', target='HighestPendingDoTEnemy', status='Burn', hits=1),
    e(kind='LedgerDeposit', target='HighestPendingDoTEnemy', power=1.0, scaleByLastRemoval=True),
], energy=100)

# passive1 = the transfer off a corpse, in the appendable slot. Both clauses the engine
# gained this pass: the moved entry is re-clocked to 2 rounds and refuses extension.
s['passive1'] = passive('sylven_persistent_edge', 'Persistent Edge', 'OnEnemyDefeated', [
    e(kind='TransferStatus', target='DefeatedUnit', status='Ledger',
      secondaryTarget='HighestHpEnemy', transferDuration=2),
    e(kind='TransferStatus', target='DefeatedUnit', status='Burn',
      secondaryTarget='HighestHpEnemy', transferDuration=2, preventExtend=True),
])

# passive2 = Wounds Remember. StoredStatusMatches compares the record against what the
# body is carrying now, which is the whole clause; the RecordFact after it keeps the
# record current, which is also "if no effect was recorded, the first one it gains is
# treated as the record instead".
s['passive2'] = passive('sylven_wounds_remember', 'Wounds Remember', 'OnEnemyBuffGained', [
    e(kind='Dispel', target='BroadcastSubject', hits=1,
      condition='StoredStatusMatches', conditionStatus='Ledger', conditionOn='BroadcastSubject'),
    e(kind='RemoveStatus', target='BroadcastSubject', status='Ledger', hits=1,
      condition='StoredStatusMatches', conditionStatus='Ledger', conditionOn='BroadcastSubject'),
    e(kind='Damage', target='BroadcastSubject', power=1.0,
      condition='StoredStatusMatches', conditionStatus='Ledger', conditionOn='BroadcastSubject'),
    e(kind='RecordFact', target='BroadcastSubject', status='Ledger',
      record='LastPositiveStatus'),
])

s['talents'] = O([
    ('talentI', talent('sylven_lasting_evidence', 'Lasting Evidence', overrides=[
        ov('active', 1, 'duration', 1),
        ov('active', 1, 'storedCap', 1.2),
    ])),
    ('talentII', talent('sylven_inherited_injury', 'Inherited Injury', appends=[
        ap('passive2', e(kind='EnergyGain', target='HighestHpEnemy', flat=-10)),
    ])),
    ('talentIII', talent('sylven_nothing_heals_cleanly', 'Nothing Heals Cleanly', effects=[])),
])

# =====================================================================================
# MEREK THE SEQUENCE THIEF - Breaker, Gale
# =====================================================================================
m = H['merek_the_sequence_thief']
m['passive'] = stats('merek_unsettled_timing_stats', 'Unsettled Timing',
    e(kind='ApplyStatus', target='Self', status='AttackUp',      magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='EffectHitUp',   magnitude=0.10, duration=99),
    e(kind='ApplyStatus', target='Self', status='SkillDamageUp', magnitude=0.08, duration=99))

m['basic'] = ability('merek_interjecting_clause', 'Interjecting Clause', [
    e(kind='Damage', target='HighestEnergyEnemy', power=1.15),
    e(kind='ApplyStatus', target='HighestEnergyEnemy', status='Mark', duration=2, chance=1),
])

m['active'] = ability('merek_stolen_interval', 'Stolen Interval', [
    e(kind='Damage', target='AllEnemies', power=1.90),
    e(kind='SwapTurnOrder', target='FrontEnemy'),
], energy=100)

# passive1 = Anticipated's answer, in the appendable slot. OnEnemyActive reaches the
# caster; HasMyMark on the broadcast subject is "an Anticipated enemy".
m['passive1'] = passive('merek_anticipated', 'Interjecting Clause (Anticipated)', 'OnEnemyActive', [
    e(kind='TurnShift', target='BroadcastSubject', magnitude=-0.10,
      condition='HasMyMark', conditionOn='BroadcastSubject'),
])

# passive2 = First Claim's designation.
m['passive2'] = passive('merek_first_claim', 'Priority Inversion', 'TurnStart', [
    e(kind='ApplyStatus', target='HighestSpeedEnemy', status='MarkAlt', duration=1, chance=1),
])

# and its payout, which needs the other trigger.
m['skills'] = O([('passive1', passive('merek_priority_inversion', 'Priority Inversion',
    'OnEnemyActive', [
    e(kind='TurnShift', target='LowestEnergyAlly', magnitude=0.20,
      condition='HasMyMark', conditionStatus='MarkAlt', conditionOn='BroadcastSubject'),
], limit='OncePerRound'))])

m['talents'] = O([
    ('talentI', talent('merek_borrowed_minute', 'Borrowed Minute', appends=[
        ap('passive2', e(kind='EnergyGain', target='Self', flat=5,
                         condition='HasMyMark', conditionOn='BroadcastSubject')),
        ap('passive2', e(kind='TurnShift', target='BroadcastSubject', magnitude=-0.08,
                         condition='HasMyMark', conditionOn='BroadcastSubject')),
    ])),
    ('talentII', talent('merek_reordered_agenda', 'Reordered Agenda', effects=[])),
    # "Defeated before acting" is SubjectHasNotActedThisRound on the corpse; the basic is
    # unrolled into the talent, which is how Marrow's talent III says "immediately uses
    # Witnessed Strike".
    ('talentIII', talent('merek_no_first_move', 'No First Move', trigger='OnEnemyDefeated',
        oncePerBattle=False, effects=[
        e(kind='EnergyGain', target='AllEnemies', flat=-12,
          condition='HasMyMark', conditionStatus='MarkAlt', conditionOn='Defeated',
          condition2='SubjectHasNotActedThisRound', conditionOn2='Defeated'),
        e(kind='Damage', target='HighestEnergyEnemy', power=1.15,
          condition='HasMyMark', conditionStatus='MarkAlt', conditionOn='Defeated',
          condition2='SubjectHasNotActedThisRound', conditionOn2='Defeated'),
        e(kind='ApplyStatus', target='HighestEnergyEnemy', status='Mark', duration=2, chance=1,
          condition='HasMyMark', conditionStatus='MarkAlt', conditionOn='Defeated',
          condition2='SubjectHasNotActedThisRound', conditionOn2='Defeated'),
    ])),
])

# =====================================================================================
# ORIS THE UNWRITTEN PATH - Tinkerer, Gale
# =====================================================================================
o = H['oris_the_unwritten_path']
o['passive'] = stats('oris_restless_hands_stats', 'Restless Hands',
    e(kind='ApplyStatus', target='Self', status='SpeedUp',       magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='EffectHitUp',   magnitude=0.06, duration=99),
    e(kind='ApplyStatus', target='Self', status='SkillDamageUp', magnitude=0.06, duration=99))

# THE BASIC CARRIES THREE SHAPES AND THE ORDER IS THE MECHANISM.
#
# Effects 0-1 are No Fixed Refrain's SECOND altered attack, placed first so that the
# first altered attack (effects 3-5) cannot arm and fire it in the same turn: step two
# reads a marker that step one has not applied yet when the turn begins.
#
# Effects 6-8 are the ordinary attack, split in two by Restless Hands: a Mark on Oris is
# the "retarget pending" flag, set by passive1, and the two blows are gated on having it
# and on lacking it, so exactly one of them lands.
o['basic'] = ability('oris_unfinished_note', 'Unfinished Note', [
    # --- No Fixed Refrain, second altered attack
    e(kind='Damage', target='LowestEnergyEnemy', power=1.50,
      condition='HasStatus', conditionStatus='MarkAlt', conditionOn='Caster'),
    e(kind='TurnShift', target='Self', magnitude=0.10,
      condition='HasStatus', conditionStatus='MarkAlt', conditionOn='Caster'),
    e(kind='RemoveStatus', target='Self', status='MarkAlt', hits=1,
      condition='HasStatus', conditionStatus='MarkAlt', conditionOn='Caster'),
    # --- No Fixed Refrain, first altered attack
    e(kind='Damage', target='HighestEnergyEnemy', power=1.50,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1, conditionOn='Caster'),
    e(kind='EnergyGain', target='HighestEnergyEnemy', flat=-20,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1, conditionOn='Caster'),
    e(kind='ApplyStatus', target='Self', status='MarkAlt', duration=3, chance=1,
      counterIndex=2, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1, conditionOn='Caster'),
    # --- the ordinary attack, retargeted or not
    e(kind='Damage', target='LowestEnergyEnemy', power=1.25,
      condition='HasStatus', conditionStatus='Mark', conditionOn='Caster'),
    e(kind='Damage', target='HighestSpeedEnemy', power=1.25,
      condition='LacksStatus', conditionStatus='Mark', conditionOn='Caster'),
    e(kind='RemoveStatus', target='Self', status='Mark', hits=1,
      condition='HasStatus', conditionStatus='Mark', conditionOn='Caster'),
    # --- and the speed rider, against whichever body the blow above found
    e(kind='Damage', target='LastTarget', power=0.45,
      condition='CasterStatBelowTarget', comparedStat='Speed', conditionOn='LastTarget'),
    e(kind='EnergyGain', target='LastTarget', flat=-10,
      condition='CasterStatBelowTarget', comparedStat='Speed', conditionOn='LastTarget'),
])

# Disrupted is the whole of Improvise the Way: magnitude is the drain, storedCap the turn
# shift, usesPerRound the "first enemy each round". InterceptDisrupted delays the holder
# and advances the disruptor's next ally, which is the clause's last sentence.
o['active'] = ability('oris_improvise_the_way', 'Improvise the Way', [
    e(kind='Damage', target='AllEnemies', power=2.10),
    e(kind='ApplyStatus', target='AllEnemies', status='Disrupted', magnitude=20.0,
      duration=2, chance=1, bypassResist=True, storedCap=0.15, usesPerRound=1),
    e(kind='GainResolve', target='Self', counterIndex=2, maxStacks=1, duration=3),
], energy=100)

# passive1 = Restless Hands' retarget flag, in the appendable slot.
o['passive1'] = passive('oris_restless_hands', 'Restless Hands', 'OnTurnDelayed', [
    e(kind='ApplyStatus', target='Self', status='Mark', duration=2, chance=1),
])

o['passive2'] = passive('oris_no_fixed_refrain', 'No Fixed Refrain', 'None', [])

o['talents'] = O([
    ('talentI', talent('oris_quicker_fingering', 'Quicker Fingering', overrides=[
        ov('basic', 0, 'power', 0.30),
        ov('basic', 3, 'power', 0.30),
        ov('basic', 1, 'magnitude', 0.05),
    ])),
    ('talentII', talent('oris_change_the_key', 'Change the Key', effects=[])),
    # "The enemy the second altered attack struck" is LastTarget, and the extra blow is
    # appended behind that attack's own gate.
    ('talentIII', talent('oris_open_ending', 'Open Ending', appends=[
        ap('basic', e(kind='Damage', target='LastTarget', power=0.90,
                      condition='HasStatus', conditionStatus='MarkAlt', conditionOn='Caster')),
    ])),
])

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')

print("authored kiva, sylven, merek, oris; re-authored zella's passive2")
for hid in ['kiva_the_forsaken_oath', 'sylven_the_unfading_blade',
            'merek_the_sequence_thief', 'oris_the_unwritten_path']:
    h = H[hid]
    slots = [k for k in ('passive', 'passive1', 'passive2') if isinstance(h.get(k), dict)]
    sk = [k for k in ('passive1', 'passive2') if isinstance((h.get('skills') or {}).get(k), dict)]
    print(f"  {hid}: basic {len(h['basic']['effects'])} eff, active {len(h['active']['effects'])} eff, "
          f"passives {slots} + skills{sk}, talents {list(h['talents'])}")

print("""
NOT AUTHORED - ten sub-clauses, each blocked on a gap left unbuilt by decision:
  kiva/active     "since Kiva's last turn" - no LastHealedRound to match LastDamagedRound (gap 10)
  kiva/active     "up to 130% of Attack"   - MissingHpCap is a fraction of the TARGET's max HP
  merek/active    "the enemy moved backward gains Skill Damage Down" (gap 8)
  merek/talentII  the same clause for the enemy moved forward - the whole talent (gap 8)
  merek/passive1  "35% of the recorded amount" - the realised shift is unreadable (gap 7)
  merek/passive2  "at the start of each round" - authored on his own TurnStart (gap 14)
  merek/basic     OnEnemyActive is raised BEFORE the cast resolves, the text says after
  oris/talentII   both halves gate on absolute Energy - the whole talent (gap 5)
  oris/talentIII  "once every 2 rounds" - a cadence an effect cannot carry
  sylven/talentIII "20% Healing Reduction" - only total HealSuppression exists (gap 6)
  sylven/passive1 "transferred Bleed deals 20% more" - no magnitude change on a move (gap 9)
""")
