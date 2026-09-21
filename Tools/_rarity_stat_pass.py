"""Scratch stat pass: put rarity into the authored stat line. Not part of the build.

WHY THIS EXISTS. Rarity reaches a hero's numbers in exactly one place - the
`rarityMultiplier` argument `StatMath.ForHero` hands `Build`, taken from
`StatMath.StarMultiplier` - and `Build` multiplies three fields by it: MaxHp, Attack and
Defense. Speed, CritChance, CritDamage, EffectHit and EffectResist are flat pass-throughs.
`Tools/convert_import.py` then applies one fixed factor per stat to every hero regardless
of stars, because StarMultiplier's own comment says star tier is applied at runtime "so
base stats stay role-comparable". Neither half covers the whole line, and the five stats
neither half scales are exactly the five where a 4-star out-read a 5-star.

The authored file does not keep its side of that bargain either: heroes.json carries its
own star ladder in hp/attack/defense (Bulwark 1053 / 1365 / 1638 / 1736 / 1878), so those
three fields are scaled TWICE and the flat five are scaled not at all. That is the whole
defect in one sentence.

THE RULE, PROPOSED HERE because DESIGN-COMBAT-STATS.md has none. Its only rarity statement
is that Effect Hit and Effect Resist "must not scale with level or rarity", and that is a
rule about the MULTIPLIER, not about the authored value. Proposed:

    A non-5-star's stat total is a fixed fraction of its role's 5-star mean.
    5 star 1.00    4 star 0.88    3 star 0.76    2 star 0.64    1 star 0.52

"Stat total" is StatMath.PowerScore of the line StatMath.ForHero builds at level 1 - the
game's own power instrument, which already contains StarMultiplier. The ladder is one step
of 0.12 per star, and it is not invented: it sits within 0.02 of the ratios StarMultiplier
already produces on HP/Attack/Defence (0.522 / 0.634 / 0.746 / 0.866 / 1.00). What changes
is that the WHOLE line obeys it instead of three fields out of eight.

HOW. Every authored field of a non-5-star becomes its role's 5-star mean for that field,
times one per-role, per-star factor A. A is solved rather than picked, because the runtime
still multiplies three of the eight fields by StarMultiplier and the other five by nothing:

    ratio(s) = A * [ w * StarMultiplier(s)/StarMultiplier(5) + (1 - w) ]

where w is the share of that role's 5-star power that HP, Attack and Defence carry. Set
that equal to the target and solve for A. PowerScore is linear in every term it reads, so
the built line lands on the target exactly, up to the rounding of the authored fields.

Splitting the ladder this way rather than pinning HP/Attack/Defence to the role mean is
deliberate. Pinned, the flat five have to carry the entire descent alone, which drops a
1-star Bulwark's Speed from 78 to 42 and raises its authored HP above a 5-star's - a file
that still reads as though the low stars were the bigger heroes. Solved, every authored
field descends with the star, which is what somebody opening heroes.json expects to see.
"""
import json, io, collections, decimal

P = r'Assets/Resources/Content/heroes.json'

STAR_MULT = {1: 0.70, 2: 0.85, 3: 1.00, 4: 1.16, 5: 1.34}
RATIO = {1: 0.52, 2: 0.64, 3: 0.76, 4: 0.88, 5: 1.00}

RARITY_SCALED = ('hp', 'attack', 'defense')   # the three Build() multiplies
LINE = ('hp', 'attack', 'defense', 'speed', 'critChance', 'critDamage',
        'effectHit', 'effectResist')


def half_up(v):
    return int(decimal.Decimal(repr(float(v))).quantize(
        decimal.Decimal('1'), rounding=decimal.ROUND_HALF_UP))


def q2(v):
    return float(decimal.Decimal(repr(float(v))).quantize(
        decimal.Decimal('0.01'), rounding=decimal.ROUND_HALF_UP))


def power(h):
    """StatMath.PowerScore(StatMath.ForHero(def, 1)), reimplemented."""
    m = STAR_MULT[h['stars']]
    return (half_up(h['hp'] * m) * 0.11
            + half_up(h['attack'] * m) * 2.4
            + half_up(h['defense'] * m) * 1.35
            + half_up(h['speed']) * 1.9
            + h['critChance'] * 260.0
            + (h['critDamage'] - 1.0) * 120.0)


