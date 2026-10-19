# Emberfall - art delivery for battle sprites

This is the technical contract for handing over a battle sprite: where the file goes, what
format it needs to be in, and exactly what the game does to it once it's in. It does not tell
you what to draw. The visual rules (colors, silhouette, material feel, what a hero is allowed
and not allowed to look like) live in DESIGN-ART-BIBLE.md. Read that one for direction, this
one for delivery.

## Scope: this document covers battle sprites only

**If you are looking for portraits, icons, backgrounds or UI panels, they are not in this file.**
This document is the contract for one thing: a character's battle sprite, at
`Sprites/Units/{spriteId}.png`. It is not a general art pipeline.

The game has four other art conventions, all of which load by filename and none of which are
described here or in the art bible:

| What | Where it goes |
|------|---------------|
| **Portrait**, framed badge (draws edge to edge over an aspect glow, no ring) | `Resources/Sprites/Portraits/{portrait}.png`, and the hero must name it in `visual.portrait` |
| **Portrait**, frameless crop (game adds disc and aspect ring) | `Resources/Sprites/Units/portrait_{heroId}.png` |
| **Background**, full-screen painted backdrop | `Resources/Backgrounds/{environmentId}.png` |
| **UI panel / button / disc** surfaces | `Resources/Art/UI/{surface}.png` |

The framed badge is the one of the four that is **declared rather than discovered**: dropping the
file in is not enough, the hero has to name it, and a hero that names a file it does not have is
reported as broken rather than quietly drawn as its initial. That is the same contract a battle
sprite is held to and for the same reason. The other three are still discovery by filename.

**Icons have no convention at all.** Aspect emblems are generated in code, item icons are
procedural discs, and six painted currency and navigation icons exist in `Resources/Art/UI/` that
nothing loads.

See **DESIGN-ART-STATE.md** for all four conventions in full, a per-screen census of what art
exists today, and the prioritized list of what to draw next. That file is status and goes stale;
re-audit rather than trusting it once art has landed.

Everything below is derived from reading the actual loading code, not from how it's supposed to
work. This document originally carried a caveat saying the sprite path lived on an unmerged
branch (`sprite-path-orrin`) and had never been seen to render; that is no longer true and the
caveat has been removed. The branch merged, the branch itself is gone, the loader is on `main`,
and sprites have been watched rendering in Play mode many times since.

As of 2026-09-06, **every hero in the game renders from delivered art** and no hero uses the
built-in procedural look any more. The split is between the two paths:

- **Five heroes render from five sprite-sheet clips each** (idle, basic attack, skill, hit
  reaction, death), which is NOT what this document describes - see the sprite sheet note in
  section 2 and section 8: Kael the Ashbound, Seraphine the Last Ember, Marrow the Burning Vow,
  Veska the Broken Oath, Orren the Cinder Hunter.
- **Six heroes render from the single static image this document does describe:** Orrin Kaal,
  Kalyx Emberwright, Wren Ilyra, Ryn Solvarr, Maeve Corrin, Nyx Serrow.

Enemies are the opposite way round. **Three of twelve** have art - Grave Moth, Hollow Husk and
Sundered Colossus, all single static images - and the other nine still use the built-in procedural
look (simple 3D shapes and colors, no image file involved) and will keep using it until someone
hands over art for them the way this document describes. DESIGN-ART-STATE.md lists which nine.

## 1. Where the file goes and what it's called

Drop the finished PNG in the project's `Sprites/Units` folder, inside the game's content
folder. Ask the engineer for the exact path on your machine if you're not sure where that is;
what matters is the folder name (`Sprites/Units`) and everything below it.

The file name has to match the hero's own internal id, in lowercase with underscores, with a
`.png` extension. Orrin Kaal's id is `orrin_kaal`, so his file is `orrin_kaal.png`. If you're
delivering art for a different hero, ask for that hero's id rather than guessing it from their
display name, since the two don't always match exactly.

Nothing else needs to change on your end beyond one line of data, described below. If the file
is present at the right name, the game uses it.

