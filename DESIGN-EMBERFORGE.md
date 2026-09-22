# Emberfall - Emberforge, the design system

A decision record from 2026-09-22. It says what Emberforge is, what it is made of, why the art is
baked rather than computed, and which screens speak it. Like every other `DESIGN-*.md` in this
repository it is a record from its date: the reasoning here is durable, the status claims are not.
`DESIGN-BUILD-ORDER.md` is the only file kept current.

## The problem it answers

The game had three visual languages and no system.

`UITheme` held a 22-colour palette, a type scale and a spacing scale, and every screen already drew
its colours from it - `DESIGN-UI-THEME.md` records that survey and it was right: **not one colour
literal existed outside `UITheme`**. What leaked was not colour identity, it was colour
*treatment*, and more than that, it was SURFACE. Every panel in the game was a flat rounded
rectangle tinted with one palette colour, because `TextureLab` generates alpha masks and an alpha
mask can only ever be one colour. A flat tinted rectangle is what placeholder UI looks like, and
that is what the game looked like.

`HubStyle` was the closest thing to a system, and its own scope note called it "the Hub's chrome
language, not the game's" - an honest description of a dialect nobody had generalised.

So the gap was not "the theme needs more values". It was that the project had no way to draw a
surface with more than one colour in it.

## The direction

**Emberforge.** The depth a mobile idle RPG gets from baked bevels - chunky glossy buttons, gold
frames, ribbons, jewel icons - in Emberfall's own warm palette, with a dark outline on everything.

The outline is the load-bearing decision. `DESIGN-ART-BIBLE` requires every character to carry a
crisp dark outline, and chrome without one sits in front of those characters as a different
material rather than as the same world's furniture. One colour, one weight, on every element
without exception: that single shared property is what makes a panel, a button, a disc and a
portrait frame read as belonging to each other even when they share nothing else.

Two button voices and no more. Ember orange is the thing to press; teal is the way back out. A
third colour of button is a third thing for a player to learn, and a screen with three of them has
stopped saying which one matters.

## What it is made of

Four files, each with one job.

- **`Emberforge.cs`** is the tokens, and the only place a colour, a weight, a radius or a step is
  written down. Nothing else in the project may write a style literal.
- **`EmberforgeArt.cs`** is the pixels. Every surface in the kit is the same section - an OUTLINE,
  a FRAME band, an INNER LINE, and a FACE with a three-stop vertical gradient, an inner top
  highlight, a soft inner shadow and optionally a lip along the bottom. A panel, a button, a pill,
  a tab, a rail disc, a rarity frame and a corner ornament are all that section with different
  numbers, which is why there is one renderer and a table of recipes rather than twenty drawing
  routines.
- **`EmberforgeSkin.cs`** decides where a surface comes from.
- **`EmberforgeUI.cs`** is the components.

### The three style files, and why all three stay

- **`UITheme` stays.** It owns the reference resolution, the type scale, the spacing scale and the
  game-meaning lookups - aspect, rarity, role, relic grade, armour temper. Those are *semantics*
  rather than *surface*, and the two sets do not overlap: there is no aspect colour in Emberforge
  and no bevel in `UITheme`.
- **`HubStyle` stays, and stays the Hub MAP's dialect.** It is left holding the translucent plates
  the building markers sit on, and that is the right home for them. A gold-framed marker over a
  painted gatehouse is a sticker on a painting. The map is layer 0 and the chrome on it must stay
  quiet enough to keep the painting readable; everything in the Hub's FRAME is Emberforge.
- **`Emberforge` is the new one**, and it is the game's, not one screen's.

## Baked sprites, not a UI shader

The two ways to get a gradient, a bevel, a gloss and a rounded outline onto a uGUI element are to
bake them into a nine-sliced texture at import, or to compute them per fragment in a custom UI
shader. **This kit is baked, and the reason is the phone rather than the desktop.**

- **Batching.** uGUI batches by material and texture. A shader drawing a gradient needs its stops,
  its radius, its lip height and its gloss fraction as per-element parameters, and
  `CanvasRenderer` does not honour `MaterialPropertyBlock` - so per-element parameters mean a
  material instance per element, and every button, panel and pill becomes its own draw call plus
  its own material. The Hub's frame alone is around forty Images. Baked sprites share one default
  UI material and pack into one atlas, so the same frame collapses to a handful of batches. On a
  mid-range Android this is the binding constraint, not fill.
- **Fragment cost where it hurts most.** The largest elements are the ones a shader would be most
  expensive on: a panel sheet or a popup plate covers a large share of a 2400x1080 screen, and the
  UI already overdraws it - map, scrim, plate, contents. A signed-distance rounded rect with a
  four-stop metal ramp, an exponential inner shadow and a gloss is tens of ALU and several branches
  per fragment; a nine-sliced sprite is one bilinear sample.
- **Startup cost disappears rather than moving.** The generator is the same code either way, but
  baked it runs on a desktop at import, and unbaked it runs on the device at load - `SetPixels32`
  and `Apply` on twenty-seven textures, on the main thread, allocating.
- **The override contract stays honest.** A painted PNG from an artist and a generated PNG go
  through the same importer, carry their nine-slice border the same way, and are read by the same
  `Resources.Load`. With a shader the generated path would be a material and the painted path a
  sprite, which is two code paths and one of them untested.

**What baking costs:** the kit is a committed binary, and changing a token means running a menu
item rather than pressing play. That is the trade, and it is the right one for a game whose chrome
changes once a month and whose frame rate matters every frame.

### The same drawing code runs in both places

`EmberforgeBaker` calls the renderer at import time and writes PNGs with their nine-slice borders
set on the importer. `EmberforgeSkin` calls the *same* renderer at runtime for any slot whose PNG
is missing, so a clone that has never run the baker still renders the correct kit instead of
magenta boxes - and says so in the console, because a page that looks right while every slot is
being generated on the device says the art is correct and says nothing about whether the bake ran.

