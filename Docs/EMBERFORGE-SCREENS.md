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
| Stat rows, ability cards (`Widgets`) | converted | `Widgets.StatRow` is shared by every page with a stat block. `Widgets.AbilityCard` is RETIRED and has no callers - it takes a `description`, and an ability's description is authored prose that several dozen abilities disagree with their own effects about and that some use to address a designer. The hero page builds its own cells from `AbilityMath` and `AbilityText` instead. |
| Item card (`ItemCard`) | converted | Keeps its chamfered silhouette on purpose - that is the item language, not the chrome language - and gains the kit's outline. |
| Save conflict chooser | converted | Both answers are primary, deliberately: there is no way out of this card, so teal would be lying. |
| Overflow menu | converted | Already on the kit. |
| Component gallery (`DesignGallery`) | converted | Gained the chip, the rule and the stat row. Its own fit guard still passes at 20:9. |
| Idle claim popup, overflow menu, panel sheet, confirm dialog | converted | All four are Hub LAYERS rather than screens the router owns, so none of them could be photographed by a schedule that only calls `Router.Show`. `PlayModeScreenshot` drives `MainMenuScreen.OpenLayerForCapture` for them now. |
| First-run prologue | converted by construction | See the First run row. |
| Battle result | converted | The card grew to 700 and both its lists scroll: the spoils are four rows after a battle and eight after a stage clear, and the damage table is one row per unit that fought, so both used to run out of the bottom of the card. |
| Battle HUD | converted | The unit tags keep their plain 7-unit bars on purpose - a nine-sliced trough has no middle left to stretch at that height and renders as two squashed corners. The kit reaches the text on them. |
| Hero detail | converted | The two panels stretch between the header and the action bar now. Fixed at 636 they needed 1056 units of height, so at 20:9 the action bar was drawn up through both of them. No ring round the portrait any more. **Every ability card now carries the ability's own NUMBER** - a flat value at this hero's stats with the authored percentage under it - and one note under the row carries the single caveat rather than five cards repeating it. The row grew 24 units for the two extra lines; they came from the matchup row, the top pad, the badge band and three gaps, and the panel's own fit guard is back to 22 units of slack against the shortest canvas. The hero's sigil left the card: it was drawn from the HERO, so five abilities carried five copies of one emblem and identified none of them. |
| Ability breakdown | converted | A layer of the hero page, opened by tapping any ability card, listing every damage, healing and ward figure that ability carries. Built rather than borrowed from `Overlays.ModalPopup`, and the capture is what decided it: that dialog's body is a fixed block of static text, and Kael's WHAT BREAKS FORWARD carries eight numbers - six fitted and the rest were not drawn. This list scrolls. It sits on the app's OVERLAY layer, like every other dialog: parented to the screen instead, its title ribbon slid under the top bar at 20:9 and nowhere else. `Tools/capture-ui.sh AbilityNumbers`. |
| Summon | converted | The banner stage stretches the same way: the action block was off the bottom of a 20:9 screen entirely. The guarantee bar is the kit's progress bar, at the sprite's own height - at 18 units it rendered as a lens. **The stage carries no hero any more.** It showed a derived one (the first 5-star with art) that read as a featured banner or as the last pull, and was neither; it now says what is true of the next pull - the opening guarantee while it lasts, then how much of the pool is not yet yours - over the odds line. `summon.png`, `summon-guarantee.png`. |
| Talents (`TalentsScreen`) | converted | New. The three talents side by side in `PlainPanel` cards, the running one framed; each prints its sentence, its numbers from the effect data (`TalentMath`, `TalentText`) and a reading measured in the player's own line (`TalentTrial`). The strip of 5-stars across the top is `SelectableCard` in a `ScrollStrip`. The numbers list is the one part of a card that scrolls, and only at 20:9, for the longest list in the roster. Photographed on a scratch copy of a real save by `TalentShots`, because a fresh profile has no 5-star to measure. |
| Inventory | converted | The category rail is a tab bar rather than eight buttons. The capture seeds real armour and relics, because the empty state proves nothing about the grid or the detail panel. |
| Campaign | converted | The painting stays the ground. The chapter row is a tab bar and the stage stepper's pips are the kit's disc in its two depths, so "you are here" is told by depth rather than by a tint on a baked bevel. |
| Settings | converted | RESET PROGRESS is teal on purpose: the kit's ember voice is the thing to press, and the one destructive control in the game is not it. |
| Profile | converted | The avatar picker scrolls horizontally and its faces are tabs. A layout group in a fixed rect does not overflow, it divides - thirty-two owned heroes were fifty pixels each with their names printed through one another. |
| Account | converted | Five cards, five text fields, and the kit's disabled button art doing the work the offline state needs. |
| Forge | converted | The slot rail is a tab bar and the three anvil actions are one voice, not three: the caller's tint used to colour each button's own border, which is three button voices on a row whose job is to offer three equal choices. |
| Formation | converted | The banner slots and the roster rows are `SelectableCard`, a third pressable thing the kit needed: built as buttons they came out as a page of solid teal, and a tab's unlit plate is far too heavy at card size. |
| Relic shop | converted | A relic on the shelf is a framed panel and one already owned is a plain one, so depth carries "you have this". The grade's colour left the card's border: every frame in this kit is gold. |
| Friends | converted | Photographed in its OFFLINE state, which is what every build with no `supabase.json` shows. The signed-in list needs a live account and a second live account that has sent a request; that is `AccountsVerification`'s job and no synthetic seeding could stand in for it. |
| Friend's team | converted | Photographed OFFLINE, which is what every build with no `supabase.json` shows. |
| Guild | converted | Your own row on the roster stands proud of the sheet and everyone else is set into it, so depth sorts a roster of thirty without a second frame colour. Photographed OFFLINE. The identity card gained DUNGEON (ember, under CHAT), and the guild blurb was cut to 600 wide because it ran under the button column; that change is NOT photographed, since the in-guild face needs a live account. |
| Guild requests | converted | Every row here is waiting on an answer, so every row is framed: the page exists only when there is something to decide. Photographed OFFLINE. |
| Guild chat | converted | Your own line stands proud and everyone else's is set in, which is depth carrying authorship. Still not right-aligned: at 1180 wide a right-aligned bubble leaves a metre of empty panel. Photographed OFFLINE. |
| Events | converted | A running event is framed and an ended one is plain; a claimable milestone stands proud of its track and the rest are set into it. Its progress bar grew from 14 units to 36 for the reason the Rift's did. |
| Deep Recital | converted | The banner takes what the cycle strip leaves, measured against the short canvas. At a fixed 560 the strip's cells sat below y 1010 and were off a 20:9 screen entirely. |
| The Proving | converted | The card's height is measured from the blocks inside it rather than guessed, and the Valor purse and the honesty line are anchored to the bottom of the screen rather than to a hand-copied 520. |
| Guild chat, shared attempt card | built on the kit | A dungeon attempt posted to chat is a framed panel, not a line: author and time where a line's would be, the floor and result in Cinzel, the line that fought as portraits, the boards it entered, and two buttons. WATCH (ember) replays the stored fight; RUN IT YOURSELF (teal) opens the same floor with your own roster. A card fought under other content says CAN'T REPLAY and why, instead of offering a WATCH that would refuse. `guild-chat-card.png`, `guild-chat-posted.png`. |
| Guild dungeon (`GuildDungeonScreen`) | built on the kit | New. The cycle, time left, how far the guild has pushed (five short bars, one per floor), your own progress and attempts, then one row per floor with its five enemies shown before anything is opened. A locked row says which floor has to fall to open it. Scrolls: five rows and the status card are taller than a 20:9 canvas. `guild-dungeon.png`. |
| Guild floor (`GuildFloorScreen`) | built on the kit | New. Enemy line on the left, each enemy with level, Aspect, what it does (read off its abilities, the same reading that built the floor), its stats at that level and its active. Your line on the right: five slots, the boards it would enter if it clears, attempts left, ATTEMPT. Your heroes in a strip under it. Opened from a replay, the subtitle and a row of portraits carry the fight you watched. Both panels measured against the 966 short canvas. `guild-dungeon-floor.png`, `guild-dungeon-handover.png`. |
| Guild dungeon result (`GuildDungeonResultScreen`) | built on the kit | New. Outcome, losses, attempts left and the line on the left, with POST TO GUILD CHAT as the gold button, win or lose. All six boards on the right: a board entered shows your place on it ("2ND OF 5"), a board missed says what kept you off it ("fielded Kael, a 5-star"). After a handover, the fight you watched is set beside yours. `guild-dungeon-result.png`. |
| Guild dungeon boards (`GuildDungeonBoardsScreen`) | built on the kit | New. A tab per floor and six board cards, one per constraint, three by two. Your row stands proud; every row can be tapped to watch that attempt. `guild-dungeon-boards.png`. |
| Battle, guild dungeon mode | converted | The existing HUD and result panel, playing a result handed in from Core (`BattleScreen.SetGuildRun`); nothing is computed on the screen. The result panel's three buttons are the flow's: forward (SEE WHERE IT PLACES YOU, or RUN IT YOURSELF after a replay), back, and WATCH AGAIN. `guild-dungeon-replay.png`, `guild-dungeon-replay-end.png`, `guild-dungeon-fight.png`, `guild-dungeon-fight-end.png`. |
| First run | converted by construction | The prologue draws nothing of its own: eight steps, three of which open `ModalPopup` and five of which run a battle. Both of those wear the kit, so this does. |

