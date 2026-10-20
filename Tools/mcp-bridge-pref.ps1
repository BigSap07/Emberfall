# Holds the Unity AI Assistant MCP bridge off for the length of one capture session, then puts the
# owner's setting back. Dot-source it:  . (Join-Path $PSScriptRoot 'mcp-bridge-pref.ps1')
#
# WHY: capture editors that stalled after entering Play often ended their log in the bridge
# (Unity.AI.MCP.Editor, ExecutableIdentityCollector checking a claude.exe client). The bridge starts in
# UnityMCPBridge.Init, an [InitializeOnLoadMethod], whenever MCPSettings.bridgeEnabled is true. The
# package honours no launch argument or environment variable outside batch mode (UNITY_MCP_DISABLE_BATCH
# and batchModeEnabled are read only when Application.isBatchMode), and captures run with graphics, so
# the one switch is the setting itself. It lives in EditorPrefs, key Unity.AI.MCP.ProjectSettings.v2:
# on Windows a REG_BINARY value Unity.AI.MCP.ProjectSettings.v2_h<hash> under
# HKCU\Software\Unity Technologies\Unity Editor 5.x, UTF-8 JSON with a trailing 0 byte. It is read at
# domain load, so it must be off before the editor starts.
#
# SAFETY:
#   - The key is machine-wide, shared with the owner's interactive editor. When any Unity.exe other than
#     the one about to start is already running, the bridge is left alone (an open editor that reloads
#     its domain while the setting is off would lose its bridge until its next reload).
#   - The original bytes are written to a backup file before the change. A session that is killed hard
#     leaves the backup behind, and the next capture session puts it back before doing anything else.
#   - On restore, only the bridgeEnabled token is put back when the editor rewrote the value during the
#     run (MCPSettingsManager.SaveSettings), so any other change it saved is kept.
#   - EMBERFALL_CAPTURE_KEEP_MCP=1 leaves the bridge on for that session.
#   - Nothing is deleted: a used backup file is moved to <backup>.restored.

$script:McpPrefSubKey = 'Software\Unity Technologies\Unity Editor 5.x'
$script:McpPrefPrefix = 'Unity.AI.MCP.ProjectSettings.v2_h'
$script:McpPrefBackup = Join-Path $env:LOCALAPPDATA 'Emberfall\mcp-bridge-pref.backup'
$script:McpPrefState = $null

function Get-McpPrefName($key) {
    foreach ($n in $key.GetValueNames()) { if ($n.StartsWith($script:McpPrefPrefix)) { return $n } }
    return ''
}

function Set-BridgeToken([byte[]]$bytes, [string]$value) {
    $text = [Text.Encoding]::UTF8.GetString($bytes)
    $changed = [regex]::Replace($text, '"bridgeEnabled"\s*:\s*(true|false)', ('"bridgeEnabled": ' + $value))
    # The comma keeps the byte[] whole; a bare return would unroll it into object[].
    return ,([Text.Encoding]::UTF8.GetBytes($changed))
}

function Get-BridgeToken([byte[]]$bytes) {
    $m = [regex]::Match([Text.Encoding]::UTF8.GetString($bytes), '"bridgeEnabled"\s*:\s*(true|false)')
    if ($m.Success) { return $m.Groups[1].Value }
    return ''
}

# Puts back a backup left by a session that never reached its restore. Returns a line to log, or ''.
function Restore-StaleMcpBackup {
    if (-not (Test-Path -LiteralPath $script:McpPrefBackup)) { return '' }
    $lines = [IO.File]::ReadAllLines($script:McpPrefBackup)
    if ($lines.Count -lt 2) { return 'mcp bridge: a backup file without content was left; ignored' }
    $name = $lines[0]
    $original = [Convert]::FromBase64String($lines[1])
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($script:McpPrefSubKey, $true)
    if ($null -eq $key) { return 'mcp bridge: the EditorPrefs key is missing; stale backup not applied' }
    try {
        $current = $key.GetValue($name)
        if ($null -eq $current) { $key.SetValue($name, $original, [Microsoft.Win32.RegistryValueKind]::Binary) }
        else { $key.SetValue($name, [byte[]](Set-BridgeToken $current (Get-BridgeToken $original)), [Microsoft.Win32.RegistryValueKind]::Binary) }
    } finally { $key.Close() }
    Move-Item -LiteralPath $script:McpPrefBackup -Destination ($script:McpPrefBackup + '.restored') -Force
    return ('mcp bridge: a backup left by an interrupted session was put back (bridgeEnabled ' + (Get-BridgeToken $original) + ')')
}

