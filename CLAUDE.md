# Emberfall — working notes for agents

An original mobile idle RPG prototype: collectible heroes, automatic 5-a-side turn-based combat,
campaign progression, summoning, idle income, local saving. Landscape, touch-first, Android.

## Ground rules

- **`DESIGN-BUILD-ORDER.md` is the only document kept current.** Read its **Now / Next** and
  **Already done, do not rebuild** sections before assuming anything about what is built.
- **Every other `DESIGN-*.md` is a decision record from its date.** The reasoning in them is
  durable; the status claims, names and numbers are not. Never conclude from one that a system
  is unbuilt.
- **`README.md`, `UI-AUDIT.md`, `GYM-SESSION.md` and `STATE-OF-THE-GAME.md` are superseded** and
  must not be used as evidence.
- **Cite symbols, never line numbers.** Write "`ApplyLedger` in `CombatSimulation.cs`", not
  "`CombatSimulation.cs:1060`".
- **Cite observable file content, never commit hashes.** Write what the commit proved, not which
  commit proved it.
- **A branch name may be cited only in `DESIGN-BUILD-ORDER.md`'s Now / Next**, only while the
  branch is live, and only alongside a description of what it contains. Unmerged work has no
  observable state in the tree, so a pointer is the only thing that can express it. When the
  branch merges, the line moves to **Already done** and the branch name is replaced by what is
  now observable in the files.
- **When a session finds a design doc wrong against real code, it edits that doc in the same
  commit.**
- **A commit that changes a screen's layout is not done** until that screen has been captured
  with `PlayModeScreenshot`.

## Project facts

- **Unity 6.5** (`6000.5.9f1`, see `ProjectSettings/ProjectVersion.txt`).
- **Built-In Render Pipeline.** Not URP. The shaders in `Assets/Resources/Shaders` carry a URP
  sub-shader so URP will render, but Built-In is what the project is designed against, and
  `GameApp` warns at startup if an SRP is active. Never create a new project on top of this
  folder — the template can switch the pipeline.
- Requires the **`com.unity.ugui`** package (`Packages/manifest.json`). The UI is uGUI, built
  in code; the scene is deliberately near-empty.
- **Combat is a pure function.** `CombatSimulation.Run()` in `Assets/Scripts/Core` resolves the
  whole fight up front and returns an ordered list of `BattleEvent`s. The battle screen is a
  *player* for that list: it never computes damage, never picks a target, and cannot change the
  outcome. **No game logic goes in UI.** If the screen seems to be missing information, it is
  almost always already in the event list and merely unread — read the record, don't recompute
  it. See `DESIGN-BATTLE-READABILITY.md`.