### The three states a hero can be in, and how the game tells them apart

This used to be two states and it did not work, because most of the roster has no art yet. What
follows is what the game does now.

**Not drawn yet.** The hero's data carries `"spriteStatus": "pending"` and the hero renders a
neutral grey mannequin, tinted by their aspect colour, with their name under their feet.
Sixty-three of the ninety-four heroes are in this state and it is not an error; nothing is logged
and nothing goes red. The mannequin is one single file, `placeholder_dummy.png`, shared by all of
them - replace that one file and every undrawn hero changes at once.

**Drawn.** The file is there and `spriteStatus` is gone from the hero's data. **Deleting that one
line is the activation step** - the only thing anyone has to do besides handing over the file. A
hero whose art has landed while the line is still there is caught by the build check, so it
cannot be silently forgotten.

**Broken.** The hero's data says they have art (no `spriteStatus` line) and the file at that name
cannot be found or cannot be read. The game shows a flat, unmissable **magenta square**, logs an
error naming the hero and the exact path it looked in, and the build check fails with the list.
That is intentional: a hero that is supposed to have art but doesn't is meant to look broken, not
quietly fine. If you ever see a solid magenta character in a screenshot, the file is missing or
misnamed.

Why "not drawn yet" is declared in data rather than guessed from the missing file: those two
states share the same symptom. If a missing file meant "not drawn yet", then a sheet that was
misnamed, deleted or never copied across would look exactly like the sixty-three heroes nobody
has started - and the one real fault would be invisible among them. Declaring the intent keeps
them apart.

A hero with **no `sprite` name at all** is a fourth, older case: they keep the built-in procedural
look, assembled from primitives. That is what most enemies still do, and it stays.

## 2. File format

- **PNG**, 8-bit color, with a genuine alpha (transparency) channel. Not a JPEG, not a PNG
  saved without transparency.
- **The background must be transparent**, not white or black. The character is composited over
  a 3D scene, and anything opaque outside the character's silhouette will show up as a visible
  rectangle behind them.
- Partial transparency is fine and will render correctly (soft edges, semi-transparent effects
  on the character itself), since the game blends the alpha channel properly rather than just
  treating it as on/off.
- **Keep the working resolution reasonable.** Everything in `Sprites/Units` imports with a cap of
  4096 pixels on its longest edge (`Tools/texture-import-policy.py`, the Units group); anything
  larger gets shrunk down to fit that on import. This line used to say 1024, which no setting in
  the project has carried since the policy was applied. A character's body stands about 220
  screen pixels tall at 1920 x 1080, so there is little to gain past roughly 1000 pixels tall.
  There's no required exact size beyond staying at or under the cap; what determines the
  character's proportions on screen is the width-to-height *ratio* of the canvas, covered next.
- **A sheet's frames should be at least 396 pixels tall**, because every frame is fitted into a
  396-tall cell (section 3). A shorter frame is magnified to fit and comes out softer than the
  rest of its set. The usual delivery, 880 x 1040 a frame, has room to spare.

### Sprite sheets: two import settings that must not be left at their defaults

This applies only to a **sprite sheet** - one file holding a grid of animation frames - and not
to the single-figure sprites the rest of this section describes. Sheet support is shipped on
main (merged from `claude/kael-hero-animations-se66qk`): Kael the Ashbound is the first, and
currently only, hero built this way. Deliver a sheet only if you have been asked for one - every
other hero still uses a single static image, and section 8 describes what changes once a hero
has clip data instead.

The game works out how wide to draw each frame from the *shape of one cell*, and it gets that
shape by dividing the imported image's dimensions by the grid size the content file declares.
That means it measures the image **as Unity imported it**, not as you exported it - so an import
setting that changes the image's proportions silently changes the character's proportions on
screen. Two fields decide this, and Unity's defaults are wrong for a sheet:

- **Non-Power of 2** must be set to **None**. The default, *ToNearest*, rounds the width and the
  height each to the nearest power of two, and it rounds them *independently*. A 4096 x 7192
  sheet becomes 4096 x 8192 - the height stretched by about 14%, the width untouched. Every cell
  in it is now the wrong shape, and so is the character drawn from it.
