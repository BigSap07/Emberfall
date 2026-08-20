# Emberfall UI Layer Audit

Scope: `Assets/Scripts/UI/UIBuilder.cs`, `UITheme.cs`, `Widgets.cs`, `ScreenChrome.cs`, `HudTopBar.cs`,
`Overlays.cs`, `ScreenRouter.cs`, and every screen in `Assets/Scripts/UI/Screens/` (MainMenuScreen,
CampaignScreen, HeroesScreen, HeroDetailScreen, SummonScreen, BattleScreen). All UI is built at
runtime with `RectTransform` math (`UIBuilder.Box`, `TopStrip`, `BottomStrip`, `FillParent`) rather
than authored in the Editor, so every position is a literal number in C#. Target canvas is portrait
1080x1920 (`UITheme.ReferenceResolution`, README "Portrait, touch-first").

Coordinate convention used below: "distance from top" = pixels from the top edge of the 1920-tall
canvas, increasing downward. This is computed from each element's Box/TopStrip/BottomStrip
anchor + offset, since that is what actually determines overlap regardless of which anchor corner the
literal numbers are expressed in.

---

## 1. Hardcoded y-offsets and magic numbers

Every screen positions elements with raw numeric literals passed straight into `UIBuilder.Box(...)`.
None of these derive from a shared spacing constant - `UITheme` only defines `ScreenPadding` (32),
`TopBarHeight` (132), `NavBarHeight` (168, unused by any screen), `CornerRadius`/`CornerRadiusSmall`.
Everything else (gaps, card heights, internal padding) is invented per call site.

**MainMenuScreen.cs**
- `:42` `Box(logo, 0, -180, 900, 100)` - logo position/size
- `:47` `Box(underline, 0, -288, 340, 3)` - rule under logo
- `:50` `Box(sub, 0, -312, 700, 32)` - subtitle position
- `:56` `Box(card, 0, -392, 940, 300)` - squad card position/size
- `:68` `Box(_teamRow, 0, -54, 880, 232)` - team row inset inside squad card
- `:75` `Box(card, 0, -722, 940, 320)` - idle cache card position/size
- `:90/93/96` `Box(_idleRate/_idleAmounts/_idleTimer, 30, -66/-108/-160, 620, 26/46/32)` - three stacked labels, each y hand-tuned
- `:99` `Box(_claimButton.Root, -30, 32, 300, 116)`
- `:106` `Box(card, 0, 470, 940, 150)` - chapter progress card, bottom-anchored
- `:132` `Box(_continueButton.Root, 0, 286, 940, 168)`
- `:136` `Box(_continueSub, 0, 244, 900, 34)`
- `:142` `BottomStrip(nav, 200f, 30f, ScreenPadding)` - nav height/bottom-inset are magic
- `:223` `Sized(cell, 270, 196)`, `:238/241/245` team-slot portrait/name/meta offsets

**CampaignScreen.cs**
- `:27` `FillParent(scroll, ScreenPadding, TopBarHeight+292f, ScreenPadding, 24f)` - "292" and "24" magic
- `:33` `TopStrip(card, 146f, TopBarHeight+132f, ScreenPadding)` - "146"/"132" magic
- `:114` `Sized(row, 0, 76)` - chapter heading row height
- `:136` `Sized(row, 0, 186)` - stage row height (every stage card)
- `:154` `Box(badge, 22, 0, 96, 96)`
- `:169/172` `Box(name, 138, -22, 620, 34)` / `Box(subtitle, 138, -58, 640, 26)`
- `:176` `Box(pips, 138, 20, 300, 62)`
- `:188/192` `Box(rewards, -24, 24, 300, 28)` / `Box(power, -24, 54, 340, 26)`
- `:200/207/212` lock/cleared/next chip positions, all independent literals

**HeroesScreen.cs**
- `:23` `ScrollGrid(..., cellSize(470,300), spacing(20,20), 2 cols, padding(0,0,8,200))`
- `:26` `FillParent(scrollRoot, ScreenPadding, TopBarHeight+130f, ScreenPadding, 0f)` - "130" magic