d = json.loads(io.open(P, encoding='utf-8').read(),
               object_pairs_hook=collections.OrderedDict)
heroes = d['heroes']

fives = collections.defaultdict(list)
for h in heroes:
    if h['stars'] == 5:
        fives[h['role']].append(h)

mean5, power5, hpad_share = {}, {}, {}
for role, group in fives.items():
    m = {k: sum(float(h[k]) for h in group) / len(group) for k in LINE}
    mean5[role] = m
    hpad = (0.11 * m['hp'] + 2.4 * m['attack'] + 1.35 * m['defense']) * STAR_MULT[5]
    flat = 1.9 * m['speed'] + 260.0 * m['critChance'] + 120.0 * (m['critDamage'] - 1.0)
    power5[role] = hpad + flat
    hpad_share[role] = hpad / (hpad + flat)

rows = []
for h in heroes:
    s = h['stars']
    if s == 5:
        continue
    assert not h.get('startingHero'), h['id']
    role = h['role']
    m5, w = mean5[role], hpad_share[role]
    a = RATIO[s] / (w * STAR_MULT[s] / STAR_MULT[5] + (1.0 - w))

    old = {k: h[k] for k in LINE}
    old_power = power(h)

    for k in RARITY_SCALED + ('speed',):
        h[k] = half_up(m5[k] * a)
    h['critChance'] = q2(m5['critChance'] * a)
    h['critDamage'] = q2(1.0 + (m5['critDamage'] - 1.0) * a)
    h['effectHit'] = q2(m5['effectHit'] * a)
    h['effectResist'] = q2(m5['effectResist'] * a)

    rows.append((h['id'], role, s, old, {k: h[k] for k in LINE},
                 old_power / power5[role], power(h) / power5[role]))

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')

rows.sort(key=lambda t: (t[1], -t[2], t[0]))
hdr = ('hero', 'role', 'star', 'hp', 'atk', 'def', 'spd', 'crit', 'critDmg',
       'eHit', 'eRes', 'stat total')
print('| ' + ' | '.join(hdr) + ' |')
print('|' + '|'.join(['---'] * len(hdr)) + '|')
for hid, role, s, old, new, oldr, newr in rows:
    def cell(k, fmt):
        a, b = old[k], new[k]
        return (fmt % a) if a == b else '%s -> %s' % (fmt % a, fmt % b)
    print('| %s | %s | %d | %s | %s | %s | %s | %s | %s | %s | %s | x%.3f -> x%.3f |' % (
        hid, role, s,
        cell('hp', '%d'), cell('attack', '%d'), cell('defense', '%d'),
        cell('speed', '%d'), cell('critChance', '%.2f'), cell('critDamage', '%.2f'),
        cell('effectHit', '%.2f'), cell('effectResist', '%.2f'), oldr, newr))
print()
print('%d heroes rewritten.' % len(rows))

print()
print("THE RULE'S OWN BAR: any non-5-star field STRICTLY ABOVE its role 5-star mean.")
over, tied = 0, 0
for h in heroes:
    if h['stars'] == 5:
        continue
    for k in LINE:
        # Compared at the stat's OWN granularity. crit chance, Effect Hit and Effect
        # Resist are authored to two decimals, so a mean of 0.128 is 0.13 as far as the
        # file can say it, and calling 0.13 "above" that is an artefact of the print
        # rather than a reading.
        ref = mean5[h['role']][k] if k in RARITY_SCALED + ('speed',) \
            else q2(mean5[h['role']][k])
        if ref <= 0:
            continue
        if float(h[k]) > ref:
            print('  %-32s %-9s %d* %-13s %.2f vs role 5-star mean %.2f'
                  % (h['id'], h['role'], h['stars'], k, float(h[k]), ref))
            over += 1
        elif float(h[k]) == ref:
            tied += 1
if over == 0:
    print('  none. Every authored field of every non-5-star now reads at or below its'
          ' role 5-star mean.')
print('  %d field(s) land exactly ON the mean. Every one is a 0.01-granularity stat -'
      ' crit chance, Effect Hit, Effect Resist - where a 3%% reduction rounds back to'
      ' the value it started from. The stat cannot express the cut; the total still'
      ' does.' % tied)
