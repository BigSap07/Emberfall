"""Scratch authoring script for the first 4-star batch. Not part of the build.

Five of the first six 4-stars in heroes.json order. Celen is deliberately absent and the
branch report says why: two of his three clauses name an ally pick the engine does not have.

Each kit is basic + active + passive1, per the 4-star star contract, plus the overflow
"passives" list where one described clause answers a trigger of its own - the same shape
the 5-stars use when a description needs more than one hook. No talents and no stat-block
passive: those are what a 5-star has and a 4-star does not.
"""
import json, io, collections

P = r'Assets/Resources/Content/heroes.json'
d = json.loads(io.open(P, encoding='utf-8').read(), object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}
O = collections.OrderedDict

RSQUO = u'’'


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


def install(hid, basic, active, passive1, overflow=None):
    h = H[hid]
    for key in ('basic', 'active', 'passive', 'passive1', 'passive2', 'skills', 'passives', 'talents'):
        h.pop(key, None)
    h['basic'] = basic
    h['active'] = active
    h['passive1'] = passive1
    if overflow:
        h['passives'] = overflow


# =====================================================================================
# BRANIK - Bulwark, Ember. Cracked is a Mark plus the Block it eats.
# =====================================================================================
install(
    'branik_the_siege_mason',
    ability('branik_faultline_tap', 'Faultline Tap',
            'Deals 105% Attack damage and applies Cracked for 2 rounds. Cracked reduces Block by 12%.',
            [e(kind='Damage', target='FrontEnemy', power=1.05),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='BlockDown', magnitude=0.12,
               duration=2, chance=1, bypassResist=True)]),
    ability('branik_demolition_right', 'Demolition Right',
            'Deals 180% Attack damage to the front enemy. If it is Cracked, removes Cracked and '
            'grants Branik a Shield equal to 16% Max HP for 2 rounds.',
            [e(kind='Damage', target='FrontEnemy', power=1.8),
             e(kind='Shield', target='Self', selfHpRatio=0.16, duration=2,
               condition='HasMyMark', conditionOn='LastTarget'),
             e(kind='RemoveStatus', target='LastTarget', status='BlockDown', hits=1,
               condition='HasMyMark', conditionOn='Target'),
             e(kind='RemoveStatus', target='LastTarget', status='Mark', hits=1,
               condition='HasMyMark', conditionOn='Target')],
            energy=100),
    passive('branik_salvaged_cover', 'Salvaged Cover',
            'The first time each round a Cracked enemy attacks, Branik gains 8% Damage Reduction '
            'until round end.',
            'OnEnemyDamagedAlly',
            [e(kind='ApplyStatus', target='Self', status='DamageReductionUp', magnitude=0.08,
               duration=1, condition='HasMyMark', conditionOn='LastAttacker')],
            oncePerBattle=False, limit='OncePerRound'),
    overflow=[
        passive('branik_salvaged_cover_self', 'Salvaged Cover',
                'The same answer when the Cracked enemy attacks Branik himself - '
                'OnEnemyDamagedAlly excludes the body being hit, so the victim needs its own hook.',
                'OnDamaged',
                [e(kind='ApplyStatus', target='Self', status='DamageReductionUp', magnitude=0.08,
                   duration=1, condition='HasMyMark', conditionOn='LastAttacker')],
                oncePerBattle=False, limit='OncePerRound'),
    ])

