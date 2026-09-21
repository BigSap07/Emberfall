"""Scratch fix: Arden's five percent-of-Max-HP terms get a cap that can bind. Not part of
the build.

THE DEFECT. Find the Measure, Define the Weak Point and Exact Calculation carry five
Damage effects whose whole amount is `targetMaxHpRatio` - power is 0.0 on every one of
them - and each carries a `maxHpCap`. That field ceilings the term against the TARGET's
own Max HP:

    cap = target.EffectiveMaxHp * effect.MaxHpCap

so a cap of 1.0 bounds a 0.02 ratio at 100% of the bar it is already 2% of. It can only
bind when the ratio exceeds the cap, and none of the five comes within a factor of thirty
of that. All five were uncapped in practice, and against the sweep arena's 190000 to
300000 HP bodies a single 3% term paid about 9000 where his 1.25-power blow paid about
1745. Zeroing the five terms took his sweep output from 748365 to 38440, so 95% of what he
reads is the five of them.

THE REPAIR, AND WHY IT IS A FIELD NAME RATHER THAN A NUMBER. `AttackCap` is the ceiling
written in the CASTER's currency - "up to 130% of Attack" - and is applied to the whole
assembled amount, which for a power-0.0 effect is exactly the term. The five values already
in the file read as attack multiples: 1.0, 1.2, 1.0, 0.5, 1.2. Arden's own talent III
already uses `attackCap` at 1.8. The numbers were always in the caster's currency and were
put in the field that reads the target's health, so they move rather than change.

`maxHpCap` is REMOVED rather than left beside the new field. It cannot bind at any of these
ratios, so leaving it would be dead config sitting next to the live cap under a name one
letter of intent away from it, which is the exact confusion this repair exists to end.

TALENT I MOVES WITH THEM. Deeper Measure overrides basic effect 0 three ways, one of which
is `maxHpCap` by +0.1 - an override of a field that does nothing, which is why nobody had
noticed. It is repointed to `attackCap`, so "Deeper Measure" deepens the term by 0.005 of
Max HP and raises its ceiling by 0.1 of Attack, which is what the name says. This moves
Arden's 15 talent-sweep lines and they are attributed in the commit.

HIS IDENTITY IS UNCHANGED. The term still scales with the target's health and still pays
more against a big bar than a small one; it now stops at a ceiling instead of not stopping.
Against a body small enough that 2% of it is under 100% of his Attack, nothing about this
hero moves at all.
"""
import json, io, collections

P = r'Assets/Resources/Content/heroes.json'

# (ability slot, effect index, the maxHpCap now on the effect, the attackCap that replaces it).
#
# THE RATIOS ARE THE FILE'S OWN AND THE SCALE IS DOUBLED, for a reason measured rather than
# guessed. Moving the five values across unchanged bounds him correctly and leaves him the
# SMALLEST Tinkerer in the sweep arena at 74224 against Oris's 99830, which is the wrong
# answer for a hero whose whole concept is taking a slice off the biggest bar on the field.
# Kiva's 1.3 and Demos's 1.05 bound ONE rider on a kit that is otherwise Attack-scaled;
# Arden is the reverse, and these five terms ARE the kit - his 1.25-power basic blow and
# 1.9-power active blow are the riders. A ceiling of four fifths to one and a quarter of his
# own active is the right scale for an effect that is the ability rather than a rider on it,
# and his own talent III already sits at 1.8. Doubled, he reads 110043: the biggest Tinkerer
# by a little, against 748365 before.
TERMS = [
    ('basic',    0, 1.0, 2.0),
    ('active',   2, 1.2, 2.4),
    ('active',   3, 1.0, 2.0),
    ('passive1', 0, 0.5, 1.0),
    ('passive1', 1, 1.2, 2.4),
]

d = json.loads(io.open(P, encoding='utf-8').read(),
               object_pairs_hook=collections.OrderedDict)
arden = [h for h in d['heroes'] if h['id'] == 'arden_the_precise_measure'][0]

for slot, idx, was, now in TERMS:
    eff = arden[slot]['effects'][idx]
    assert eff['kind'] == 'Damage', (slot, idx, eff['kind'])
    assert eff.get('power', 0.0) == 0.0, (slot, idx, eff.get('power'))
    assert eff.get('targetMaxHpRatio'), (slot, idx)
    assert eff.get('maxHpCap') == was, (slot, idx, eff.get('maxHpCap'))
    del eff['maxHpCap']
    eff['attackCap'] = now
    print('%-9s [%d]  %.2f of Max HP  maxHpCap %.1f (inert) -> attackCap %.1f'
          % (slot, idx, eff['targetMaxHpRatio'], was, now))

ov = arden['talents']['talentI']['overrides']
hit = [o for o in ov if o['field'] == 'maxHpCap']
assert len(hit) == 1, hit
hit[0]['field'] = 'attackCap'
print('talentI   Deeper Measure: override maxHpCap -> attackCap (+%.2f)' % hit[0]['delta'])

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
