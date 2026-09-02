# Import one hero's Sorceress Sprite Analyzer export into Resources/Sprites/Units.
#
# Usage:
#   Tools/import-sprite-sheets.ps1 -Hero veska
#   Tools/import-sprite-sheets.ps1 -Hero marrow -WhatIf
#   Tools/import-sprite-sheets.ps1 -Hero orren -Source Assets/_Incoming/orren_the_cinder_hunter
#
# Reads   Assets/Resources/Sprites/Units/<short>/     (where the raw exports are dropped)
# Writes  Assets/Resources/Sprites/Units/<heroId>_<clip>.png  + <heroId>_<clip>.json
#
# ---------------------------------------------------------------------------
# Why this file exists
# ---------------------------------------------------------------------------
# Kael was imported with a PowerShell script that was never committed and no longer exists
# anywhere - not in the repo, not on disk, not in git history on any branch. Every quirk it knew
# had to be rediscovered to write this one. So this is the same job, parked somewhere it survives.
# It is deliberately a script and not a pipeline: no config, no abstraction. What it does carry is
# the list of things that are NOT obvious.
#
# ---------------------------------------------------------------------------
# The things that are not obvious
# ---------------------------------------------------------------------------
# 1. THE ENGINE HAS EXACTLY FIVE CLIP SLOTS: idle, basic, skill, hit_reaction, death. Those strings
#    are hardcoded in UnitActor.cs and SpriteAnimator. Each export also ships a `controlled` clip
#    and a second basic; both are dropped rather than imported, because nothing can ever request
#    them and they are ~70MB of dead weight per hero.
#
# 2. MATCH BY ROLE, NEVER BY FILENAME. The four exports use three naming patterns:
#    veska_basic1_mp4_sheet.png, orren_basic2_mp4_720x1280_sheet.png, marrow_basic (no digit) and
#    seraphine_skill_mp4_720x1280_sheet__1_.png (a browser duplicate-download artifact). Clips are
#    resolved by role token with an explicit per-hero override for `basic`, longest-token-first so
#    "hit_reaction" is never eaten by a looser match.
#
# 3. THE FRAMES MUST BE DOWNSCALED, AND PER-FRAME. Source frames are 720x1280 and sheets reach
#    6480x11520. Kael's are 288x512 / 3744x6144 and import at maxTextureSize 8192 - 11520 blows
#    straight past that, so Unity would rescale the sheet non-uniformly and shear the frame grid
#    (the exact failure hub.png hit: 1650x780 arriving as 2048x1024). Resampling is done ONE FRAME
#    AT A TIME into its own destination rect. Resizing the whole sheet in a single DrawImage is
#    faster and wrong: bicubic sampling bleeds pixels across frame boundaries, so every frame picks
#    up a smear of its neighbours' edges.
#
# 4. THE RAW FOLDERS MUST NOT STAY UNDER Resources/. Everything under a Resources folder is packed
#    into the build whether or not anything references it, and the four raw export folders are
#    roughly a gigabyte. This script does not delete them - it prints the move command, to be run
#    once the hero looks right in Play mode.
#
# 5. THE .meta MUST BE WRITTEN, NOT LEFT TO UNITY. A texture Unity imports with no .meta gets
#    maxTextureSize 2048 - and every sheet here is 2304 to 2592 wide, so Unity would quietly
#    downscale it and shear the frame grid, which is note 3's failure arriving through the back
#    door after the resampling was done correctly. Kael's five metas are byte-identical apart from
#    their guid, so this script copies one of them as the template and stamps a fresh guid. An
#    existing .meta is never overwritten: its guid is the asset's identity and clobbering it breaks
#    every reference to the sprite.
#
# 6. MARROW HAS A 721x1280 SHEET. One sheet in one export is 1px wider per frame than the other
#    six. Frame rects therefore come from each sheet's own manifest frameWidth/frameHeight rather
#    than from a shared constant; the destination is always exactly 288x512.
#
# 7. TRAILING TRANSPARENT FRAMES ARE REAL AND MUST BE TRIMMED. Kael's basic exported 150 frames of
#    which 145-149 were fully transparent; playing the exported range flashed him invisible for the
#    last five frames before the fallback to idle. His sidecar carries a hand-written correction
#    (end 149 -> 144). This script finds that case by alpha and writes the corrected end plus a
#    note saying it did, so nobody has to catch it in Play mode twice.
#
# ---------------------------------------------------------------------------
# What this script measures, guesses, and refuses to do
# ---------------------------------------------------------------------------
# MEASURED (trustworthy): the trailing-transparent trim, and both of heroes.json's sprite
# fractions, which are just the alpha bounding box of the IDLE clip:
#     spriteFootFraction = empty margin below the feet   / canvas height
#     spriteBodyFraction = head-to-feet span             / canvas height
# Kael's were set by hand, and re-measuring his idle sheet with this code reproduces them: 0.223
# against his authored 0.222, and 0.551 against his authored 0.555. That agreement is the only
# reason to trust these - the doc comments in Definitions.cs describe the intent, not the arithmetic.
# CAVEAT: spriteBodyFraction is defined as the body "not counting anything raised above the head".
# Alpha cannot tell a raised weapon from a head, so this measurement is only correct when the IDLE
# pose keeps the weapon at or below head height - true for Kael, which is why his numbers matched.
# The measurement is printed, never written into heroes.json; check the idle pose before using it.
#
# GUESSED (do not trust): impactFraction and playbackSpeed are not in the export and cannot be
# derived from it. Kael's were hand-picked (0.5 / 2.75) and his own sidecar says plainly they are
# starting values "meant to be eyeballed and retuned in Play mode". The seeds below carry the same
# warning into every sidecar they write.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string] $Hero,
    [string] $Source,
    [string] $HeroId,
    [string] $Basic,
    [int]    $FrameWidth  = 288,
    [int]    $FrameHeight = 512
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Short folder name -> hero id in heroes.json, and which of the two basic-attack clips to ship.
# Which basic is a design call, not a technical one; one was picked per hero.
$heroes = @{
    'veska'     = @{ id = 'veska_the_broken_oath';    basic = 'basic1' }
    'seraphine' = @{ id = 'seraphine_the_last_ember'; basic = 'basic1' }
    'marrow'    = @{ id = 'marrow_the_burning_vow';   basic = 'basic'  }
    'orren'     = @{ id = 'orren_the_cinder_hunter';  basic = 'basic1' }
}

