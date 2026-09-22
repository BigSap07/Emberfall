"""Scratch authoring script for the fourth 4-star batch, second half. Not part of the build.

Ruma, Toren, Leon, Lio and Nadine, in heroes.json order. The last five placeholder 4-star
kits in the file: with these the tier is complete at twenty-six.

Same shape as batches one to three. No engine additions.
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
# RUMA - Tinkerer, Umbra. False Lead is Nysera's deflect link pointed the other way.
#
# THE WHOLE KIT IS ONE MECHANIC READ CAREFULLY. RedirectShare's deflect path requires the
# link to be HELD by the attacker and to have been created BY the body being hit, so a link
# Ruma stamps on an enemy, naming another enemy as its partner, moves a blow aimed at RUMA
# onto that other enemy and nobody else's blow anywhere. That is Decoy Round exactly.
#
# And "the redirected target takes 20% less damage" is the deflect's own magnitude rather
# than a second clause: at 0.8 only four fifths of the blow moves, and the deflect path
# discards the remainder instead of leaving it on Ruma - its comment says an 80% deflect is
# not a 20% hit that still lands.
# =====================================================================================
install(
    'ruma_the_false_trail',
    ability('ruma_decoy_round', 'Decoy Round',
            'Deals 120% Attack damage and applies False Lead for 2 rounds. The next single '
            'target skill aimed at Ruma lands on one of that enemy’s own allies instead, '
            'for 20% less damage.',
            [e(kind='Damage', target='FrontEnemy', power=1.2),
             e(kind='LinkUnits', target='LastTarget', secondaryTarget='RandomEnemy',
               duration=2, linkTransfer='Deflect', magnitude=0.8, charges=1)]),
    ability('ruma_everyone_looks_here', 'Everyone Looks Here',
            'Deals 170% Attack damage to all enemies and grants Ruma Concealed for 1 round. '
            'A Concealed unit cannot be targeted directly.',
            [e(kind='Damage', target='AllEnemies', power=1.7),
             e(kind='ApplyStatus', target='Self', status='Stealth', duration=1, chance=1)],
            energy=100),
    passive('ruma_spare_route', 'Spare Route',
            'Whenever False Lead sends a blow somewhere else, Ruma gains 15 Energy.',
            # OnRedirectSent fires on the RULE OWNER - the unit whose link moved the hit -
            # and deliberately not when that owner is also the destination, which is every
            # ordinary Covered ward. Ruma is never the destination here, so it is hers.
            'OnRedirectSent',
            [e(kind='EnergyGain', target='Self', flat=15)],
            oncePerBattle=False))

# =====================================================================================
# TOREN - Bulwark, Umbra. Sealed is a Mark and the door it shuts on new buffs.
#
# BuffSuppression is what "cannot receive new buffs" is in this engine, and Toren is its
# second author after Seraphine's Volume Closed.
#
# DRAFT (gaps 1): "redirects 25% of THAT hit" cannot be done. OnEnemyDamagedAlly fires once
# the blow has landed, and nothing can reach back into a resolved hit, so the ward goes up
# for the rest of the round and answers the next one.
# =====================================================================================
install(
    'toren_the_closed_door',
    ability('toren_lockstep', 'Lockstep',
            'Deals 240% Attack damage and applies Sealed for 2 rounds. A Sealed target '
            'cannot receive new buffs.',
            [e(kind='Damage', target='FrontEnemy', power=2.4),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='BuffSuppression', duration=2,
               chance=1, bypassResist=True)]),
    ability('toren_no_entry', 'No Entry',
            'Deals 410% Attack damage and Taunts the front enemy for 2 rounds. While Taunted '
            'it is Sealed.',
            [e(kind='Damage', target='FrontEnemy', power=4.1),
             e(kind='ApplyStatus', target='LastTarget', status='Taunt', duration=2, chance=1),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='BuffSuppression', duration=2,
               chance=1, bypassResist=True)],
            energy=100),
    passive('toren_unasked_question', 'Unasked Question',
            'The first time each round a Sealed enemy strikes somebody other than Toren, he '
            'takes a 15% Max HP Shield and stands in front of that ally for the rest of the '
            'round, taking 25% of every hit aimed at them.',
            # BroadcastVictim, not BroadcastSubject: only the former resolves before the
            # Taunt anchor, which is the fault Orelle's ward was found with.
            'OnEnemyDamagedAlly',
            [e(kind='Shield', target='Self', targetMaxHpRatio=0.15, duration=2,
               condition='HasMyMark', conditionOn='LastAttacker'),
             e(kind='ApplyStatus', target='BroadcastVictim', status='Covered', magnitude=0.25,
               duration=1, chance=1, bypassResist=True,
               condition='HasMyMark', conditionOn='LastAttacker')],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# LEON - Marksman, Lumen. A Case Number is a Mark, and the file is closed on a kill.
#
# DRAFT (gaps 20): "revealing current HP, Energy, and buffs to allies" has nothing to hook.
# The engine has no information layer and the battle screen already reads the whole record,
# so the clause is cosmetic and is carried in the text only.
# DRAFT (gaps 17): "10% per displayed buff, up to 40%" has no count of how many buffs a body
# carries, so it is 40% against a body carrying any buff at all.
# =====================================================================================
install(
    'leon_the_numbered_arrow',
    ability('leon_catalogue_shot', 'Catalogue Shot',
            'Deals 270% Attack damage and assigns Case Number for 2 rounds, listing the '
            'target for the whole line.',
            [e(kind='Damage', target='FrontEnemy', power=2.7),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2)]),
    ability('leon_close_the_case', 'Close the Case',
            'Deals 490% Attack damage to an enemy with Case Number, and 190% more if it is '
            'carrying a buff.',
            [e(kind='Damage', target='EnemyWithMyMark', power=4.9),
             e(kind='Damage', target='EnemyWithMyMark', power=1.9,
               condition='HasAnyBeneficialStatus', conditionOn='Target')],
            energy=100),
    passive('leon_no_unfiled_death', 'No Unfiled Death',
            'When an enemy with Case Number falls, every ally gains 8 Energy and the '
            'highest-Attack enemy is listed in its place.',
            'OnEnemyDefeated',
            [e(kind='EnergyGain', target='AllAllies', flat=8,
               condition='HasMyMark', conditionOn='Defeated'),
             e(kind='ApplyStatus', target='HighestAttackEnemy', status='Mark', duration=2,
               condition='HasMyMark', conditionOn='Defeated')],
            oncePerBattle=False))

# =====================================================================================
# LIO - Mender, Lumen. Name the wound first; the treatment follows from it.
#
# SubjectLostStatusThisFiring is how "when Lio removes a debuff, that ally recovers" is
# said, and it is said inside the ability rather than in the passive - Orelle's Hard Remedy
# is the precedent. OnStatusRemoved fires on the REMOVER and publishes no subject, so a
# passive on it cannot name the body the debuff left.
#
# DRAFT (gaps 18): that is why the Energy half pays the lowest-HP ally, which is the body
# Tested Touch cleansed by construction rather than by the engine saying so.
# DRAFT (gaps 4): "Diagnosed allies receive 18% more healing" is a healing-received
# modifier and StatusKind has none in the upward direction - HealSuppression and
# HealingReduction both cut. Diagnosed is a Mark and Lio's own healing reads it, so the
# extra comes from him rather than from every source.
# =====================================================================================
install(
    'lio_the_clear_diagnosis',
    ability('lio_tested_touch', 'Tested Touch',
            'Deals 130% Attack damage, removes one debuff from the lowest-HP ally, and a '
            'cleansed ally recovers 5% of the HP it is missing.',
            [e(kind='Damage', target='FrontEnemy', power=1.3),
             e(kind='Cleanse', target='LowestHpAlly', hits=1),
             e(kind='Heal', target='AllAllies', targetMissingHpRatio=0.05,
               condition='SubjectLostStatusThisFiring', conditionOn='Target')]),
    ability('lio_name_the_wound', 'Name the Wound',
            'Deals 230% Attack damage to all enemies and grants Diagnosed for 2 rounds. Lio '
            'heals a Diagnosed ally for 55% Attack on top of whatever else he pays them.',
            [e(kind='Damage', target='AllEnemies', power=2.3),
             e(kind='ApplyStatus', target='AllAllies', status='Mark', duration=2),
             e(kind='Heal', target='AllAllies', power=0.55,
               condition='HasMyMark', conditionOn='Target')],
            energy=100),
    passive('lio_exact_treatment', 'Exact Treatment',
            'Whenever Lio strips a debuff, the lowest-HP ally gains 10 Energy.',
            'OnStatusRemoved',
            [e(kind='EnergyGain', target='LowestHpAlly', flat=10)],
            oncePerBattle=False, limit='OncePerRound'))

# =====================================================================================
# NADINE - Vanguard, Lumen. Registered is a Mark, and hiding is what it will not allow.
#
# DRAFT (gaps 21): "Registered targets cannot become Concealed or Untargetable" needs a ban
# on gaining ONE named status. BuffSuppression bans every beneficial status at once, which
# is far wider than the sentence, so the prevention is answered after the fact instead:
# Proper Place strips the concealment the moment it lands.
# DRAFT (gaps 22): "it cannot move" has nothing to forbid. There is no movement in this
# engine beyond SwapPositions, and no status blocks that.
# =====================================================================================
install(
    'nadine_the_border_clerk',
    ability('nadine_stamp_of_entry', 'Stamp of Entry',
            'Deals 310% Attack damage and applies Registered for 2 rounds.',
            [e(kind='Damage', target='FrontEnemy', power=3.1),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2)]),
    ability('nadine_hold_the_line', 'Hold the Line',
            'Deals 520% Attack damage and holds the target in place for 2 rounds, during '
            'which it takes 12% more damage from Nadine.',
            [e(kind='Damage', target='FrontEnemy', power=5.2),
             e(kind='ApplyStatus', target='LastTarget', status='Mark', duration=2),
             e(kind='ApplyStatus', target='LastTarget', status='Vulnerable', magnitude=0.12,
               duration=2, chance=1, bypassResist=True)],
            energy=100),
    passive('nadine_proper_place', 'Proper Place',
            'The first time each round a Registered enemy hides itself, Nadine strikes it '
            'for 110% Attack damage and strips the concealment.',
            # Two-condition gating: Registered AND now carrying Stealth. OnEnemyBuffGained
            # reaches her for any beneficial status an enemy gains, and TriggerStatus filters
            # only the carrier-side OnStatusGained, so the narrowing is done here.
            'OnEnemyBuffGained',
            [e(kind='Damage', target='BroadcastSubject', power=1.1,
               condition='HasMyMark', conditionOn='BroadcastSubject',
               condition2='HasStatus', conditionStatus2='Stealth',
               conditionOn2='BroadcastSubject'),
             e(kind='Dispel', target='BroadcastSubject', hits=1,
               condition='HasMyMark', conditionOn='BroadcastSubject',
               condition2='HasStatus', conditionStatus2='Stealth',
               conditionOn2='BroadcastSubject')],
            oncePerBattle=False, limit='OncePerRound'))

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('authored 5 four-star kits: ruma, toren, leon, lio, nadine')
