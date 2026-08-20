# Emberfall — working notes for agents

An original mobile idle RPG prototype: collectible heroes, automatic 3v3 turn-based combat,
campaign progression, summoning, idle income, local saving. Portrait, touch-first, Android.

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
- **Content is JSON** in `Assets/Resources/Content` (`heroes.json`, `enemies.json`,
  `campaign.json`, `balance.json`). Balance and content changes go there, not into code.
- **Code goes on branches, never on `main`.** Branch first, commit there, leave `main` alone.
- **Design docs commit straight to `main`.** They touch no code, and every agent needs to read
  them.
- Verify with `Tools/build-check.ps1` before calling work done.

## Update posts (Discord)

When a group of tasks finishes, post a summary to the Discord webhook in the
`EMBERFALL_WEBHOOK` environment variable, as an **embed**.

**Never write the webhook URL into any file in this repo — only ever the variable name.**

What goes in it:

- **title** — a short, plain-language headline of what changed.
- **description** — the summary, for someone who was not here and does not read code. Say what a
  **player would notice**, not what files changed. Two to four sentences. No file paths, no
  commit hashes, no jargon. If nothing player-visible changed, say exactly that, in one line,
  instead.
- **footer** — the branch name.
- **timestamp** — UTC, ISO 8601.
- **color** — `0xE2571E` (ember orange) for **feature work**: anything a player could see or
  play. `0x6E7681` (muted grey) for **docs and maintenance**: documentation, refactors, tooling,
  build fixes, dependency bumps — work with no player-visible effect. When a batch is both, it
  is feature work; the grey is for batches that are *only* upkeep.

No `fields` listing files or commits. The commit feed already covers that.

### The command (PowerShell — verified, returns `HTTP 204`)

```powershell
$title = 'Short plain-language headline of what changed'
$desc = @'
Two to four sentences on what a player would notice. Quotes " and & are fine.
'@
$branch = (git rev-parse --abbrev-ref HEAD)
$payload = @{ embeds = @( @{
    title       = $title
    description = $desc
    color       = 0xE2571E   # feature work; 0x6E7681 for docs / maintenance
    footer      = @{ text = "branch: $branch" }
    timestamp   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
} ) } | ConvertTo-Json -Depth 6 -Compress
$tmp = Join-Path $env:TEMP 'emberfall-update.json'
[IO.File]::WriteAllText($tmp, $payload, [Text.UTF8Encoding]::new($false))
curl.exe -sS -o "$env:TEMP\emberfall-resp.txt" -w "HTTP %{http_code}`n" -X POST -H "Content-Type: application/json" --data-binary "@$tmp" "$env:EMBERFALL_WEBHOOK"
```

Success is **HTTP 204** with an empty body. Anything else means the post did not land.

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
- Pass the URL as `"$env:EMBERFALL_WEBHOOK"` and keep `-sS -o <file>` so the token never lands
  in console output or in the repo.
- If `$env:EMBERFALL_WEBHOOK` is empty, the shell predates the variable being set. Open a new
  shell, or rehydrate it for the session:
  `$env:EMBERFALL_WEBHOOK = [Environment]::GetEnvironmentVariable('EMBERFALL_WEBHOOK','User')`