# Turns the bridge off for the session about to start. $otherUnity: PIDs of Unity editors already running.
# Returns a line to log.
function Disable-McpBridgeForCapture([int[]]$otherUnity) {
    $stale = Restore-StaleMcpBackup
    $prefix = if ($stale) { $stale + '; ' } else { '' }
    if ($env:EMBERFALL_CAPTURE_KEEP_MCP -and $env:EMBERFALL_CAPTURE_KEEP_MCP.Trim() -eq '1') { return ($prefix + 'mcp bridge: left on (EMBERFALL_CAPTURE_KEEP_MCP=1)') }
    if ($otherUnity -and $otherUnity.Count -gt 0) { return ($prefix + 'mcp bridge: left on, another Unity editor is running (PID ' + ($otherUnity -join ', ') + ') and shares the setting') }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($script:McpPrefSubKey, $true)
    if ($null -eq $key) { return ($prefix + 'mcp bridge: no EditorPrefs key, nothing to turn off') }
    try {
        $name = Get-McpPrefName $key
        if (-not $name) { return ($prefix + 'mcp bridge: no saved bridge setting, nothing to turn off (the package default is on)') }
        $original = [byte[]]$key.GetValue($name)
        $token = Get-BridgeToken $original
        if ($token -ne 'true') { return ($prefix + 'mcp bridge: already off (bridgeEnabled ' + $token + '), left as it is') }
        New-Item -ItemType Directory -Force (Split-Path -Parent $script:McpPrefBackup) | Out-Null
        [IO.File]::WriteAllLines($script:McpPrefBackup, [string[]]@($name, [Convert]::ToBase64String($original)), [Text.UTF8Encoding]::new($false))
        $off = Set-BridgeToken $original 'false'
        $key.SetValue($name, $off, [Microsoft.Win32.RegistryValueKind]::Binary)
        $script:McpPrefState = [pscustomobject]@{ Name = $name; Original = $original; Off = $off }
        return ($prefix + 'mcp bridge: turned off for this session (EditorPrefs ' + $name + ', backup ' + $script:McpPrefBackup + ')')
    } finally { $key.Close() }
}

# Puts the owner's setting back after the session. Safe to call more than once. Returns a line to log.
function Restore-McpBridgeAfterCapture {
    $s = $script:McpPrefState
    if ($null -eq $s) { return '' }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($script:McpPrefSubKey, $true)
    if ($null -eq $key) { return 'mcp bridge: WARNING the EditorPrefs key is gone; backup kept at ' + $script:McpPrefBackup }
    $note = ''
    try {
        $current = $key.GetValue($s.Name)
        if ($null -eq $current -or [Convert]::ToBase64String([byte[]]$current) -eq [Convert]::ToBase64String($s.Off)) {
            $key.SetValue($s.Name, $s.Original, [Microsoft.Win32.RegistryValueKind]::Binary)
            $note = 'mcp bridge: owner setting restored (bridgeEnabled true)'
        } else {
            $key.SetValue($s.Name, [byte[]](Set-BridgeToken ([byte[]]$current) 'true'), [Microsoft.Win32.RegistryValueKind]::Binary)
            $note = 'mcp bridge: the editor rewrote the setting during the run; its other fields kept, bridgeEnabled put back to true'
        }
    } finally { $key.Close() }
    if (Test-Path -LiteralPath $script:McpPrefBackup) { Move-Item -LiteralPath $script:McpPrefBackup -Destination ($script:McpPrefBackup + '.restored') -Force }
    $script:McpPrefState = $null
    return $note
}