- **Max Size** must be at least the sheet's real longest edge - 4096 for a 4096-wide sheet. The
  default cap of 2048 shrinks anything larger. On its own that scales evenly and costs only
  sharpness, but stacked on the rounding above it compounds the distortion and throws away
  detail the sheet needed in the first place.

Neither one announces itself. A sheet imported at the defaults renders perfectly happily, just
at the wrong proportions, so it is worth checking on the asset first rather than hunting for the
problem in the animation data or the grid numbers.

This matters in practice: Kael the Ashbound's shipped sheets are 3744 x 6144, well past the
default 2048 cap, and were imported with **None** and **8192** to avoid exactly this. A
downscaled or distorted sheet would still play back at the right timing (the frame math works in
UV space, which is resolution-independent), just at a visibly lower resolution than the source
art - so this is a quality issue on an otherwise-working sheet, not a correctness one.

### Sprite sheets: compression, and why the cell is 336 x 396

Nobody delivering art sets these; they are written into every `.meta` in `Sprites/Units` by
`Tools/texture-import-policy.py`, and a new sheet's `.meta` is copied from an existing one.

- **Windows: BC7, not crunched.** It used to be DXT5 Crunched. DXT5 stores two colours per 4 x 4
  block, and when a unit was drawn at 1.6 screen pixels per texel those blocks showed as blotches.
  BC7 costs the same 8 bits per pixel in memory, but it cannot be crunched, and crunch was
  what kept the sheets small on disk: they packed to 139 MB in the Windows player as DXT5
  Crunched and are about 3 GB as BC7 at the larger cell, before the zip.
- **Android: ASTC 6x6**, 3.56 bits per pixel. It was ASTC 5x5.
- **Max size 4096, no power-of-two resize, no mipmaps, bilinear.**

Both formats constrain the cell. BC7 compresses nothing whose width or height is not a multiple
of 4 (Unity has refused it since 2021.2; the sheet would ship uncompressed at four times the
memory), and a sheet is 7 to 9 cells wide and 7 or 8 tall, so the cell itself has to be a
multiple of 4. ASTC 6x6 wants it a multiple of 6 as well, so that every frame boundary is a block
boundary and no block mixes two frames. **336 x 396 is the smallest cell at 1.5 times the old
220 x 260 that is a multiple of 12 both ways and keeps its shape within half a pixel.** A 9 x 8
sheet is 3024 x 3168.

## 3. Sizing: fixed height, no fixed box

This is the part most different from drawing a portrait or an icon into a set frame, so it's
worth being precise about.

**The game fixes every sprite character's height in the 3D scene to one constant number**, the
same number regardless of what hero it is or what size canvas you draw on. Every image, no
matter what pixel dimensions it's saved at, gets scaled so it stands that same height in the
world.

**The width is not fixed.** It's calculated directly from your canvas's own width-to-height
ratio: a wider canvas produces a visibly wider, bulkier-looking character; a narrower canvas
produces a slimmer one. There is no target box you're drawing into and no padding to hit a
required aspect ratio. Whatever proportions your canvas has are the proportions the character
will have on screen, scaled up or down together to match the fixed height. **This holds for a
single static image only.** A sprite sheet goes through the importer first, which does fix the
cell shape; see "Sprite sheets: the importer pads every frame to one cell" below. A perfectly square
canvas would read unusually wide and squat for a person; the shipped placeholder uses a
canvas a bit under six units wide for every ten units tall, which reads as a plausible human
proportion at the fixed height the game uses. Use that as a rough starting ratio if you want
a normal build, and go wider for a deliberately bulky read or narrower for a slight one.

### Sprite sheets: the importer pads every frame to one cell

A sheet does not get the freedom above. `Tools/rewire_sprites.py` turns every delivered sheet
into a grid of **336 x 396 cells** (aspect 0.8485) before the game sees it, and the sidecar
records that cell as `frameWidth` and `frameHeight`. The loader reads the character's width off
that cell, so what decides the on-screen width is what the importer puts inside it. `fit_frame`
in the script does this for each source frame:

