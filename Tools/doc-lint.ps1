# Documentation lint for Emberfall.
#
# Enforces the citation half of CLAUDE.md's Ground rules against every *.md in the repo. Those
# rules exist because a doc that points at a line number, a hash or a branch is telling a reader
# where to go looking instead of telling them what is true, and all three rot silently: the line
# moves, the branch is deleted, the hash survives a rebase into meaninglessness. Nothing here
# reads C# or needs a compiler, so it runs anywhere git and PowerShell do.
#
#   pwsh Tools/doc-lint.ps1                  # lint, exit 1 on any NEW violation
#   pwsh Tools/doc-lint.ps1 -WriteBaseline   # re-record the baseline from what is there now
#
# THREE CLASSES:
#
#   line-number  a C# filename followed by a colon and digits, e.g. Foo.cs:120. Always a
#                violation; there is no version of this that ages well.
#
#   dead-hash    a word-bounded hex run of 7+ characters that does NOT resolve to a commit in
#                this repository. A hash that still resolves is left alone - it is at least
#                checkable - while one that does not is a dangling pointer and is reported.
#                Note the 7-character floor also keeps six-digit colour values (F2C14E, 0xE2571E)
#                out of the match by construction rather than by a special case.
#
#   dead-branch  a token carrying a known branch prefix that has no matching ref on origin.
#                A live branch passes. This is why DESIGN-BUILD-ORDER.md's Now / Next needs no
#                exception here: CLAUDE.md permits a branch name there only while the branch is
#                live, which is exactly the condition this check already tests. Its live pointers
#                pass and its dead ones fail, with no per-file carve-out to keep in sync.
#
# THE BASELINE. Tools/doc-lint-baseline.txt holds every violation that existed when this was
# introduced. Baselined entries are printed but do not fail the run; anything not in it fails with
# exit 1. Both counts print on every run so the debt is visible and its shrinkage is observable,
# rather than the whole thing being switched off until someone has time to fix it.
#
# REGENERATE IT AGAINST ORIGIN'S VIEW, NOT A LOADED LOCAL CLONE. Both the hash and the branch check
# ask git what exists, so their verdict depends on which refs the running clone happens to hold. A
# machine carrying unpushed branches resolves hashes that are dead for everybody else, and
# -WriteBaseline there records fewer violations than CI will find. That is not hypothetical: the
# first baseline written for this file was three entries short for exactly that reason, and CI
# caught it on the first push. If a run fails on a hash you can resolve locally, check whether the
# branch holding it has actually been pushed before assuming the linter is wrong.
#
# THERE IS NO CARVE-OUT FOR HASHES ANYWHERE, including DESIGN-BUILD-ORDER.md's Now / Next. What
# limits the check is not a file exception but the resolve test above: it reports a hash only once
# that hash has stopped resolving.
#
# Stated plainly, because it is the gap between this tool and the rule it serves: every hash in
# Now / Next currently resolves, so none of them is reported and none is in the baseline. This
# lints for DANGLING pointers, which is strictly weaker than CLAUDE.md's "cite observable file
# content, never commit hashes". Closing that gap means reporting every hash regardless of whether
# it resolves - at which point Now / Next's hashes do land in the baseline and come out when that
# section is next rewritten, which is the same moment its entries should stop naming commits and
# start describing what the files now show. That is a deliberate decision for a human, not
# something to tighten quietly.

[CmdletBinding()]
param(
    [string]$Root,
    [switch]$WriteBaseline
)

$ErrorActionPreference = 'Stop'

# Resolved in the BODY, not as a param default. Windows PowerShell 5.1 does not populate
# $PSScriptRoot while it is binding parameters, so a default of Split-Path -Parent $PSScriptRoot
# fails with "cannot bind argument ... empty string" the moment the script is run with -File.
# build-check.ps1 next door resolves its own root the same way for the same reason.
if (-not $Root) { $Root = Split-Path -Parent $PSScriptRoot }
$BaselinePath = Join-Path $PSScriptRoot 'doc-lint-baseline.txt'