$repo   = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo "Assets\Resources\Sprites\Units"

if ($heroes.ContainsKey($Hero)) {
    if (-not $HeroId) { $HeroId = $heroes[$Hero].id }
    if (-not $Basic)  { $Basic  = $heroes[$Hero].basic }
}
if (-not $HeroId) { throw "unknown hero '$Hero' - pass -HeroId and -Basic explicitly" }
if (-not $Basic)  { $Basic = 'basic1' }

if ($Source) { $inDir = Join-Path $repo $Source } else { $inDir = Join-Path $outDir $Hero }
if (-not (Test-Path $inDir))  { throw "no staged export at $inDir" }
if (-not (Test-Path $outDir)) { throw "missing $outDir" }

$manifestPath = Join-Path $inDir "sprite_sheets.json"
if (-not (Test-Path $manifestPath)) { throw "no sprite_sheets.json in $inDir" }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

# Ordered longest-token-first: hit_reaction resolves before anything that could match loosely.
$wanted = [ordered]@{
    'hit_reaction' = 'hit_reaction'
    'idle'         = 'idle'
    'skill'        = 'skill'
    'death'        = 'death'
    'basic'        = $Basic
}

# See note 5. Kael's metas are the template; his settings (maxTextureSize 8192, no mipmaps) are
# what these sheets need, and sourcing them from his file rather than hardcoding a copy means a
# future change to his import settings carries to every hero imported after it.
function Write-TextureMeta([string] $pngPath) {
    $metaPath = "$pngPath.meta"
    if (Test-Path $metaPath) { return 'kept' }   # never clobber an existing guid
    $template = Get-ChildItem (Join-Path $outDir "kael_the_ashbound_*.png.meta") -ErrorAction SilentlyContinue |
                Select-Object -First 1
    if (-not $template) { return 'no-template' }
    $guid = [guid]::NewGuid().ToString('N')
    $text = (Get-Content $template.FullName -Raw) -replace '(?m)^guid: [0-9a-f]{32}\s*$', "guid: $guid"
    [IO.File]::WriteAllText($metaPath, $text, (New-Object Text.UTF8Encoding($false)))
    return 'written'
}

function Find-Sheet([string] $token) {
    $hits = @($manifest.sprites | Where-Object { $_.image -match [regex]::Escape($token) })
    if ($hits.Count -eq 0) { return $null }
    # Shortest name wins: marrow's "basic" must resolve to marrow_basic_mp4_sheet.png and not
    # marrow_basic2_mp4_sheet.png, and seraphine's "skill" prefers a clean name over a __1_ dupe.
    return ($hits | Sort-Object { $_.image.Length })[0]
}

