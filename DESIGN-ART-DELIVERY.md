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
| **Portrait**, framed badge (draws edge to edge, no ring) | `Resources/Sprites/Portraits/{heroId}.png` |
| **Portrait**, frameless crop (game adds disc and aspect ring) | `Resources/Sprites/Units/portrait_{heroId}.png` |
| **Background**, full-screen painted backdrop | `Resources/Backgrounds/{environmentId}.png` |
| **UI panel / button / disc** surfaces | `Resources/Art/UI/{surface}.png` |

**Icons have no convention at all.** Aspect emblems are generated in code, item icons are
procedural discs, and six painted currency and navigation icons exist in `Resources/Art/UI/` that
nothing loads.

See **DESIGN-ART-STATE.md** for all four conventions in full, a per-screen census of what art
exists today, and the prioritized list of what to draw next. That file is status and goes stale;
re-audit rather than trusting it once art has landed.

Everything below is derived from reading the actual loading code on the branch that adds this
path (`sprite-path-orrin`), not from how it's supposed to work. Nothing in this document has
been confirmed by actually seeing it render. That branch has not merged and has not been looked
at in the editor by anyone yet. Treat the numbers as accurate to the code and the visuals as
unverified.

As of this writing, six heroes and three enemies render from the single static image this
document describes: Orrin Kaal, Kalyx Emberwright, Wren Ilyra, Ryn Solvarr, Maeve Corrin, Nyx
Serrow, Grave Moth, Hollow Husk and Sundered Colossus. Kael the Ashbound, the roster's seventh
hero, is the one exception this document does not cover: instead of a single file, he renders
from five sprite-sheet clips (idle, basic attack, skill, hit reaction, death) - see the sprite
sheet note in section 2 and section 8 for what that path adds. Every other hero and every enemy
still uses the game's built-in procedural look (simple 3D shapes and colors, no image file
involved) and will keep using it until someone hands over art for them the same way this
document describes.

## 1. Where the file goes and what it's called

Drop the finished PNG in the project's `Sprites/Units` folder, inside the game's content
folder. Ask the engineer for the exact path on your machine if you're not sure where that is;
what matters is the folder name (`Sprites/Units`) and everything below it.

The file name has to match the hero's own internal id, in lowercase with underscores, with a
`.png` extension. Orrin Kaal's id is `orrin_kaal`, so his file is `orrin_kaal.png`. If you're
delivering art for a different hero, ask for that hero's id rather than guessing it from their
display name, since the two don't always match exactly.

Nothing else needs to change on your end. A hero either has an image on file or doesn't; there
is no separate step to "turn the image on." If the file is present at the right name, the game
uses it. If it's missing, that hero simply keeps the built-in procedural look. Handing over the
file is the entire activation step.

One exception, useful to know but not something you need to act on: if a hero's data has
already been told to expect an image (Orrin's has), and the file at that name can't be found or
can't be read, the game does not fall back to the built-in look. It shows the character as a
flat, unmissable magenta square instead. That's intentional: a hero that's supposed to have art
but doesn't get any is meant to look broken, not quietly fine. If you ever see a solid magenta
character in a screenshot, the file is either missing or misnamed.

## 2. File format

- **PNG**, 8-bit color, with a genuine alpha (transparency) channel. Not a JPEG, not a PNG
  saved without transparency.
- **The background must be transparent**, not white or black. The character is composited over
  a 3D scene, and anything opaque outside the character's silhouette will show up as a visible
  rectangle behind them.
- Partial transparency is fine and will render correctly (soft edges, semi-transparent effects
  on the character itself), since the game blends the alpha channel properly rather than just
  treating it as on/off.
- **Keep the working resolution reasonable.** The current import setting caps the image at 1024
  pixels on its longest edge; anything larger gets shrunk down to fit that on import, so
  there's no benefit to delivering bigger than that. The shipped placeholder is 256 x 448.
  There's no required exact size beyond staying at or under that cap; what determines the
  character's proportions on screen is the width-to-height *ratio* of the canvas, covered next.

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
will have on screen, scaled up or down together to match the fixed height. A perfectly square
canvas would read unusually wide and squat for a person; the shipped placeholder uses a
canvas a bit under six units wide for every ten units tall, which reads as a plausible human
proportion at the fixed height the game uses. Use that as a rough starting ratio if you want
a normal build, and go wider for a deliberately bulky read or narrower for a slight one.

One more thing worth knowing: each hero already has an overall size number set in the game's
data (a general "how big does this character read" multiplier, not something this document
asks you to touch, and not specific to sprites - the same number also scales the built-in
procedural look). Orrin's is already set larger than the default, because he's meant to read as
a heavy tank. That scaling happens automatically after your image is placed at the fixed
height described above; you don't need to compensate for it by drawing him bigger.

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

Not yet verified: like everything else in this document, the 30 degree turn has been derived
from the code rather than seen rendering. In particular, which of the two sides ends up showing
the un-mirrored art is worth confirming in the editor the first time real three-quarter art
lands. If it turns out reversed, it is a one-line engine fix rather than a redraw, so do not
pre-compensate for it in the art.

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
