"""Scratch script for the two text repairs in the five-star review. Not part of the build.

Neither of these touches an effect, so no fingerprint moves and no band reading changes.
Both are cases where the sentence a player reads and the behaviour the engine resolves
disagree, and in both the behaviour is the one to keep.

DEMOS. Notice of Loss reads "At turn end, Arrears loses 4% current HP" and names no
bound. The shipped tick does carry one - an attackCap of 105% of Demos's Attack on
demos_arrears_tick - and that bound is the only thing holding the clause inside the star
band: removing it was tried and measured at x3.40 of his role's 5-star median against
x0.28 with it in place (Docs/ENGINE-GAPS.md group 12). The bound is written in the
tick passive's own description and nowhere a reader of the basic would find it, so the
basic's sentence promises an unbounded percentage the kit does not deliver. The text
moves; the cap stays.

KIVA III. Unclaimed Victory reads "12% of her Max HP ... and 12% more for each Broken
Link she is holding, up to 36%". The three Shield clauses that pay it are authored
correctly and the pool adds, so 12 / 24 / 36 is what the engine WOULD pay. It never
pays more than 12. Measured over the four dark arenas that can raise a Forsaken kill
(s_soft_mid_big, s_soft_big, s_soft_soft_big, s_soft4_triple) at three seeds each, and
over the hero sweep at five: every payout is exactly 1118, which is 12% of her 9320 Max
HP at level 40, and there is never a second ShieldGain event beside it. The cause is the
rate the kit-defect pass already named and did not change - Abandoned Support banks at
most one Broken Link a round and Severance Paid spends one on the next basic against a
Forsaken body - so the kill arrives at zero links every time. The two link-gated clauses
are authored, reachable in principle and inert at these rates, and the text now says so
rather than advertising a 36% no measurement has ever produced.
"""
import collections
import io
import json

O = collections.OrderedDict

DEMOS_BASIC = ("Deals 180% Attack damage and applies Arrears for 2 rounds. At turn end, Arrears "
               "loses 4% of its current HP, bounded at 105% of Demos's Attack. The bound is not in "
               "Pinoy's sentence and is the only thing holding the clause inside the star band; it "
               "is enforced on the Notice of Loss tick passive - see Docs/ENGINE-GAPS.md group 12.")

KIVA_III = ("When Kiva defeats a Forsaken target, she gains a Shield equal to 12% of her Max HP "
            "for 2 rounds, and 12% more for each Broken Link she is holding, up to 36%. All Broken "
            "Links are consumed. Measured: every payout in every harness arena is 12%, because "
            "Abandoned Support banks at most one Broken Link a round and Severance Paid spends it "
            "on the next basic against a Forsaken body, so a kill finds none held. The two "
            "link-gated clauses are authored and inert at those rates.")


def load(path):
    return json.loads(io.open(path, encoding='utf-8').read(), object_pairs_hook=O)


def save(path, doc):
    io.open(path, 'w', encoding='utf-8', newline='').write(
        json.dumps(doc, indent=2, ensure_ascii=False) + '\n')


def hero(doc, hero_id):
    return [h for h in doc['heroes'] if h['id'] == hero_id][0]


P = r'Assets/Resources/Content/heroes.json'
d = load(P)

demos = hero(d, 'demos_the_debt_collector')
assert demos['basic']['id'] == 'demos_notice_of_loss', demos['basic']['id']
tick = [p for p in demos['passives'] if p['id'] == 'demos_arrears_tick'][0]
assert tick['effects'][0]['attackCap'] == 1.05, 'the cap this sentence is about has moved'
demos['basic']['description'] = DEMOS_BASIC

kiva = hero(d, 'kiva_the_forsaken_oath')
t3 = kiva['talents']['talentIII']
assert t3['id'] == 'kiva_unclaimed_victory', t3['id']
assert len(t3['effects']) == 4, 'Unclaimed Victory is no longer three shields and a spend'
t3['description'] = KIVA_III

save(P, d)

# The text authority for a talent is import/Heroes.json, the same place the Kiva III
# wording was carried to when it was first decided. Demos's basic is not touched there:
# Pinoy's sentence is Pinoy's, and the bound is ours.
I = r'import/Heroes.json'
imp = load(I)
hero(imp, 'kiva_the_forsaken_oath')['talents']['talentIII']['description'] = KIVA_III
save(I, imp)

print('demos: the basic names the 105%-of-Attack bound its tick already enforces')
print('kiva III: the text says what the kit pays, 12%, and why the other two clauses do not')
