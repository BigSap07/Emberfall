"""Scratch authoring script for the four linked heroes. Not part of the build.

Writes basic, active, both passives and all three talents for Talren, Vaelis, Nysera
and Rovan into Assets/Resources/Content/heroes.json, replacing their placeholders.
Stat blocks, visuals and every other hero are left exactly as they are.

Passive slot order matters and is used deliberately. UnitDef.ReadPassives appends in
this order, and TalentOverrideDef / TalentAppendDef can only address the first two:

    Passives[0] = flat "passive"           -> addressable as "passive1"
    Passives[1] = flat "passive1"          -> addressable as "passive2"
    Passives[2] = flat "passive2"          -> not addressable
    Passives[3] = skills.passive1          -> not addressable
    Passives[4] = skills.passive2          -> not addressable

So anything a talent has to append to lives in flat "passive1".
"""
import json, io, collections

P = r'D:\Emberfall\Game\Assets\Resources\Content\heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}

O = collections.OrderedDict
def e(**kw):
    return O(sorted(kw.items(), key=lambda kv: (kv[0] != 'kind', kv[0] != 'target', kv[0])))

BULWARK, STRIKER, BREAKER, MENDER, WARDEN, STALKER, VANGUARD, BINDER, MARKSMAN, TINKERER = range(10)

def stats(hid, name, *effs, **kw):
    """The BattleStart half of a described passive.

    NAMED AFTER THE PASSIVE IT IS HALF OF, not "Stats". Every split passive in the
    roster does this - Nereia's two halves are both "Triage Buffer", Marrow's are both
    "The Cost Is Named" - because a player reading a hero screen is reading one passive
    that happens to need two triggers. `kw` carries the output/stat modifier when the
    described passive has one, so a modifier needs no shell passive of its own.
    """
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

# =====================================================================================
# TALREN THE WAYFINDER - Marksman, Tide
# =====================================================================================
t = H['talren_the_wayfinder']
t['passive'] = stats('talren_adaptive_path_stats', 'Adaptive Path',
    e(kind='ApplyStatus', target='Self', status='AttackUp',   magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='SpeedUp',    magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='Focus',      magnitude=0.08, duration=99))

# Basic. The echo's guard is SubjectNotDamagedByMeThisRound on the echo's own target:
# if the lowest-HP enemy IS the one the opening blow hit, no other enemy has lower HP.
t['basic'] = ability('talren_redirecting_shot', 'Redirecting Shot', [
    e(kind='Damage', target='HighestAttackEnemy', power=1.2),
    # Route, and the Crit Damage that rides with it. conditionOn LastTarget resolves to the
    # body the blow above just landed on; SubjectIsNotMyPreviousTarget compares it against
    # the body before that.
    e(kind='GainResolve', target='Self', maxStacks=3, duration=99,
      condition='SubjectIsNotMyPreviousTarget', conditionOn='LastTarget'),
    e(kind='ApplyStatus', target='Self', status='CritDamageUp', magnitude=0.06, duration=99,
      stackOnReapply=True, maxStacks=3,
      condition='SubjectIsNotMyPreviousTarget', conditionOn='LastTarget'),
    e(kind='EchoDamage', target='LowestHpEnemy', power=0.4,
      condition='SubjectNotDamagedByMeThisRound'),
])

# Active. Two one-charge links on Talren carry the trigger; two MarkAlt entries carry the
# record itself, because a link is spent when it answers and talent III still has to name
# both recorded bodies afterwards.
t['active'] = ability('talren_change_the_course', 'Change the Course', [
    e(kind='Damage', target='HighestAttackEnemy', power=2.2),
    e(kind='Damage', target='LowestHpEnemy', power=1.3),
    e(kind='ApplyStatus', target='HighestAttackEnemy', status='MarkAlt', duration=2, chance=1),
    e(kind='ApplyStatus', target='LowestHpEnemy', status='MarkAlt', duration=2, chance=1),
    e(kind='LinkUnits', target='Self', secondaryTarget='HighestAttackEnemy',
      linkTransfer='None', magnitude=1.0, charges=1, duration=2),
    e(kind='LinkUnits', target='Self', secondaryTarget='LowestHpEnemy',
      linkTransfer='None', magnitude=1.0, charges=1, duration=2),
], energy=100)