- **Fit by height.** The frame is scaled uniformly (never stretched) to 396 tall.
- **Pad to the cell.** The scaled frame is centred horizontally in a transparent 336 x 396 cell.
  A 720 x 1280 frame (aspect 0.5625) becomes 223 x 396 at an x offset of 56; an 880 x 1040
  frame (0.846) becomes 335 x 396 with one transparent column beside it.
- **Fit by width if it does not fit.** If the scaled frame would be wider than 336 (a square
  944 x 944 frame, for instance), it is scaled to 336 wide instead and centred vertically. The
  script prints a `NOTE` line for every clip where that happens, so it is never silent.
- **A frame shorter than 396 is magnified, and said so.** The script prints an `UPSCALED` line.

So an artist can deliver a sheet at any frame aspect and the character keeps its drawn
proportions, but a slim frame yields a slim character inside empty padding, not a wider cell.
The earlier importer stretched every frame to the cell whatever its shape; the five Ember
5-stars, delivered at 720 x 1280, rendered 1.3x to 1.65x too wide until this was fixed. The
padding is transparent, so it costs nothing on screen, and the sidecar, the import settings and
`heroes.json` are unaffected by a frame's aspect.

**The one exception is Brenna**, whose 944 x 944 frames were cut into square cells before
`fit_frame` existed. Her sheets keep that shape at **360 x 360**, so her fractions hold.

**The cell used to be 220 x 260.** At that size a hero was drawn at about 1.57 screen pixels per
sheet texel at 1920 x 1080, and elites and bosses at about 2.0, which is what made compression
blocks visible. At 336 x 396 the median hero is drawn at 1.03. Every sheet was re-exported from
the original deliveries with `rewire_sprites.py --reexport`, which matches each delivered file to
the shipped clip it reproduces by comparing pixels, rather than guessing from file names.
Only the PNG and the sidecar's two pixel fields change; every other number in a sidecar, and both
fractions in the content files, is a fraction of the cell and holds.

**The memory cost.** A 5v5 battle with five 7-clip heroes against five 5-clip enemies holds 60
sheets: 575 MB of texture on Windows and 255 MB on Android, against 247 MB and 158 MB at the old
cell and formats.

One more thing worth knowing: each hero already has an overall size number set in the game's
data (a general "how big does this character read" multiplier, not something this document
asks you to touch, and not specific to sprites - the same number also scales the built-in
procedural look). Orrin's is already set larger than the default, because he's meant to read as
a heavy tank. That scaling happens automatically after your image is placed at the fixed
height described above; you don't need to compensate for it by drawing him bigger.

**Enemies are the exception: for them, how big you draw the figure in its frame is kept.** The
fixed height throws that away, so a goblin drawn small and an elite drawn large came out the same
height and only the size number told them apart, and those numbers were set before any art
existed. Chapter 1 and 2's elites and bosses rendered 23 to 34 percent larger than the heroes'
art scale, and the beasts smaller. Each of those enemies' size number is now derived from its
art as `scale x (median hero magnification) / (its magnification)`, where a unit's magnification
is its screen pixels per sheet texel at 1920 x 1080: `SilhouetteTop x scale x 130.6 /
(spriteBodyFraction x frameHeight)`, 130.6 being BattleStage's pixels per world unit (1080 / 7.3)
times cos 28 for the camera's pitch. The median hero reads 1.03 at the 396 cell (it was 1.57 at
260, when the enemies were set). That draws every enemy at the median hero's texel density, so
they stand in the proportions you drew them in. The four named
bosses (the Collector, the Shepherd, the Forsaken, the Waykeeper) then get a deliberate 10
percent on top. **So for an enemy, draw it at the size it should be beside a hero drawn in the
same frame size.** An enemy delivered later needs the same derivation when it is wired; nothing
applies it automatically.

## 4. A raised weapon needs its own headroom reported, or it shrinks the body