Splitting those two is the usual way this rots: the tool and the fallback drift, and the game looks
different depending on whether somebody remembered to press a menu item.

### Where a surface comes from

Three steps, in order:

1. `Resources/Sprites/Hud/<slot>.png` - **hand-painted art.** If a file is there, that is the art.
   Deliberately the folder `HudSkin` already reads, so an artist has one place to drop PNGs for the
   whole game rather than one per subsystem.
2. `Resources/Sprites/Emberforge/<slot>.png` - **the baked kit.** What ships.
3. The runtime generator.

So a re-skin is a folder of PNGs and no code change, which is the contract `HudSkin` already
promised, and a retune of the kit is editing the token file and re-running the baker.

### Two things the renderer had to get right

- **Anti-aliasing is 3x3 supersampling, not a distance-based alpha ramp.** A ramp works for a
  single silhouette and falls apart at a band boundary, where two different colours both want the
  same pixel: a gold frame against a dark outline aliases into a bright fringe. Supersampling
  resolves the silhouette and every internal boundary with one mechanism, and it is free because it
  runs at import.
- **The vertical borders of a nine-slice are wide on purpose.** A nine-slice stretches its middle
  row, so a three-stop gradient baked across the whole height would flatten to a single stretched
  scanline the moment the element is taller than the sprite. Giving the top and bottom borders most
  of the sprite's height puts both ramps inside the fixed regions and leaves only the mid stop to
  stretch - so a tall panel gets a lit top band, a long flat midtone and a shadowed bottom band.

## Type

Cinzel for titles, Barlow Condensed for everything else, both SIL Open Font License 1.1 from Google
Fonts, both licences shipped beside the files.

**They are legacy dynamic `Font` assets, not TMP font assets, and that is a stated deviation from
the brief rather than an omission.** Every one of the twenty-one screens in this project draws text
with `UnityEngine.UI.Text`, there is no TextMeshPro anywhere in the tree and no TMP package in
`Packages/manifest.json`. A TMP font asset can only be drawn by a `TMP_Text` component, so
importing two of them would have meant either migrating every screen in the game off `Text` - a
change with nothing to do with a design system, touching far more than the screens this pass owns -
or shipping two SDF atlases that nothing draws. A dynamic `Font` renders the same outlines through
the same rasteriser and is what the text in this game is actually made of. The same two `.ttf`
files are the input to a TMP font asset the day the project moves, so nothing has to be re-sourced.

Cinzel is shipped as its variable file renamed, because a `Resources` path containing square
brackets is a path every call site would have to escape.

## The components

Panel, TitleRibbon, Button, Tab, CurrencyPill, ProgressBar, PortraitFrame, PlayerPlate, IconButton,
plus the plain and sunken surfaces the others are built against.

Two are worth recording the reasoning for.

**The button's pressed and disabled states are SPRITES, not tints.** A colour multiplier on a baked
bevel darkens the highlight and the shadow by the same factor, which is exactly what light does not
do, and it is why a tinted chunky button looks like a photograph of a button with the brightness
turned down. A press is the lip shrinking and the face dropping into the space it frees - the face
falls exactly as far as the lip shrinks, so the outer silhouette never moves - and the only way the
highlight moves with it is to draw the button at the pressed depth.

**The portrait frame carries the rarity and a glow behind it carries the aspect.** `Widgets`
records the collision this resolves: it had one ring to spend and spent it on aspect, dropping the
rarity read entirely. A frame plus a glow has two channels, so a badge can say both.

## Rendering the states without faking them

The component gallery holds a button in its pressed art by driving the same component a pointer
drives, rather than by setting a sprite on the page. A gallery that reaches past the component to
paint the state it wants is a gallery that can show a state the component cannot actually produce.

## What captures found that the compiler could not

Recorded because all three are the same class of fault and the project keeps meeting it.

- The campaign card's title ran out of both ends of its ribbon, because a ribbon's width comes from
  the panel it labels and its text comes from content. Ribbons best-fit now.
- A currency pill ate its own figure: `Box` pivots on its anchor, so a plus anchored to the pill's
  right edge put its hit rect's centre half a thumb *inside* the pill.
- Three sections of the gallery overflowed their panels **at 20:9 and nowhere else.** The canvas
  scales on the geometric blend of the two aspect ratios, so a 20:9 phone gets a logical canvas of
  roughly 2147x966 while the 16:9 editor sits at 1920x1080 - the phone is 114 units SHORTER. A
  layout sized against the editor window is a layout that has not been sized against the device.
  The gallery now asserts its own height against 966 and says so in the console when a column
  outgrows it.

The general rule, which `CLAUDE.md` already states and this pass paid for again: **a screen is not
verified until it has been photographed**, and it has to be photographed at both aspects.

## Deliberately not done

- **The other eighteen screens.** They read `UITheme` and write their own metrics, exactly as
  before. `ScreenChrome` keeps its original header untouched and gains a second, opt-in Emberforge
  one, so a screen converts by changing which of the two it calls and nothing else - every metric a
  page measures its body against is unchanged.
- **The panel sheet** that hosts profile, account, friends, settings and guild. It is unconverted
  on purpose: it frames screens that have not had their visual pass, and a gold frame around old
  chrome is worse than either language on its own.
- **The top bar's backing plate**, which is only visible on unconverted screens and is the seam
  between the two languages.
- **The currency plus.** The component has one and the gallery shows it; no pill on the Hub carries
  one, because this build has no screen that gives a player gold, shards or relic dust in exchange
  for anything. The summon screen and the relic shop both SPEND. A control that opens nothing is
  worse than no control.
