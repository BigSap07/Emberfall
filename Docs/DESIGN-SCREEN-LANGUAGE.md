# DESIGN-SCREEN-LANGUAGE.md

Status: look decided 2026-10-25 (Skin A2, section 11). Owner decision. Formation is the first build, everything else follows it.

## Why this exists

Every screen was being re-argued. These rules are the default for every screen. A screen may break one only with a written reason in its brief.

## 1. Frame (every screen)

- Round back disc top left. HOME only where it really goes home (R52 rule).
- Currency pills top right, at most two plus the plus buttons that already exist. No other top bar items on inner screens.
- Title is one short word, beside the back disc. No subtitle line.
- One main panel. Maximum two levels of surface (page, panel), a third only for a selected detail.

## 2. Navigation inside a screen

- Sections are a tab rail on the left or right edge of the panel (text tabs, one lit).
- Filters: one row of aspect emblems or one Filter button. Never both.
- Sorting and layout choices live behind one small control.

## 3. Tiles (heroes and items)

- A hero tile is portrait, level, stars and one small role badge. The tile colour carries the aspect, so no aspect emblem on the tile. No name on the tile. Name, role and stats live in the detail view.
- State is one mark: in squad (check), locked (padlock), new (dot). Never two marks on one tile.
- An item tile is icon, count, tier rim (R47 ItemTile and palette).
- Minimum tile touch size 48 px at the 1920x1080 reference.

## 4. Select, then read, then act

- Tap a tile: it lights, and a detail panel shows name, role, the three or four stats that matter, and the action.
- The detail panel carries the one Primary button for the selection.
- Compare before commit: placing or equipping shows the change (+ or -) before the button is pressed.

## 5. Buttons (R53)

- One orange Primary per screen. Secondary for every other action, including paid ones. Quiet for back, close, cancel. Danger tone for destructive.
- A row or card has at most one button.

## 6. Density budget (R51 method)

- Roster or grid screens: chrome around the grid at most 12 elements, tiles count as one group.
- Detail screens: at most 4 stat rows, 1 Primary, 1 to 2 Secondary.
- List rows: at most 4 pieces of information per row.

## 7. The Hub

Four corner clusters, the middle stays free. Top left player card. Top right currencies, mail, friends, menu. Bottom left one strip of text destinations. Bottom right the one big action (Continue Campaign).

## 8. Chat

A drawer over the Hub, not a screen. Needs moderation rules before any world channel. Until then a Discord link in Settings.

## 9. What we do NOT copy

Idle Heroes' yellow and gold accents, wood panels, ribbons and icon set. Cream surfaces are fine, the accents and ornaments are ours. Never pixel for pixel.

## 10. Order of work

Formation, Heroes roster, Bag with detail panel (also the gear equip flow), Hero Detail, Hub. One screen per run.

## 11. The look: Skin A2

Spec boards: handoff/R55-FORMATION/mockups/FormationA2.dc.html and FormationA2b.dc.html (PNGs beside them), plan in SKIN-PLANS.md.

- Surfaces: cream panels with a tan well, 3px dark brown outline plus a 4px bottom bevel on panels, tiles and buttons. No gradients, no glows, no gold corner ornaments.
- Type: Lilita One with a dark stroke for titles, numbers and button text. Nunito for body. Both are open licence fonts, confirm the licence files before adding them to the repo.
- Colour roles: ember orange #E8802A only for the single Primary. Teal #3FA7B8 for Secondary, the lit filter, selected tile and lit slots. Aspect colours fill tiles at full saturation as the UI uses them today (Ember #D6452B, Tide #3C8FD6, Verdant #4FA35A, Gale #8FD3C9, Umbra #8A5CC2, Lumen #E5C14A). Stars #F4A62A on a small dark plate. Open question: DESIGN-ART-BIBLE lists different hex values for some aspects (Umbra bone-white, Lumen cobalt). Not resolved, keep the values above.
- Identity:
  - Cinder Notch emblems: flat-top hexagonal ring of six bars with one bar absent, gap position is the aspect (Ember top, Tide upper right, Verdant lower right, Gale bottom, Umbra lower left, Lumen upper left). Used in the filter row, squad slot corners and detail card. Not on roster tiles.
  - Squad slots are rounded squares with the top left corner cut, the emblem seated in the cut.
  - One Cinder Seam per screen at most: a 1px low contrast crack crossing a panel rim. Never touching text or controls.
- Background: the scene image dimmed and blurred behind the panel.
- Weaknesses to watch on a phone: tile faces are small inside the colour block, the Gale tile and the teal selection ring are the closest colour pair.
