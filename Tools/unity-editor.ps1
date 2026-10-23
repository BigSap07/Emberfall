# Finds the Unity editor this project needs, the way Tools/build-windows.ps1 does: an explicit path wins, then
# EMBERFALL_UNITY_EXE, then the version named in ProjectSettings/ProjectVersion.txt under the Unity Hub install
# folders (the default one and the secondary install path Unity Hub records). Dot-source it:
#
#   . (Join-Path $PSScriptRoot "unity-editor.ps1")
#   $exe = Find-UnityEditor $root $UnityExe          # throws with the reason when it finds nothing
#   Test-AndroidModule $exe                           # $null when the Android module is complete, else what is missing
#
# Read only. It starts nothing and writes nothing.

function Find-UnityEditor([string]$root, [string]$explicit = "") {
    if ($explicit) {
        if (-not (Test-Path $explicit)) { throw "$explicit does not exist." }
        return $explicit
    }
    if ($env:EMBERFALL_UNITY_EXE) {
        if (-not (Test-Path $env:EMBERFALL_UNITY_EXE)) { throw "EMBERFALL_UNITY_EXE points at $($env:EMBERFALL_UNITY_EXE), which does not exist." }
        return $env:EMBERFALL_UNITY_EXE
    }
    $versionLine = Get-Content (Join-Path $root "ProjectSettings\ProjectVersion.txt") |
                   Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
    if (-not $versionLine) { throw "ProjectSettings/ProjectVersion.txt names no editor version." }
    $editorVersion = ([regex]::Match($versionLine, '^m_EditorVersion:\s*(\S+)')).Groups[1].Value
    $hubRoots = @("C:\Program Files\Unity\Hub\Editor")
    if ($env:APPDATA) {
        $secondary = Join-Path $env:APPDATA "UnityHub\secondaryInstallPath.json"
        if (Test-Path $secondary) {
            $extra = (Get-Content $secondary -Raw | ConvertFrom-Json)
            if ($extra) { $hubRoots = @("$extra") + $hubRoots }
        }
    }
    foreach ($hubRoot in $hubRoots) {
        $candidate = Join-Path $hubRoot "$editorVersion\Editor\Unity.exe"
        if (Test-Path $candidate) { return $candidate }
    }
    throw ("no Unity $editorVersion found under " + ($hubRoots -join " or ") + ". Pass -UnityExe or set EMBERFALL_UNITY_EXE.")
}

# What the Android build needs next to the editor: the Android player, and its SDK, NDK (IL2CPP) and OpenJDK.
# Returns an array of missing parts, empty when the module is complete.
function Get-AndroidModuleGaps([string]$unityExe) {
    $editorDir = Split-Path -Parent $unityExe
    $player = Join-Path $editorDir "Data\PlaybackEngines\AndroidPlayer"
    $gaps = @()
    if (-not (Test-Path $player)) { return @("Android Build Support (the AndroidPlayer folder is missing)") }
    foreach ($part in @(@("SDK", "Android SDK and Platform Tools"), @("NDK", "Android NDK (needed by IL2CPP)"), @("OpenJDK", "OpenJDK"))) {
        if (-not (Test-Path (Join-Path $player $part[0]))) { $gaps += $part[1] }
    }
    return $gaps
}

function Get-AndroidInstallSteps([string]$unityExe, [string[]]$gaps) {
    $version = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $unityExe))
    return @(
        ("The Android module is incomplete for Unity ${version}: " + ($gaps -join ", ")),
        "To install it: open Unity Hub, Installs, the gear on Unity $version, Add modules, tick",
        "'Android Build Support' with 'OpenJDK' and 'Android SDK & NDK Tools', then Install.",
        "Close the editor and run this again."
    )
}