# passive1 = Where the Arrow Lands, the Redirecting Shot half. OnEchoFired reaches the
# caster of an EchoDamage; BroadcastVictim is the body the echo struck.
t['passive1'] = passive('talren_where_the_arrow_lands', 'Where the Arrow Lands', 'OnEchoFired', [
    e(kind='EnergyGain', target='Self', flat=8),
    e(kind='Heal', target='Self', power=0.35),
    e(kind='Damage', target='BroadcastVictim', power=0.5, condition='HpBelow', conditionValue=0.3),
])

# passive2 = the recorded strike, and the same Where the Arrow Lands payout for the
# Change the Course half. triggerSourceIsSelf is "attacked by Talren"; the dispatch spends
# the answering link's charge, which is "the next time".
t['passive2'] = passive('talren_recorded_strike', 'Change the Course (record)', 'OnLinkedUnitHit', [
    e(kind='Damage', target='OtherLinkedUnit', power=0.7),
    e(kind='Damage', target='OtherLinkedUnit', power=0.5, condition='HpBelow', conditionValue=0.3),
    e(kind='EnergyGain', target='Self', flat=8),
    e(kind='Heal', target='Self', power=0.35),
    e(kind='GainResolve', target='Self', counterIndex=2, maxStacks=2, duration=99),
], triggerSourceIsSelf=True)

t.pop('skills', None)
t['talents'] = O([
    ('talentI', O([('id', 'talren_open_route'), ('name', 'Open Route'), ('trigger', 'None'),
        ('effects', []),
        ('appends', [
            O([('ability', 'basic'), ('effect', e(kind='Damage', target='HighestAttackEnemy', power=0.7,
                condition='StatusStacksAtLeast', conditionStatus='Resolve', conditionValue=3,
                conditionOn='Caster',
                condition2='SubjectIsNotMyPreviousTarget', conditionOn2='Target',
                consumesResolve=True))]),
            # Crit Damage is kept in step with Route by removing it exactly when Route is gone.
            O([('ability', 'basic'), ('effect', e(kind='RemoveStatus', target='Self',
                status='CritDamageUp', hits=10,
                condition='LacksStatus', conditionStatus='Resolve', conditionOn='Caster'))]),
        ])])),
    ('talentII', O([('id', 'talren_dual_record'), ('name', 'Dual Record'), ('trigger', 'None'),
        ('effects', []),
        ('appends', [
            O([('ability', 'active'), ('effect', e(kind='ApplyStatus', target='HighestHpEnemy',
                status='MarkAlt', duration=2, chance=1))]),
            O([('ability', 'active'), ('effect', e(kind='LinkUnits', target='Self',
                secondaryTarget='HighestHpEnemy', linkTransfer='None', magnitude=1.0,
                charges=1, duration=2))]),
        ])])),
    # Second trigger of the record: OnStackThreshold on the counter passive2 banks, which
    # consumes the two stacks as it fires. 35% per Route is three gated effects, the shape
    # Kael's Rubble already uses. AllEnemies + HasMyMark is "both recorded targets".
    ('talentIII', passive('talren_final_path', 'Final Path', 'OnStackThreshold', [
        e(kind='Damage', target='AllEnemies', power=0.35,
          condition='StatusStacksAtLeast', conditionStatus='Resolve', conditionValue=1, conditionOn='Caster',
          condition2='HasMyMark', conditionStatus2='MarkAlt', conditionOn2='Target'),
        e(kind='Damage', target='AllEnemies', power=0.35,
          condition='StatusStacksAtLeast', conditionStatus='Resolve', conditionValue=2, conditionOn='Caster',
          condition2='HasMyMark', conditionStatus2='MarkAlt', conditionOn2='Target'),
        e(kind='Damage', target='AllEnemies', power=0.35,
          condition='StatusStacksAtLeast', conditionStatus='Resolve', conditionValue=3, conditionOn='Caster',
          condition2='HasMyMark', conditionStatus2='MarkAlt', conditionOn2='Target'),
        e(kind='RemoveStatus', target='Self', status='Resolve', hits=10,
          condition='StatusStacksAtLeast', conditionStatus='Resolve', conditionValue=1, conditionOn='Caster'),
        e(kind='RemoveStatus', target='Self', status='CritDamageUp', hits=10,
          condition='LacksStatus', conditionStatus='Resolve', conditionOn='Caster'),
    ], counterIndex=2, stackThresholdStatus='Resolve', stackThresholdCount=2)),
])