## The guild dungeon's shots are a test run too

`GuildDungeonShots` (`Emberfall/Capture/Guild dungeon`, or
`Unity.exe -projectPath . -executeMethod Emberfall.EditorTools.GuildDungeonShots.Run`) copies the
real save into a scratch identity, holds the cloud off (`GameApp.NoCloudKey`, so no anonymous
account is signed up on the live project), and puts an in-memory guild of three members behind
`IGuildDungeonBackend`. It then presses the real buttons: chat, WATCH a guildmate's lost fight,
RUN IT YOURSELF, ATTEMPT, the result, POST TO GUILD CHAT, the boards. Last, it reads the chat as
the guildmate and replays the posted fight. It photographs each step at both aspects and writes
what it checked to `Tools/Screenshots/guild-dungeon-e2e.txt`, including a hash comparison showing
the real save is untouched.

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
| `EmberforgeUI.SelectableCard` | (Panel, PanelPlain) | A plate you can press, which gains the kit's gold frame while it is the one being acted on. The kit's third pressable thing, and it exists because the other two say the wrong sentence about a container: a button has two voices and neither of them is "a slot", and a tab's unlit plate is the darkest surface in the kit - right over a sheet, far too heavy at the size of a hero card. |
| `UIBuilder.ScrollStrip` | - | The horizontal counterpart of `ScrollList`. A layout group in a fixed rect does not overflow, it divides. |
| `Emberforge.OrnamentClearance` | - | How far in from a framed panel's corner a control has to stay to clear the corner diamond. Four screens hung a full-width button off a panel's bottom at the panel's own padding and printed it over the two lower ornaments. |
| `EmberforgeButtonSkin.SetKind` | - | Changes which voice a built button speaks in. For one shape of control the two voices are a state rather than a property. |
| `EmberforgeUI.Ground` | - | The page's own ground, as one call rather than the same three lines on every screen. Returned so an embeddable page can hide it inside a panel sheet. |

