# Emberfall - UI theme shape

A survey of what `UITheme` holds today versus what each screen hardcodes, and a proposal for where
the line between them should sit. No screen's visual output changes as a result of this document
or the migration that follows it: every value moved is moved at its existing number.

## What UITheme already holds, and holds well

Palette (22 colours), type scale (6 sizes), layout metrics (`ReferenceResolution`, `ScreenPadding`,
`TopBarHeight`, `NavBarHeight`, two corner radii), a 9-step spacing scale, the font accessor, and
the colour helpers (`Hex`, `WithAlpha`, `Shade`, `Mix`). Plus the game-specific lookups:
`AspectColor`, `RarityColor`, `RarityName`, `RoleColor`, `RoleName`, `Stars`.

The palette is in genuinely good shape. **Not one `Hex("...")` literal exists anywhere in the UI
outside `UITheme` itself.** Every screen already draws its colours from the named palette. That is
the part of a theme most projects get wrong, and this one has it right already.

## What is hardcoded per screen

The leak is not colour identity, it is **colour treatment**: the near-black used to separate text
from a busy background, the near-black used to push a panel behind a modal, and the tints that
express a button's interaction state. Thirty two `new Color(...)` literals across nine files, and
they fall into a small number of groups rather than being thirty two independent decisions.

### 1. Text edge treatment, 14 call sites

Every one is black at some alpha, at some distance, through `UIBuilder.Outlined` or
`UIBuilder.Shadowed`. Alphas observed: 0.5, 0.55, 0.6, 0.7, 0.85, 0.9, 0.9, 0.9, 0.95, 0.95, 0.95.
Distances: 1.2, 1.4, 1.5, 1.5, 1.5, 1.8, 1.8, 2, 2.4, 3, 3, 4, 5.

These are not eleven different intentions. They are three: text on a UI panel (quiet), text over
artwork or the battlefield (strong), and display text that wants a drop shadow rather than an
outline. The spread within each group is drift, not design.

Two are not black and are doing something else: `MainMenuScreen`'s logo outline
(`0.35, 0.12, 0.02, 0.9`, a warm ember rim) and `BattleScreen`'s result title
(`0.2, 0.05, 0, 0.95`). Those are deliberate per-screen accents and should stay per-screen.

### 2. Scrim and dim, 7 call sites

Black at 0.72 (modal dim), 0.72 (locked portrait), 0.72 (locked portrait again, duplicated
verbatim in a second file), 0.78 (battle result), 0.85 (summon reveal), 0.34 (battle banner
backing), 0.22 (campaign locked hatch).

The three 0.72s are the same intent. The 0.78 and 0.85 are the same intent at drifted values.

### 3. Button interaction tints, 4 call sites, all already central

`UIBuilder.MakeButton` hardcodes `highlightedColor 1.08`, `pressedColor 0.82`,
`disabledColor 0.55/0.7`, `fadeDuration 0.06`, and a `0.07` white sheen. These already live in one
shared place, so they are not scattered, but they are unnamed magic numbers in the middle of a
construction method rather than stated theme values.

### 4. One duplication worth naming

The locked-hero portrait scrim is written twice, identically, in `Widgets.cs` and
`HeroDetailScreen.cs`: `TextureLab.Circle(160)`, `new Color(0, 0, 0, 0.72f)`, `FillParent(0f)`.
Same three lines, same numbers, two files.

## What should NOT move into the theme

Stated explicitly, because the temptation on a task like this is to centralise everything and end
up with a theme nobody can read.

**Per-screen layout numbers stay per-screen.** Box positions, card widths, portrait diameters, row
heights, the 840x520 modal panel, `ScreenChrome`'s 108f header and 76f back button. These are
composition, not identity. A theme that holds `HeroCardPortraitDiameter` is a theme that has
absorbed a screen.

**One-off alphas stay per-screen.** `WithAlpha` is called with 21 distinct values across the UI and
the frequency table is flat: 0.95 four times, 0.75 four times, 0.6 four times, then a long tail of
singletons. That is a distribution with no shared scale hiding in it. Forcing one would mean
changing values to fit, which this migration explicitly does not do.

**Aspect, rarity and role colours stay as lookups, not constants.** They are already correct.

**Animation timings stay where they are.** Twelve `Time.deltaTime` sites, essentially all with
distinct rates, each tuned to its own motion. There is no scale here either.

## Proposed shape

Three new groups in `UITheme`, and nothing else:

```
Ink        the near-black text edge treatment, three tiers plus their distances
Scrim      the near-black overlays, three tiers
Interactive  button state tints and the sheen, moved out of UIBuilder's body
```

Plus one new seam, described below, for painted art.

Each is a named tier rather than a raw number, so a call site reads as an intention
(`UITheme.InkStrong`) rather than a measurement (`new Color(0f, 0f, 0f, 0.95f)`).

**Tiers are assigned to existing call sites by nearest existing value, and the tier values
themselves are chosen from the values already in use.** Where a screen sat at 0.9 and its group's
tier is 0.9, it is unchanged. Where a screen sat at 0.95 and the tier is 0.9, that screen moves by
0.05 alpha on a text outline, which is the entire visual delta of this migration and is not
perceptible in isolation. Every such case is listed in the migration commit message rather than
being silently absorbed.

## The painted-texture hook

`TextureLab` generates every UI surface procedurally: `RoundedRect`, `Circle`, `Ring`, `Glow`,
`VerticalFade`, `Hatch`, `Sigil`, `White`. Roughly 60 call sites across the UI. When the designer
delivers painted UI art, it has to be able to replace those surfaces without editing 60 call sites.

The project already has the right convention for this and it should be reused rather than
reinvented. `BattleAssets.BackdropTexture` loads `Resources/Backgrounds/{environmentId}.png`,
falls back to a generated texture when the file is absent, and caches both paths identically, so
adding a painting is a file drop rather than a code change. `BattleAssets.UnitSprite` does the same
for `Resources/Sprites/Units/{spriteId}.png`.

`UISkin` mirrors that exactly, for `Resources/Art/UI/{name}.png`:

- A named request (`UISkin.Panel`, `UISkin.ButtonFill`) resolves to a painted sprite if the file
  exists, and to the current `TextureLab` generator if it does not.
- Missing is the normal case, not a bug, so the fallback is silent and never magenta. This matches
  `BackdropTexture`'s reasoning, not `UnitSprite`'s.
- Cached on the same key either way, so the painted and generated paths run the same code
  downstream.
- Nine-slice borders come from the sprite's own border metadata when painted, and from the existing
  radius argument when generated.

Until art arrives every call resolves to the generator it already used, so wiring `UISkin` in is a
no-op by construction. That is the point: the seam can land now and be proven inert, rather than
being invented under time pressure on the day the art shows up.

## Flagged for a human, not decided here

Two things surfaced during the survey that look like they want a real visual decision. Neither is
touched by this migration, and both are marked with a `THEME:` comment at the site.

**The battle screen's text edge treatment is doing load-bearing work no other screen asks of it.**
Seven of the fourteen outline sites are in `BattleScreen`, at the highest alphas in the project
(0.9 to 0.95), because that text sits over a painted backdrop and moving characters rather than a
flat panel. Grouping it with the rest of the UI is arguably wrong: it may want its own tier, or a
different technique entirely (a soft backing plate rather than an outline). Flagged, not resolved.

**The campaign screen's locked-stage treatment is a 0.22 black hatch, far lighter than every other
locked or disabled state in the project** (portraits use 0.72). Either the hatch texture is doing
the work and the alpha is correct, or this is drift and locked stages read as available. That is a
judgement about how locked should feel, which is a design call, not a migration one.
