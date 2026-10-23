# Called from the end of build-check.ps1's Finish. Writes the merge marker the push guard
# (Tools/guard/guard.py, rule 3) looks for: `git rev-parse --git-path build-check-ok`.
# A FULL run (not -Quick, not -NoRun) that ends green writes "<HEAD tree hash> <UTC time>";
# any other run overwrites the file with FAIL (never deletes). Cannot change the build verdict.
param([int]$Code, [string]$Mode, [switch]$NoRun)
try {
    $root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $path = (& git -C $root rev-parse --git-path build-check-ok 2>$null)
    if (-not $path) { return }
    if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $root $path }
    $text = "FAIL"
    if ($Code -eq 0 -and $Mode -eq "full" -and -not $NoRun) {
        $tree = (& git -C $root rev-parse "HEAD^{tree}" 2>$null)
        if ($tree) { $text = $tree + " " + [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
    }
    Set-Content -LiteralPath $path -Value $text -Encoding ASCII
} catch {
    Write-Host ("[guard] merge marker not written: " + $_.Exception.Message) -ForegroundColor Yellow
}
