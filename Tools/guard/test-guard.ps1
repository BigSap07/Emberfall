# Pipes sample hook JSON into Tools/guard/guard.py exactly as Claude Code does (stdin JSON, exit code,
# stderr) and checks every rule blocks and ordinary commands pass. Works in Windows PowerShell 5.1 and pwsh.
#   pwsh Tools/guard/test-guard.ps1        # exit 0 only if every case passes
$ErrorActionPreference = "Stop"
$guard = Join-Path $PSScriptRoot "guard.py"
$cwd = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Invoke-Guard($tool, $inputObj, $env) {
    $payload = @{ hook_event_name = "PreToolUse"; tool_name = $tool; tool_input = $inputObj; cwd = $cwd } | ConvertTo-Json -Depth 5 -Compress
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = "python"
    $psi.Arguments = '-I "' + $guard + '"'
    $psi.RedirectStandardInput = $true; $psi.RedirectStandardError = $true; $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    foreach ($k in @("EMBERFALL_GUARD_NOW", "EMBERFALL_GUARD_BRANCH", "EMBERFALL_GUARD_MARKER_OK")) {
        if ($psi.EnvironmentVariables.ContainsKey($k)) { $psi.EnvironmentVariables.Remove($k) }
    }
    if ($env) { foreach ($k in $env.Keys) { $psi.EnvironmentVariables[$k] = [string]$env[$k] } }
    $p = [Diagnostics.Process]::Start($psi)
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($payload)
    $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length); $p.StandardInput.Close()
    $err = $p.StandardError.ReadToEnd(); $null = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()
    return @{ Code = $p.ExitCode; Err = $err }
}

$blockCases = New-Object System.Collections.Generic.List[object]
$okCases = New-Object System.Collections.Generic.List[object]
function Block($name, $tool, $in, $rule, $env) { $blockCases.Add(@{ Name = $name; Tool = $tool; In = $in; Rule = $rule; Env = $env }) }
function Ok($name, $tool, $in, $env) { $okCases.Add(@{ Name = $name; Tool = $tool; In = $in; Env = $env }) }
function B($c) { @{ command = $c } }

# Rule 1: deletes
Block "rm -rf"                 "Bash" (B 'rm -rf build') "RULE 1"
Block "rm in a chain"          "Bash" (B 'ls && echo hi; rm file.txt') "RULE 1"
Block "del"                    "PowerShell" (B 'del C:\x\y.txt') "RULE 1"
Block "Remove-Item"            "PowerShell" (B 'Get-ChildItem | Remove-Item -Recurse -Force') "RULE 1"
Block "remove-item lowercase"  "PowerShell" (B 'remove-item foo') "RULE 1"
Block "rmdir"                  "Bash" (B 'rmdir /s /q out') "RULE 1"
Block "rd"                     "PowerShell" (B 'rd out -Recurse') "RULE 1"
Block "git clean"              "Bash" (B 'git clean -fdx') "RULE 1"
Block "git -C x clean"         "Bash" (B 'git -C D:/Emberfall/Game clean -n') "RULE 1"
Block "rm inside bash -c"      "Bash" (B 'bash -c "rm -rf /tmp/x"') "RULE 1"
Block "find -delete"           "Bash" (B 'find . -name "*.tmp" -delete') "RULE 1"
# Rule 2: secrets
Block "Read supabase.json"     "Read" @{ file_path = 'D:\Emberfall\secrets\supabase.json' } "RULE 2"
Block "Read under secrets"     "Read" @{ file_path = 'D:\Emberfall\secrets\anything.txt' } "RULE 2"
Block "Read supabase.json rel" "Read" @{ file_path = 'config/supabase.json' } "RULE 2"
Block "cat supabase.json"      "Bash" (B 'cat supabase.json') "RULE 2"
Block "Get-Content secrets"    "PowerShell" (B 'Get-Content D:\Emberfall\secrets\keys.txt') "RULE 2"
Block "type via forward slash" "Bash" (B 'type D:/Emberfall/secrets/x') "RULE 2"
Block "grep supabase.json"     "Bash" (B 'grep -r key ./supabase.json') "RULE 2"
Block "Grep tool in secrets"   "Grep" @{ pattern = 'key'; path = 'D:\Emberfall\secrets' } "RULE 2"
Block "Edit supabase.json"     "Edit" @{ file_path = 'D:\Emberfall\secrets\supabase.json'; old_string = 'a'; new_string = 'b' } "RULE 2"
# Rule 3: push to main
$noMarker = @{ EMBERFALL_GUARD_MARKER_OK = "0"; EMBERFALL_GUARD_BRANCH = "run/x" }
$onMain = @{ EMBERFALL_GUARD_MARKER_OK = "0"; EMBERFALL_GUARD_BRANCH = "main" }
Block "push origin main"       "Bash" (B 'git push origin main') "RULE 3" $noMarker
Block "push HEAD:main"         "Bash" (B 'git push origin HEAD:main') "RULE 3" $noMarker
Block "push --all"             "Bash" (B 'git push --all origin') "RULE 3" $noMarker
Block "push refs/heads/main"   "Bash" (B 'git push origin run/x:refs/heads/main') "RULE 3" $noMarker
Block "push -u origin main"    "Bash" (B 'git push -u origin main') "RULE 3" $noMarker
Block "bare push on main"      "Bash" (B 'git push') "RULE 3" $onMain
Block "push HEAD on main"      "Bash" (B 'git push origin HEAD') "RULE 3" $onMain
Block "push main after add"    "Bash" (B 'git add -A && git commit -m x && git push origin main') "RULE 3" $noMarker
# Rule 4: Unity quiet hours
Block "unity-editor.ps1 03:00" "PowerShell" (B 'pwsh Tools/unity-editor.ps1 -Action run') "RULE 4" @{ EMBERFALL_GUARD_NOW = "03:00" }
Block "Unity.exe 04:15"        "Bash" (B '"C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe" -batchmode -quit') "RULE 4" @{ EMBERFALL_GUARD_NOW = "04:15" }
Block "-projectPath 05:29"     "PowerShell" (B 'Start-Process unity -ArgumentList "-projectPath D:\x"') "RULE 4" @{ EMBERFALL_GUARD_NOW = "05:29" }
Block "build-android 03:30"    "PowerShell" (B 'pwsh Tools/build-android.ps1') "RULE 4" @{ EMBERFALL_GUARD_NOW = "03:30" }
Block "capture-batch 04:00"    "Bash" (B 'pwsh Tools/capture-batch.ps1') "RULE 4" @{ EMBERFALL_GUARD_NOW = "04:00" }
Block "Unity Hub 05:00"        "PowerShell" (B 'Start-Process "Unity Hub.exe"') "RULE 4" @{ EMBERFALL_GUARD_NOW = "05:00" }