# Git probes, each wrapped so a native command's stderr cannot become a terminating error.
# PowerShell 5.1 turns a redirected native stderr line into an ErrorRecord, and under
# ErrorActionPreference = Stop that aborts the script - so the preference is relaxed inside the
# function scope only, and the verdict is taken from the exit code.
$script:CommitCache = @{}
function Test-CommitResolves([string]$Hash) {
    if ($script:CommitCache.ContainsKey($Hash)) { return $script:CommitCache[$Hash] }
    $ErrorActionPreference = 'Continue'
    $null = & git -C $Root cat-file -e "$Hash^{commit}" 2>&1
    $ok = ($LASTEXITCODE -eq 0)
    $script:CommitCache[$Hash] = $ok
    return $ok
}

$script:BranchCache = @{}
function Test-BranchLive([string]$Name) {
    if ($script:BranchCache.ContainsKey($Name)) { return $script:BranchCache[$Name] }
    $ErrorActionPreference = 'Continue'
    $null = & git -C $Root rev-parse --verify --quiet "origin/$Name" 2>&1
    $ok = ($LASTEXITCODE -eq 0)
    $script:BranchCache[$Name] = $ok
    return $ok
}

# ---------------------------------------------------------------- patterns
#
# Every pattern is bounded on both sides so it cannot match inside a longer alphanumeric run. The
# hash pattern additionally refuses a leading '#' so an eight-digit RGBA colour written as a CSS
# value is not mistaken for a short hash.
$ReLineNumber = '(?<![A-Za-z0-9_])([A-Za-z0-9_]+\.cs:[0-9]+)'
$ReHash       = '(?<![0-9A-Za-z#])([0-9a-fA-F]{7,40})(?![0-9A-Za-z])'
$ReBranch     = '(?<![A-Za-z0-9_/.-])((?:feat|fix|chore|docs|wip|claude|retune|balance)/[A-Za-z0-9._/-]+|build-order-[A-Za-z0-9._-]+)'

# Branch tokens never legitimately end in sentence punctuation, and markdown puts plenty there.
function Get-TrimmedToken([string]$Token) {
    return ($Token -replace '[.,;:)\]`''"]+$', '')
}

# ---------------------------------------------------------------- scan
#
# TRACKED FILES ONLY, via git ls-files rather than a filesystem walk. The working tree holds 577
# markdown files and the repository holds 33: the rest are nested git worktrees under .claude,
# package caches and other scratch that happens to live inside the folder. Linting those would
# triple the debt with copies of the same documents and make the baseline shift whenever a
# worktree is created or removed. It also matches what CI sees, since a checkout only ever
# contains tracked files.
$ErrorActionPreference = 'Continue'
$tracked = @(& git -C $Root ls-files '*.md' 2>&1 |
             Where-Object { $_ -is [string] -and $_.Trim().Length -gt 0 })
$ErrorActionPreference = 'Stop'

$mdFiles = @()
foreach ($t in $tracked) {
    $full = Join-Path $Root ($t -replace '/', [char]92)
    if (Test-Path -LiteralPath $full) { $mdFiles += (Get-Item -LiteralPath $full) }
}
$mdFiles = @($mdFiles | Sort-Object FullName)

$found = New-Object System.Collections.Generic.List[object]