# =====================================================================================
# VAELIS THE UNFIXED BLADE - Striker, Tide
# =====================================================================================
v = H['vaelis_the_unfixed_blade']
v['passive'] = stats('vaelis_never_still_stats', 'Never Still',
    e(kind='ApplyStatus', target='Self', status='SpeedUp',      magnitude=0.10, duration=99),
    e(kind='ApplyStatus', target='Self', status='Focus',        magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='CritDamageUp', magnitude=0.08, duration=99))

# Basic. Departure is a mutual one-charge link between Vaelis and the body he left: the
# enemy's half makes "a target with Departure" testable, his half carries the trigger.
# PreviousTarget falls back to the front enemy when there is no previous, and
# SubjectIsMyPreviousTarget is false against that fallback, so the clause stays silent.
v['basic'] = ability('vaelis_leaving_cut', 'Leaving Cut', [
    e(kind='Damage', target='LowestHpEnemy', power=1.25),
    e(kind='LinkUnits', target='PreviousTarget', secondaryTarget='Self', linkMutual=True,
      linkTransfer='None', magnitude=1.0, charges=1, duration=2,
      condition='SubjectIsMyPreviousTarget', conditionOn='Target'),
])

# Active. MoveDuration re-clocks the moved buffs to 1 round, which is the clause's own
# wording and what a plain move could not say.
v['active'] = ability('vaelis_passed_position', 'Passed Position', [
    e(kind='Damage', target='HighestAttackEnemy', power=2.2),
    e(kind='Damage', target='LowestHpEnemy', power=1.5),
    e(kind='MoveBuffs', target='HighestAttackEnemy', secondaryTarget='LowestHpEnemy',
      hits=10, moveDuration=1, condition='HasStatus', conditionStatus='Linked'),
    e(kind='MoveBuffs', target='LowestHpEnemy', secondaryTarget='HighestAttackEnemy',
      hits=10, moveDuration=1, condition='HasStatus', conditionStatus='Linked'),
], energy=100)

# passive1 = the Departure trigger, in the slot talents can append to ("passive2").
# triggerSourceIsAlly is "the next ALLIED hit"; the dispatch spends the charge, which is
# "the NEXT". LastTarget is "Vaelis's current target". The three role counters are Never
# Still's record, banked with SubjectRoleIs against the ally that landed the blow.
v['passive1'] = passive('vaelis_departure', 'Departure', 'OnLinkedUnitHit', [
    e(kind='Damage', target='LastTarget', power=0.45),
    e(kind='GainResolve', target='Self', counterIndex=2, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=BULWARK, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=2, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=VANGUARD, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=3, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=MENDER, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=3, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=WARDEN, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=4, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=STRIKER, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=4, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=BREAKER, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=4, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=STALKER, conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', counterIndex=4, maxStacks=1, duration=99,
      condition='SubjectRoleIs', conditionValue=MARKSMAN, conditionOn='BroadcastSource'),
], triggerSourceIsAlly=True)

v['passive2'] = passive('vaelis_opening_moves', 'Opening Moves', 'OnEnemyDefeated', [
    e(kind='LinkUnits', target='HighestHpEnemy', secondaryTarget='Self', linkMutual=True,
      linkTransfer='None', magnitude=1.0, charges=1, duration=2,
      condition='HasStatus', conditionStatus='Linked', conditionOn='Defeated'),
    e(kind='TurnShift', target='Self', magnitude=0.15,
      condition='HasStatus', conditionStatus='Linked', conditionOn='Defeated'),
], limit='OncePerRound')

# Never Still's payout rides on the basic: the banked role counter decides which bonus the
# next Basic Attack against a different target carries, and spends itself. There is no shell
# passive for it - the BattleStart half already carries the name a player reads.
v.pop('skills', None)

