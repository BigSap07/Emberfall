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
