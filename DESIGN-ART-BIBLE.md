# Emberfall - Short Art Bible

The core visual principle: **every hero looks like a complete person reconstructed from an incomplete memory.** Aspect is visible as a rule acting upon that reconstruction, not as elemental VFX or faction clothing.

All characters: ~4 heads tall, large head/hands/gear, crisp dark outline, cel shading, maximum 3 major value groups per silhouette. Aspect read must survive at thumbnail size with VFX disabled.

## 1. Aspect palettes + material worlds

| Aspect      | Accent                          | Material world                                                                                                                                                                                                                                   |
| ----------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Ember**   | **Hot vermilion-red** `#E84532` | Things made through irreversible processes: blackened forged steel, cracked lacquer, welded seams, quenched glass, ceramic repaired with conspicuously different material. Every object looks like it has survived a previous version of itself. |
| **Tide**    | **Electric cyan** `#28C7D9`     | Translucent laminates, polished alloys, mirrored surfaces, flexible glass, articulated joints, reversible garments. Materials visually refuse to commit to one direction or configuration.                                                       |
| **Verdant** | **Oxidized jade** `#62A77A`     | Patched hide, old wood, horn, tarnished copper, layered cloth, grafted materials. Nothing gets replaced when damaged: another layer is attached and allowed to keep growing.                                                                     |
| **Gale**    | **Acid chartreuse** `#C6E34A`   | Paper-thin metal, suspended plates, loose ribbons, wire, separated armor components. Pieces look detached from gravity and from one another, held in approximately the correct place by an unseen rule.                                          |
| **Umbra**   | **Bone-white** `#E7E0D0`        | Matte black ceramic, smoked glass, unfinished cloth, blank metal, surfaces where ornamentation visibly stops. Bone-white highlights trace the **edges around missing information**, never the missing area itself.                               |
| **Lumen**   | **Hard cobalt blue** `#3568E8`  | Enamel, ruled stone, engraved brass, porcelain, measured grids, stamped plates. Objects carry labels, borders, registration marks and exact divisions, as though reality requires documentation to remain real.                                   |

**Global rule:** the accent occupies roughly **10 to 15%** of a hero. Never tint the entire character by aspect.

---

## 2. The six silhouette rules

These are mandatory. Remove color and VFX; the aspect should remain guessable.

**Ember - irreversible asymmetry.**
One side of the silhouette has visibly *overtaken* the other: a repaired weapon has become larger than its original form, armor grows outward around an old break, one sleeve has been replaced three times. Ember silhouettes look like an escalation that was never rolled back.

**Tide - directional contradiction.**
Every silhouette contains two strong shapes pointing somewhere different: coat flowing left while weapon architecture pulls right, opposing crescents, reversed layers, handles mounted off-axis. You should never be able to draw one clean line of force through a Tide hero.

**Verdant - accumulation.**
Nothing ends cleanly. Armor overlaps older armor; straps continue beyond where needed; repaired gear sprouts additional supports. Verdant silhouettes become **denser toward their damaged/important areas**, because preservation leaves physical history behind.

**Gale - broken continuity.**
At least one major silhouette feature is physically separated from the body: floating shoulder plate, disconnected weapon segment, hovering sleeve cuff, split cloak. Large deliberate gaps make the eye complete shapes that aren't actually connected.

**Umbra - missing silhouette.**
A shape that clearly *should exist* does not. Half a coat hem terminates in a ruler-straight absence; a sword jumps from hilt to upper blade with nothing connecting them; one side of a helmet has no contour. Never fill the absence with smoky VFX. **The empty background is the effect.**

**Lumen - imposed boundary.**
The silhouette contains conspicuous geometric containment: square mantle, circular collar, perfectly straight hem, weapon enclosed by a frame. Organic shapes visibly stop when they encounter these boundaries. Lumen looks like somebody drew a diagram around a human and forced the human to fit.

---

## 3. Three Emberfall signatures

Every summoned hero gets all three, regardless of civilization or aspect.

### The Cinder Seam

Every hero has **one thin, dark reconstruction seam** crossing their body and continuing across clothing/armor as though the entire person had once been broken there. It ignores material boundaries. Where exposed skin crosses it, the seam continues through the skin.

Its location differs per hero. Never symmetrical.

### The Memory Error

Every hero contains **one impossible reconstruction** visible in their model: a buckle fastening nothing, an armor strap disappearing into skin, two incompatible shoe designs merging halfway down a boot, writing that changes alphabet mid-word, a weapon grip built for six fingers.

One per character. Large enough to notice in character-view art, not necessarily combat.

### The Cinder Notch

Every hero has the same small **six-sided broken-ring mark**, physically integrated somewhere into their design: punched through a sword guard, sewn into a collar, carved into a prosthetic, scarred into skin.

One of its six segments is always missing. **Which segment is absent corresponds to the hero's aspect.**

That becomes Emberfall's tiny recurring visual signature without forcing people from impossible civilizations to share fashion.

---

## 4. Explicit bans

**No elemental cosplay.**
No Ember character gets flames because Ember. No Tide character gets water waves because Tide. No Verdant character gets leaves because Verdant. VFX may eventually reference aspect behavior, but costume design never uses literal element shorthand.

