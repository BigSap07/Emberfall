# The C# compiler and the way to run what it builds, for Tools/build-check.ps1 and
# Tools/solve-recommended-power.ps1. Dot-sourced, not run.
#
# WINDOWS IS UNCHANGED: the .NET Framework csc.exe at its fixed path, called with exactly the arguments the
# scripts always passed, and the exes it builds run directly. Nothing below takes a different branch there.
#
# EVERYWHERE ELSE (Linux, macOS, ubuntu-latest in CI) there is no csc.exe, so the fallback is the .NET SDK's
# own Roslyn compiler (sdk/<version>/Roslyn/bincore/csc.dll), run through `dotnet exec`:
#   - the SAME source lists, in the same order (see Get-SourceFiles), and the same targets and defines;
#   - -langversion:5, so anything the Windows csc (C# 5) would reject is rejected here too;
#   - -noconfig -nostdlib against the Microsoft.NETCore.App reference pack, because .NET has no mscorlib;
#   - the exe is a .NET assembly, run as `dotnet <exe>` with a <exe-name>.runtimeconfig.json beside it.
#     That file also carries the server-GC setting that <exe>.config carries for .NET Framework.
#
# dotnet is found on PATH, then at $DOTNET_ROOT, then at ~/.dotnet. The newest SDK and reference pack win.

$script:OnWindows = ($env:OS -eq 'Windows_NT')

# The temp folder: $env:TEMP on Windows, as always; it is unset on Linux and macOS.
function Get-TempDir {
    if ($env:TEMP) { return $env:TEMP }
    return [IO.Path]::GetTempPath()
}

