# BRIEF-FORMATION.md

Screen: Formation (team setup). Rules and look: DESIGN-SCREEN-LANGUAGE.md. Spec: mockups\FormationA2 (state 1) and FormationA2b (state 2).

## Job

Pick five heroes, put them in the right slots, see the power, confirm. Under 20 seconds for a returning player.

## Layout (landscape, 1920x1080 reference)

- Left about 55 percent: the squad. Back column of 3, front column of 2, BACK and FRONT chips, an ENEMY cue on the far side. Slots show the portrait large. Empty slot is a dashed teal plus. Slot numbers 1 to 5.
- Right about 45 percent: aspect filter row (ALL plus six Cinder Notch emblems), hero count, roster tile grid, then the detail card for the selected hero.
- Bottom strip: SQUAD POWER, hint line, AUTO FILL (Secondary), CONFIRM (the one Primary).
- Back disc top left, title FORMATION. No currency pills here.

## Interactions

- Tap a hero: goes to the first empty slot, or if the squad is full the slots light up to pick which to swap.
- Tap a slot then a hero: places it. Tap a filled slot then another slot: swaps.
- Drag works as a shortcut, never the only way.
- Remove control on a filled slot at least 44 px (reference size).
- Detail card shows name, role, aspect, level, stars, and the power change if placed or swapped (+ or -).

## States

1. Squad partly filled, a roster hero selected (+410 if placed in the empty slot).
2. Squad full, a roster hero selected (+96 if swapped into a slot).
3. Roster filtered by an aspect.

## Real data

Roles are Warden, Binder, Stalker, Tinkerer. Term is "squad" everywhere. Aspect colours as in the look section. Portraits from the real hero data, not placeholders.

## Must keep working

Every current Formation function: auto fill, swap, front and back, power readout, the onboarding Team step line. Nothing removed, only moved or simplified.

## Acceptance

- BEFORE and AFTER captures at 1920x1080, 1920x1200 and 2400x1080 in states 1, 2 and 3.
- Element count before and after (R51 method), chrome at most 12 elements.
- One Primary (CONFIRM), ButtonHierarchyCheck passes.
- Touch targets at least 48 px at the 1920x1080 reference, text at least as large as today's smallest.
- Roster tile faces cropped tighter than the mock (the mock leaves them small), Gale tile vs teal ring still distinguishable.
- Fonts: confirm the Lilita One and Nunito licence files before adding them. If the licence is not clear, stop and report.
- FULL build-check exit 0, fingerprints unchanged, real save unchanged.
- Nothing about rules, stats, save format or art changes.
