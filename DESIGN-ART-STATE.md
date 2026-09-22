# Emberfall - art state

What art actually exists today, screen by screen, and what is still procedural. Produced by a
read-only audit of the repo on 2026-09-06, checked against the code and the asset folders rather
than against any document's claims.

This file is **status, not direction**. DESIGN-ART-BIBLE.md sets the visual rules and
DESIGN-ART-DELIVERY.md specifies how a battle sprite is handed over; neither is changed by
anything here. Re-run the audit rather than trusting this file once art has landed.

## Why this file exists

Two portrait conventions were found sitting side by side, both live, neither documented outside a
code comment. That was possible because **the art documents cover exactly one delivery convention
between them** - `Sprites/Units/{spriteId}.png`, for battle sprites - and say nothing about
portraits, backgrounds, UI surfaces or icons. There was no doc-level statement for a second
convention to contradict.

Four conventions exist in the code. Until this file they were documented only in the comments of
the classes that implement them, so `grep portrait DESIGN-ART-DELIVERY.md` returned nothing.

## 1. Per-screen census

All fourteen screens in `ScreenRouter.cs`. Every one inherits painted `panel.png`,
`panel_frame.png`, `button.png` and `disc.png` through `UIBuilder`, correctly nine-sliced (borders
17/23/25 px, verified in the `.meta` files). **"Bespoke" below means art beyond those four shared
surfaces.**

| # | Screen | Bespoke real art | Everything else |
|---|--------|------------------|-----------------|
| 1 | **MainMenu** | `Backgrounds/hub.png` painted map | HUD frame is procedural: player plate, currency pills, right rail, five nav cells, idle chest. Every sprite in it resolves through `HudSkin`, so a PNG dropped at `Resources/Sprites/Hud/<slot>.png` replaces one without a code change. The badge dock and squad strip it used to list are gone. |
| 2 | **Battle** | backdrops (**3 of 6** environments), 11/11 hero sprites, **3 of 12** enemy sprites | HP bars, VFX, damage numbers procedural |
| 3 | Profile | none | avatar through the portrait widget; panels only |
| 4 | Formation | none | banner slots and roster through the portrait widget |
| 5 | Heroes | none | hero cards through the portrait widget |
| 6 | HeroDetail | none | 176 px portrait; stat rows procedural |
| 7 | Campaign | none | team strip portraits; stage rows procedural |
| 8 | Summon | none | portal is `TextureLab.Sigil`; reveal portrait at 340 px |
| 9 | Proving | none | 96 px portraits; panels |
| 10 | **RelicShop** | **none** | relic icons are procedural discs |
| 11 | **Inventory** | **none** | armour and relic icons procedural discs |
| 12 | **Forge** | **none** | fully procedural |
| 13 | **Events** | **none** | fully procedural |
| 14 | **Recital** | **none** | fully procedural |

**Five screens show no real art at all**, not even an inherited hero face: RelicShop, Inventory,
Forge, Events, Recital. Inventory and Forge are where the player spends gold and looks at gear, and
every item in the game is a tinted procedural disc.

Seven more - Profile, Formation, Heroes, HeroDetail, Campaign, Summon, Proving - have no art of
their own. They look finished only where a hero portrait happens to land.

## 2. The four in-code-only conventions

None of these appear in the art documents. All four are real, live, and load by filename.

### 2.1 Portraits, framed - `Resources/Sprites/Portraits/{portrait}.png`

A finished badge carrying its own frame. Drawn edge to edge over its aspect glow: **no disc, no
inner glow, no aspect ring, no inset.** Resolved first in `Widgets.cs` (`DeclaredBadge`), so it
wins wherever it exists.

**Superseded on two counts since this audit was taken, both observable in the tree.**

**It is declared, not discovered.** The path is keyed on `visual.portrait` in `heroes.json`, not on
the hero id, and a hero with no `portrait` key claims no badge and draws its initial. A hero that
declares one and has no file is an error naming the hero and the path, in `Widgets.DeclaredBadge`
at runtime and in `Tools/DevHarness`'s PORTRAIT DELIVERY block offline. The file rule was right
while the folder was empty and nothing claimed a badge; it stopped being right once 94 heroes did,
because then silence covered "never delivered" and "delivered and then lost" equally.

**The tier is full.** All 94 heroes have a badge, one per hero, delivered at 1254 px and resized to
512 on import. Sprite/Single, `alphaIsTransparency` on, no mipmaps, max size 512, Normal Quality
compression. The five Ember badges this audit found are gone along with the five heroes they were
drawn for.

### 2.2 Portraits, frameless - `Resources/Sprites/Units/portrait_{heroId}.png`

A head-and-shoulders crop with no frame. `Widgets.Portrait` supplies the ground itself: aspect-
tinted disc, glows, aspect ring, and a 14.65% inset so the square art is inscribed in the round
disc.

**This tier currently holds zero files.** The five that lived here were deleted once the same five
shipped framed. The loader stays because a frameless delivery is still valid, but note that
`Tools/make_portraits.py` still writes into this path, and anything it produces is silently
shadowed by framed art for the same hero - which is now true of every hero, since all 94 declare a
framed badge.

**The folder still selects between 2.1 and 2.2 for a file that is merely dropped in.** What changed
is that 2.1 is no longer reached by dropping a file in: it needs the hero to declare the name as
well. 2.2 is unchanged and remains discovery-only.

### 2.3 Backgrounds - `Resources/Backgrounds/{environmentId}.png`

Full-screen painted backdrops, loaded by `BattleAssets` and by `MainMenuScreen` for `hub`. A
missing file falls back to a generated gradient, silently and without error.