**HeroDetailScreen.cs**
- `:50` `FillParent(scroll, ScreenPadding, TopBarHeight+130f, ScreenPadding, 236f)` - "130"/"236" magic
- `:62` `Sized(_identityCard, 0, 348)`
- `:71,74,77,80,84,87,90` - portrait/name/title/chips/level/power/slot-hint, seven independently hand-placed offsets inside one card
- `:92-97` `_slotRow` - see dead-code note in section 4
- `:113` `Sized(_statsCard, 0, 330)`
- `:122` `FillParent(list, 16, 16, 16, 16)` - card padding
- `:123` `VStack(list, 4f, ...)` - row spacing
- `:125-133` five `StatRow(..., 58f)` calls - row height
- `:148` `BottomStrip(bar, 200, 0, 0)` - action bar height
- `:158/162` level-up button rects

**SummonScreen.cs**
- `:49` `Box(_portal, 0, 120, 760, 760)`
- `:70` `Box(card, 0, 346, 940, 170)` - rates card
- `:109/112` cost label / summon button rects
- `:127,130,133,136,140,143,146` - seven reveal-overlay element rects, each an independent literal (see section 2 for the overlap this produces)

**BattleScreen.cs**
- `:96` `TopStrip(bar, 112, 24, ScreenPadding)` - top HUD
- `:104/108/111/115` stage label / round label / speed / skip button rects
- `:122,133,137` banner rects
- `:154,158,161,169,172,176,180` result panel rects (title, sub, rewards card, heading, list inset, two buttons)
- `:292,299,304,309,312,315,319` - unit health-bar internal layout (name/status/hp/shield/energy strip), six literals per bar, instantiated per combatant

**Widgets.cs**
- `:48` `StarRow` size formula (`starSize*count + 4f*(count-1)`) - the "4f" gap is a private magic number
- `:86` `AbilityCard` `Sized(root, 0, 172)` fixed card height
- `:95,106,109,112-116` - icon/kind/title/body rects, see dead-code note in section 4
- `:127,147,150,153,156,159,162,167` - `HeroCard` portrait/name/stars/role-chip/level/power/deployed-badge rects, eight independent literals per card

---

## 2. Overlap risk at 1080x1920

Walking each screen's cumulative offsets (all figures below are exact, derived from the literals above; "distance from top" as defined at the top of this doc):

### HeroDetailScreen - confirmed overflow bug
`StatsCard` (`HeroDetailScreen.cs:113`) is fixed at 330px tall. Its inner list
(`:122` `FillParent(list, 16,16,16,16)`) leaves 298px of interior height. Five `StatRow`s at
58px each (`:125-133`) plus a 4px VStack spacing (`:123`) between them need:
`5*58 + 4*4 = 290 + 16 = 306px`. 306 > 298 - an 8px shortfall. Because this list is a plain
`VerticalLayoutGroup` inside a fixed-size container (no `ContentSizeFitter`, no scroll), the fifth row
("CRITICAL") is pushed 8px past the card's bottom edge/rounded panel and frame (`Frame("Edge", ...)`
at `:119`) on every single hero, every time this screen is opened - not a hypothetical, a static
guaranteed clip/overlap with the card's own border.

### SummonScreen - reveal overlay, confirmed 2px overlap
`_revealTitle` (`SummonScreen.cs:139-140`, `y=-84, h=36`) spans screen-center-relative y [-102, -66].
`_revealNote` (`:142-143`, `y=-150, h=100`) spans y [-200, -100]. The two intervals overlap by
2px ([-102, -100]) purely from the literals - before any text is even rendered. `_revealNote` is
a `WrappedLabel` fed `outcome.Message + " Converted to " + goldRefund + " gold and " + shardRefund +
" shards."` (`:234-236`), so on a duplicate-summon outcome the text is long enough to wrap to 2-3 lines
at FontSmall (24px) in an 820px-wide box - which only makes the pre-existing 2px overlap with the
title above it worse, since `VerticalOverflow.Truncate` clips growth but does not resize the box.

### CampaignScreen - stage row, 2px gap between stacked labels
Inside `BuildStageRow` (186px tall row), `power` (`:190-192`, anchored bottom-right, `y=54,h=26`)
spans row-bottom-relative [54,80]; `rewards` (`:186-188`, `y=24,h=28`) spans [24,52]. Gap between
them is 2px (`54-52`). Both are right-aligned single-line FontTiny labels stacked in
the same column on every unlocked stage card - a 2px gap is well within normal font-rendering jitter
and will visibly kiss/touch on many devices.

### HeroDetailScreen - identity card, 6px gap
`slotHint` ("BANNER SLOT", `:89-90`, `y=-218,h=26`) ends at 244px from the card top; `_slotRow`
(`:92-97`) starts at 250px from the card top (see computation in section 4). 6px gap between a caption
and the buttons it labels - visually crowds together on every hero.

