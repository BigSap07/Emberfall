# The compile cache for Tools/build-check.ps1: skip csc when nothing it would read has changed.
#
# Dot-sourced, not run. A compile is skipped only when a stamp beside the output says the output
# was built from EXACTLY these inputs, and it is keyed on all of them:
#
#   - the bytes of every source file (SHA256 of each), and their ORDER and full paths, because
#     partial classes initialise their statics in the order csc is handed the files;
#   - the compiler: its path, size, last-write time and file version;
#   - the target, the defines and the output path;
#   - the output itself: its length and last-write time, so an output replaced behind the stamp's
#     back (the solver rebuilds the same DevHarness.exe) never counts as the one that was stamped.
#
# Any one of them changing is a miss, and a miss is just the compile that always ran. A source
# file that cannot be read is a miss too, so csc reports the real error. The compiler's filtered
# output (its warnings) is stored in the stamp and replayed on a hit, so a cached run prints what
# a compiled one does. The stamp is a gitignored file beside the output, under Tools/DevHarness/bin.
#
# EMBERFALL_NO_COMPILE_CACHE=1 turns it off: every compile runs.

$script:CompileCacheFormat = "emberfall-compile-cache-1"

function Get-CompileKey([string[]]$sources, [string]$target, [string]$outputPath, [string]$defines, [string]$csc) {
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        $hex = { param([byte[]]$b) ([BitConverter]::ToString($sha.ComputeHash($b))).Replace("-", "") }
        $info = Get-Item -LiteralPath $csc
        $sb = New-Object Text.StringBuilder
        [void]$sb.AppendLine($script:CompileCacheFormat)
        [void]$sb.AppendLine("csc|" + $info.FullName + "|" + $info.Length + "|" + $info.LastWriteTimeUtc.Ticks + "|" + $info.VersionInfo.FileVersion)
        [void]$sb.AppendLine("target|" + $target + "|out|" + $outputPath + "|defines|" + $defines)
        foreach ($s in $sources) {
            [void]$sb.AppendLine($s + "|" + (& $hex ([IO.File]::ReadAllBytes($s))))
        }
        return (& $hex ([Text.Encoding]::UTF8.GetBytes($sb.ToString())))
    } catch {
        return $null
    }
}

function Get-OutputStamp([string]$outputPath) {
    $o = Get-Item -LiteralPath $outputPath
    return ("out|" + $o.Length + "|" + $o.LastWriteTimeUtc.Ticks)
}

# Returns the stored compiler lines when the output is current for this key, else $null.
function Test-CompileCache([string]$key, [string]$outputPath) {
    if ($env:EMBERFALL_NO_COMPILE_CACHE -eq "1") { return $null }
    if (-not $key) { return $null }
    $stampPath = $outputPath + ".stamp"
    if (-not (Test-Path -LiteralPath $outputPath)) { return $null }
    if (-not (Test-Path -LiteralPath $stampPath)) { return $null }
    $lines = @([IO.File]::ReadAllLines($stampPath))
    if ($lines.Count -lt 2) { return $null }
    if ($lines[0] -ne $key) { return $null }
    if ($lines[1] -ne (Get-OutputStamp $outputPath)) { return $null }
    if ($lines.Count -gt 2) { return ,@($lines[2..($lines.Count - 1)]) }
    return ,@()
}

function Write-CompileCache([string]$key, [string]$outputPath, [string[]]$compilerLines) {
    $stampPath = $outputPath + ".stamp"
    if (-not $key) { [IO.File]::WriteAllLines($stampPath, [string[]]@("no key"), [Text.UTF8Encoding]::new($false)); return }
    $all = @($key, (Get-OutputStamp $outputPath)) + @($compilerLines)
    [IO.File]::WriteAllLines($stampPath, [string[]]$all, [Text.UTF8Encoding]::new($false))
}

# For a script that rebuilds an output it does not cache (the solver): the stamp then matches nothing.
function Reset-CompileCache([string]$outputPath) {
    [IO.File]::WriteAllLines($outputPath + ".stamp", [string[]]@("rebuilt outside the cache"), [Text.UTF8Encoding]::new($false))
}
