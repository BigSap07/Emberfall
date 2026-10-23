# Builds an Android playtest APK: the file a friend sideloads. The Android twin of Tools/build-windows.ps1.
#
#   powershell -ExecutionPolicy Bypass -File Tools/build-android.ps1                 # release, offline
#   powershell -ExecutionPolicy Bypass -File Tools/build-android.ps1 -Cloud          # release, cloud
#   ... -Defines "EMBERFALL_TESTSTORE;EMBERFALL_PLAYLOG" -OutputRoot D:\Somewhere -UnityExe <path>
#
# It wraps EmberfallBuild.BuildAndroidPlaytest and invents no second build path. What it adds is the same
# discipline build-windows.ps1 brings to the Windows zip:
#
#   - it REFUSES A DIRTY TREE, before and after: the player carries the short hash, and a hash that does not
#     describe the contents is worse than none;
#   - it OWNS THE CLOUD CONFIG exactly as build-windows.ps1 does (same stash folder, so either script repairs a
#     run the other was killed in): offline is the default and moves Assets/Resources/Config/supabase.json out
#     for the build and back afterwards; -Cloud keeps the file untouched. The script never prints it;
#   - -Defines "A;B" are extra scripting defines for this one APK (EmberfallBuild.RequestedDefines), never
#     written to the project;
#   - the editor runs in batchmode with -buildTarget Android, so it never shows a switch-target dialog; the
#     release backend (IL2CPP, ARM64) is applied by EmberfallBuild over ApplyPlayerSettings. The Android module
#     (SDK, NDK, OpenJDK) is checked before Unity starts and the install steps are printed when it is not there;
#   - ProjectSettings.asset is saved before and put back if the editor rewrote it, so the tree check after the
#     build compares like with like. Nothing is deleted;
#   - it checks the APK it got (IL2CPP arm64 library, no other ABI) and writes a .sizes.txt beside it. The editor
#     log is kept beside the output.
#
# The debug keystore signs the APK, which is fine for a sideloaded test build and not for a store.
# Close the editor on this project first.

param(
    [string]$OutputRoot = "",
    [string]$UnityExe = "",
    [switch]$Cloud,
    [string]$Defines = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "unity-editor.ps1")

$network = if ($Cloud) { "cloud" } else { "offline" }
$variant = "release-$network"
Write-Host "MODE: android $variant" -ForegroundColor Cyan

$definesList = @()
if ($Defines) {
    $definesList = @($Defines -split '[;,]' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($name in $definesList) {
        if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "BUILD REFUSED: '$name' is not a scripting define name." }
    }
}
$definesText = ($definesList | Select-Object -Unique) -join ";"

function Fail([string]$message) { throw "BUILD REFUSED: $message" }

function Invoke-Git {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & git -C $root @args 2>$null
        $script:GitExit = $LASTEXITCODE
        return $output
    } finally {
        $ErrorActionPreference = $previous
    }
}

# A flagged file is a problem only when its content, cleaned the way git add would clean it, is not HEAD's blob
# (a line-ending or stat-only difference is not). See the same function in build-windows.ps1.
function Test-SameAsHead([string]$path) {
    $full = Join-Path $root ($path -replace '/', [char]92)
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { return $false }
    $headBlob = "$(Invoke-Git rev-parse --verify --quiet "HEAD:$path")".Trim()
    if ($script:GitExit -ne 0 -or -not $headBlob) { return $false }
    $diskBlob = "$(Invoke-Git hash-object --path $path -- $full)".Trim()
    if ($script:GitExit -ne 0 -or -not $diskBlob) { return $false }
    return ($diskBlob -eq $headBlob)
}

