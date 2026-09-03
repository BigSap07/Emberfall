# Render a hero's idle frames with the measured head/feet lines drawn on, so the one thing
# import-sprite-sheets.ps1 cannot decide for itself can be decided by looking.
#
# Usage:
#   Tools/show-sprite-fractions.ps1 -HeroId veska_the_broken_oath
#   Tools/show-sprite-fractions.ps1 -HeroId orren_the_cinder_hunter -Frames 0,12,24,36
#
# Writes a PNG next to the scratch path given by -Out (default: the repo's Tools/Screenshots).
#
# ---------------------------------------------------------------------------
# What this is for
# ---------------------------------------------------------------------------
# spriteBodyFraction is the character's body "head to feet, not counting anything raised above
# the head". The importer measures it as the alpha bounding box of the idle clip, which is only
# the same thing while the idle pose keeps every weapon and prop at or below head height. Alpha
# cannot tell a raised warhammer from a hat.
#
# When it is wrong it is wrong quietly and in a specific direction: an overhang makes the measured
# span too TALL, which makes the authored bodyFraction too LARGE, which makes canvasHeight
# (Height / bodyFraction) too SMALL, and the hero renders SHORT by exactly the ratio of the
# overhang. Nothing errors. It just looks a bit off next to the others, which is a hard thing to
# notice and an easy thing to misattribute - Veska was suspected of exactly this and was innocent
# (her hat tip really was her highest point; her hookblades peaked 27px lower).
#
# So: look at the red line. If it sits on hair, hat or horns, the number is right. If it sits on a
# weapon held above the head, measure the real head top off this image and pass the corrected
# fraction by hand.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $HeroId,
    [int[]] $Frames = @(0, 15, 30, 45),
    [string] $Out,
    [int]    $FrameWidth  = 288,
    [int]    $FrameHeight = 512
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repo   = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo "Assets\Resources\Sprites\Units"
if (-not $Out) { $Out = Join-Path $repo "Tools\Screenshots" }
if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out | Out-Null }

$png  = Join-Path $outDir "${HeroId}_idle.png"
$json = Join-Path $outDir "${HeroId}_idle.json"
if (-not (Test-Path $png))  { throw "no idle sheet for $HeroId at $png" }
if (-not (Test-Path $json)) { throw "no idle sidecar for $HeroId at $json" }

$side = Get-Content $json -Raw | ConvertFrom-Json
$top    = [int]$side.contentBox.topPx
$bottom = [int]$side.contentBox.bottomPx
$body   = $side.contentBox.bodyFraction
$foot   = $side.contentBox.footFraction

$src = [System.Drawing.Bitmap]::FromFile($png)
$sheetCols = [int]($src.Width / $FrameWidth)
$n = $Frames.Count
$W = $n * $FrameWidth
$H = $FrameHeight + 34
$dst = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($dst)
$g.Clear([System.Drawing.Color]::FromArgb(255, 28, 28, 34))

for ($i = 0; $i -lt $n; $i++) {
    $f = $Frames[$i]
    $sx = ($f % $sheetCols) * $FrameWidth
    $sy = [math]::Floor($f / $sheetCols) * $FrameHeight
    $sr = New-Object System.Drawing.Rectangle $sx, $sy, $FrameWidth, $FrameHeight
    $dr = New-Object System.Drawing.Rectangle ($i * $FrameWidth), 34, $FrameWidth, $FrameHeight
    $g.DrawImage($src, $dr, $sr, [System.Drawing.GraphicsUnit]::Pixel)
}
$src.Dispose()

$penHead = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 255, 70, 70)), 2
$penFeet = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(230, 80, 255, 80)), 2
$g.DrawLine($penHead, 0, (34 + $top),    $W, (34 + $top))
$g.DrawLine($penFeet, 0, (34 + $bottom), $W, (34 + $bottom))

$f1 = New-Object System.Drawing.Font "Consolas", 15
$g.DrawString("$HeroId   red=measured head y=$top   green=feet y=$bottom   body=$body foot=$foot",
              $f1, [System.Drawing.Brushes]::White, 6, 7)
$g.Dispose()

$path = Join-Path $Out "$HeroId-fractions.png"
$dst.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$dst.Dispose()
Write-Host $path
Write-Host "  red on hair/hat/horns -> bodyFraction $body is correct."
Write-Host "  red on a raised weapon -> too tall; the hero will render SHORT. Measure the real head"
Write-Host "  top off this image and pass the corrected fraction by hand."