for eff in [
    e(kind='ApplyStatus', target='Self', status='ArmorBreakUp', magnitude=0.10, duration=1,
      counterIndex=2, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1, conditionOn='Caster',
      condition2='SubjectIsNotMyPreviousTarget', conditionOn2='LastTarget'),
    e(kind='Heal', target='Self', power=0.50,
      counterIndex=3, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionCounterIndex=3, conditionValue=1, conditionOn='Caster',
      condition2='SubjectIsNotMyPreviousTarget', conditionOn2='LastTarget'),
    e(kind='Damage', target='LastTarget', power=0.35,
      counterIndex=4, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionCounterIndex=4, conditionValue=1, conditionOn='Caster',
      condition2='SubjectIsNotMyPreviousTarget', conditionOn2='LastTarget'),
]:
    v['basic']['effects'].append(eff)

v['talents'] = O([
    ('talentI', O([('id', 'vaelis_false_exit'), ('name', 'False Exit'), ('trigger', 'None'),
        ('effects', []),
        ('appends', [
            O([('ability', 'basic'), ('effect', e(kind='ApplyStatus', target='PreviousTarget',
                status='EffectResistDown', magnitude=0.10, duration=2, chance=1, bypassResist=True,
                condition='SubjectIsMyPreviousTarget', conditionOn='Target'))]),
            # "until it triggers" - removed by the Departure trigger itself.
            O([('ability', 'passive2'), ('effect', e(kind='RemoveStatus', target='BroadcastSubject',
                status='EffectResistDown', hits=1))]),
        ])])),
    ('talentII', O([('id', 'vaelis_stolen_position'), ('name', 'Stolen Position'), ('trigger', 'None'),
        ('effects', []),
        ('overrides', [
            O([('ability', 'active'), ('effectIndex', 2), ('field', 'hits'), ('delta', -8)]),
            O([('ability', 'active'), ('effectIndex', 3), ('field', 'hits'), ('delta', -8)]),
        ])])),
    ('talentIII', O([('id', 'vaelis_no_place_to_hold'), ('name', 'No Place to Hold'), ('trigger', 'None'),
        ('effects', []),
        ('appends', [
            O([('ability', 'passive2'), ('effect', e(kind='EnergyGain', target='Self', flat=12,
                condition='SubjectRoleIs', conditionValue=MENDER, conditionOn='BroadcastSource'))]),
            O([('ability', 'passive2'), ('effect', e(kind='EnergyGain', target='Self', flat=12,
                condition='SubjectRoleIs', conditionValue=WARDEN, conditionOn='BroadcastSource'))]),
        ])])),
])

# =====================================================================================
# NYSERA THE MISSING ARROW - Marksman, Umbra
# =====================================================================================
n = H['nysera_the_missing_arrow']
# Absence scales her damage per stack - the in-pipeline modifier, so a crit carries it.
# It rides on the passive it is part of rather than on a shell: AddPassive adopts the
# modifier off whichever passive declares it, with no regard for that passive's trigger,
# so a passive that exists only to hold it would need an effect it does not have.
n['passive'] = stats('nysera_absence_stats', 'Absence',
    e(kind='ApplyStatus', target='Self', status='SpeedUp',     magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='Focus',       magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='EffectHitUp', magnitude=0.08, duration=99),
    outputModStatus='Resolve', outputModPerStack=0.05, outputModScope='AllDamage')

n['basic'] = ability('nysera_unseen_shot', 'Unseen Shot', [
    e(kind='Damage', target='HighestAttackEnemy', power=1.2),
    e(kind='ApplyStatus', target='HighestAttackEnemy', status='Mark', duration=2, chance=1),
])

# The two targets become Linked, and the link is a Deflect that Nysera OWNS: it fires when
# its holder attacks her, sends the blow to the holder's partner at 80%, and the deflect
# branch spends its charge - "the Link ends after triggering once".
n['active'] = ability('nysera_where_i_was_not', 'Where I Was Not', [
    e(kind='Damage', target='HighestAttackEnemy', power=2.15),
    e(kind='Damage', target='LowestHpEnemy', power=1.25),
    e(kind='LinkUnits', target='HighestAttackEnemy', secondaryTarget='LowestHpEnemy',
      linkMutual=True, linkTransfer='Deflect', magnitude=0.8, charges=1, duration=2),
], energy=100)