Read this section even if your character holds nothing above their head - the short version,
"leave it alone," is the answer for most heroes, but knowing why matters the one time it isn't.

Section 3 said the game fixes the character's height to one constant, and that whatever canvas
you draw maps to that height. That is true only when the whole canvas is body. It stopped being
true the first time a piece of art held a weapon above its own head: the canvas needed to be
taller than the body alone to fit the weapon, the game still mapped that whole taller canvas to
the one fixed height, and the body itself - the part a player actually reads as "how big is this
character" - ended up rendered shorter than every procedural hero standing next to it. That was
a real bug, not a hypothetical one, and this section is the fix.

**If your canvas is entirely body - feet to the top of the head, nothing held or worn reaching
higher - you have nothing to do. Leave this alone.** This is the default, it is free, and it is
still how every hero shipped so far is drawn.

**If something reaches above the head - a raised staff, tall hair, a banner, wings, anything -
you have to report how much of the canvas is body, or the engine has no way to tell your body
apart from the headroom around it and will shrink both together to fit the fixed height.**
Measure it yourself, directly off your own file: what fraction of the canvas's total height,
counted from the bottom, is the character's own body? Feet to the top of the head only - not the
weapon, not hair or a banner that reaches higher. That fraction, a number between 0 and 1, is
what gets handed to the engineer wiring up your file, as a value called `spriteBodyFraction` in
the hero's data. It is not a number you set yourself in code, but it has to come from you: no
automated check can reliably tell your body apart from a raised prop by looking at the pixels
alone - a full cloak, tall hair and a held weapon can all look identical to that kind of check,
and guessing wrong either shrinks the body that should be full size or clips a weapon that should
have room. You are the only one who knows where your own body stops and the prop begins.

Width is not part of this. A weapon held out to the side still just widens the canvas, which
already produces a proportionally wider quad without touching how large the body reads - that
was never the axis this bug affected, only height was, because height is the one fixed number
everything else is measured against.

## 5. Framing: where the feet go

The bottom edge of your canvas is treated as ground level. The game positions the image so its
bottom edge sits exactly on the floor of the scene. If you leave empty transparent space below
the character's feet, they will visibly float above the ground by that fraction of the canvas
height. Crop tight to the feet at the bottom.

There's more freedom at the top: headroom above the character (for a raised weapon, a tall
hood, hair, antenna, whatever the silhouette calls for) is fine, since only the bottom edge is
treated as a fixed reference point - but "fine" here means it won't be cropped or clipped, not
that it's free. See section 4 first: any headroom you draw has to be reported as a fraction of
the canvas, or it shrinks the body along with itself.

**Do not draw a ground shadow into the image.** The game already places its own soft shadow
blob under every character automatically, sprite or not. A shadow baked into your art will
either double up with the real one or sit at the wrong angle relative to it.

## 6. Facing: draw at three quarters, not square on

**Draw the character turned 30 degrees off square-on, toward their own left.** Their own left is
the viewer's right, so on your canvas the character's body and face angle toward the right-hand
edge while still looking substantially at the viewer. This is the classic three-quarter portrait
angle, at the shallow end of it: a clear turn, both eyes still visible, the far shoulder only
slightly narrowed. Not a profile, and not a full 45 degrees.

The exact number matters, so match it rather than approximating: **30 degrees**.

The reason is that the game turns the image by that same 30 degrees in the 3D scene, mirrored by
side. The player's team stands on the left of the screen turned toward the right; the enemy team
stands on the right turned toward the left, so the two lines face each other across the middle.
Because the two sides turn opposite ways, **the enemy side displays the identical file mirrored** -
there is no separate enemy artwork, and no way to opt one side out of the flip.

Two consequences worth designing around:

- **Sprites are flat.** The game turns a flat image, which foreshortens it; it does not rotate a
  3D character. Art drawn square-on and then turned reads as a flat card standing at an angle,
  not as a character facing sideways. Drawing the turn into the art is what makes the two agree,
  and it is the whole reason this section exists.