### MainMenuScreen - continue button to its subtitle, 8px gap
`_continueButton` (`:131-132`, bottom-anchored `y=286,h=168`) top edge is at distance-from-top 1466px;
`_continueSub` (`:135-136`, `y=244,h=34`) spans bottom-inset [244,278], i.e. distance-from-top
[1642,1676]. Its top (1642... corrected) sits only 8px below the continue button's bottom edge
(distance-from-top 1634, i.e. 1642-1634=8). Not an overlap today, but there is zero margin for a longer
"Next: ..." string (built from stage code + name at `:275`) to push visually against the button above it.

### Fixed, non-scrolling stacks fit - but with near-zero margin
- MainMenuScreen's five stacked cards (title block, squad card, idle cache, chapter progress,
  continue button, nav bar) all fit inside 1920px with the tightest gaps being the 8px case above and a
  16px gap between the chapter-progress card bottom (1450) and the continue button top (1466)
  (`:106`/`:132`). Any future increase to font size or card height here has no slack.
- BattleScreen's result panel (`:143-184`) has the opposite problem: `_resultTitle`/`_resultSub` are only
  5px apart (`:154` bottom edge 595 vs `:158` top edge 600) while the space between the rewards card
  and the two action buttons is over 300px of unused vertical space (`:161` card bottom 1141 vs `:176`
  button top 1502) - see section 3, this is the same screen being both too tight and too loose.

### Variable-length content that is genuinely safe today only by coincidence
- CampaignScreen's scrollable stage list (`:25`, `ScrollList` with `ContentSizeFitter`) and
  HeroDetailScreen's ability-card list (`:48`, same pattern) both grow the scroll content correctly, so
  they will not visually overlap other elements - but neither has a max-content guard, and the fixed
  `AbilityCard` height of 172px (`Widgets.cs:86`) combined with a hard-truncated 72px-tall description
  box (see section 4) means long ability text is silently cut rather than expanding the card.

---

## 3. Inconsistent spacing between screens

**Header-to-content gap** (from `ScreenChrome.Header` bottom edge at 252px-from-top, since all three
screens below reuse `ScreenChrome.Header`):

| Screen | Formula | Gap |
|---|---|---|
| HeroesScreen (`:26`) | `TopBarHeight + 130f` | 10px |
| HeroDetailScreen (`:50`) | `TopBarHeight + 130f` | 10px |
| CampaignScreen (`:33`) | `TopBarHeight + 132f` | 12px |

Same conceptual gap ("header to first content card"), two different literals (130 vs 132) producing a
2px inconsistency that exists purely because nobody shares a constant.

**Button-to-button spacing** (`HStack`/manual gap, same conceptual "row of buttons/chips"):

| Location | Value |
|---|---|
| `Widgets.cs:49` StarRow | 4px |
| `HeroDetailScreen.cs:98` slot buttons | 12px |
| `CampaignScreen.cs:60` team portraits | 12px |
| `BattleScreen.cs:111/115` speed/skip (manual offsets, not HStack) | 12px (derived) |
| `SummonScreen.cs:83` rarity-rate row | 16px |
| `MainMenuScreen.cs:143` nav buttons | 20px |

Six different literals (4/12/12/12/16/20) express what is conceptually the same "gap between adjacent
interactive chips" - no shared value.

**Card internal padding** (edge of card background to first child):

| Location | Value |
|---|---|
| `Widgets.cs:95` AbilityCard icon inset | 20px |
| `HeroDetailScreen.cs:122` StatsCard FillParent | 16px |
| `MainMenuScreen.cs:87` idle-cache heading | 30px (x) |
| `CampaignScreen.cs:154` stage badge inset | 22px |
| `Overlays.cs:87/95` ModalPopup title inset | 60px (derived from (840-720)/2) |
| `Overlays.cs:18/32` Toast box vs. label inset | 20px |

Padding values span 16-60px with no common unit. `UITheme.ScreenPadding` (32) is used consistently for
outer screen margins (headers, scroll insets) - that convention is not extended to card-internal
padding, which is where the inconsistency lives.

**Bottom action-bar height**: `HeroDetailScreen.cs:148` uses `200f`; `MainMenuScreen.cs:142` nav bar
also uses `200f` - these two agree, but neither derives from `UITheme.NavBarHeight` (168), which is
declared in `UITheme.cs:53` and then never referenced anywhere in `Assets/Scripts/UI/Screens/` or
`ScreenChrome.cs`/`HudTopBar.cs` - a shared constant that exists but is dead/unused, while the actual
call sites re-declare 200 by hand.