# passive1 = Vanishing Mark's trigger. OnTargeted reaches the body being aimed at;
# BroadcastSource is the enemy doing the aiming, and HasMyMark on it is "that enemy".
# Removing the mark makes the condition false, so it answers once per mark by itself.
n['passive1'] = passive('nysera_vanishing_mark', 'Vanishing Mark', 'OnTargeted', [
    e(kind='RemoveStatus', target='BroadcastSource', status='Mark', hits=1,
      condition='HasMyMark', conditionOn='BroadcastSource'),
    e(kind='ApplyStatus', target='Self', status='Stealth', duration=1, chance=1,
      condition='HasMyMark', conditionOn='BroadcastSource'),
    e(kind='GainResolve', target='Self', maxStacks=3, duration=99,
      condition='HasMyMark', conditionOn='BroadcastSource'),
])

# passive2 = Unaccounted For. OnStatusGained with TriggerStatus Stealth is "whenever she
# becomes Untargetable"; the Hidden Arrow is a counter the basic spends while she is hidden.
n['passive2'] = passive('nysera_unaccounted_for', 'Unaccounted For', 'OnStatusGained', [
    e(kind='GainResolve', target='Self', counterIndex=2, maxStacks=1, duration=99),
], triggerStatus='Stealth', limit='OncePerRound')

for eff in [
    e(kind='Damage', target='HighestAttackEnemy', power=0.7,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1, conditionOn='Caster',
      condition2='HasStatus', conditionStatus2='Stealth', conditionOn2='Caster'),
    e(kind='EnergyGain', target='Self', flat=10, counterIndex=2, consumesResolve=True,
      condition='StatusStacksAtLeast', conditionCounterIndex=2, conditionValue=1, conditionOn='Caster',
      condition2='HasStatus', conditionStatus2='Stealth', conditionOn2='Caster'),
]:
    n['basic']['effects'].append(eff)

n.pop('skills', None)

n['talents'] = O([
    ('talentI', O([('id', 'nysera_deeper_absence'), ('name', 'Deeper Absence'), ('trigger', 'None'),
        ('effects', []),
        ('overrides', [
            O([('ability', 'basic'), ('effectIndex', 1), ('field', 'duration'), ('delta', 1)]),
        ]),
        ('appends', [
            O([('ability', 'passive2'), ('effect', e(kind='Dispel', target='BroadcastSource', hits=1,
                condition='HasMyMark', conditionOn='BroadcastSource'))]),
        ])])),
    # OnRedirectSent reaches the link's owner, which is Nysera - the deflect's third party.
    ('talentII', passive('nysera_false_target', 'False Target', 'OnRedirectSent', [
        e(kind='Damage', target='BroadcastVictim', power=0.25),
        e(kind='EnergyGain', target='BroadcastSource', flat=-10),
    ])),
    ('talentIII', O([('id', 'nysera_last_known_position'), ('name', 'Last Known Position'),
        ('trigger', 'OnStatusGained'), ('triggerStatus', 'Stealth'), ('oncePerBattle', False),
        ('effects', [
            e(kind='ApplyStatus', target='Self', status='ArmorBreakUp', magnitude=0.15, duration=1),
        ])])),
])

# =====================================================================================
# ROVAN THE ADAPTIVE COMMANDER - Bulwark, Tide
# =====================================================================================
r = H['rovan_the_adaptive_commander']
r['passive'] = stats('rovan_adaptive_guard_stats', 'Adaptive Guard',
    e(kind='ApplyStatus', target='Self', status='MaxHpUp',   magnitude=0.10, duration=99),
    e(kind='ApplyStatus', target='Self', status='DefenseUp', magnitude=0.08, duration=99),
    e(kind='ApplyStatus', target='Self', status='BlockUp',   magnitude=0.08, duration=99))

# Deflected is a Mark from Rovan carrying one use a round; the Shield is sized by the blow
# that enemy just landed, which ScaleByLastDamage reads inside the broadcast window.
r['basic'] = ability('rovan_deflection_strike', 'Deflection Strike', [
    e(kind='Damage', target='FrontEnemy', power=1.1),
    e(kind='ApplyStatus', target='FrontEnemy', status='Mark', duration=2, chance=1, usesPerRound=1),
])

r['active'] = ability('rovan_pass_the_guard', 'Pass the Guard', [
    e(kind='Damage', target='AllEnemies', power=1.85),
    e(kind='ShieldTransfer', target='LowestHpAlly', magnitude=1.0),
    e(kind='ApplyStatus', target='LowestHpAlly', status='Covered', magnitude=0.25,
      duration=2, chance=1),
], energy=100)

