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
| 1 | **MainMenu** | `Backgrounds/hub.png` painted map | badge dock: 5 framed portraits, 6 procedural sigils; action discs and top bar procedural |
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

### 2.1 Portraits, framed - `Resources/Sprites/Portraits/{heroId}.png`

A finished badge carrying its own frame. Drawn edge to edge: **no disc, no glows, no aspect ring,
no inset.** Resolved first in `Widgets.cs` (`FramedPortrait`), so it wins wherever it exists.

Delivered at 512 px, Sprite/Single, `alphaIsTransparency` on, coverage-preserving mipmaps. Present
for the five Ember heroes: Kael, Seraphine, Marrow, Veska, Orren.

### 2.2 Portraits, frameless - `Resources/Sprites/Units/portrait_{heroId}.png`

A head-and-shoulders crop with no frame. `Widgets.Portrait` supplies the ground itself: aspect-
tinted disc, glows, aspect ring, and a 14.65% inset so the square art is inscribed in the round
disc.

**This tier currently holds zero files.** The five that lived here were deleted once the same five
shipped framed. The loader stays because a frameless delivery is still valid, but note that
`Tools/make_portraits.py` still writes into this path, and anything it produces is silently
shadowed by framed art for the same hero.

**The folder is what selects between 2.1 and 2.2.** There is no flag in `heroes.json`; dropping a
file into one folder or the other is the whole decision.

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
  explicitly not combat scale. Nobody has audited the five delivered badges against them.

## 4. Inconsistencies, same element handled differently

- **Portraits: two conventions.** See 2.1 and 2.2. One live with five files, one live with none.
- **Aspect ring: present on 6 heroes, absent on 5.** Sigil heroes get an aspect-coloured ring;
  framed badges deliberately skip it, because the art carries its own frame and four cardinal
  spikes that a ring would sit outside of and clip. Harmless while all five framed heroes are
  Ember. **It becomes a real inconsistency the moment a non-Ember hero is delivered framed** - which
  is the next six.
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
2. **Six framed hero portraits: Orrin Kaal, Kalyx Emberwright, Wren Ilyra, Ryn Solvarr, Maeve
   Corrin, Nyx Serrow.** Same framed template as the five delivered.
   **Blocked on a decision:** these are non-Ember, and framed art currently drops the aspect ring.
   Five Ember badges hid that question; these six ask it.
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

**Two decisions gate the list:** the aspect-ring rule for non-Ember framed portraits (blocks 2), and
whether an icon convention exists at all (blocks 3 and 6).