foreach ($file in $mdFiles) {
    $rel = $file.FullName.Substring($Root.Length + 1) -replace '\\', '/'
    $lineNo = 0
    foreach ($line in (Get-Content -LiteralPath $file.FullName)) {
        $lineNo++

        foreach ($m in [regex]::Matches($line, $ReLineNumber)) {
            $found.Add([pscustomobject]@{ File = $rel; Line = $lineNo; Class = 'line-number'; Token = $m.Groups[1].Value })
        }

        foreach ($m in [regex]::Matches($line, $ReHash)) {
            $tok = $m.Groups[1].Value
            # An all-decimal run is a number, not a hash. Without this the Unix timestamps in
            # DESIGN-BUILD-ORDER.md's event-window table (1788220800 and three siblings) are
            # perfectly valid hex, resolve to nothing, and get reported as dead commits. Requiring
            # at least one a-f costs the ability to catch a hash that happens to be all digits,
            # which is about a 4% shape for a seven-character abbreviation and rarer the longer it
            # gets - a good trade against flagging every date and id in the docs.
            if ($tok -notmatch '[a-fA-F]') { continue }
            if (Test-CommitResolves $tok) { continue }
            $found.Add([pscustomobject]@{ File = $rel; Line = $lineNo; Class = 'dead-hash'; Token = $tok })
        }

        foreach ($m in [regex]::Matches($line, $ReBranch)) {
            $tok = Get-TrimmedToken $m.Groups[1].Value
            if ($tok -eq 'main' -or $tok -eq 'HEAD') { continue }
            if (Test-BranchLive $tok) { continue }
            $found.Add([pscustomobject]@{ File = $rel; Line = $lineNo; Class = 'dead-branch'; Token = $tok })
        }
    }
}

# One normalised entry per file+token pair. Deduped, so a token repeated within a file is one
# piece of debt rather than several, and the baseline does not churn when a paragraph is reflowed.
function Get-Entry($v) { return ($v.File + '|' + $v.Token) }

$entries = @($found | ForEach-Object { Get-Entry $_ } | Sort-Object -Unique)

if ($WriteBaseline) {
    # WriteAllLines with an explicit BOM-less UTF8Encoding, not Set-Content -Encoding UTF8.
    # PowerShell 5.1's UTF8 means UTF8-with-BOM, and those three bytes land on the FIRST entry.
    # Every later read then sees a first line that matches nothing, so one real violation silently
    # reports as new forever. The same BOM trap is documented for the Discord webhook in CLAUDE.md.
    [IO.File]::WriteAllLines($BaselinePath, $entries, [Text.UTF8Encoding]::new($false))
    Write-Host ("doc-lint: wrote " + $entries.Count + " entries to " + (Split-Path $BaselinePath -Leaf))
    exit 0
}

$baseline = @()
if (Test-Path $BaselinePath) {
    # Strip a BOM on read as well as refusing to write one: a baseline edited by hand, or by a
    # tool that is less careful, must not fail closed on an invisible character.
    $baseline = @(Get-Content -LiteralPath $BaselinePath |
                  ForEach-Object { $_ -replace "^\uFEFF", "" } |
                  Where-Object { $_.Trim().Length -gt 0 })
}
$baselineSet = @{}
foreach ($b in $baseline) { $baselineSet[$b] = $true }

$new = New-Object System.Collections.Generic.List[object]
$old = New-Object System.Collections.Generic.List[object]
$seen = @{}
foreach ($v in $found) {
    $entry = Get-Entry $v
    if ($seen.ContainsKey($entry)) { continue }
    $seen[$entry] = $true
    if ($baselineSet.ContainsKey($entry)) { $old.Add($v) } else { $new.Add($v) }
}

Write-Host ""
Write-Host "[doc-lint] $($mdFiles.Count) markdown files scanned"

if ($new.Count -gt 0) {
    Write-Host ""
    Write-Host "  NEW violations (these fail the run):" -ForegroundColor Red
    foreach ($v in ($new | Sort-Object File, Line)) {
        Write-Host ("    {0}:{1}  {2}  {3}" -f $v.File, $v.Line, $v.Class.PadRight(11), $v.Token)
    }
}

if ($old.Count -gt 0) {
    Write-Host ""
    Write-Host "  baselined (known debt, not failing):" -ForegroundColor DarkYellow
    foreach ($v in ($old | Sort-Object File, Line)) {
        Write-Host ("    {0}:{1}  {2}  {3}" -f $v.File, $v.Line, $v.Class.PadRight(11), $v.Token)
    }
}

Write-Host ""
Write-Host ("[doc-lint] baselined: " + $old.Count + "   new: " + $new.Count)

if ($new.Count -gt 0) {
    Write-Host "[doc-lint] FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "[doc-lint] ok" -ForegroundColor Green
exit 0
