"""Scratch authoring script for the third 4-star batch. Not part of the build.

The six heroes after Vanno in heroes.json order: Corin, Halwen, Jory, Maera, Maren, Eira.
Same shape as the first two batches - basic + active + passive1, plus the overflow
"passives" list where one described clause answers a trigger of its own. No talents and no
stat-block passive.

NO ENGINE ADDITIONS. One dormant tripwire is lit and flipped: Maera's Watch the Water is
the first shipped content to apply StatusKind.GuaranteedCrit. Everything else reuses
mechanics that already carry shipped occupants.

Where a clause could not be said, it is approximated the way batch two approximated its
own, marked draft in the branch report, and given a row in Docs/ENGINE-GAPS.md. Each one
is noted at the kit it belongs to.
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
# CORIN - Binder, Verdant. Settling is a stack of Slow and the count IS the clause.
#
# No Mark. "Enemies with Settling" is MyStatusStacksAtLeast on Slow, which counts only the
# entries whose SourceId is the caster, so a Slow somebody else applied cannot answer for
# Corin's. That is what the condition was added for and it saves the kit a second status.
# =====================================================================================
install(
    'corin_the_patience_engineer',
    ability('corin_measurement_strike', 'Measurement Strike',
            'Deals 210% Attack damage and applies Settling for 2 rounds. Settling reduces '
            'Speed by 8% each round, up to 24%.',
            [e(kind='Damage', target='FrontEnemy', power=2.1),
             # stackOnReapply with maxStacks 3 is the whole "up to 24%": AddStack adds a
             # separate entry per application to a cap of three, and EffectiveSpeed sums
             # every Slow magnitude, so three entries of 0.08 are exactly 24% and a fourth
             # application refreshes rather than deepens.
             e(kind='ApplyStatus', target='LastTarget', status='Slow', magnitude=0.08,
               duration=2, chance=1, bypassResist=True, stackOnReapply=True, maxStacks=3)]),
    ability('corin_weight_of_time', 'Weight of Time',
            'Deals 400% Attack damage to enemies with Settling. Targets at 24% Speed Down '
            'are Stunned for 1 round.',
            [e(kind='Damage', target='AllEnemies', power=4.0,
               condition='MyStatusStacksAtLeast', conditionStatus='Slow', conditionValue=1,
               conditionOn='Target'),
             e(kind='ApplyStatus', target='AllEnemies', status='Stun', duration=1, chance=1,
               bypassResist=True,
               condition='MyStatusStacksAtLeast', conditionStatus='Slow', conditionValue=3,
               conditionOn='Target')],
            energy=100),
    passive('corin_load_bearing', 'Load Bearing',
            'When Settling expires, apply 15% Defense Down for 2 rounds.',
            # triggerStatus narrows the applier-side expiry broadcast to Slow, so a Mark or
            # a Burn shedding on the same body does not pay this out. Elyra and Arden both
            # read OnAppliedStatusExpired this way.
            'OnAppliedStatusExpired',
            [e(kind='ApplyStatus', target='BroadcastSubject', status='DefenseDown',
               magnitude=0.15, duration=2, chance=1, bypassResist=True)],
            triggerStatus='Slow', oncePerBattle=False))

# =====================================================================================
# HALWEN - Bulwark, Verdant. Reserve is Guard: a charge spent by USE rather than by time.
#
# DRAFT (gaps 9): "When Reserve prevents damage" has no hook. The damage funnel spends the
# charge and removes the entry inline; the only thing that can observe a Guard mitigation
# is a Ledger carrying FunnelSource.GuardMitigation, which pays out when it caps rather
# than when it prevents. Rationed Strength answers the blow instead of the prevention.
# =====================================================================================
install(
    'halwen_the_reserve_keeper',
    ability('halwen_reserve_swing', 'Reserve Swing',
            'Deals 60% Attack damage and grants Reserve for 2 rounds. Reserve reduces the '
            'next damage taken by 18%.',
            [e(kind='Damage', target='FrontEnemy', power=0.6),
             e(kind='ApplyStatus', target='Self', status='Guard', magnitude=0.18, duration=2,
               charges=1, chance=1)]),
    ability('halwen_open_the_reserve', 'Open the Reserve',
            'Deals 90% Attack damage to all enemies and grants Reserve to all allies for 2 '
            'rounds.',
            [e(kind='Damage', target='AllEnemies', power=0.9),
             e(kind='ApplyStatus', target='AllAllies', status='Guard', magnitude=0.18,
               duration=2, charges=1, chance=1)],
            energy=100),
    passive('halwen_rationed_strength', 'Rationed Strength',
            'The first time each round an enemy strikes an ally, that ally recovers 4% Max '
            'HP, or gains 6 Energy if it is already whole.',
            # BroadcastVictim rather than BroadcastSubject: the two name the same ally and
            # only BroadcastVictim resolves before the Taunt anchor. Orelle paid for that
            # lesson with a 1.2M-HP body wearing her ward.
            'OnEnemyDamagedAlly',
            [e(kind='EnergyGain', target='BroadcastVictim', flat=6,
               condition='HpAbove', conditionValue=0.99, conditionOn='Target'),
             e(kind='Heal', target='BroadcastVictim', targetMaxHpRatio=0.04)],
            oncePerBattle=False, limit='OncePerRound'),
    overflow=[
        # The same answer when Halwen is the body hit. OnEnemyDamagedAlly excludes the
        # victim, which is the third time this tier has needed a second hook for it -
        # Branik's Salvaged Cover was the first.
        passive('halwen_rationed_strength_self', 'Rationed Strength',
                'The same recovery when Halwen is the one struck - OnEnemyDamagedAlly '
                'excludes the body being hit, so the victim needs its own hook.',
                'OnDamaged',
                [e(kind='EnergyGain', target='Self', flat=6,
                   condition='HpAbove', conditionValue=0.99, conditionOn='Caster'),
                 e(kind='Heal', target='Self', targetMaxHpRatio=0.04)],
                oncePerBattle=False, limit='OncePerRound'),
    ])

# =====================================================================================
# JORY - Vanguard, Verdant. The gate that is worth more the longer it is hit.
#
# DRAFT (gaps 4): "it deals 15% less damage to Jory" is per-source and every stat status in
# the game is global, so this is DamageReductionUp against everybody. Kest's "takes 10%
# less from it" is the same sentence and took the same answer.
# DRAFT (gaps 10): "the same enemy twice in a round" needs a per-subject firing count, and
# those are charged against a broadcast subject. OnDamaged is owner-centric and has none,
# so the counterattack is once a round against whoever struck him.
# =====================================================================================
install(
    'jory_the_old_gate',
    ability('jory_holdfast_thrust', 'Holdfast Thrust',
            'Deals 145% Attack damage. If Jory was attacked since his last turn, he heals '
            '4% Max HP.',
            [e(kind='Damage', target='FrontEnemy', power=1.45),
             e(kind='Heal', target='Self', targetMaxHpRatio=0.04,
               condition='SubjectDamagedSinceMyTurn', conditionOn='Caster')]),
    ability('jory_bar_the_way', 'Bar the Way',
            'Deals 230% Attack damage and Taunts the front enemy for 2 rounds, while Jory '
            'takes 15% less damage.',
            [e(kind='Damage', target='FrontEnemy', power=2.3),
             e(kind='ApplyStatus', target='LastTarget', status='Taunt', duration=2, chance=1),
             e(kind='ApplyStatus', target='Self', status='DamageReductionUp', magnitude=0.15,
               duration=2)],
            energy=100),
    passive('jory_worn_hinges', 'Worn Hinges',
            'The first time each round an enemy hits Jory, he counterattacks it for 110% '
            'Attack damage.',
            'OnDamaged',
            [e(kind='Damage', target='LastAttacker', power=1.1)],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# MAERA - Marksman, Verdant. The light that answers whoever is shooting at the harbour.
#
# LIGHTS A DORMANT TRIPWIRE. StatusKind.GuaranteedCrit has been guarded as unreached since
# the tier-six pass; Watch the Water is the first shipped content to apply it, and the
# guard is flipped to a confirmation naming her rather than deleted.
#
# DRAFT (gaps 8, twice): the engine remembers who hit ME, not who hit MY SIDE.
# LastAttackerId is per-unit, so "the target attacked an ally since my last turn" becomes
# SubjectActedThisRound and "the enemy that most recently damaged an ally" becomes
# HighestDamageDealtEnemy, which is a career total rather than a recent act.
# DRAFT (gaps 13): "she cannot be controlled" is dropped. ControlImmunity is a UnitDef stat
# and StatusKind has no entry that raises it.
# =====================================================================================
install(
    'maera_the_harbor_eye',
    ability('maera_returning_shot', 'Returning Shot',
            'Deals 280% Attack damage; if the target has already acted this round, deals '
            '70% additional Attack damage.',
            [e(kind='Damage', target='FrontEnemy', power=2.8),
             e(kind='Damage', target='LastTarget', power=0.7,
               condition='SubjectActedThisRound', conditionOn='Target')]),
    ability('maera_keep_the_beacon', 'Keep the Beacon',
            'Deals 540% Attack damage to the enemy that has dealt the most damage. On '
            'defeat, allies recover 7% Max HP.',
            [e(kind='Damage', target='HighestDamageDealtEnemy', power=5.4)],
            energy=100),
    passive('maera_watch_the_water', 'Watch the Water',
            'The first time each ally falls below 45% HP, Maera’s next attack is a '
            'guaranteed Critical.',
            # firesPerSubject is what "each ally" means: the budget is charged once per
            # firing against the broadcast subject, and charged AFTER the effects resolve,
            # so the first fall reads zero rather than one.
            'OnAllyHpBelowThreshold',
            [e(kind='ApplyStatus', target='Self', status='GuaranteedCrit', duration=2,
               chance=1)],
            threshold=0.45, firesPerSubject=1, oncePerBattle=False),
    overflow=[
        # OnEnemyDefeated rather than OnKill, the standing rule this tier already follows:
        # DealRawDamage raises OnKill at a hardcoded depth 0, so a lethal OnKill passive is
        # unbounded recursion and the harness forbids one. triggerAbilityId narrows the
        # defeat broadcast back to the one cast the text names.
        passive('maera_keep_the_beacon_relief', 'Keep the Beacon',
                'The relief on Keep the Beacon only: an enemy defeated during that cast '
                'sends every ally 7% of their Max HP.',
                'OnEnemyDefeated',
                [e(kind='Heal', target='AllAllies', targetMaxHpRatio=0.07)],
                triggerAbilityId='maera_keep_the_beacon', oncePerBattle=False),
    ])

# =====================================================================================
# MAREN - Mender, Verdant. Recovery is Regen, and the save is Brenna's shape.
#
# DRAFT (gaps 12): "Recovery heals 3% Max HP each turn" is a percentage of the HOLDER, and
# ApplyStatus resolves a Regen magnitude as the applier's Attack times the authored value.
# The tick is written in Maren's own currency at the value that comes to about 3% of an
# ally's bar at the level the number was set against.
#
# RedirectLethal at power 0 on purpose. Brenna's Survival of the Stubborn sends the overkill
# back into the protector at power 1.0; Pinoy's sentence here says only "leave them at 1 HP"
# and says nothing about Maren paying for it, so the redirect saves and charges nothing.
# =====================================================================================
install(
    'maren_the_long_sleeper',
    ability('maren_waking_touch', 'Waking Touch',
            'Deals 130% Attack damage and heals the lowest-HP ally for 60% Attack.',
            [e(kind='Damage', target='FrontEnemy', power=1.3),
             e(kind='Heal', target='LowestHpAlly', power=0.6)]),
    ability('maren_unfinished_rest', 'Unfinished Rest',
            'Deals 210% Attack damage to all enemies and gives the two lowest-HP allies '
            'Recovery for 3 rounds. Recovery heals about 3% Max HP each turn.',
            [e(kind='Damage', target='AllEnemies', power=2.1),
             e(kind='ApplyStatus', target='TwoLowestHpAllies', status='Regen', magnitude=0.35,
               duration=3, chance=1)],
            energy=100),
    passive('maren_do_not_disturb', 'Do Not Disturb',
            'When an ally with Recovery would die, remove Recovery and leave them at 1 HP. '
            'Once per battle.',
            'OnAllyWouldDie',
            [e(kind='RedirectLethal', target='BroadcastSubject', power=0.0,
               condition='HasStatus', conditionStatus='Regen', conditionOn='Target'),
             e(kind='RemoveStatus', target='BroadcastSubject', status='Regen', hits=1,
               condition='HasStatus', conditionStatus='Regen', conditionOn='Target')],
            oncePerBattle=True))

# =====================================================================================
# EIRA - Warden, Gale. Cover that moves, because cover that cannot move is a cage.
#
# DRAFT (gaps 11): "moves the target to the back" needs placement at a named slot and
# SwapPositions picks the partner at random from the target's own side. It displaces the
# target, and "if another enemy is alive" is inherent - with no candidate the effect is a
# no-op by construction.
# DRAFT (gaps 3, twice): nothing on a resolving hit says whether it was aimed at one body
# or at the line, so "the first AREA hit each round" is the first hit, and "singled out"
# is "hit".
# DRAFT (gaps 2): a Guard charge does not refill at a round boundary, so Mobile Cover
# answers one blow across its two rounds rather than one a round.
# =====================================================================================
install(
    'eira_the_rolling_barricade',
    ability('eira_shove_aside', 'Shove Aside',
            'Deals 150% Attack damage and shoves the target out of position if another '
            'enemy is alive.',
            [e(kind='Damage', target='FrontEnemy', power=1.5),
             e(kind='SwapPositions', target='LastTarget')]),
    ability('eira_redeploy_cover', 'Redeploy Cover',
            'Deals 250% Attack damage to all enemies and grants Mobile Cover for 2 rounds; '
            'it reduces the next hit each ally takes by 20%.',
            [e(kind='Damage', target='AllEnemies', power=2.5),
             e(kind='ApplyStatus', target='AllAllies', status='Guard', magnitude=0.20,
               duration=2, charges=1, chance=1)],
            energy=100),
    passive('eira_leave_no_corner', 'Leave No Corner',
            'The first time each round an ally with Mobile Cover is struck, its turn '
            'progress advances by 12%.',
            'OnEnemyDamagedAlly',
            [e(kind='TurnShift', target='BroadcastVictim', magnitude=0.12,
               condition='HasStatus', conditionStatus='Guard', conditionOn='Target')],
            oncePerBattle=False, limit='OncePerRound'))

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('authored 6 four-star kits: corin, halwen, jory, maera, maren, eira')
