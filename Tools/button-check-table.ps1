<#
.SYNOPSIS
  Prints the button hierarchy table (R53) from the BUTTONCHECK lines a capture run left in the editor log.

.DESCRIPTION
  Assets/Editor/ButtonHierarchyCheck.cs writes one line per capture:
    BUTTONCHECK screen_<Name>@<W>x<H>.png primary=N secondary=N quiet=N disabled=N legacy=N names=...
  This script reads those lines from one or more Unity logs (the capture-batch launch logs), keeps the
  last line per capture file, and prints one row per screen with the Primary count at each size.
  A row with more than one Primary at any size is listed under EXCEPTIONS. Exit 0 always: it reports,
  it does not gate. The check itself is editor only, so release builds never see any of this.

.EXAMPLE
  powershell -NoProfile -File Tools/button-check-table.ps1 -Logs D:\...\capture-batch-after1080-launch1.log,...
#>
param(
    [Parameter(Mandatory = $true)][string[]]$Logs,
    [string]$OutFile = ''
)

# powershell -File hands a comma list over as one string, so split it here
$Logs = @($Logs | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$rows = @{}
$sizes = New-Object System.Collections.Generic.SortedSet[string]
foreach ($log in $Logs) {
    foreach ($line in [IO.File]::ReadLines($log)) {
        $i = $line.IndexOf('BUTTONCHECK ')
        if ($i -lt 0) { continue }
        $m = [regex]::Match($line.Substring($i), '^BUTTONCHECK screen_(?<n>.+)@(?<s>\d+x\d+)\.png primary=(?<p>\d+) secondary=(?<sec>\d+) quiet=(?<q>\d+) disabled=(?<d>\d+) legacy=(?<l>\d+) names=(?<names>.*?)( MORE THAN ONE PRIMARY)?\s*$')
        if (-not $m.Success) { continue }
        $name = $m.Groups['n'].Value
        $size = $m.Groups['s'].Value
        [void]$sizes.Add($size)
        if (-not $rows.ContainsKey($name)) { $rows[$name] = @{} }
        $rows[$name][$size] = @{ P = [int]$m.Groups['p'].Value; Names = $m.Groups['names'].Value }
    }
}

$out = New-Object System.Collections.Generic.List[string]
$sz = @($sizes)
$out.Add(('{0,-22}' -f 'screen') + (($sz | ForEach-Object { '{0,11}' -f $_ }) -join ''))
$bad = New-Object System.Collections.Generic.List[string]
foreach ($name in ($rows.Keys | Sort-Object)) {
    $cells = foreach ($s in $sz) { if ($rows[$name].ContainsKey($s)) { '{0,11}' -f $rows[$name][$s].P } else { '{0,11}' -f '-' } }
    $out.Add(('{0,-22}' -f $name) + ($cells -join ''))
    foreach ($s in $sz) {
        if ($rows[$name].ContainsKey($s) -and $rows[$name][$s].P -gt 1) {
            $bad.Add($name + '@' + $s + ' primary=' + $rows[$name][$s].P + ': ' + $rows[$name][$s].Names)
        }
    }
}
$out.Add('')
$over = @($rows.Keys | Where-Object { $n = $_; @($sz | Where-Object { $rows[$n].ContainsKey($_) -and $rows[$n][$_].P -gt 1 }).Count -gt 0 }).Count
$out.Add('screens=' + $rows.Count + ' with_more_than_one_primary=' + $over)
if ($bad.Count -gt 0) { $out.Add('EXCEPTIONS'); foreach ($b in $bad) { $out.Add('  ' + $b) } }
$out | ForEach-Object { Write-Host $_ }
if ($OutFile) { [IO.File]::WriteAllLines($OutFile, $out.ToArray(), (New-Object System.Text.UTF8Encoding($false))) }
exit 0
