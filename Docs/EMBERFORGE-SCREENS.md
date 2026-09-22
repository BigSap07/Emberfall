# Emberforge, screen by screen

What this branch is doing and how far it has got. `DESIGN-EMBERFORGE.md` is the decision record
for what the kit IS and why it is baked; this is the ledger of which surfaces speak it.

`DESIGN-BUILD-ORDER.md` is not touched from here. It gets its entry when this branch merges.

## How a screen is counted converted

All five, or it is not converted:

1. Its ground is the kit's, not the shared near-black violet every unconverted page sits on.
2. Its header is `ScreenChrome.ForgedHeader` (or it is embedded in a sheet that draws one).
3. Every surface on it comes from `EmberforgeUI`. No `TextureLab.RoundedRect` plate, no
   `UIBuilder.Frame` hairline, no `UISkin.Panel`, no `UIBuilder.PrimaryButton` family.
4. Every label is `EmberforgeUI.Label`, `.Title` or `.Body`, so it is in Barlow Condensed or
   Cinzel rather than the default face, and carries the kit's dark stroke where it sits on colour.
5. It has been photographed at **both** aspects and the pair has been looked at.

## The shots

`Tools/capture-ui.sh <Target> [<Target> ...]` drives an open editor and writes a pair per screen:

- `Docs/ui-shots/<screen>.png` at **1920x1080**, the reference canvas `UITheme` lays out against.
- `Docs/ui-shots/<screen>-2400.png` at **2400x1080**, which is 20:9.

Both, always. The canvas scaler blends the two aspect ratios, so a 20:9 phone gets a logical
canvas roughly 2147x966 - about 114 units SHORTER than the 1920x1080 an editor capture proves.
`DESIGN-EMBERFORGE` records three sections of the component gallery that overflowed their panels
at 20:9 and nowhere else. One aspect is half a check.

## Status

| Screen / surface | Status | Notes |
| --- | --- | --- |
| Hub (`MainMenuScreen`) | converted | Landed on `main` with the kit. This branch removed the `SourceFileWidth`/`SourceFileHeight` workaround: the paintings import at their authored size now, so the fitter reads the sprite's own rect. |
| Hero list (`HeroesScreen`) | converted | Landed on `main` with the kit. |
| Idle claim popup | converted | Landed on `main` with the kit. |
| Building nametags | converted | Landed on `main` with the kit. |
| Toast, confirm dialog (`Overlays`) | converted | Panel plus ribbon; the dialog's two answers are the kit's two button voices. |
| Panel sheet (`HudWidgets.Sheet`, `PanelHost`) | converted | Was deliberately held back while the five screens it frames were unconverted. They are converted, so it is. |
| Close cross, tab bar (`HudWidgets`) | converted | Both forward to `EmberforgeUI`, so a sheet's cross and a popup's cross cannot drift. |
| Top bar backing (`HudTopBar`) | converted | The seam between the two languages. A recess with a gold rule under it. |
| Currency strip (`CurrencyStrip`) | converted | Shared by the shop, the Rift, the Recital and Events. |
| Stat rows, ability cards (`Widgets`) | converted | Shared by every page with a stat block. |
| Item card (`ItemCard`) | converted | Keeps its chamfered silhouette on purpose - that is the item language, not the chrome language - and gains the kit's outline. |
| Save conflict chooser | converted | Both answers are primary, deliberately: there is no way out of this card, so teal would be lying. |
| Overflow menu | converted | Already on the kit. |
| Component gallery (`DesignGallery`) | converted | Gained the chip, the rule and the stat row. |
| Battle result | converted | The card grew to 700 and both its lists scroll: the spoils are four rows after a battle and eight after a stage clear, and the damage table is one row per unit that fought, so both used to run out of the bottom of the card. |
| Battle HUD | converted | The unit tags keep their plain 7-unit bars on purpose - a nine-sliced trough has no middle left to stretch at that height and renders as two squashed corners. The kit reaches the text on them. |
| Hero detail | converted | The two panels stretch between the header and the action bar now. Fixed at 636 they needed 1056 units of height, so at 20:9 the action bar was drawn up through both of them. No ring round the portrait any more. |
| Summon | converted | The banner stage stretches the same way: the action block was off the bottom of a 20:9 screen entirely. The guarantee bar is the kit's progress bar, at the sprite's own height - at 18 units it rendered as a lens. |
| Inventory | converted | The category rail is a tab bar rather than eight buttons. The capture seeds real armour and relics, because the empty state proves nothing about the grid or the detail panel. |
| Campaign | converted | The painting stays the ground. The chapter row is a tab bar and the stage stepper's pips are the kit's disc in its two depths, so "you are here" is told by depth rather than by a tint on a baked bevel. |
| Settings | converted | RESET PROGRESS is teal on purpose: the kit's ember voice is the thing to press, and the one destructive control in the game is not it. |
| Profile | converted | The avatar picker scrolls horizontally and its faces are tabs. A layout group in a fixed rect does not overflow, it divides - thirty-two owned heroes were fifty pixels each with their names printed through one another. |
| Account | converted | Five cards, five text fields, and the kit's disabled button art doing the work the offline state needs. |
| Forge | pending | |
| Formation | pending | |
| Relic shop | pending | |
| Friends | pending | |
| Friend's team | pending | |
| Guild | pending | |
| Guild requests | pending | |
| Guild chat | pending | |
| Events | pending | |
| Deep Recital | pending | |
| The Proving | pending | |
| First run | pending | |

## Components this branch added to the kit

| Component | Slots baked | Why it did not already exist |
| --- | --- | --- |
| `EmberforgeUI.Chip` | `forge_chip_body`, `forge_chip_line` | Aspect, role, temper and grade tags are on eight screens and every one drew a flat translucent rectangle. Two sprites rather than one for the reason the currency jewel is two: `Image.color` multiplies the whole sprite, so a single tinted chip tints the kit's dark outline along with its fill and every aspect ends up with a differently coloured outline. |
| `EmberforgeUI.Rule` | `forge_rule` | The hairline between two sections of one panel. Gold, because everything structural in the kit is gold, and outlined above and below so it reads as inlaid rather than as a scratch. |
| `EmberforgeUI.StatRow` | (built from `forge_rule`) | One row of a stat table. `Widgets.StatRow` forwards to it, which converts every stat block in the game from one edit. |
| `EmberforgeUI.Sheet` | (Panel) | The large framed surface a hosted screen sits in. Not a slot of its own: a sheet and a panel are one object at two sizes. |
| `EmberforgeUI.CloseDisc` | (Disc) | The X in the corner of every layer. |
| `EmberforgeUI.TextField` | (Sunken) | Five text boxes across the account and guild screens. |
| `EmberforgeUI.Heading` | - | A section heading inside a panel. Deliberately NOT a ribbon: a ribbon is laid across a panel and overhangs it, which is right for the one thing a panel is called and wrong for the third heading down a scrolling card. |
| `EmberforgeUI.Sunken` | (Sunken) | The recess as a child, for a node that already has a Graphic on it. |