# =====================================================================================
# DEMOS - Binder, Ember. Arrears is a Mark plus the healing it refuses.
# =====================================================================================
install(
    'demos_the_debt_collector',
    ability('demos_notice_of_loss', 'Notice of Loss',
            'Deals 105% Attack damage and applies Arrears for 2 rounds. At turn end, Arrears '
            'loses 4% current HP.',
            [e(kind='Damage', target='FrontEnemy', power=1.05),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='HealingReduction', magnitude=0.2,
               duration=2, chance=1, bypassResist=True)]),
    ability('demos_compounding_sentence', 'Compounding Sentence',
            'Deals 175% Attack damage to enemies with Arrears and extends Arrears by 1 round.',
            [e(kind='Damage', target='AllEnemies', power=1.75,
               condition='HasMyMark', conditionOn='Target'),
             e(kind='ExtendStatus', target='AllEnemies', status='Mark', duration=1,
               condition='HasMyMark', conditionOn='Target'),
             e(kind='ExtendStatus', target='AllEnemies', status='HealingReduction', duration=1,
               condition='HasMyMark', conditionOn='Target')],
            energy=100),
    passive('demos_no_appeal', 'No Appeal',
            'Enemies with Arrears receive 20% less healing; on defeat, their Arrears moves to the '
            'highest-HP enemy.',
            'OnEnemyDefeated',
            [e(kind='TransferStatus', target='DefeatedUnit', status='HealingReduction',
               secondaryTarget='HighestHpEnemy', condition='HasMyMark', conditionOn='Target'),
             e(kind='TransferStatus', target='DefeatedUnit', status='Mark',
               secondaryTarget='HighestHpEnemy', condition='HasMyMark', conditionOn='Target')],
            oncePerBattle=False),
    overflow=[
        # BroadcastSource, not BroadcastSubject, and the difference is the Taunt anchor.
        # OnEnemyTurnEnd names the same enemy in both fields, but BroadcastSubject resolves
        # AFTER the anchor, so a living enemy Taunt holder answers for its side and the tick
        # lands on the protector instead of on the body carrying Arrears - with a percent-of-HP
        # term, on whichever body happens to be biggest. BroadcastSource resolves before the
        # anchor, so the tick reaches its own carrier.
        passive('demos_arrears_tick', 'Notice of Loss',
                'The Arrears tick: a body carrying it loses 4% of its current HP as its own turn '
                'ends.',
                'OnEnemyTurnEnd',
                # attackCap is a deviation from the written text and the report flags it.
                # A percent-of-current-HP tick has no ceiling of its own, so what it is worth
                # is a property of whatever body it lands on rather than of the kit. On the
                # scale the number was written against - import stats, hp 352 and attack 33 -
                # 4% of a body is 0.43 of the caster's Attack, and that relationship is the
                # design. Against a bar several times a hero's it is several times his basic,
                # and against the sweep arena's 200,000-HP dummies it was 2,600 a tick. The cap
                # holds the authored relationship where the author set it, in the caster's own
                # currency, which is how the roster already answers this - Kiva's "up to 130%
                # of Attack".
                [e(kind='Damage', target='BroadcastSource', power=0, targetCurrentHpRatio=0.04,
                   attackCap=0.6, condition='HasMyMark', conditionOn='BroadcastSource')],
                oncePerBattle=False),
    ])

# =====================================================================================
# KEST - Striker, Ember. The lock is the targeting; the Mark is the memory of it.
# =====================================================================================
install(
    'kest_the_bridgebreaker',
    ability('kest_commitment_cut', 'Commitment Cut',
            'Deals 120% Attack damage. If Kest attacked this target last turn, deals 35% '
            'additional Attack damage.',
            [e(kind='Damage', target='LastTarget', power=1.2),
             e(kind='Damage', target='LastTarget', power=0.35,
               condition='HasMyMark', conditionOn='Target')]),
    ability('kest_no_way_back', 'No Way Back',
            'Deals 260% Attack damage to Kest' + RSQUO + 's last target. On defeat, attacks the '
            'lowest-HP enemy for 100% Attack damage.',
            [e(kind='Damage', target='LastTarget', power=2.6)],
            energy=100),
    passive('kest_commit_to_the_return', 'Commit to the Return',
            'Kest cannot change targets while his target lives, but deals 12% more damage to it '
            'and takes 10% less damage while committed.',
            'AfterBasicAttack',
            [e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='Vulnerable', magnitude=0.12,
               duration=2, chance=1, bypassResist=True),
             e(kind='ApplyStatus', target='Self', status='DamageReductionUp', magnitude=0.10,
               duration=2)],
            oncePerBattle=False),
    overflow=[
        # OnEnemyDefeated rather than OnKill, and the difference is a standing engine rule:
        # DealRawDamage raises OnKill at a hardcoded depth 0, so a lethal OnKill passive is
        # unbounded recursion and the harness forbids one outright. The defeat broadcast is
        # bounded by _inDefeatDispatch instead - a death inside a death raises no second
        # broadcast - so the same clause is safe there. triggerAbilityId narrows it back to
        # "on No Way Back", which is what the text says and what OnKill would have given free.
        passive('kest_no_way_back_finish', 'No Way Back',
                'The follow-through on No Way Back only: an enemy defeated during that cast '
                'carries into the lowest-HP enemy.',
                'OnEnemyDefeated',
                [e(kind='Damage', target='LowestHpEnemy', power=1.0)],
                triggerAbilityId='kest_no_way_back', oncePerBattle=False),
    ])