---

## 4. Text overflow risk

Grounded against `Assets/Resources/Content/heroes.json` and `campaign.json` string lengths.

1. **`Widgets.cs:111-116` `AbilityCard` body/description text** - `WrappedLabel` with
   `VerticalOverflow.Truncate`, hard-fixed to 72px tall (`:112` sets a `Box` that is then
   immediately overwritten by the explicit anchor block at `:113-116` - a dead first call, see below).
   The longest current ability description is Wren's passive: "At the start of battle, every ally is
   wrapped in a ward worth 9% of Wren's health." (84 characters). At FontTiny (20px) in the
   resulting ~868px-wide box this fits in roughly 2-3 lines and just clears the 72px budget, but there
   is no ellipsis or size feedback - any hero added later with a longer active/passive description (or
   a localized string, which routinely runs 20-40% longer than English) will be silently clipped
   with no visual indication text was cut.
   - Dead code: `Widgets.cs:112` calls `UIBuilder.Box(body.rectTransform, 124f, -84f, 0f, 72f, ...)`
     which sets sizeDelta/anchoredPosition, then `:113-116` immediately reassigns
     anchorMin/anchorMax/offsetMin/offsetMax on the same RectTransform, making line 112 a no-op. A
     future edit to line 112 expecting it to change the box will silently do nothing - same pattern
     repeats at `HeroDetailScreen.cs:92-97` (`_slotRow`).

2. **`SummonScreen.cs:142-143` `_revealNote`** - dynamically built string
   (`outcome.Message + "  Converted to " + goldRefund + " gold and " + shardRefund + " shards."`,
   `:236`) truncated at a fixed 100px box that (per section 2) already overlaps `_revealTitle` above it
   by 2px. Longer hero names or larger refund numbers push this from 2 lines to 3-4, worsening the
   overlap with no truncation indicator.

3. **`Widgets.cs:149-150` `HeroCard` name label** - `UIBuilder.Label` (not `WrappedLabel`, so
   `horizontalOverflow = Overflow`, no wrap, no clip) in a 260px-wide box at FontSmall (28px). The
   longest current hero name, "Kalyx Emberwright" (17 chars), renders at roughly 260-290px - already at
   or past the nominal box width. Because overflow is unclipped, a longer name (e.g. any two-word title
   with a longer surname) will visibly extend past the card's centered text region toward the portrait
   or role chip below it (`:152-156`) rather than wrapping or shrinking.

4. **`HeroDetailScreen.cs:73-74` `_nameLabel`** - same Label/Overflow pattern, 620px box at
   FontTitle (44px). Current longest name fits comfortably (~440px), but there is no wrap/shrink
   fallback if a longer name is ever added to heroes.json.

5. **`CampaignScreen.cs:171-172` stage `subtitle`** - Label/Overflow, 640px box at FontTiny (20px).
   The longest current subtitle, "Everyone who held this pass is stacked and burning." (53 chars),
   renders at roughly 580-600px - already close to the 640px box edge. Any longer subtitle will overflow
   past the row's right edge; because the row itself sits inside a masked ScrollRect viewport
   (`UIBuilder.ScrollShell` adds a RectMask2D on the viewport, not per-row), overflowed characters will
   be silently clipped by the outer scroll mask rather than wrapping.

6. **`Overlays.cs:94-98` `ModalPopup` title/body** - `_body` is a WrappedLabel with Truncate at a
   fixed 230px box; used for text like the reset-progress confirmation
   (`MainMenuScreen.cs:327-329`, ~131 characters, wraps to ~3 lines and fits today) but again has no
   ellipsis fallback if a future confirmation message runs longer.

---

## 5. Proposed spacing scale

The values actually found in the codebase cluster loosely around: 2, 4, 6, 8, 10, 12, 14, 16, 18, 20,
22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 44, 46, 48, 50, 54, 58, 60, 62, 74, 76, 92, 96, 108, 110,
116, 120, 126, 130, 132, 146, 150, 158, 160, 168, 170, 172, 176, 186, 196, 200, 210, 218, 220, 222,
232, 236, 244, 260, 270, 288, 292, 300, 312, 320, 330, 334, 340, 346, 348, 392, 420, 462, 470, 620,
640, 692, 722, 760, 800, 820, 860, 880, 900, 940, 980... - i.e. almost every literal is unique, with no
evidence of a shared unit.

