# Guild dungeon size and attempts: spec for the UI run

Written for the run that builds the client side of the guild dungeon's size and attempt rules
(`DESIGN-BUILD-ORDER.md`, **Guild dungeon size and attempts**). Nothing here is built. The core
rules, the server migration and the harness checks exist; this is what the screens must show on
top of them. **No game logic goes in UI**: every number below is read from `GuildDungeonStatus`,
`GuildDungeonScale` or `GuildDungeonRewards`, never recomputed in a screen.

Look and feel: the existing guild dungeon screens' fonts, colours, buttons and panels
(`GuildDungeonScreen`, `GuildFloorScreen`, `GuildDungeonBoardsScreen`, `GuildDungeonResultScreen`).
No new art. Every changed screen is captured with `PlayModeScreenshot` before the work counts as
done, at 1920x1080, with a 5-floor guild and a 10-floor guild.

## What the player must be able to read

1. **How long this week's ladder is, and why.** "10 FLOORS THIS CYCLE - set at 31 members when the
   cycle opened." Read `GuildDungeonStatus.FloorCount` and `GuildDungeonStatus.LadderMembers`.
   When `LadderMembers` is -1 (a server before 0016), show the floor count alone.
2. **That the ladder will grow, and when.** One quiet line under it, only when the guild is below
   the cap: "A guild of 15 gets 7 floors from next cycle." Read the next band from
   `GuildDungeonScale.LadderFor(content, members)` with the live member count from `GuildService`.
   Never promise a change this cycle: the ladder is fixed until it rolls.
3. **How many attempts the member has left this cycle, as well as on each floor.** A counter in the
   dungeon header: "ATTEMPTS 9 / 15 THIS CYCLE". Cap from `GuildDungeonStatus.CycleAttemptCap`
   (fall back to `GuildDungeonScale.CycleAttemptCap(balance)` when it is -1), used from
   `GuildDungeonStatus.AttemptsUsed` (fall back to `GuildDungeonScale.CycleAttemptsUsed` over the
   rows). The per-floor "2 of 3 left" stays on each floor row, and each row's number is
   `GuildDungeonScale.AttemptsLeft`, which already takes the lesser of the two.
4. **Why a floor cannot be attempted.** Every disabled ATTEMPT button shows the sentence
   `GuildDungeonScale.CanAttempt` returns as its reason, verbatim: "no attempts left this cycle
   (15 of 15)", "floor 7 opens when somebody in your guild clears floor 6", "your guild's dungeon
   is 6 floors this cycle". Do not write separate copy for these.
5. **That deep floors pay nothing extra yet.** Where a floor row or the result screen would show a
   reward, read `GuildDungeonRewards.ForFirstClear`. While `Placeholder` is true, show "Reward not
   decided yet" in the muted text colour, and never a 0 with a currency icon. The existing Waymark
   lines for the first attempt and the first clear of any floor are unchanged.

## Layout changes

- **The floor list holds up to ten rows.** Today it is sized for five. At ten it must still fit
  the 1920x1080 frame without covering the header or the bottom bar: a scroll view, or two
  columns of five (block 1 and block 2), whichever the existing panel accommodates without new
  art. The Champion floors (5 and 10) keep the Champion treatment they have today.
- **The pip strip** (`DrawPips`) already takes the floor count. Check it does not overflow at ten;
  if it does, shrink the pip spacing, not the pips.
- **The boards screen's floor tabs** (`BuildTabs`) already take the floor count. At ten tabs, check
  the tab row fits; if it does not, scroll it.
- **Block marker.** A thin divider between floor 5 and floor 6 with the caption "SECOND BLOCK -
  the climb starts again", so a member does not read floor 6 as harder than floor 5. It is not.

## Not in scope

- No purchase of any kind: no "buy attempts" button, no refill, no ad. Attempts cannot be bought;
  the harness fails the build if any store or pass grant names the dungeon.
- No change to how a fight is chosen, fought or replayed. Floors 1 to 5 are the same fights as
  before.
- No change to the Guild Path or guild tech screens.

## States to capture

1. A guild of 4 on cycle open: 5 floors, set at 4 members, "a guild of 10 gets 6 floors from next
   cycle", 15 of 15 attempts left.
2. A guild of 30 mid-cycle: 10 floors, floor 6 open, the member at 15 of 15 used, every ATTEMPT
   button disabled with the cycle sentence, a guildmate's floor 7 clear on the boards.
3. A guild of 12 whose cycle opened at 9 members: 5 floors, "set at 9 members", and the next-cycle
   line saying 6.
4. A member who changed guild after spending 15 attempts: status says 15 used, no row of theirs is
   visible, every button disabled with the cycle sentence.
5. The result screen after clearing floor 8: the Waymark lines as today, and "Reward not decided
   yet" for the floor.
