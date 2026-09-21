"""Scratch authoring script for the fourth 4-star batch, first half. Not part of the build.

Finn, Niko, Sable, Tamsin and Iolan, in heroes.json order. Same shape as batches one to
three: basic + active + passive1, no talents, no stat-block passive.

NO ENGINE ADDITIONS. Every mechanic already exists; two standing guards grow an occupant.

Where a clause could not be said it is approximated, marked draft in the branch report and
given a row in Docs/ENGINE-GAPS.md under the capability that is missing. Each one is noted
at the kit it belongs to.
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
# FINN - Tinkerer, Gale. Ringing is a Mark and the noise it makes at the wrong moment.
#
# CancelCast rather than EffectKind.Interrupt, and the text is why: Interrupt sets a bar to
# zero all-or-nothing, and Finn's sentence cancels the cast AND takes 20 Energy, which are
# two things. CancelCast is the cast-in-flight family's own effect and Finn joins Dahlia,
# Kest and Maera as a named occupant of it.
#
# DRAFT (gaps 14): "at round end" has no trigger. PassiveTrigger has RoundStart and no
# mirror, so Aftershock Clock answers each Ringing enemy's OWN turn ending instead.
# DRAFT (gaps 15): "a 35% chance" is not expressible. EffectDef.Chance is read in exactly
# one place, the ApplyStatus landing roll, so a TurnShift either happens or does not. The
# clause is made certain and limited to once a round instead.
# =====================================================================================
install(
    'finn_the_bellmaker',
    ability('finn_ringing_slug', 'Ringing Slug',
            'Deals 230% Attack damage and applies Ringing for 2 rounds. A Ringing enemy '
            'loses 5 Energy whenever it casts an active skill.',
            [e(kind='Damage', target='FrontEnemy', power=2.3),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2)]),
    ability('finn_no_quiet_moment', 'No Quiet Moment',
            'Deals 400% Attack damage to enemies with Ringing, cancels a cast in flight and '
            'takes 20 Energy off every Ringing enemy.',
            [e(kind='Damage', target='AllEnemies', power=4.0,
               condition='HasMyMark', conditionOn='Target'),
             # Reaches only the body whose cast is in flight and only while it is; against
             # AllEnemies that is whichever one is mid-cast, and nothing at all otherwise.
             e(kind='CancelCast', target='AllEnemies'),
             e(kind='EnergyGain', target='AllEnemies', flat=-20,
               condition='HasMyMark', conditionOn='Target')],
            energy=100),
    passive('finn_aftershock_clock', 'Aftershock Clock',
            'The first time each round a Ringing enemy ends its turn, it is delayed by 15% '
            'turn progress.',
            # BroadcastSource, not BroadcastSubject: OnEnemyTurnEnd names the same enemy in
            # both, and only BroadcastSource resolves before the Taunt anchor. Demos's
            # Arrears tick paid for that lesson.
            'OnEnemyTurnEnd',
            [e(kind='TurnShift', target='BroadcastSource', magnitude=-0.15,
               condition='HasMyMark', conditionOn='BroadcastSource')],
            oncePerBattle=False, limit='OncePerRound'),
    overflow=[
        passive('finn_ringing_toll', 'Ringing Slug',
                'The toll itself: a Ringing enemy loses 5 Energy as it casts.',
                'OnEnemyActive',
                [e(kind='EnergyGain', target='BroadcastSubject', flat=-5,
                   condition='HasMyMark', conditionOn='BroadcastSubject')],
                oncePerBattle=False),
    ])

# =====================================================================================
# NIKO - Stalker, Gale. Flustered is a Mark and the Effect Hit it costs.
#
# DRAFT (gaps 16): "forces its next Basic Attack onto a random ally" is Provoked, which
# constrains one attacker onto whoever APPLIED it and nobody else. Niko cannot hand the
# blow to somebody else, so it lands on him.
# DRAFT (gaps 9): "when a Flustered enemy attempts a debuff" has no trigger. Nothing fires
# when a unit APPLIES a status, and there is no ally-side broadcast for one being received,
# so Ruin the Agenda answers the enemy's active cast, which is when the debuffs come.
# =====================================================================================
install(
    'niko_the_uninvited_guest',
    ability('niko_bad_entrance', 'Bad Entrance',
            'Deals 175% Attack damage and applies Flustered for 2 rounds, reducing Effect '
            'Hit by 15%.',
            [e(kind='Damage', target='FrontEnemy', power=1.75),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='EffectHitDown',
               magnitude=0.15, duration=2, chance=1, bypassResist=True)]),
    ability('niko_take_the_floor', 'Take the Floor',
            'Deals 320% Attack damage to a Flustered enemy and forces its next attack onto '
            'Niko.',
            [e(kind='Damage', target='EnemyWithMyMark', power=3.2),
             e(kind='ApplyStatus', target='EnemyWithMyMark', status='Provoked', duration=1,
               chance=1, bypassResist=True)],
            energy=100),
    passive('niko_ruin_the_agenda', 'Ruin the Agenda',
            'When a Flustered enemy casts an active skill, Flustered is replaced by Silence '
            'for 1 round. Silence prevents active skills.',
            'OnEnemyActive',
            # THE SILENCE LANDS FIRST, AND THE ORDER IS THE WHOLE POINT. Every effect here
            # gates on HasMyMark, so stripping the Mark before the others run leaves them
            # reading a body they no longer recognise - the appended-effect defect five
            # talents were repaired for. Apply, then strip what is being replaced, then
            # strip the Mark last.
            [e(kind='ApplyStatus', target='BroadcastSubject', status='Silence', duration=1,
               chance=1, bypassResist=True,
               condition='HasMyMark', conditionOn='BroadcastSubject'),
             e(kind='RemoveStatus', target='BroadcastSubject', status='EffectHitDown', hits=1,
               condition='HasMyMark', conditionOn='BroadcastSubject'),
             e(kind='RemoveStatus', target='BroadcastSubject', status='Mark', hits=1,
               condition='HasMyMark', conditionOn='BroadcastSubject')],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# SABLE - Vanguard, Gale. Every clause is turn order, in both directions.
#
# OnTurnDelayed fires on the unit that CAUSED the delay, which is exactly what Never
# Waiting wants: her own active delays, and the passive pays her. The "next Basic Attack"
# half is a MarkAlt she puts on herself and her own basic reads and spends, because a flat
# AttackUp would raise everything she does for a round rather than one blow.
# =====================================================================================
install(
    'sable_the_queue_cutter',
    ability('sable_cut_in', 'Cut In',
            'Deals 130% Attack damage and advances Sable’s next turn by 10%. If Never '
            'Waiting is banked, deals 30% additional Attack damage and spends it.',
            [e(kind='Damage', target='FrontEnemy', power=1.3),
             e(kind='Damage', target='LastTarget', power=0.3,
               condition='HasMyMark', conditionStatus='MarkAlt', conditionOn='Caster'),
             e(kind='RemoveStatus', target='Self', status='MarkAlt', hits=1),
             e(kind='TurnShift', target='Self', magnitude=0.10)]),
    ability('sable_wrong_order', 'Wrong Order',
            'Deals 215% Attack damage to all enemies and delays the highest-Energy enemy by '
            '25% turn progress.',
            [e(kind='Damage', target='AllEnemies', power=2.15),
             e(kind='TurnShift', target='HighestEnergyEnemy', magnitude=-0.25)],
            energy=100),
    passive('sable_never_waiting', 'Never Waiting',
            'Whenever Sable delays an enemy, she gains 12 Energy and banks 30% Attack damage '
            'onto her next Basic Attack.',
            'OnTurnDelayed',
            [e(kind='EnergyGain', target='Self', flat=12),
             e(kind='ApplyStatus', target='Self', status='MarkAlt', duration=2, chance=1)],
            oncePerBattle=False))

# =====================================================================================
# TAMSIN - Breaker, Gale. Pull the thread and the order comes apart.
#
# DRAFT (gaps 17): "6% for each target buff, up to 18%" has no count of how many buffs a
# body carries. HasAnyBeneficialStatus answers whether it has one and not how many, so the
# clause is 6% flat and 18% against a buffed body.
# DRAFT (gaps 4): "take 15% more Skill Damage" is per-source here. Vulnerable is the one
# per-source entry in the game and reads only Tamsin's own damage; a universal Skill Damage
# taken modifier does not exist.
# DRAFT (gaps 18): "the first enemy delayed each round" cannot be named. OnTurnDelayed sets
# the broadcast SOURCE to the delayer and leaves the subject unset - the enum's own summary
# says an ability wanting the delayed unit to suffer something should carry that beside the
# shift - so Unraveling Order lands on the whole line once a round instead.
# =====================================================================================
install(
    'tamsin_the_loose_thread',
    ability('tamsin_snag', 'Snag',
            'Deals 135% Attack damage and removes 6% turn progress, or 18% from a buffed '
            'target.',
            [e(kind='Damage', target='FrontEnemy', power=1.35),
             e(kind='TurnShift', target='LastTarget', magnitude=-0.06),
             e(kind='TurnShift', target='LastTarget', magnitude=-0.12,
               condition='HasAnyBeneficialStatus', conditionOn='Target')]),
    ability('tamsin_pull_the_seam', 'Pull the Seam',
            'Deals 215% Attack damage to all enemies. Targets above 50% turn progress are '
            'delayed 20% and take 15% more damage from Tamsin for 2 rounds.',
            [e(kind='Damage', target='AllEnemies', power=2.15),
             e(kind='TurnShift', target='AllEnemies', magnitude=-0.20,
               condition='TurnProgressAbove', conditionValue=0.5, conditionOn='Target'),
             e(kind='ApplyStatus', target='AllEnemies', status='Vulnerable', magnitude=0.15,
               duration=2, chance=1, bypassResist=True,
               condition='TurnProgressAbove', conditionValue=0.5, conditionOn='Target')],
            energy=100),
    passive('tamsin_unraveling_order', 'Unraveling Order',
            'The first time each round Tamsin delays an enemy, every enemy gains 10% Defense '
            'Down for 2 rounds.',
            'OnTurnDelayed',
            [e(kind='ApplyStatus', target='AllEnemies', status='DefenseDown', magnitude=0.10,
               duration=2, chance=1, bypassResist=True)],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# IOLAN - Binder, Umbra. Unlisted is a Mark and the bar it will not let fill.
#
# Disrupted is what "cannot gain Energy" is in this engine: it intercepts the first energy
# gain each round and drains instead of granting.
#
# DRAFT (gaps 19): "if none is removed, applies 10% Effect Hit Down" needs the negation of
# HasAnyBeneficialStatus and there is none, so the rider lands whether or not the Dispel
# found something.
# =====================================================================================
install(
    'iolan_the_redacted_judge',
    ability('iolan_strike_the_line', 'Strike the Line',
            'Deals 160% Attack damage, removes one buff from the target and applies 10% '
            'Effect Hit Down for 2 rounds.',
            [e(kind='Damage', target='FrontEnemy', power=1.6),
             e(kind='Dispel', target='LastTarget', hits=1),
             e(kind='ApplyStatus', target='LastTarget', status='EffectHitDown',
               magnitude=0.10, duration=2, chance=1, bypassResist=True)]),
    ability('iolan_record_withheld', 'Record Withheld',
            'Deals 260% Attack damage to all enemies and applies Unlisted for 1 round. An '
            'Unlisted enemy loses 8 Energy instead of gaining it, once that round.',
            [e(kind='Damage', target='AllEnemies', power=2.6),
             e(kind='ApplyStatus', target='AllEnemies', status='Mark', duration=1),
             e(kind='ApplyStatus', target='AllEnemies', status='Disrupted', magnitude=8,
               duration=1, chance=1, bypassResist=True, usesPerRound=1)],
            energy=100),
    passive('iolan_missing_precedent', 'Missing Precedent',
            'When an Unlisted enemy finishes casting an active skill, one of its buffs is '
            'struck from the record.',
            'OnEnemyActiveResolved',
            [e(kind='Dispel', target='BroadcastSubject', hits=1,
               condition='HasMyMark', conditionOn='BroadcastSubject')],
            oncePerBattle=False))

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('authored 5 four-star kits: finn, niko, sable, tamsin, iolan')