A small scale that would cover essentially every legitimate use once adopted:

```
4   - hairline/rule thickness, star-row gap
8   - tight inline gap (delta text offset, icon-to-label micro gaps)
12  - button/chip row spacing, small card internal gaps
16  - card internal padding (standard), stat-row spacing baseline
24  - section gap (header-to-content, card-to-card gap), screen-edge secondary margin
32  - ScreenPadding (already exists) - outer screen margin, primary card padding
48  - card-to-card gap on dense screens, action-bar internal margin
64  - large block gap (title block, hero-card portrait offsets)
96  - big feature spacing (idle-cache internal blocks, portal/reveal layout)
```

Everything else (card heights like 172/186/300/330/348, portrait sizes like 84/122/128/176/340,
button heights like 76/96/116/118/126/130/160/168) is a content-sized dimension, not spacing, and can
stay bespoke - but the gaps between those elements should snap to the scale above.

Screens needing the most rework to adopt it, most to least:
1. **BattleScreen.cs** - the unit-bar stack (`:292-319`, six literals: 84/58/36/27/16/9) and the result
   panel (7 more independent rects, `:154-180`) are the least scale-aligned code in the project; almost
   nothing here is a multiple of 4, let alone 8.
2. **HeroDetailScreen.cs** - seven independent offsets inside the identity card (`:71-97`) plus the
   StatsCard overflow bug (section 2) means this file needs both a spacing pass and a bugfix.
3. **CampaignScreen.cs** - `BuildStageRow` (`:129-220`) hand-places nine elements per row with values
   that do not share a base unit (22/24/54/58/62/138 etc.).
4. **SummonScreen.cs** - the reveal overlay (`:126-146`) is seven bespoke rects that also happen to
   overlap (section 2); consolidating to a VStack with the proposed scale would fix the overlap and the
   inconsistency in one pass.
5. **MainMenuScreen.cs** - mostly consistent internally but still hand-tuned per card; lower priority
   since nothing here currently overlaps.
6. **HeroesScreen.cs** - smallest surface area, only the header-gap inconsistency (section 3) to fix.

---

## 6. Fix order by visual impact

1. **Fix the StatsCard 8px content overflow** (`HeroDetailScreen.cs:113,122,123,125-133`) - every
   hero's stat panel clips/overlaps its own card border on the CRITICAL row. This is the only defect in
   this audit that is guaranteed visible on a core, frequently-visited screen with zero conditions
   required to trigger it. Highest priority.

2. **Fix the SummonScreen reveal overlay overlap** (`SummonScreen.cs:139-143`) - title/note overlap by
   2px in the base case and get worse with real refund/message text, on the screen players see every
   time they pull a summon (a core monetization/engagement loop). Second priority because it is both
   guaranteed and on a high-frequency screen.

3. **Fix the CampaignScreen power/rewards 2px gap** (`CampaignScreen.cs:188,192`) - visible on every
   unlocked stage card in a list players scroll through constantly; a 4px nudge resolves it.

4. **Add wrap/shrink or a size guard to ability descriptions and stage subtitles**
   (`Widgets.cs:111-116`, `CampaignScreen.cs:171-172`, `SummonScreen.cs:142-143`) - not visibly broken
   with today's JSON content, but the margin is thin (multiple strings sit within 5-15% of their box
   limit) and any future content addition or localization pass will silently truncate/clip with no
   warning. High latent risk, currently invisible.

5. **Remove the two dead Box() calls** (`Widgets.cs:112` before `:113-116`; `HeroDetailScreen.cs:93`
   before `:94-97`) - not visibly broken (the override always wins today), but it is a live trap for the
   next person who edits either call site expecting it to do something.

6. **Unify the header-to-content gap** (10px in `HeroesScreen.cs:26`/`HeroDetailScreen.cs:50` vs 12px in
   `CampaignScreen.cs:33`) and the six different button-gap literals (section 3) - cosmetic, 2px in the
   worst case, but cheap to fix once a shared constant exists.

7. **Introduce the spacing scale from section 5 and route the ~150+ scattered literals through it** -
   the underlying fix that prevents 1-6 from recurring. Lowest urgency by itself (nothing breaks today
   from the inconsistency alone) but should follow, not precede, the concrete bugs above so the
   refactor is not done twice.