- **Anything that must not mirror will mirror.** Text, insignia on one shoulder, a weapon held in
  a specific hand, an asymmetric scar: all of these flip on the enemy side. Prefer designs that
  survive being mirrored, or accept that the enemy version reads reversed.

Orrin Kaal's shipped placeholder is drawn square-on and predates this rule, so until it is
redrawn it will show this exact mismatch. That is expected and is not a bug in the loader.

**Verified, and it needed the one-line fix this paragraph predicted.** The engine turned both
sides but mirrored neither: `UnitRig.Build` yaws the two lines opposite ways, which really does
turn a procedural 3D body around, but a sprite is a flat quad and a 30 degree yaw only leans it.
Both lines showed the same drawing facing the same way. It went unseen because the campaign never
puts a hero on the enemy side and only three enemy defs carry art at all; The Proving and the Dev
Arena, which both field heroes as opponents, are where it shows.

The rule now lives in one named place, `UnitRig.SpriteFlipped(spriteMirror, facingForward)`, and
reads `spriteMirror ^ !facingForward`. **The player's line is unchanged** - it shows what it always
showed - and the enemy line is that same file mirrored. `spriteMirror` keeps the meaning this
document gives it, "this file is authored facing the other way", and applies on both sides, so a
hero carrying it still turns to face the opposing line wherever it stands.

**Correction (R15 facing audit, read from every clip of every sheet).** The paragraph above describes what the rule was meant to do, not what the renderer shows. A sprite is a quad seen from behind (the rig turns it about 180 degrees), so `SpriteFlipped` true shows the sheet AS DRAWN and false shows it MIRRORED. Read that way: a hero sheet drawn facing right needs `spriteMirror: true` to face right on the player's line, and 24 of the 31 delivered hero sheets carry it. The other seven were delivered without it; six of them (Edrin, Oris, Zella, Nysera, Velis, Arden) face left on the player's line, and the R15 "v2" look inverts their mirror in `stage-v2.json` (`faceFlip`) rather than editing `heroes.json`. All 31 hero sheets and the placeholder mannequin are drawn facing right; all 26 enemy sheets are drawn facing left, with `spriteMirror` false, which is what puts them facing left on the enemy line. So "draw facing right" is still the instruction for a hero, and an enemy sheet is the exception to it: draw it facing left. `Tools/DevHarness/facing-audit.txt` records the reading, and `Screen:RosterSheet` (Docs/CAPTURE.md) photographs it.

One consequence for art already delivered: the three enemy defs that carry sprites
(`hollow_husk`, `grave_moth`, `sundered_colossus`) now render mirrored in campaign battles, which
is what this section has always said would happen. If any of them was drawn pre-compensated, it
will now read reversed and wants a look.

**State the strike frame for every attack clip.** One frame number per basic, basic2 and skill -
the frame where the weapon connects or the effect is released - in the delivery manifest beside
that clip's frame range. The game hangs the damage number, the flash and the hit reaction off it,
and it is not derivable: it has been guessed from the art by measuring each frame's width, which
is right for a side swing and wrong for an overhead one, where the raised weapon is widest at the
top of the windup. Sixteen clips had to be set by hand off contact sheets because of it. A single
integer per clip from the person who drew it removes the guess.

## 7. What the game does to the art at runtime

The character isn't static once it's in a battle. Three effects apply directly to your image's
pixels, automatically, with no extra art required from you. It helps to know what they do so
nothing you paint gets lost or looks wrong when they trigger.

**Hit flash.** When the character takes damage, the entire visible image (everything that isn't
transparent) blends toward solid white for a fraction of a second, harder on a critical hit than
a normal one, then fades back to normal almost immediately. This applies uniformly across the
whole character, not as an outline or a rim effect. There's nothing to design around here beyond
knowing it happens; a full-character white flash reads fine on essentially any art.

**Cast glow.** When the character uses their special move, the image brightens on its own
colors for roughly a third of a second, peaking partway through and fading back out. It's a
self-multiply, not a colored overlay: a mid-tone color gets noticeably brighter, but a pixel
that's pure black stays pure black, since brightening zero produces zero. If part of the design
is meant to visibly glow when the hero casts, don't render that part as pure black; give it at
least a little color for the brightening to act on.