# Normal commands
Ok "git status"            "Bash" (B 'git status')
Ok "git add -A"            "Bash" (B 'git add -A')
Ok "git commit"            "Bash" (B 'git commit -m "Fix the formation row"')
Ok "git commit heredoc"    "Bash" (B "git commit -m `"`$(cat <<'HEREEND'`nTrim notes, no rm or del or git clean here`n`nCo-Authored-By: X`nHEREEND`n)`"")
Ok "git diff"              "Bash" (B 'git diff --stat HEAD~1')
Ok "git log"               "Bash" (B 'git log --oneline -20')
Ok "git push feature"      "Bash" (B 'git push origin run/x')
Ok "git push -u feature"   "Bash" (B 'git push -u origin run/r30-b') $noMarker
Ok "git push HEAD on feat" "Bash" (B 'git push origin HEAD') $noMarker
Ok "bare push on feature"  "Bash" (B 'git push') $noMarker
Ok "push main with marker" "Bash" (B 'git push origin main') @{ EMBERFALL_GUARD_MARKER_OK = "1" }
Ok "ls"                    "Bash" (B 'ls -la Tools')
Ok "dotnet build"          "Bash" (B 'dotnet build Tools/DevHarness -c Release')
Ok "pwsh build-check"      "PowerShell" (B 'pwsh Tools/build-check.ps1 -Quick')
Ok "Get-ChildItem"         "PowerShell" (B 'Get-ChildItem -Recurse Tools | Select-Object -First 5')
Ok "Read normal file"      "Read" @{ file_path = 'D:\Emberfall\Game-r30b\CLAUDE.md' }
Ok "Edit normal file"      "Edit" @{ file_path = 'D:\Emberfall\Game-r30b\README.md'; old_string = 'a'; new_string = 'rm b' }
Ok "Grep normal"           "Grep" @{ pattern = 'ApplyLedger'; path = 'Assets/Scripts' }
Ok "Glob normal"           "Glob" @{ pattern = '**/*.cs' }
Ok "words containing rm"   "Bash" (B 'echo format perform confirm firmware reform.txt')
Ok "words containing del"  "Bash" (B 'ls model.json Models/delta.cs')
Ok "unity script by day"   "PowerShell" (B 'pwsh Tools/unity-editor.ps1 -Action run') @{ EMBERFALL_GUARD_NOW = "12:00" }
Ok "unity just before 03"  "PowerShell" (B 'pwsh Tools/build-windows.ps1') @{ EMBERFALL_GUARD_NOW = "02:59" }
Ok "unity at 05:30"        "PowerShell" (B 'pwsh Tools/build-windows.ps1') @{ EMBERFALL_GUARD_NOW = "05:30" }
Ok "no-unity at night"     "Bash" (B 'git status') @{ EMBERFALL_GUARD_NOW = "04:00" }

$bp = 0; $np = 0; $fail = 0
foreach ($c in $blockCases) {
    $r = Invoke-Guard $c.Tool $c.In $c.Env
    $good = ($r.Code -eq 2) -and ($r.Err -match [regex]::Escape($c.Rule))
    if ($good) { $bp++; Write-Host ("PASS  block  " + $c.Name) } else { $fail++; Write-Host ("FAIL  block  " + $c.Name + "  exit=" + $r.Code + " stderr=" + $r.Err) -ForegroundColor Red }
}
foreach ($c in $okCases) {
    $r = Invoke-Guard $c.Tool $c.In $c.Env
    if ($r.Code -eq 0) { $np++; Write-Host ("PASS  allow  " + $c.Name) } else { $fail++; Write-Host ("FAIL  allow  " + $c.Name + "  exit=" + $r.Code + " stderr=" + $r.Err) -ForegroundColor Red }
}
Write-Host ""
Write-Host ("SUMMARY: blocked cases passed " + $bp + "/" + $blockCases.Count + ", normal cases passed " + $np + "/" + $okCases.Count + ", failures " + $fail)
if ($fail -gt 0) { exit 1 }
exit 0
