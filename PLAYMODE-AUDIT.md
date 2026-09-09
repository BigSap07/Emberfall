# Emberfall - Play mode audit

What a running build actually does, measured in Play mode at 1600x900 on 2026-09-09 against
`main`. Produced by driving the live editor: entering Play mode, stepping frames deterministically
at 20ms each, sampling every unit's transform every frame, walking every screen in `ScreenRouter`,
and measuring text geometry against its containers.

This file is **status, not direction**. It says what was true on the date above and nothing about
what should change. Nothing here is a design opinion: every entry is a number or a screenshot.
Re-run the audit rather than trusting this file - a single layout commit invalidates section 7,
and the battle sections describe three stages out of twenty-four.

The probes are kept in `Tools/playmode-audit.cs.txt`, including the four harness traps and the
three measurement mistakes that produced false findings on the first passes. Read that before
re-running, not after.

## What was NOT tested, and why

Stated first because a findings list with silent gaps reads as a clean bill of health.

- **Clickability was not tested at all.** Screens were driven with `onClick.Invoke()`, which calls
  the handler directly. It does not raycast and does not go through the `EventSystem`, so a button
  under an overlay, with a broken hit area, outside its parent's rect, or in a non-interactable
  `CanvasGroup` still reports success. **No claim in this document says any control is clickable.**
  Every "reachable" result below means the route ran, not that a finger could start it.
- **Z-fighting was not tested.** No reliable measurement was found. The units are procedural
  primitives that overlap by construction, so coincident-bounds detection returns the rig's own
  body parts and says nothing about coplanar depth conflict. Reported as untested rather than
  passed.
- **The Gale re-entry extra turn never fired**, so its out-of-order case was not exercised. Ryn
  Solvarr was deployed and fought (his rendering passed every check), but the game's own combat log
  shows every unit taking exactly one turn per round. A second `Turn` line for one unit in one round
  is the only trace an `ExtraTurn` leaves, and there was none. Not a pass.
- **Sections 1 to 4 cover three stages** - 1-1, 1-12 and 2-6, plus 1-10 and 1-12 for the battle HUD
  - not all twenty-four.
- **Section 7 measures text.** Overlap between non-text elements, icon crowding and colour contrast
  were not measured.

## Findings, worst first

| # | Sev | Finding | Where | Screenshot |
|---|-----|---------|-------|------------|
| 1 | High | The hero power readout `POWER 1205` is drawn entirely underneath the opaque CHANGE BANNER button and is never visible; 2750px2 of its glyph box sits under `Hero/Formation`, both at full alpha and active in hierarchy | Hero Detail | `Tools/Screenshots/audit_HeroDetail.png` |
| 2 | Medium | The front enemy nameplate is occluded by the AUTO button - "Hollow Husk" renders as "Hollow Hus." with its HP bar running under the button; 144px2 of glyph box under `Auto` | Battle, stage 1-10 | `Tools/Screenshots/audit_Battle_ch1_10.png`, `Tools/Screenshots/screen_Battle.png` |
| 3 | Low | Action label and cost line overlap inside all three action buttons: `SALVAGE`/`+4 SLAG` 356px2, `COMBINE`/`30 SLAG` 350px2, `REROLL`/`12 SLAG` 350px2. Roughly 2-3px of vertical glyph-box overlap; both lines stay legible | Forge | `Tools/Screenshots/audit_Forge.png` |
| 4 | Low | `SALVAGE` and its cost overlap the tray graphic behind them, 293px2 and 211px2; both stay legible | Forge | `Tools/Screenshots/audit_Forge.png` |
| 5 | Info | Every enemy in the three sampled stages falls back to the procedural rig - `SpriteAnimator.TryCreate` returns null without an `idle` clip. All five deployed heroes have clips. This is the designed fallback, recorded because it bounds what section 4 can mean | Battle, all sampled stages | `Tools/Screenshots/audit_Battle_ch1_12.png` |

Finding 2 is stage-dependent: it reproduces on 1-10 and not on 1-1 or 1-12, because which slot the
top enemy occupies changes with the roster.

## 1. Grounding - PASS

Every unit's rig root sampled every frame, 2200 frames per stage, three stages, 27 units.

- **Resting Y is exactly 0.0000 for every unit on every stage**, matching the slot plane
  (`BattleStage` seats actors at `(slot.x, 0, slot.z)`).
- The only departure from zero is a peak of **0.279**, on units that cast. That is
  `UnitActor.CastRoutine`'s authored lift, `sin(k * PI) * 0.28`, which the routine resets to
  `Vector3.zero` on completion. It is a deliberate animation and it lands back on the plane.
- No unit's feet left the ground outside that animation. No resting Y differed from its slot's
  expected Y by any amount.