All three apply to the **placeholder mannequin** exactly as they apply to finished art - it is
the same flat card on the same code path, so an undrawn hero still idles, lunges at its target,
flashes white when hit, throws damage numbers and fades on death. Nothing about a placeholder is
a special case at runtime; the only difference is which image is on the card.

**Death fade.** When the character is defeated, the image stays fully visible for about a fifth
of a second, then fades smoothly to fully transparent over roughly the following half second, a
straightforward transparency fade with no special effect painted over it (no dissolve texture,
no burn edge; that treatment exists elsewhere in the game for the built-in procedural look but
not for sprites). Separately, and not as a request to draw anything differently, the whole
character also tips backward as it fades, the same motion used for the built-in look. A flat
image tipping over will read differently than a character with actual depth falling over; this
hasn't been looked at yet and is worth an early look once someone can see it in motion.

## 8. Not yet handled

Stated plainly, so nothing here is assumed that doesn't exist yet:

- **No animation, for a hero with a single static image (every hero but Kael).** The image is a
  single rigid card. The game can move, tilt and fade that whole card (idle sway, a lunge toward
  a target, a flinch on being hit, the death tip described above), but nothing inside the image
  itself animates. A cape can't flutter on its own, a blade can't swing independently, eyes can't
  blink. Whatever the character is doing has to already be true in the single frame you deliver.
- **No multi-frame or flipbook support, for a hero with a single static image.** There's no
  mechanism for swapping between several images for the same hero (an attack pose versus an idle
  pose, for instance). One hero, one file - unless that hero has clip data instead, per the
  sprite sheet note in section 2. Kael the Ashbound is the only hero built that way today; he has
  five clips (idle, basic attack, skill, hit reaction, death) instead of one image.
- **No separate weapon layer.** The built-in procedural look assembles a weapon as its own
  piece, positioned and held independently of the body. The sprite path has no equivalent; a
  weapon has to be painted directly into the same single image as the character. There's no way
  to swap a weapon without swapping the whole character image.
- **No sprite-specific size or crop adjustment.** Beyond the fixed height, the derived width and
  each hero's existing overall size number (all covered in section 3), there is no additional
  per-sprite control, no separate offset, margin, or fine-tune knob. If a finished image reads
  too big, too small, or off-position at the fixed height, the fix today is re-cropping or
  re-scaling the file itself before it's delivered, not a setting anyone can adjust afterward.

## 9. Reconciling with the art bible

DESIGN-ART-BIBLE.md sets the visual rules; this document doesn't repeat them. Two of its own
notes are directly relevant to anyone drawing a battle sprite, so read them there rather than
here: the note on the Umbra silhouette rule being the highest-risk one to render and worth
testing first, and the note that the Cinder Seam, Memory Error and Cinder Notch are
portrait-and-detail-scale signatures, not combat-scale ones, and don't need to appear on a
battle sprite at all.

One open item the bible asks for that this loader cannot currently deliver: several of the
material descriptions in the aspect palette table (Tide's mirrored surfaces and flexible glass,
Umbra's "bone-white highlights trace the edges around missing information") describe a material
responding to light and angle, a highlight that shifts, a surface that catches a rim of light
differently depending on where you look at it from. The built-in procedural look has exactly
that: a baked-in rim light and a directional shading response, so its materials visibly react to
angle. The sprite path has none of that. It displays your flat pixels exactly as painted, with
no added lighting, no rim highlight, no directional shading of any kind, on top of what the
three runtime effects in section 7 do. Whatever light-and-shadow read a material needs has to be
painted directly into the artwork as a fixed, unchanging highlight. It cannot be added by the
engine the way it can for a procedural character. This is a real gap between what the bible
describes and what the current loader can do, not a rule to work around quietly, and it should
be treated as an open question for whoever owns the art pipeline rather than something each
artist re-solves per hero.