Present: `ashen`, `ember`, `hollow`, `hub`. Missing: `coast`, `deep`, `tidal`, `proving`.

Only two screens use a full-screen background at all. **For the other twelve there is no
convention - not unimplemented, undefined.**

### 2.4 UI surfaces - `Resources/Art/UI/{surface}.png`

`UISkin` resolves four named surfaces - `panel`, `button`, `disc`, `panel_frame` - preferring a
painted file and falling back to `TextureLab`. Painted panels and buttons **must** carry a
nine-slice border set in the importer, or their corners stretch.

This is the one pipeline that is fully implemented, complied with, and inherited by every screen
without any screen asking for it.

### Icons: three unrelated systems, no convention

Not a fourth convention so much as the absence of one:

- **Aspect emblems** are generated: `TextureLab.Sigil(id, size)`.
- **Item icons** (armour, relics) are tinted procedural discs.
- **Currency and nav icons** are painted files that **nothing loads**.

## 3. Documented or delivered, but not implemented

- **Six painted UI icons are shipped and dead.** `icon_gold`, `icon_shard`, `icon_heroes`,
  `icon_summon`, `icon_relics`, `icon_inventory` sit in `Resources/Art/UI/`. `UISkin.Surface` lists
  only the four surfaces above, and the string `icon_` appears in no `.cs` file anywhere in the
  repository's history. The commit that added them says so plainly: *"six currency and nav icons,
  not yet wired."* Known rather than undetected - but currency displays are still procedural
  circles while the art sits on disk. They are also unprocessed: **1008x1008 each**, against
  36-96 px for every other UI surface.
- **The bible's material rules cannot be honoured on the sprite path.** DESIGN-ART-DELIVERY.md
  section 9 states this itself: Tide's mirrored surfaces and Umbra's shifting bone-white edge
  highlights need light response, the procedural rig has it, the sprite path has none. Highlights
  must be painted in as fixed. The document hands this to whoever owns the pipeline as an open
  question; it is still open.
- **The Umbra silhouette rule was to be proven in engine before dependent art was made** -
  DESIGN-ART-BIBLE.md, Engine note 1, which calls it the highest-risk rule in the document. Nyx
  Serrow is the only Umbra hero and ships a single static sprite. No evidence the test happened.
- **`proving.png` is documented in code as a drop-in needing no code change**
  (`BattleAssets`, `ProvingGenerator`) and does not exist. The Proving renders on a generated
  backdrop.
- **The bible's three signatures have never been checked against the framed portraits.** The Cinder
  Seam, Memory Error and Cinder Notch are called out as *"portrait and detail art"* scale,
  explicitly not combat scale. Nobody has audited the badges against them, and there are 94 of them
  now rather than five, so the audit is both larger and more worth doing than when this was written.

## 4. Inconsistencies, same element handled differently

- **Portraits: two conventions.** See 2.1 and 2.2. One live with 94 files, one live with none.
- **Aspect ring: RESOLVED, and not by adding the ring.** This entry predicted the inconsistency
  would become real the moment a non-Ember hero was delivered framed. All 94 arrived framed across
  all six aspects, in one gold frame identical for every one of them, so aspect would have stopped
  being visible on a badge anywhere. The ring is still refused for the reason given - it would sit
  outside the frame's own gold border and clip the four cardinal spikes. What a framed badge gets
  instead is an **aspect glow behind it**, which colours the space around the spikes rather than
  crossing them, and which is gated on the same `showRing` flag the ring is. Sigil heroes are
  unchanged.
- **Backgrounds: 2 screens painted, 12 flat.**
- **Icons: three unrelated systems**, above.
- **Backdrop coverage splits down the middle.** `ashen`/`ember`/`hollow` painted;
  `coast`/`deep`/`tidal` generated. That is **all twelve chapter-2 stages** plus half the Recital's
  aspect floors, which map aspects to environments including the three missing ones.
- Minor, code-only, no artist impact: six screens call `UISkin.Panel(n)` directly while eight go
  through `UIBuilder`. Both land on the same file.

## 5. Prioritized list for the artist

1. **Three battlefield backdrops: `coast`, `deep`, `tidal`.** Biggest visible surface in the game.
   Unlocks twelve campaign stages and half the Recital. Convention exists, matches the three already
   delivered, **zero code needed** - drop into `Resources/Backgrounds/{id}.png`.
2. ~~**Six framed hero portraits.**~~ **Done, and at a different scale than this item imagined.**
   All 94 heroes are delivered framed, across all six aspects. The six heroes named here were
   deleted in the roster swap before the delivery arrived. The aspect-ring decision this item was
   blocked on is made; see section 4.
3. **Resize and wire the six existing UI icons.** The art is done. It needs downscaling from 1008 px
   and roughly two lines in `UISkin.Surface`. Cheapest real-art win available.
4. **Nine enemy battle sprites:** `cinder_imp`, `blight_hound`, `stone_warden`, `ashen_revenant`,
   `tidecaller_wretch`, `riptide_channeler`, `tideturned`, `fractured_chorister`, `the_unmade`.
   `the_unmade` is the prologue's antagonist and currently renders as a blocky purple primitive in a
   scripted story beat.
5. **`proving.png`** - one file, documented as needing no code change.
6. **Item icons for Inventory, Forge and RelicShop.** **Blocked on a decision:** no icon convention
   exists to deliver against. Largest job on the list and the one that most needs a decision before
   any art starts.
7. **Bespoke treatment for the five zero-art screens.** Lowest priority - they are functional, and
   items 1-6 buy more visible ground per hour.

**One decision still gates the list:** whether an icon convention exists at all (blocks 3 and 6).
The aspect-ring rule that used to gate item 2 is settled, and item 2 is delivered.