# =====================================================================================
# ORELLE - Mender, Ember.
# =====================================================================================
install(
    'orelle_the_fever_warden',
    ability('orelle_measured_tap', 'Measured Tap',
            'Deals 100% Attack damage and heals the lowest-HP ally for 55% Attack.',
            [e(kind='Damage', target='FrontEnemy', power=1.0),
             e(kind='Heal', target='LowestHpAlly', power=0.55)]),
    ability('orelle_hard_remedy', 'Hard Remedy',
            'Deals 155% Attack damage to all enemies, removes one debuff from each ally, and '
            'cleansed allies recover 8% missing HP.',
            [e(kind='Damage', target='AllEnemies', power=1.55),
             e(kind='Cleanse', target='AllAllies', hits=1),
             e(kind='Heal', target='AllAllies', targetMissingHpRatio=0.08,
               condition='SubjectLostStatusThisFiring', conditionOn='Target')],
            energy=100),
    passive('orelle_triage_after_loss', 'Triage After Loss',
            'The first time an ally falls below 35% HP, grant them a 12% Max HP Shield for 2 '
            'rounds.',
            # BroadcastVictim rather than BroadcastSubject: the two name the same ally here, but
            # only BroadcastVictim resolves before the Taunt anchor. Aimed at the subject, a
            # living enemy taunter answered for its side and Orelle warded the ENEMY - 12% of a
            # 1.2M-HP body in the sweep arena, which is how it was found.
            'OnAllyHpBelowThreshold',
            [e(kind='Shield', target='BroadcastVictim', targetMaxHpRatio=0.12, duration=2)],
            threshold=0.35, oncePerBattle=True))

# =====================================================================================
# TAVIA - Marksman, Ember. Exposed Frequency is a Mark plus the Defense it opens.
# =====================================================================================
install(
    'tavia_the_signal_huntress',
    ability('tavia_rangefinder_bolt', 'Rangefinder Bolt',
            'Deals 115% Attack damage to the highest-Attack enemy and applies Exposed Frequency '
            'for 2 rounds. Allied attacks ignore 8% Defense against it.',
            [e(kind='Damage', target='HighestAttackEnemy', power=1.15),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='DefenseDown', magnitude=0.08,
               duration=2, chance=1, bypassResist=True)]),
    ability('tavia_cut_the_wire', 'Cut the Wire',
            'Deals 230% Attack damage to an enemy with Exposed Frequency, then transfers its '
            'buffs to the lowest-HP enemy for 1 round.',
            [e(kind='Damage', target='EnemyWithMyMark', power=2.3),
             e(kind='MoveBuffs', target='EnemyWithMyMark', secondaryTarget='LowestHpEnemy',
               hits=3, moveDuration=1)],
            energy=100),
    passive('tavia_dead_channel', 'Dead Channel',
            'When an enemy with Exposed Frequency gains a buff, Tavia deals 45% Attack damage to '
            'it once per round.',
            'OnEnemyBuffGained',
            [e(kind='Damage', target='BroadcastSubject', power=0.45,
               condition='HasMyMark', conditionOn='BroadcastSubject')],
            oncePerBattle=False, limit='OncePerRound'))

io.open(P, 'w', encoding='utf-8', newline='').write(json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('authored 5 four-star kits: branik, demos, kest, orelle, tavia')