- **Content is JSON** in `Assets/Resources/Content`, eight files: `armor.json`, `balance.json`,
  `campaign.json`, `enemies.json`, `events.json`, `floor-conditions.json`, `heroes.json`,
  `relics.json`. Balance and content changes go there, not into code. All eight are hashed into
  `ContentDatabase.ContentVersion`, so any edit retires every recorded guild dungeon replay. A
  ninth, `guild-path.json` (the Guild Path's pay and prices), is deliberately NOT hashed: nothing
  in it is read by a fight, so it loads through `ContentDatabase.LoadGuildPath` after `Load`.
- **Code goes on branches, never on `main`.** Branch first, commit there, leave `main` alone.
- **Design docs commit straight to `main`.** They touch no code, and every agent needs to read
  them.
- Verify with `Tools/build-check.ps1` before calling work done.

## Update posts (Discord)

**ON HOLD until the roster is authored: agents never post, to either webhook, even when
a batch finishes.** The rest of this section is the procedure for when posting resumes.

When a group of tasks finishes, post a summary to the staff webhook in
`EMBERFALL_WEBHOOK`. If the batch includes feature work — anything a player could see or
play — also post a separate community-facing version to `EMBERFALL_COMMUNITY_WEBHOOK`.
Pure docs/maintenance/tooling batches never go to community.

**Never write either webhook URL into any file in this repo — only ever the variable
names.**

### Staff post

- **title** — short, plain-language headline of what changed.
- **description** — for someone who was not here and does not read code. Say what a
  **player would notice**, not what files changed. Two to four sentences. No file paths,
  no commit hashes, no jargon. If nothing player-visible changed, say exactly that, in
  one line, instead.
- **footer** — the branch name.
- **timestamp** — UTC, ISO 8601.
- **color** — `0xE2571E` (ember orange) for feature work, `0x6E7681` (muted grey) for
  docs/maintenance only. A batch with both is feature work.

### Community post (feature-work batches only)

Write this as its own pass, not a copy of the staff description. Staff copy can carry
dev framing ("retuned the economy," "wired the shop backend"); community copy should
read like a devlog written for someone who plays the game, not someone who builds it.
Lead with what changed for them, skip anything that assumes repo/system knowledge.

- **title** — same headline energy as staff, but written for players.
- **description** — two to four sentences, player-facing only. No branch names, no task
  numbers, no internal system names (e.g. say "you can now buy relics with dust you earn
  from clears" not "Relic Shop screen wired to the Relic Progression backend").
- **footer** — static text, `"Emberfall devlog"`. Never the branch name.
- **timestamp** — UTC, ISO 8601.
- **color** — always `0xE2571E`, since this channel only ever receives feature work.

No `fields` on either post. The commit feed already covers file-level detail for staff;
community gets none of that by design.

### The command (PowerShell — verified, returns `HTTP 204`)

```powershell
$staffTitle = 'Short plain-language headline of what changed'
$staffDesc = @'
Two to four sentences on what a player would notice. Quotes " and & are fine.
'@
$isFeatureWork = $true   # set false for docs/maintenance-only batches
$color = if ($isFeatureWork) { 0xE2571E } else { 0x6E7681 }
$branch = (git rev-parse --abbrev-ref HEAD)

$staffPayload = @{ embeds = @( @{
    title       = $staffTitle
    description = $staffDesc
    color       = $color
    footer      = @{ text = "branch: $branch" }
    timestamp   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
} ) } | ConvertTo-Json -Depth 6 -Compress

$tmp = Join-Path $env:TEMP 'emberfall-update.json'
[IO.File]::WriteAllText($tmp, $staffPayload, [Text.UTF8Encoding]::new($false))
curl.exe -sS -o "$env:TEMP\emberfall-resp.txt" -w "HTTP %{http_code}`n" -X POST -H "Content-Type: application/json" --data-binary "@$tmp" "$env:EMBERFALL_WEBHOOK"

if ($isFeatureWork) {
    $communityTitle = 'Player-facing headline, same event, written for players'
    $communityDesc = @'
Two to four sentences, written fresh for players. No branch names, no system names.
'@
    $communityPayload = @{ embeds = @( @{
        title       = $communityTitle
        description = $communityDesc
        color       = 0xE2571E
        footer      = @{ text = 'Emberfall devlog' }
        timestamp   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    } ) } | ConvertTo-Json -Depth 6 -Compress

    $tmp2 = Join-Path $env:TEMP 'emberfall-update-community.json'
    [IO.File]::WriteAllText($tmp2, $communityPayload, [Text.UTF8Encoding]::new($false))
    curl.exe -sS -o "$env:TEMP\emberfall-resp-community.txt" -w "HTTP %{http_code}`n" -X POST -H "Content-Type: application/json" --data-binary "@$tmp2" "$env:EMBERFALL_COMMUNITY_WEBHOOK"
}
```

Success is **HTTP 204** with an empty body on each request that fires. Anything else
means that post did not land.

Why it is shaped that way — each of these was hit for real:

- **`-Depth 6` is not optional.** `ConvertTo-Json` defaults to depth 2, which silently renders
  the nested footer as the literal string `"System.Collections.Hashtable"` and posts it.
- `curl` alone is an **alias for `Invoke-WebRequest`** in Windows PowerShell 5.1. It must be
  `curl.exe`.
- **Do not pipe the JSON into curl.** Piping to a native exe here prepends a UTF-8 **BOM**
  (`EF BB BF`) to the body, and Discord rejects that. Writing the file with
  `UTF8Encoding($false)` and passing `--data-binary "@file"` avoids it.
- Let `ConvertTo-Json` do the escaping. Hand-escaping quotes, backslashes and newlines inside a
  JSON string in PowerShell is where this breaks.
- Use a **single-quoted here-string** (`@'` … `'@`) for the description so `$` and backticks stay
  literal. Its closing `'@` must be at column 0.
- `color` is a **number, not a hex string** — `0xE2571E` is fine, `"#E2571E"` is rejected.
- Pass the URL as `"$env:EMBERFALL_WEBHOOK"` / `"$env:EMBERFALL_COMMUNITY_WEBHOOK"` and keep
  `-sS -o <file>` so tokens never land in console output or in the repo.
- If either webhook variable is empty, the shell predates it being set. Open a new shell,
  or rehydrate it for the session, e.g.:
  `$env:EMBERFALL_COMMUNITY_WEBHOOK = [Environment]::GetEnvironmentVariable('EMBERFALL_COMMUNITY_WEBHOOK','User')`
