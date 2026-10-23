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