function Get-TreeProblems {
    $problems = @()
    $status = @(Invoke-Git status --porcelain --untracked-files=all)
    if ($script:GitExit -ne 0) { Fail "git status failed in $root." }
    foreach ($line in $status) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $code = $line.Substring(0, 2)
        $path = $line.Substring(3).Trim('"')
        if ($code -eq "??") {
            if ($path -match '^(Assets|Packages|ProjectSettings)/') { $problems += "untracked  $path" }
        } elseif ($code -match '^[ MT][ MT]$' -and (Test-SameAsHead $path)) {
            continue
        } else {
            $problems += "changed    $path"
        }
    }
    return $problems
}

# Whether a stream holds an ASCII string, read in chunks (resources.assets runs to hundreds of megabytes).
# Chunks overlap by the needle's length so a match across a boundary is still found.
function Test-StreamContains([IO.Stream]$stream, [string]$needle) {
    $chunkSize = 16MB
    $buffer = New-Object byte[] ($chunkSize + $needle.Length)
    $carry = 0
    while ($true) {
        $read = $stream.Read($buffer, $carry, $chunkSize)
        if ($read -le 0) { return $false }
        $count = $carry + $read
        if ([Text.Encoding]::ASCII.GetString($buffer, 0, $count).Contains($needle)) { return $true }
        $carry = [Math]::Min($needle.Length - 1, $count)
        [Array]::Copy($buffer, $count - $carry, $buffer, 0, $carry)
    }
}

# The same search over one APK entry, which has to be opened again per needle: a zip entry stream cannot rewind.
function Test-EntryContains($entry, [string]$needle) {
    $stream = $entry.Open()
    try { return (Test-StreamContains $stream $needle) } finally { $stream.Dispose() }
}

