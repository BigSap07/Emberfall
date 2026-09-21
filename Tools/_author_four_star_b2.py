"""Scratch authoring script for the second 4-star batch. Not part of the build.

The four heroes after Celen in heroes.json order: Mika, Rhys, Siofra, Vanno. Same shape as
the first batch - basic + active + passive1, plus the overflow "passives" list where one
described clause answers a trigger of its own. No talents and no stat-block passive.

Every mechanic below already existed. The only engine addition in this batch is the pair
of ally target modes Celen needed, and none of these four uses either one.

FOUR PLACES WHERE PINOY'S TEXT AND THE ENGINE DO NOT MEET. Each is flagged in the branch
report as draft rather than quietly reworded:

1. RHYS, Crossing Cut: "swaps HP percentage with the lowest-HP ally". There is no swap.
   EffectKind.HpTransfer moves HP one way between the caster and its target and is the
   nearest thing that exists - it is one of the dormant tripwires, so lighting it is
   sanctioned and its guard is flipped to a confirmation naming Rhys. A true swap needs a
   new effect kind, and this batch's one engine change is spent.

2. RHYS, Shared Peril: "when an ally takes a hit exceeding 25% Max HP". No condition reads
   the SIZE of a hit - EffectCondition asks about HP, statuses, stats and history, never
   about the blow that just landed - and StatusKind.Covered reads neither a per-hit
   magnitude nor a per-round budget, because CoverFraction looks only at Magnitude. So the
   guard goes up on the first strike of each round instead, and covers that ally for the
   round.

3. SIOFRA, Bad Omen and Forecast Reversed: "for 2 rounds". InvertStatus carries the flipped
   entry's OWN remaining duration across - that is what makes it an inversion rather than a
   fresh debuff - so the reversal lasts as long as the buff had left, not two rounds.

4. MIKA, Parts Exchange: "moves each effect to an unmarked enemy". Nothing can name "an
   enemy without my mark"; the enum has EnemyWithMyMark and no negation of it. Loose Parts
   moves to the highest-HP enemy instead, which is the answer Demos's No Appeal already
   gives to the same question.

LOOSE PARTS IS TWO ENTRIES, and that is the batch-one pattern rather than a new idea. Every
named effect in this tier is a Mark plus whatever the description says it does - Cracked is
a Mark plus Block, Arrears a Mark plus refused healing. Loose Parts is a Mark plus a Link,
because "the next ALLY to hit it" is expressible in exactly one way: OnLinkedUnitHit with
TriggerSourceIsAlly, which the enum's own comment describes as "when an ally hits the unit
I am bound to". The Mark carries the targeting and the conditions; the Link carries the
trigger. It is inert as a damage path - no LinkTransfer, no LinkAmountPower - so it moves
nothing on its own.
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
# MIKA - Tinkerer, Tide. Loose Parts is a Mark plus the Link that hears an ally land on it.
# =====================================================================================
install(
    'mika_the_salvage_pilot',
    ability('mika_reclaimed_round', 'Reclaimed Round',
            'Deals 165% Attack damage and applies Loose Parts for 2 rounds. The next ally to '
            'hit it gains 14 Energy.',
            [e(kind='Damage', target='FrontEnemy', power=1.65),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             # Mika HOLDS the link and the enemy is the partner, because DispatchLinkPassive
             # fires on whoever carries the entry NAMING the body that was hit.
             e(kind='LinkUnits', target='Self', secondaryTarget='LastTarget', duration=2)]),
    ability('mika_parts_exchange', 'Parts Exchange',
            'Deals 295% Attack damage to enemies with Loose Parts, then moves Loose Parts to '
            'the highest-HP enemy for 2 rounds.',
            [e(kind='Damage', target='AllEnemies', power=2.95,
               condition='HasMyMark', conditionOn='Target'),
             e(kind='TransferStatus', target='EnemyWithMyMark', status='Mark',
               secondaryTarget='HighestHpEnemy'),
             e(kind='LinkUnits', target='Self', secondaryTarget='HighestHpEnemy', duration=2)],
            energy=100),
    passive('mika_find_a_use', 'Find a Use',
            'The first time each round an ally hits an enemy carrying Loose Parts, that ally '
            'gains 14 Energy and Mika gains a Shield equal to 45% Attack for 1 round.',
            'OnLinkedUnitHit',
            [e(kind='EnergyGain', target='BroadcastSource', flat=14),
             e(kind='Shield', target='Self', power=0.45, duration=1)],
            triggerSourceIsAlly=True, oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# RHYS - Vanguard, Tide. He takes what the line cannot.
# =====================================================================================
install(
    'rhys_the_fence_walker',
    ability('rhys_crossing_cut', 'Crossing Cut',
            'Deals 145% Attack damage and gives the lowest-HP ally 45% of the HP they are '
            'missing, out of Rhys’s own, if they are below 50% HP.',
            [e(kind='Damage', target='FrontEnemy', power=1.45),
             # Rhys is the donor: TransferFromTarget stays false, so the caster pays.
             # Clamped to Hp - 1 inside the engine, so this cannot kill him.
             e(kind='HpTransfer', target='LowestHpAlly', targetMissingHpRatio=0.45,
               condition='HpBelow', conditionValue=0.5, conditionOn='Target')]),
    ability('rhys_change_sides', 'Change Sides',
            'Deals 270% Attack damage and swaps Rhys’s buffs with the target’s '
            'debuffs for 2 rounds.',
            [e(kind='Damage', target='FrontEnemy', power=2.7),
             e(kind='MoveBuffs', target='Self', secondaryTarget='LastTarget', hits=3,
               moveDuration=2),
             e(kind='MoveDebuffs', target='LastTarget', secondaryTarget='Self', hits=3,
               moveDuration=2)],
            energy=100),
    passive('rhys_shared_peril', 'Shared Peril',
            'The first time each round an enemy strikes an ally, Rhys stands in front of that '
            'ally for the rest of the round and takes 30% of every hit aimed at them.',
            # BroadcastVictim rather than BroadcastSubject, the lesson Orelle's Triage After
            # Loss paid for: the two name the same ally here, and only BroadcastVictim
            # resolves before the Taunt anchor, so a living enemy taunter cannot answer for
            # its side and hand the ward to the wrong line.
            'OnEnemyDamagedAlly',
            [e(kind='ApplyStatus', target='BroadcastVictim', status='Covered', magnitude=0.30,
               duration=1, chance=1, bypassResist=True)],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# SIOFRA - Breaker, Tide. A prediction is only true once it has been turned over.
# =====================================================================================
install(
    'siofra_the_turncoat_oracle',
    ability('siofra_bad_omen', 'Bad Omen',
            'Deals 105% Attack damage and changes one buff on the target into its matching '
            'debuff, for whatever the buff had left.',
            [e(kind='Damage', target='FrontEnemy', power=1.05),
             e(kind='InvertStatus', target='LastTarget', hits=1)]),
    ability('siofra_forecast_reversed', 'Forecast Reversed',
            'Deals 190% Attack damage to all enemies, reverses one modifier on each, and every '
            'enemy reversed loses 10 Energy.',
            [e(kind='Damage', target='AllEnemies', power=1.9),
             e(kind='InvertStatus', target='AllEnemies', hits=1),
             # Prediction Cost, carried by the active itself. The passive below is the same
             # rule on the basic; there is no AfterActive trigger to hang a second copy on.
             e(kind='EnergyGain', target='AllEnemies', flat=-10)],
            energy=100),
    passive('siofra_prediction_cost', 'Prediction Cost',
            'Whenever Siofra turns a buff into a debuff, that target loses 10 Energy, once per '
            'round.',
            'AfterBasicAttack',
            [e(kind='EnergyGain', target='LastTarget', flat=-10)],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# VANNO - Stalker, Tide. Misdirected is a Mark and the route it sends things down.
# =====================================================================================
install(
    'vanno_the_detour_king',
    ability('vanno_wrong_turn', 'Wrong Turn',
            'Deals 290% Attack damage and applies Misdirected for 2 rounds. The first buff a '
            'Misdirected enemy gains each round goes to one of Vanno’s allies instead.',
            [e(kind='Damage', target='FrontEnemy', power=2.9),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2)]),
    ability('vanno_road_not_taken', 'Road Not Taken',
            'Deals 360% Attack damage to a Misdirected enemy and moves its debuffs to the '
            'highest-HP enemy for 2 rounds.',
            [e(kind='Damage', target='EnemyWithMyMark', power=3.6),
             e(kind='MoveDebuffs', target='EnemyWithMyMark', secondaryTarget='HighestHpEnemy',
               hits=3, moveDuration=2)],
            energy=100),
    passive('vanno_exit_unmarked', 'Exit Unmarked',
            'When a Misdirected enemy falls, Vanno cannot be targeted until his next turn.',
            'OnEnemyDefeated',
            [e(kind='ApplyStatus', target='Self', status='Stealth', duration=1, chance=1,
               condition='HasMyMark', conditionOn='Defeated')],
            oncePerBattle=False),
    overflow=[
        passive('vanno_wrong_turn_misdirect', 'Wrong Turn',
                'The misdirection itself: a buff landing on a Misdirected enemy is taken off it '
                'and given to a random ally of Vanno’s, once per round.',
                'OnEnemyBuffGained',
                [e(kind='MoveBuffs', target='BroadcastSubject', secondaryTarget='RandomAlly',
                   hits=1, moveDuration=2,
                   condition='HasMyMark', conditionOn='BroadcastSubject')],
                oncePerBattle=False, limit='OncePerRound'),
    ])

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('authored 4 four-star kits: mika, rhys, siofra, vanno')