# Puts a Get-ChildItem file list into the order Windows produces, as full paths. A no-op on Windows.
#
# NTFS hands back each folder's names sorted case-insensitively, and Get-ChildItem -Recurse emits a folder's
# files and then descends into its subfolders in that order. Linux and macOS hand names back in whatever order
# the filesystem stores them (ext4 hashes them), so the same Get-ChildItem gives a different order there, and
# the order is not cosmetic: partial classes initialise their statics in the order csc is handed the files,
# and the compile cache keys on it. So off Windows the list is re-sorted into the NTFS walk: compared path
# component by component, OrdinalIgnoreCase (NTFS compares upper-cased UTF-16, which is what OrdinalIgnoreCase
# does), with a folder's own files before its subfolders.
#
# The scripts keep their Get-ChildItem expressions and only append | Sort-SourceOrder, because the harness's
# CheckSolverAndGateCompileOneList reads those expressions out of both scripts to prove the lists are equal.
function Sort-SourceOrder {
    $paths = @($input | ForEach-Object { if ($_ -is [IO.FileSystemInfo]) { $_.FullName } else { "$_" } })
    if ($script:OnWindows) { return $paths }
    $sep = [char]1
    $keys = [string[]]@($paths | ForEach-Object {
        $parts = $_.Split([char[]]@('/', '\'))
        $k = for ($i = 0; $i -lt $parts.Length; $i++) {
            if ($i -eq $parts.Length - 1) { '0' + $parts[$i] } else { '1' + $parts[$i] }
        }
        $k -join $sep
    })
    $sorted = [string[]]$paths.Clone()
    [Array]::Sort($keys, $sorted, [StringComparer]::OrdinalIgnoreCase)
    return $sorted
}

# Returns @{ Kind = 'netfx'|'dotnet'; Path = <the compiler file, for the compile cache key>; ... } or $null.
function Get-CSharpCompiler {
    if ($script:OnWindows) {
        $csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
        if (-not (Test-Path $csc)) { return $null }
        return @{ Kind = 'netfx'; Path = $csc; Describe = $csc }
    }
    $dotnet = $null
    $cmd = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { $dotnet = $cmd.Source }
    if (-not $dotnet) {
        foreach ($d in @($env:DOTNET_ROOT, (Join-Path $HOME '.dotnet'))) {
            if ($d -and (Test-Path (Join-Path $d 'dotnet'))) { $dotnet = Join-Path $d 'dotnet'; break }
        }
    }
    if (-not $dotnet) { return $null }
    # dotnet on PATH is often a symlink (/usr/bin/dotnet -> /usr/lib/dotnet/dotnet); the SDK sits beside the target.
    $item = Get-Item -LiteralPath $dotnet
    if ($item.LinkType -eq 'SymbolicLink' -and $item.Target) {
        $target = @($item.Target)[0]
        if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path (Split-Path -Parent $dotnet) $target }
        $dotnet = (Resolve-Path -LiteralPath $target).ProviderPath
    }
    $home_ = Split-Path -Parent $dotnet
    $byVersion = { $v = $null; if ([Version]::TryParse(($_.Name -replace '-.*$', ''), [ref]$v)) { $v } else { [Version]'0.0' } }
    $sdk = Get-ChildItem (Join-Path $home_ 'sdk') -Directory -ErrorAction SilentlyContinue |
           Where-Object { Test-Path (Join-Path $_.FullName 'Roslyn/bincore/csc.dll') } |
           Sort-Object $byVersion | Select-Object -Last 1
    $pack = Get-ChildItem (Join-Path $home_ 'packs/Microsoft.NETCore.App.Ref') -Directory -ErrorAction SilentlyContinue |
            Sort-Object $byVersion | Select-Object -Last 1
    if (-not $sdk -or -not $pack) { return $null }
    $refDir = Get-ChildItem (Join-Path $pack.FullName 'ref') -Directory | Sort-Object Name | Select-Object -Last 1
    $refs = @(Get-ChildItem $refDir.FullName -Filter *.dll | Sort-Object Name | ForEach-Object { '-r:' + $_.FullName })
    $cscDll = Join-Path $sdk.FullName 'Roslyn/bincore/csc.dll'
    return @{
        Kind = 'dotnet'; Path = $cscDll; Dotnet = $dotnet; References = $refs; RuntimeVersion = $pack.Name
        Describe = ("$cscDll (.NET SDK " + $sdk.Name + ", -langversion:5, reference pack " + $pack.Name + ")")
    }
}

# Runs the compiler with the arguments the Windows call has always used; returns its output, sets $LASTEXITCODE.
function Invoke-CSharpCompiler($compiler, [string[]]$arguments) {
    if ($compiler.Kind -eq 'netfx') { return (& $compiler.Path @arguments 2>&1) }
    $all = @('exec', $compiler.Path, '-noconfig', '-nostdlib', '-langversion:5') + $compiler.References + $arguments
    return (& $compiler.Dotnet @all 2>&1)
}

# Beside an exe the dotnet fallback built: how `dotnet <exe>` finds its runtime, with server GC and no
# concurrent GC, the settings Write-HarnessConfig gives the .NET Framework build. Written only when it differs.
function Write-RuntimeConfig($compiler, [string]$exePath) {
    if ($compiler.Kind -ne 'dotnet') { return }
    $parts = $compiler.RuntimeVersion.Split('.')
    $text = ("{`n  `"runtimeOptions`": {`n    `"tfm`": `"net" + $parts[0] + "." + $parts[1] + "`",`n" +
             "    `"rollForward`": `"LatestMajor`",`n" +
             "    `"framework`": { `"name`": `"Microsoft.NETCore.App`", `"version`": `"" + $compiler.RuntimeVersion + "`" },`n" +
             "    `"configProperties`": { `"System.GC.Server`": true, `"System.GC.Concurrent`": false }`n  }`n}`n")
    $path = [IO.Path]::ChangeExtension($exePath, '.runtimeconfig.json')
    if ((Test-Path -LiteralPath $path) -and ([IO.File]::ReadAllText($path) -eq $text)) { return }
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
}

# Runs an exe the compiler built: directly on Windows, through `dotnet` elsewhere. Output is the pipeline.
function Invoke-BuiltExe($compiler, [string]$exePath, [string[]]$arguments = @()) {
    if ($compiler.Kind -eq 'netfx') { & $exePath @arguments }
    else { & $compiler.Dotnet $exePath @arguments }
}