# ------------------------------------------------------------------------------ the cloud config
$config = Join-Path $root "Assets\Resources\Config\supabase.json"
$configMeta = "$config.meta"
$stashDir = Join-Path $env:LOCALAPPDATA ("Emberfall-build\cloud-stash\" + (($root -replace '[:\\/]+', '-').Trim('-')))
$stashed = Join-Path $stashDir "supabase.json"
$stashedMeta = Join-Path $stashDir "supabase.json.meta"
$script:ConfigMoved = $false

function Restore-CloudConfig {
    if (-not (Test-Path $stashed)) { return $true }
    if (Test-Path $config) {
        Write-Host "  WARNING  $config exists AND a copy is stashed in $stashDir. Left both alone;" -ForegroundColor Yellow
        Write-Host "           compare them and keep the one you want." -ForegroundColor Yellow
        return $false
    }
    Move-Item $stashed $config
    if ((Test-Path $stashedMeta) -and -not (Test-Path $configMeta)) { Move-Item $stashedMeta $configMeta }
    return $true
}

function Stash-CloudConfig {
    New-Item -ItemType Directory -Force $stashDir | Out-Null
    $script:ConfigMoved = $true
    Move-Item $config $stashed
    if (Test-Path $configMeta) { Move-Item $configMeta $stashedMeta -Force }
}

$exitCode = 1
$script:UnityProcess = $null
$script:ApkPath = $null
$succeeded = $false
$settingsFile = Join-Path $root "ProjectSettings\ProjectSettings.asset"
$settingsBackup = $null

try {
    if (Test-Path $stashed) {
        if (-not (Restore-CloudConfig)) { Fail "the cloud config is both in the project and in the stash." }
        Write-Host "  restored the cloud config a previous run moved out and never put back" -ForegroundColor Yellow
    }

    Write-Host "Emberfall Android playtest build"
    Write-Host "  project  $root"

    $null = Invoke-Git rev-parse --git-dir
    if ($script:GitExit -ne 0) { Fail "$root is not a git working tree." }
    foreach ($marker in @("MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply")) {
        $gitPath = "$(Invoke-Git rev-parse --git-path $marker)".Trim()
        if (-not $gitPath) { continue }
        if (-not [IO.Path]::IsPathRooted($gitPath)) { $gitPath = Join-Path $root $gitPath }
        if (Test-Path $gitPath) {
            $what = if ($marker -like "rebase-*") { "rebase" } else { $marker }
            Fail "a $what is in progress ($gitPath exists). Finish or abort it first."
        }
    }

    $before = @(Get-TreeProblems)
    if ($before.Count -gt 0) {
        Write-Host ""
        Write-Host "The working tree does not match HEAD:" -ForegroundColor Red
        $before | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Fail "commit or remove these first. A build stamped with HEAD's hash would not be the code HEAD describes."
    }

    $ignored = @(Invoke-Git status --porcelain --ignored=matching --untracked-files=all -- Assets |
                 Where-Object { $_ -like "!! *" } | ForEach-Object { $_.Substring(3).Trim('"') })
    foreach ($path in $ignored) {
        if ($path -notmatch '/Resources/') { continue }
        if ($path -match '^Assets/Resources/(Config/supabase\.json(\.meta)?|BuildStamp\.txt(\.meta)?)$') { continue }
        Fail "$path is gitignored but sits in a Resources folder, so it would ship without being in the commit."
    }

    # The cloud project's address, read before anything moves the file, so the APK can be searched for it.
    $cloudHost = ""
    if (Test-Path $config) {
        $url = "$((Get-Content $config -Raw | ConvertFrom-Json).url)"
        if ($url -match '^https?://([^/]+)') { $cloudHost = $Matches[1] }
    }
    if ($Cloud) {
        if (-not (Test-Path $config)) { Fail "-Cloud needs Assets/Resources/Config/supabase.json, and it is not there." }
        if (-not $cloudHost) { Fail "Assets/Resources/Config/supabase.json has no usable url." }
        $cloudLine = "ON: the app signs in to $cloudHost"
    } else {
        $cloudLine = "OFF: no account service, nothing syncs anywhere"
    }

    $hash = (Invoke-Git rev-parse --short HEAD)
    if ($script:GitExit -ne 0 -or -not $hash) { Fail "could not read HEAD's hash." }
    $hash = "$hash".Trim()
    $branch = "$(Invoke-Git rev-parse --abbrev-ref HEAD)".Trim()
    Write-Host "  commit   $hash on $branch"
    Write-Host "  cloud    $cloudLine"
    if ($definesText) { Write-Host "  defines  $definesText" }

    $remotes = @(Invoke-Git branch -r --contains HEAD)
    if ($remotes.Count -eq 0) {
        Write-Host "  WARNING  $hash is on no remote branch. Push it before this build leaves the machine." -ForegroundColor Yellow
    }

    # ------------------------------------------------------------------------------------ unity
    try { $UnityExe = Find-UnityEditor $root $UnityExe } catch { Fail $_.Exception.Message }
    Write-Host "  unity    $UnityExe"
    $gaps = @(Get-AndroidModuleGaps $UnityExe)
    if ($gaps.Count -gt 0) {
        Get-AndroidInstallSteps $UnityExe $gaps | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        Fail "the Android module is not complete."
    }

    $lock = Join-Path $root "Temp\UnityLockfile"
    if (Test-Path $lock) {
        try { $handle = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $handle.Close() }
        catch { Fail "an editor has this project open. Close it; Unity will not build a project another editor holds." }
    }

    if (-not $OutputRoot) { $OutputRoot = Join-Path (Split-Path -Parent $root) "Emberfall-Builds" }
    $OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
    if ($OutputRoot.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        Fail "$OutputRoot is inside the working tree. Pick a folder outside it."
    }
    New-Item -ItemType Directory -Force $OutputRoot | Out-Null
    Write-Host "  output   $OutputRoot"

    $buildSource = Get-Content (Join-Path $root "Assets\Editor\EmberfallBuild.cs") -Raw
    $versionMatch = [regex]::Match($buildSource, 'public const string Version = "([^"]+)"')
    if (-not $versionMatch.Success) { Fail "could not read EmberfallBuild.Version." }
    $version = $versionMatch.Groups[1].Value
    $apkName = "Emberfall-$version-$hash-$variant-android"
    $apk = Join-Path $OutputRoot "$apkName.apk"
    # Nothing is deleted: an APK of this commit and variant that is already there stops the run. The path is
    # remembered only after this check, so the cleanup below can never touch a build this run did not make.
    foreach ($existing in @($apk, (Join-Path $OutputRoot "$apkName.sizes.txt"))) {
        if (Test-Path $existing) { Fail "$existing already exists. Pass a different -OutputRoot; this script never overwrites or deletes a build." }
    }
    $script:ApkPath = $apk

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $log = Join-Path $OutputRoot "build-android-$hash-$variant-$stamp.log"
    $unityArgs = @(
        "-quit", "-batchmode",
        "-projectPath", "`"$root`"",
        "-buildTarget", "Android",
        "-executeMethod", "Emberfall.EditorTools.EmberfallBuild.BuildAndroidPlaytest",
        "-emberfallOutput", "`"$OutputRoot`"",
        "-emberfallCommit", $hash,
        "-emberfallVariant", $variant,
        "-logFile", "`"$log`""
    )
    if ($definesText) { $unityArgs += @("-emberfallDefines", "`"$definesText`"") }

    # Saved before Unity starts, put back if the editor rewrote it (see the header).
    if (Test-Path $settingsFile) { $settingsBackup = [IO.File]::ReadAllBytes($settingsFile) }

    if (-not $Cloud -and (Test-Path $config)) {
        Stash-CloudConfig
        Write-Host "  moved the cloud config out for this build; it goes back when the run ends"
    }

    Write-Host ""
    Write-Host "Building. The first Android build is slow (IL2CPP); the editor log is $log"
    $started = Get-Date
    $script:UnityProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
    $null = $script:UnityProcess.Handle
    $script:UnityProcess.WaitForExit()
    $minutes = [Math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    if ($script:UnityProcess.ExitCode -ne 0) {
        Write-Host ""
        if (Test-Path $log) {
            Write-Host "Last lines of the editor log:" -ForegroundColor Red
            Get-Content $log -Tail 40 | ForEach-Object { Write-Host "  $_" }
        }
        Fail "Unity exited with code $($script:UnityProcess.ExitCode) after $minutes min."
    }

    if ($script:ConfigMoved) {
        if (-not (Restore-CloudConfig)) { Fail "could not put the cloud config back." }
        $script:ConfigMoved = $false
    }

    if ($settingsBackup -ne $null -and (Test-Path $settingsFile)) {
        $nowBytes = [IO.File]::ReadAllBytes($settingsFile)
        $same = ($nowBytes.Length -eq $settingsBackup.Length)
        if ($same) { for ($i = 0; $i -lt $nowBytes.Length; $i++) { if ($nowBytes[$i] -ne $settingsBackup[$i]) { $same = $false; break } } }
        if (-not $same) {
            [IO.File]::WriteAllBytes($settingsFile, $settingsBackup)
            Write-Host "  put ProjectSettings.asset back as it was: the editor rewrote it on its way out" -ForegroundColor Yellow
        }
    }

    # ------------------------------------------------------------------------------------ the APK
    if (-not (Test-Path $apk)) { Fail "Unity reported success and $apk does not exist." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($apk)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName })
        if (-not ($names -contains "lib/arm64-v8a/libil2cpp.so")) { Fail "the APK holds no lib/arm64-v8a/libil2cpp.so, so it is not the IL2CPP ARM64 build." }
        $others = @($names | Where-Object { $_ -match '^lib/(armeabi-v7a|x86|x86_64)/' })
        if ($others.Count -gt 0) { Fail "the APK carries another ABI ($($others[0]))." }

        # resources.assets holds the stamp and the cloud config as raw text when the APK stores it plainly. If it
        # is compressed or moved the search finds nothing, which is reported as unchecked rather than as a pass.
        $entry = $zip.Entries | Where-Object { $_.FullName -eq "assets/bin/Data/resources.assets" } | Select-Object -First 1
        if ($entry) {
            if (-not (Test-EntryContains $entry $hash)) { Fail "the commit $hash is not packed into the APK, so it would say 'unstamped'." }
            if ($cloudHost) {
                $hasHost = Test-EntryContains $entry $cloudHost
                if ($Cloud -and -not $hasHost) { Fail "a cloud build without $cloudHost packed inside it." }
                if (-not $Cloud -and $hasHost) { Fail "an OFFLINE build with $cloudHost packed inside it. Do not hand it out." }
            }
            Write-Host "  checked  commit and cloud host in resources.assets"
        } else {
            Write-Host "  WARNING  assets/bin/Data/resources.assets is not stored plainly in the APK, so the packed commit and cloud host were not checked." -ForegroundColor Yellow
        }
    } finally {
        $zip.Dispose()
    }

    $after = @(Get-TreeProblems)
    if ($after.Count -gt 0) {
        Write-Host ""
        Write-Host "The build changed the working tree:" -ForegroundColor Red
        $after | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Fail "the APK was built from files HEAD does not hold. Commit whatever the editor wrote if it is meant to stay, then build again."
    }

    $label = "BUILD $version ($hash)"
    $apkMb = [Math]::Round((Get-Item $apk).Length / 1MB, 1)
    Write-Host ""
    Write-Host "BUILT Emberfall $version ($hash), android, $network, in $minutes min" -ForegroundColor Green
    Write-Host "  apk      $apk"
    Write-Host "  sizes    apk $apkMb MB"
    $sizes = Join-Path $OutputRoot "$apkName.sizes.txt"
    if (Test-Path $sizes) {
        Add-Content -Path $sizes -Value "apk file: $apkMb MB" -Encoding UTF8
        Get-Content $sizes | ForEach-Object { Write-Host "           $_" }
    } else {
        Write-Host "           (no size report: EmberfallBuild could not write $sizes, see the editor log)" -ForegroundColor Yellow
    }
    Write-Host "  log      $log"
    Write-Host "  cloud    $cloudLine"
    if ($definesText) { Write-Host "  defines  $definesText" }
    Write-Host "  the app shows '$label' in the Hub corner and at the foot of Settings."
    $succeeded = $true
    $exitCode = 0
}
catch {
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
}
finally {
    if ($script:UnityProcess -and -not $script:UnityProcess.HasExited) {
        Write-Host "Stopping the Unity build that was still running." -ForegroundColor Yellow
        Stop-Process -Id $script:UnityProcess.Id -Force
        $script:UnityProcess.WaitForExit()
    }
    if ($script:ConfigMoved) {
        if (Restore-CloudConfig) { Write-Host "  put the cloud config back" }
    }
    # A run that failed after Unity wrote the settings still puts them back.
    if ($settingsBackup -ne $null -and -not $succeeded -and (Test-Path $settingsFile)) {
        $nowBytes = [IO.File]::ReadAllBytes($settingsFile)
        $same = ($nowBytes.Length -eq $settingsBackup.Length)
        if ($same) { for ($i = 0; $i -lt $nowBytes.Length; $i++) { if ($nowBytes[$i] -ne $settingsBackup[$i]) { $same = $false; break } } }
        if (-not $same) { [IO.File]::WriteAllBytes($settingsFile, $settingsBackup) }
    }
    # An APK this run did not finish checking is renamed, never deleted, so it cannot be mistaken for a checked one.
    if (-not $succeeded -and $script:ApkPath -and (Test-Path $script:ApkPath)) {
        Move-Item $script:ApkPath "$($script:ApkPath).REFUSED"
        Write-Host "  the unchecked APK is at $($script:ApkPath).REFUSED. Do not hand it out." -ForegroundColor Red
    }
}
exit $exitCode