## What this pass retired

- **`ScreenChrome.Header`.** There were two page headers for the length of the conversion, and
  the original's own note said it retires when the last caller has moved. Every screen calls
  `ForgedHeader` now, so it has.
- **`HubStyle`'s drawing.** `Surface`, `Pill`, `SurfaceChild`, `Tail` and `SoonBadge` drew the
  Hub's own dialect and had no callers left once the nametags, the currency pills and the player
  plate moved to the kit. What stays in that file is the METRICS - `TouchMin`, the rail's disc and
  gap, the nav cell, the sheet's header and its tab row - which are measurements of this game's
  layout rather than of what a surface looks like, and Emberforge owns only the second.
- **`UIBuilder.Chip` has no callers.** It is left in place beside the other `UIBuilder` primitives
  and documented as retired; nothing in the game draws a tag through it.
- **`Widgets.AbilityCard` has no callers either**, and is retired the same way and in the same
  place. Its signature is the reason: it takes a `description`, and the hero page's cards are
  built from the effect DATA now, through `AbilityMath` in Core and `AbilityText` in UI. See
  `Docs/DESIGN-DAMAGE-DISPLAY.md` for why a percentage is not the number to lead with.

## Deliberately not converted

- **The unit tags over the battlefield.** Their health, ward and energy bars stay plain
  rectangles. The kit's trough and fill are nine-sliced with most of their height in their top and
  bottom borders, so at 7 units tall there is no middle left to stretch and both render as two
  squashed corners. The kit reaches the NAMES and the status line on those tags, which is where it
  can do its job.
- **The item card's silhouette.** It stays chamfered. That is the shape this game draws an OBJECT
  with, as against the rounded shape it draws chrome with, and rounding it would have made a helm
  and the panel behind it the same thing. It carries the kit's outline now, which is the one
  property every element in Emberforge shares.
- **The signed-in states of Account, Friends, Friend's team, Guild, Guild requests and Guild
  chat.** All six are photographed OFFLINE, which is what every build with no `supabase.json`
  shows and is the state this project's offline promise is about. The signed-in ones need a live
  account, a second live account and a relationship between them; that is `AccountsVerification`'s
  job and no synthetic seeding could stand in for it.