# passive1 = the Deflected conversion, in the appendable slot.
r['passive1'] = passive('rovan_deflected_conversion', 'Deflection Strike (Deflected)',
    'OnEnemyDamagedAlly', [
    e(kind='Shield', target='Self', power=0.2, scaleByLastDamage=True, duration=2,
      condition='HasMyMark', conditionOn='BroadcastSource',
      condition2='StatusUseAvailable', conditionStatus2='Mark', conditionOn2='BroadcastSource'),
])

r['passive2'] = passive('rovan_adaptive_guard', 'Adaptive Guard', 'OnRedirectReceived', [
    e(kind='MoveDebuffs', target='BroadcastSubject', secondaryTarget='Self', hits=1),
], limit='OncePerRound')

# Return the Guard: both doors into it, each its own passive because the triggers differ.
r['skills'] = O([
    ('passive1', passive('rovan_return_the_guard', 'Return the Guard',
        'OnAppliedAllyStatusExpired', [
        e(kind='ShieldTransfer', target='BroadcastSubject', magnitude=1.0, transferFromTarget=True),
        e(kind='Damage', target='AllEnemies', power=0.75,
          condition='LastMovedAboveHpFraction', conditionValue=0.10, conditionOn='BroadcastSubject',
          condition2='HasMyMark', conditionOn2='Target'),
    ], triggerStatus='Covered')),
    ('passive2', passive('rovan_return_the_guard_death', 'Return the Guard (defeat)',
        'OnAllyDefeated', [
        e(kind='ShieldTransfer', target='DefeatedUnit', magnitude=1.0, transferFromTarget=True,
          condition='HasStatus', conditionStatus='Covered', conditionOn='Defeated'),
        e(kind='Damage', target='AllEnemies', power=0.75,
          condition='LastMovedAboveHpFraction', conditionValue=0.10, conditionOn='Defeated',
          condition2='HasMyMark', conditionOn2='Target'),
    ])),
])

r['talents'] = O([
    ('talentI', O([('id', 'rovan_shared_angle'), ('name', 'Shared Angle'), ('trigger', 'None'),
        ('effects', []),
        ('appends', [
            O([('ability', 'active'), ('effect', e(kind='ApplyStatus', target='LowestHpAlly',
                status='EffectResistUp', magnitude=0.10, duration=2, chance=1))]),
        ])])),
    # 8 Energy per 10% Max HP moved, up to 16 - two gated grants, the cap being that there
    # are two. LastMovedAboveHpFraction reads what ShieldTransfer just moved.
    ('talentII', O([('id', 'rovan_no_wasted_guard'), ('name', 'No Wasted Guard'), ('trigger', 'None'),
        ('effects', []),
        ('appends', [
            O([('ability', 'active'), ('effect', e(kind='EnergyGain', target='LowestHpAlly', flat=8,
                condition='LastMovedAboveHpFraction', conditionValue=0.10))]),
            O([('ability', 'active'), ('effect', e(kind='EnergyGain', target='LowestHpAlly', flat=8,
                condition='LastMovedAboveHpFraction', conditionValue=0.20))]),
        ])])),
    ('talentIII', O([('id', 'rovan_guard_in_motion'), ('name', 'Guard in Motion'),
        ('trigger', 'OnAppliedAllyStatusExpired'), ('triggerStatus', 'Covered'),
        ('oncePerBattle', False),
        ('effects', [
            e(kind='Shield', target='SecondLowestHpAlly', selfHpRatio=0.08, duration=2),
        ])])),
])

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print("authored talren, vaelis, nysera, rovan")
for hid in ['talren_the_wayfinder', 'vaelis_the_unfixed_blade',
            'nysera_the_missing_arrow', 'rovan_the_adaptive_commander']:
    h = H[hid]
    slots = [k for k in ('passive', 'passive1', 'passive2') if isinstance(h.get(k), dict)]
    sk = [k for k in ('passive1', 'passive2') if isinstance((h.get('skills') or {}).get(k), dict)]
    print(f"  {hid}: basic {len(h['basic']['effects'])} eff, active {len(h['active']['effects'])} eff, "
          f"passives {slots} + skills{sk}, talents {list(h['talents'])}")