The first pass of this check reported a completely motionless battle because it sampled
`actor.transform`, which never moves - the lunge writes `Rig.Root.localPosition` and the death sink
writes `Rig.Body.localPosition`. Corrected before any result was recorded.

## 2. Framing and layering - PASS on framing, layering partly untested

- **No unit left the camera frustum on any frame**: 0 off-frame frames out of 2200 per stage, all
  27 units, both sides, all five slots.
- Viewport spread stays comfortably inside the frame: x 0.18 to 0.81, y 0.13 to 0.84.
- **No unit is drawn behind another**: the five slots per side occupy disjoint vertical screen
  bands (0.13-0.15, 0.18-0.20, 0.29-0.32, 0.44-0.47, 0.50-0.52, 0.70-0.74, 0.76-0.79, 0.81-0.84),
  so no two units share screen space to contest.
- Camera distance by slot is 8.47 / 8.74 / 11.89 / 14.14 / 16.12. The back-rank outer slot is
  nearer the camera than the front-rank inner slot, which is the diagonal formation working as
  designed, not a depth inversion: rank is the x axis, not z.
- **Z-fighting: not tested.** See the untested section.

## 3. Facing - PASS

Every ally held exactly **150 degrees** and every enemy exactly **210 degrees** for the whole of
every sampled battle - 6600 frames across three stages, with no other value ever recorded. That is
`UnitRig`'s `180 -/+ ThreeQuarterTurn` rule intact, allies leaning toward +X and enemies toward -X.
The regression this check exists for is not present.

## 4. Animation integrity - PARTIAL

- **Nothing is stuck after death.** Frame-to-frame movement of every dead unit, measured from 120
  frames (2.4s) after death to the end of the battle, is **0.0000** for all fourteen deaths across
  the three stages. No unit kept animating past its death.
- **No `Animator` components exist anywhere in a battle.** The rigs are procedural, driven by
  `UnitRig` plus an optional `SpriteAnimator`, so "an Animator stuck in a state" has nothing to
  read and cannot be reported either way.
- **No missing-clip fallback fired as a fault.** All five deployed heroes carry a `SpriteAnimator`;
  every enemy in the three stages carries none and renders procedurally, which is the documented
  fallback for a unit that ships no `idle` clip.
- **The Gale extra turn was not exercised** - see the untested section.

## 5. Reachability - PASS

All fourteen screens in `ScreenRouter` were entered and returned from. **Every one routed to its
own screen class and raised zero errors on entry.** No route errored, no screen threw, and no
screen is unreachable from the hub:

- Ten are one hop from the hub: Campaign, Events, Forge, Formation, Inventory, Proving, Recital,
  RelicShop, Summon, and the hub itself.
- Profile hangs off the rank pill in `HudTopBar`, which is present on the hub.
- Battle is entered from Campaign, Recital and Proving.
- Heroes and HeroDetail are two and three hops out, via Formation. The hub's node labelled HEROES
  opens **Formation**, not Heroes; Heroes has exactly one inbound route, from Formation.

Reachable here means the route ran without error. It does not mean the button can be pressed - see
the untested section.

## 6. Console - PASS

**Zero errors** across the whole run: the three-stage battle walk, the fourteen-screen sweep, the
capture sequence and the layout passes. One warning, and it is one of the three known ambient ones
(the AI-toolkit account timeout). The Input Manager and Dynamic Batching deprecation warnings did
not recur after the editor restart part-way through the session. Nothing outside that set appeared.

## 7. Layout at 1600x900 - 11 of 14 screens clean

`Emberfall/Capture/ALL of the above, in sequence` was run at the forced resolution and produced all
eight of its targets at a verified 1600x900. The six screens it does not cover were shown and
captured individually at the same resolution. Text geometry was then measured on all fourteen.

- **No text is clipped by its container anywhere.** Zero across all fourteen screens.
- **Nothing sits outside the safe area.** Zero, once scroll content under a `RectMask2D` is
  excluded - that content is clipped by a mask on purpose.
- **Clean:** Main Menu, Profile, Formation, Heroes, Campaign, Summon, Relic Shop, Inventory,
  Events, Recital, Proving.
- **Not clean:** Hero Detail (finding 1), Forge (findings 3 and 4), Battle (finding 2).

Three earlier versions of this measurement produced false findings that are not in the table above,
recorded here so a re-run does not repeat them: counting `Overflow` text as clipped flagged the
Proving title, which renders in full; comparing `RectTransform`s instead of glyph boxes flagged
every left/right-aligned label-and-value row on Profile, Proving and Recital; and measuring the
battle HUD after 35 settle frames caught all eight nameplates stacked at the origin before they had
been positioned. All three were checked against the captures and withdrawn.