**No aspect uniforms.**
There is no “Lumen architecture,” “Umbra people” or “Ember kingdom” aesthetic. Two heroes sharing an aspect can come from civilizations separated by ten thousand years, or from mutually incompatible histories. **Aspect controls composition; origin controls fashion.**

**No gacha-detail soup.**
No filling empty areas with belts, gems, gold piping, floating trinkets, ribbons and tiny ornaments merely to signal rarity. Each hero gets **one dominant silhouette idea, one dominant piece of gear, one memory error.** Empty space is intentional.

---

# 5. The Cinder

The Cinder is **not a flame**.

It is a small floating fragment of reality roughly the size of a human heart: an irregular piece of translucent, almost colorless material containing **six razor-thin internal fractures**.

At rest, those fractures nearly meet in the center but never connect. Looking through the Cinder slightly misaligns whatever is behind it, as though the world on one side remembers being positioned differently.

When inactive, it has no aspect color.

When summoning, the fractures briefly illuminate in all six aspect accents, and **a dark human silhouette appears inside the fragment before the person exists outside it**. Reconstruction starts from the hero's Cinder Seam and spreads outward until the remembered person is complete.

The Cinder never grows a face, speaks through a mouth, sprouts mascot features, or becomes a cute companion. **It is the surviving punctuation mark at the end of reality's original law.**

---

# 6. UI rules

These are rules for drawing things in the interface. They do not change anything above. The colours of the
item tile live in one table, `ItemTilePalette` in `ItemTilePalette.cs`, and the tile itself is drawn in one
place, `ItemTile` in `ItemTile.cs`. Every screen that shows an item calls it. A screen never picks a rarity
colour of its own.

## Rarity palette

Armor has four tiers, the `ArmorTemper` ladder. Relics have two grades, the `RelicGrade` ladder. They are two
separate ladders and each has its own colours. The value is the plate, then the rim.

| Tier | Ladder | Plate | Rim |
|---|---|---|---|
| Rough | armor | #3C342E | #7A6D62 |
| Honed | armor | #1B4E5C | #3FA7B8 |
| Gilded | armor | #6B4F14 | #D9A441 |
| Radiant | armor | #A24A0E | #FFC070 |
| Legendary | relic | #5A4411 | #F2C14E |
| Mythic | relic | #5C1A52 | #E84FD1 |

Shared colours: item silhouette #E9E3D8, outline #0B0C18. An empty or locked slot is the same plate dimmed:
plate #1E1712, rim #4A3F36.

A tier must read without its colour. Four things rise together from Rough to Radiant: the plate gets lighter
or more saturated, the rim gets thicker, one more pip is lit, and the number goes up. The top tier of each
ladder also gets a faint glow ring and a gem on the bottom-right corner of the frame. The harness checks that
all four channels rise, and that the tiers stay in order when the tile is turned to greyscale.

## Tile layout

The tile is a rounded square plate, scaled to whatever size the caller asks for, with fixed corners:

- top left: the aspect set icon, small
- top right: the tier number
- bottom row: the tier pips
- bottom left: the worn check, only when the piece is worn
- centre: the item silhouette, with weapons drawn larger because their art is narrow

## At most four marks

A tile carries at most four marks: the aspect icon, the number, the pips and the worn check. The silhouette is
the item itself and the corner gem is part of the plate, so neither counts. A fifth mark is not added to make
a screen more informative. If a screen needs to say more, it says it in text beside the tile. Under about
50 units a tile cannot carry its four marks, so a row that shows one is made taller instead of the tile smaller.

## Plate and gold rim

The panels around items use one family of colours:

| Role | Colour |
|---|---|
| Plate, dark brown | #2A1D17 |
| Rim, worn gold | #D9A441 |
| Accent and primary action, ember orange | #E8802A |
| Secondary accent, teal | the Honed plate and rim above |

Gold marks the edge of a surface. Ember orange is for the one thing on a screen the player is meant to press.
Teal is the quiet second voice.

## One focal point per screen

TODO, not yet done: one focal point per screen, secondary surfaces quieter than the primary. Several screens
still show many surfaces at the same weight. The tile run only fixed the clutter inside the tile and wrote
the rules above. A pass over the busy screens is a separate piece of work.

---

## Engine notes

The two notes below are **not art direction**. Nothing above changes. These are implementation
observations about rendering the rules above in the game, recorded here so they travel with the
document.

**1. Test the Umbra silhouette rule first, not last.** The Umbra rule in section 2 is an absence
with nothing filling it, and it is the highest risk item in this document to render. Against a dark
UI panel a missing contour can read as a rendering fault rather than a design choice: the player
sees a bug, not an aspect. Because the rule forbids filling the gap with VFX, there is no fallback
to hide behind, so this is the one rule whose viability has to be proven in engine before the art
depending on it is produced.

**2. The three signatures are character-art scale, not combat scale.** The Cinder Seam, the Memory
Error and the Cinder Notch in section 3 are detail work. At roughly 64 pixels in a hero row they
will not be visible at all. They apply to portrait and detail art, and should not be budgeted for
or evaluated on combat sprites.
