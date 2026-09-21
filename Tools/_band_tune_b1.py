"""Scratch tuning pass: bring the first 4-star batch inside the star band. Not part of the build.

NUMBERS ONLY. Every edit here is a magnitude on an effect that already exists, plus the
matching number in the description so Pinoy's text stays true. No effect is added, removed
or retargeted, no trigger changes, no condition changes: the five kits do exactly what they
did, harder. That constraint is the whole point - the band is a statement about how big a
4-star is, and answering it by giving the hero another clause would be answering a
different question.

Run from the repo root: python Tools/_band_tune_b1.py
"""
import json, io, collections, re, sys

P = r'Assets/Resources/Content/heroes.json'

# (hero, ability slot, effect index, field) -> new value.
# Slot is 'basic' / 'active' / 'passive1' / 'passives.<n>'.
EDITS = [
    # ---- BRANIK, Bulwark. x0.30. ------------------------------------------------------
    ('branik_the_siege_mason', 'basic', 0, 'power', 1.55),
    ('branik_the_siege_mason', 'basic', 2, 'magnitude', 0.18),
    ('branik_the_siege_mason', 'active', 0, 'power', 3.40),
    ('branik_the_siege_mason', 'active', 1, 'selfHpRatio', 0.34),
    ('branik_the_siege_mason', 'passive1', 0, 'magnitude', 0.12),
    ('branik_the_siege_mason', 'passives.0', 0, 'magnitude', 0.12),

    # ---- DEMOS, Binder. x0.16. --------------------------------------------------------
    ('demos_the_debt_collector', 'basic', 0, 'power', 1.80),
    ('demos_the_debt_collector', 'basic', 2, 'magnitude', 0.30),
    ('demos_the_debt_collector', 'active', 0, 'power', 3.60),
    ('demos_the_debt_collector', 'passives.0', 0, 'targetCurrentHpRatio', 0.07),
    ('demos_the_debt_collector', 'passives.0', 0, 'attackCap', 1.05),

    # ---- ORELLE, Mender. x0.37. -------------------------------------------------------
    ('orelle_the_fever_warden', 'basic', 0, 'power', 1.40),
    ('orelle_the_fever_warden', 'basic', 1, 'power', 0.90),
    ('orelle_the_fever_warden', 'active', 0, 'power', 2.40),
    ('orelle_the_fever_warden', 'active', 2, 'targetMissingHpRatio', 0.15),
    ('orelle_the_fever_warden', 'passive1', 0, 'targetMaxHpRatio', 0.22),

    # ---- TAVIA, Marksman. x0.32. ------------------------------------------------------
    ('tavia_the_signal_huntress', 'basic', 0, 'power', 1.75),
    ('tavia_the_signal_huntress', 'basic', 2, 'magnitude', 0.14),
    ('tavia_the_signal_huntress', 'active', 0, 'power', 3.70),
    ('tavia_the_signal_huntress', 'passive1', 0, 'power', 0.85),
]

# Description rewrites, applied after the numbers. (hero, slot, old fragment, new fragment).
TEXT = [
    ('branik_the_siege_mason', 'basic', '105% Attack damage', '155% Attack damage'),
    ('branik_the_siege_mason', 'basic', 'reduces Block by 12%', 'reduces Block by 18%'),
    ('branik_the_siege_mason', 'active', '180% Attack damage', '340% Attack damage'),
    ('branik_the_siege_mason', 'active', 'Shield equal to 16% Max HP', 'Shield equal to 34% Max HP'),
    ('branik_the_siege_mason', 'passive1', 'gains 8% Damage Reduction', 'gains 12% Damage Reduction'),
    ('branik_the_siege_mason', 'passives.0', '', ''),

    ('demos_the_debt_collector', 'basic', '105% Attack damage', '180% Attack damage'),
    ('demos_the_debt_collector', 'basic', 'loses 4% current HP', 'loses 7% current HP'),
    ('demos_the_debt_collector', 'active', '175% Attack damage', '360% Attack damage'),
    ('demos_the_debt_collector', 'passive1', 'receive 20% less healing', 'receive 30% less healing'),
    ('demos_the_debt_collector', 'passives.0', 'loses 4% of its current HP', 'loses 7% of its current HP'),

    ('orelle_the_fever_warden', 'basic', '100% Attack damage', '140% Attack damage'),
    ('orelle_the_fever_warden', 'basic', 'for 55% Attack', 'for 90% Attack'),
    ('orelle_the_fever_warden', 'active', '155% Attack damage', '240% Attack damage'),
    ('orelle_the_fever_warden', 'active', 'recover 8% missing HP', 'recover 15% missing HP'),
    ('orelle_the_fever_warden', 'passive1', 'a 12% Max HP Shield', 'a 22% Max HP Shield'),

    ('tavia_the_signal_huntress', 'basic', '115% Attack damage', '175% Attack damage'),
    ('tavia_the_signal_huntress', 'basic', 'ignore 8% Defense', 'ignore 14% Defense'),
    ('tavia_the_signal_huntress', 'active', '230% Attack damage', '370% Attack damage'),
    ('tavia_the_signal_huntress', 'passive1', 'deals 45% Attack damage', 'deals 85% Attack damage'),
]


def slot(hero, name):
    if name.startswith('passives.'):
        return hero['passives'][int(name.split('.')[1])]
    return hero[name]


d = json.loads(io.open(P, encoding='utf-8').read(),
               object_pairs_hook=collections.OrderedDict)
H = {h['id']: h for h in d['heroes']}

for hid, sl, idx, field, value in EDITS:
    eff = slot(H[hid], sl)['effects'][idx]
    before = eff.get(field)
    if before is None:
        sys.exit('%s %s[%d] has no field %s' % (hid, sl, idx, field))
    eff[field] = value
    print('%-28s %-12s [%d] %-22s %s -> %s' % (hid, sl, idx, field, before, value))

for hid, sl, old, new in TEXT:
    if not old:
        continue
    ab = slot(H[hid], sl)
    desc = ab.get('description', '')
    if old not in desc:
        sys.exit('%s %s: description does not contain %r\n  %s' % (hid, sl, old, desc))
    ab['description'] = desc.replace(old, new)

io.open(P, 'w', encoding='utf-8', newline='').write(
    json.dumps(d, indent=2, ensure_ascii=False) + '\n')
print('\ntuned %d effect field(s) and %d description(s).'
      % (len(EDITS), len([t for t in TEXT if t[2]])))