# Vertical alpha bounds of one frame within a locked 32bpp bitmap. $null when the frame is empty.
function Get-FrameBounds($data, [int]$fx, [int]$fy, [int]$w, [int]$h) {
    $stride = $data.Stride
    $row = New-Object byte[] ($stride)
    $minY = $h
    $maxY = -1
    for ($y = 0; $y -lt $h; $y++) {
        $ptr = [IntPtr]::Add($data.Scan0, ($fy + $y) * $stride)
        [System.Runtime.InteropServices.Marshal]::Copy($ptr, $row, 0, $stride)
        $alphaBase = $fx * 4 + 3
        for ($x = 0; $x -lt $w; $x++) {
            if ($row[$alphaBase + $x * 4] -gt 8) {
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
                break
            }
        }
    }
    if ($maxY -lt 0) { return $null }
    return @{ minY = $minY; maxY = $maxY }
}

Write-Host "hero      : $Hero  ->  $HeroId"
Write-Host "source    : $inDir"
Write-Host "basic clip: $Basic"
Write-Host "frame     : ${FrameWidth}x${FrameHeight}"
Write-Host ""

$idleBottom = $null
$idleTop = $null

foreach ($clip in $wanted.Keys) {
    $token  = $wanted[$clip]
    $sprite = Find-Sheet $token
    if ($null -eq $sprite) {
        Write-Host ("  {0,-13} SKIPPED - no sheet matching '{1}'" -f $clip, $token)
        continue
    }

    $srcPath = Join-Path $inDir $sprite.image
    if (-not (Test-Path $srcPath)) {
        Write-Host ("  {0,-13} SKIPPED - {1} not on disk" -f $clip, $sprite.image)
        continue
    }

    $cols   = [int]$sprite.sheet.cols
    $rows   = [int]$sprite.sheet.rows
    $frames = [int]$sprite.sheet.frames
    $sfw    = [int]$sprite.frameWidth
    $sfh    = [int]$sprite.frameHeight
    $dstW   = $cols * $FrameWidth
    $dstH   = $rows * $FrameHeight
    $outPng = Join-Path $outDir "${HeroId}_${clip}.png"

    if (-not $PSCmdlet.ShouldProcess($outPng, "write ${dstW}x${dstH} from $($sprite.image)")) { continue }

    $src = [System.Drawing.Bitmap]::FromFile($srcPath)
    try {
        $dst = New-Object System.Drawing.Bitmap $dstW, $dstH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($dst)
        $g.CompositingMode   = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        # Per frame, never whole-sheet: see note 3.
        for ($n = 0; $n -lt $frames; $n++) {
            $sc = $n % $cols
            $sr = [math]::Floor($n / $cols)
            $srcRect = New-Object System.Drawing.Rectangle ($sc * $sfw), ($sr * $sfh), $sfw, $sfh
            $dstRect = New-Object System.Drawing.Rectangle ($sc * $FrameWidth), ($sr * $FrameHeight), $FrameWidth, $FrameHeight
            $g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
        }
        $g.Dispose()
    } finally { $src.Dispose() }

    # One lock serves both the trailing trim (note 7) and the foot measurement.
    $full = New-Object System.Drawing.Rectangle 0, 0, $dstW, $dstH
    $data = $dst.LockBits($full, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $lastReal = $frames - 1
    $bottom = -1
    $top = $FrameHeight
    try {
        while ($lastReal -ge 0) {
            $c = $lastReal % $cols
            $r = [math]::Floor($lastReal / $cols)
            if ($null -ne (Get-FrameBounds $data ($c * $FrameWidth) ($r * $FrameHeight) $FrameWidth $FrameHeight)) { break }
            $lastReal--
        }
        for ($n = 0; $n -le $lastReal; $n++) {
            $c = $n % $cols
            $r = [math]::Floor($n / $cols)
            $b = Get-FrameBounds $data ($c * $FrameWidth) ($r * $FrameHeight) $FrameWidth $FrameHeight
            if ($null -ne $b) {
                if ($b.maxY -gt $bottom) { $bottom = $b.maxY }
                if ($b.minY -lt $top)    { $top    = $b.minY }
            }
        }
    } finally { $dst.UnlockBits($data) }

    $dst.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
    $sizeMb = (Get-Item $outPng).Length / 1MB
    $dst.Dispose()

    if ($clip -eq 'idle') { $idleBottom = $bottom; $idleTop = $top }

    $fps = 30
    if ($sprite.defaultAnimation -and $sprite.defaultAnimation.fps) { $fps = [int]$sprite.defaultAnimation.fps }

    # idle loops at rest at its authored rate. Everything else is an action beat: these clips run
    # ~2.2s at 1x, well past this game's attack pace, so they are sped up. Seeded, not derived.
    $isIdle = ($clip -eq 'idle')
    $anim = [ordered]@{ start = 0; end = $lastReal; fps = $fps; loop = $true }
    if (-not $isIdle) {
        $anim['impactFraction'] = 0.5
        $anim['playbackSpeed']  = 1.30
    }

    $footFrac = [math]::Round((($FrameHeight - 1 - $bottom) / $FrameHeight), 4)
    $bodyFrac = [math]::Round((($bottom - $top) / $FrameHeight), 4)
    $side = [ordered]@{
        generatedBy = 'Sorceress Sprite Analyzer'
        importedBy  = 'Tools/import-sprite-sheets.ps1'
        sprite      = "${HeroId}_${clip}"
        image       = "${HeroId}_${clip}.png"
        frameWidth  = $FrameWidth
        frameHeight = $FrameHeight
        sheet       = [ordered]@{ cols = $cols; rows = $rows; frames = $frames; imageWidth = $dstW; imageHeight = $dstH }
        defaultAnimation = $anim
        sourceSheet   = $sprite.image
        downscaleNote = "Frames resampled ${sfw}x${sfh} -> ${FrameWidth}x${FrameHeight}, one frame at a time. The source sheet was $($sprite.sheet.imageWidth)x$($sprite.sheet.imageHeight), past the 8192 maxTextureSize Kael's sheets import at; Unity would have rescaled it non-uniformly and sheared the frame grid."
        contentBox    = [ordered]@{ topPx = $top; bottomPx = $bottom
                                    footFraction = $footFrac; bodyFraction = $bodyFrac }
    }
    if ($lastReal -ne ($frames - 1)) {
        $side['correctionNote'] = "end corrected from the exported $($frames - 1) to ${lastReal}: frames above that are fully transparent (verified by alpha), and playing through them flashes the unit invisible before the fallback to idle. Same defect Kael's basic sheet had."
    }
    if (-not $isIdle) {
        $side['tuningNote'] = "impactFraction and playbackSpeed are NOT in the export and are not derived from anything - they are seeded starting values, exactly as Kael's were. Eyeball them in Play mode and retune."
    }

    [IO.File]::WriteAllText((Join-Path $outDir "${HeroId}_${clip}.json"),
                            ($side | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))

    $meta = Write-TextureMeta $outPng
    if ($meta -eq 'no-template') {
        Write-Warning "no kael_the_ashbound_*.png.meta to template from - $([IO.Path]::GetFileName($outPng)) will import at Unity's 2048 default and be sheared. See note 5."
    }

    $trim = ""
    if ($lastReal -ne ($frames - 1)) { $trim = " trimmed $($frames - 1)->$lastReal" }
    Write-Host ("  {0,-13} {1,4} frames  {2,5}x{3,-5} {4,6:N1} MB  content y {5}..{6}  meta {7}{8}" -f $clip, ($lastReal + 1), $dstW, $dstH, $sizeMb, $top, $bottom, $meta, $trim)
}

Write-Host ""
if ($null -ne $idleBottom) {
    $foot = [math]::Round((($FrameHeight - 1 - $idleBottom) / $FrameHeight), 3)
    $body = [math]::Round((($idleBottom - $idleTop) / $FrameHeight), 3)
    Write-Host "heroes.json visual block for ${HeroId} - measured off the idle clip:"
    Write-Host "    ""spriteBodyFraction"": $body, ""spriteFootFraction"": $foot"
    Write-Host "  idle content spans y $idleTop..$idleBottom of $FrameHeight. The same measurement on Kael's idle"
    Write-Host "  sheet returns 0.551 / 0.223 against his hand-authored 0.555 / 0.222, which is why these are"
    Write-Host "  trustworthy. bodyFraction is only right if the idle pose keeps the weapon below head height."
}
Write-Host ""
Write-Host "Once this hero looks right in Play mode, get the raw sheets out of Resources/ (note 4):"
Write-Host "  mv Assets/Resources/Sprites/Units/$Hero Assets/_Incoming/$HeroId"
Write-Host "  rm Assets/Resources/Sprites/Units/$Hero.meta"
