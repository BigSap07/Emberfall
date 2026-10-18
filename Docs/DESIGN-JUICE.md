# DESIGN-JUICE: hit feedback that lands

This is a decision record from 2026-10-18 (run R12-JUICE). It says what each battle effect does, how
long it lasts and how to switch it off. The numbers live in the `juice` block of
`Assets/Resources/Content/vfx.json`; read that file for the truth about any one value.

## What it is

Battles already had a hit flash, a short hit stop, a knockback, a crit shake and tinted flipbooks.
R12 tunes them to one set of numbers and adds what was missing: a skill hit stop, a skill shake, a
pop on the damage numbers, numbers that stack instead of overlapping, additive aspect tint with a
glow, a skill moment (dim plus ring) and a death with a flash, a sink and a smooth HP bar.

All of it is presentation. `CombatSimulation`, the event list and every number in a fight are
untouched. The battle screen still only plays the record (`DESIGN-BATTLE-READABILITY.md`).

## The flag

- **`juice.enabled` in `vfx.json`**, default `true`. `false`, or no `juice` block at all, is the whole
  rollback: the battle plays exactly as it did before R12.
- `vfx.json` is not part of `ContentDatabase.ContentVersion`, so flipping it retires no guild replay.
- `EMBERFALL_JUICE=0` or `1` overrides it for one editor or development launch.
- `JuiceProfile` (in `Assets/Scripts/Core/Data`) reads the block. `JuiceProfile.Legacy` holds the
  pre-R12 values. `Juice.On` is what the battle code asks.
- Every new code line sits behind `Juice.On`. The old lines are still in the source, unchanged, and
  a harness tripwire checks that they are.

## Each effect

| Effect | When | Juice on | Before R12 (flag off) |
|---|---|---|---|
| Hit flash | every damaging hit | white, full strength, gone in 0.08 s | 0.75 (1.0 on a crit), about 0.19 s |
| Hit stop | the struck unit and the attacker freeze; the timeline holds once per action | 0.05 s normal, 0.09 s crit, 0.12 s skill impact | 0.05 s and 0.09 s, sprites only |
| Screen shake | crit and skill impact only | crit 0.09 for 0.16 s, skill 0.15 for 0.22 s, none on a normal hit or a kill | 0.14 crit, 0.18 on burst and shadow skills, 0.12 on a kill |
| Knockback | struck unit, away from the attacker, eases back | 10 px normal, 13 px skill, 16 px crit | about 20 px, 48 px on a crit |
| Damage numbers | every number | pop from 1.4x to 1x in 0.15 s, then rise and fade | no pop |
| Crit number | a crit | orange, 1.5x, a "CRIT" tag on its own line above | "CRIT" prefix on one line |
| Heal and shield | heal, shield gain | green and rising; light blue | the same |
| Stacking | several numbers on one unit | a new number starts at the bottom and pushes the live ones up one line (36 px, eased over 0.08 s); at most 4 per unit, 14 on screen | could overlap |
| Aspect tint | the six effect sheets | additive, tinted by the caster's aspect accent, soft glow behind the impact (0.18 s) | alpha blended, older palette on some calls |
| Skill moment | the first damaging impact of a skill | backdrop dims to 70 percent for 0.30 s (in 0.05, hold 0.15, out 0.10), a ring in the caster's aspect colour flashes behind the caster | none |
| Death | a unit dies | white flash, then fade with a sink; the HP bar empties over 0.20 s | fade, smaller sink, bar snaps to empty |

Aspect accents (DESIGN-ART-BIBLE section 1): Ember #E84532, Tide #28C7D9, Verdant #62A77A, Gale
#C6E34A, Umbra #E7E0D0, Lumen #3568E8.

The hit stop never touches `Time.timeScale` and never reaches the simulation. Sounds are not
delayed: the picture holds while the sound plays.

## The cap

A big turn cannot pile up effects. `BattleVfx.Take` refuses a quad past 40 live ones, sparks first,
crits and kills last (`JuiceMath.Admit`). Flipbooks stay at 8. Damage numbers stop at 14 on screen
and 4 per unit; the oldest goes early. Everything together is at most 62.

## Screen shake setting

Settings, Battle card: **SCREEN SHAKE: ON / OFF**, default on. It is a device setting
(`ScreenShakePrefs`, `PlayerPrefs` key `Emberfall.ScreenShake`), never in the save. It works with the
flag on or off.

## Proof

- Harness checks J1 to J15 in `Tools/DevHarness/JuiceChecks.cs`: the shipped numbers, rollback
  (flag off gives `Legacy` field by field, old lines still in source, every hook behind `Juice.On`),
  not hashed, simulation untouched (stage, hero and talent baselines unchanged), the cap, number
  stacking, curves, and the capture moment finder over 72 real fights.
- Capture target `Screen:BattleMoment=<stage>.<kind>[.<aspect>][.off]` stops on a hit, a crit, a
  skill by an aspect, a heal or a death. `.off` shows the same moment with the flag off. See
  `Docs/CAPTURE.md`.

## Limits

- The skill moment needs a damaging impact, so it lands with the blow, the hit stop and the shake. A
  heal-only or buff-only skill gets no dim and no ring. The owner may want it there too.
- Numbers pinned under the top bar wait at the same ceiling, so very old, faint numbers can share
  that line.

- Sprite units have no shader dissolve; they fade with a sink.
- The optional freeze of a flipbook during the hit stop was not built.
