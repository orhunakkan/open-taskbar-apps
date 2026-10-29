# Draws the app icon and writes assets\app.ico.
#   .\make-icon.ps1                         build the default design
#   .\make-icon.ps1 -Design bolt            build another design
#   .\make-icon.ps1 -PreviewDir <folder>    write PNG previews of every design instead
param(
    [string]$Design = 'launch',
    [string]$PreviewDir
)
Add-Type -AssemblyName System.Drawing

$IconSizes = 16, 20, 24, 32, 40, 48, 64, 256

function Col([string]$hex, [int]$alpha = 255) {
    [System.Drawing.Color]::FromArgb($alpha, [System.Drawing.ColorTranslator]::FromHtml($hex))
}

function Fill([string]$hex, [int]$alpha = 255) { [System.Drawing.SolidBrush]::new((Col $hex $alpha)) }

function Stroke([string]$hex, [float]$width) {
    $p = [System.Drawing.Pen]::new((Col $hex), $width)
    $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
    $p
}

function Gradient($s, [string]$from, [string]$to) {
    [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new(0, 0), [System.Drawing.PointF]::new($s, $s), (Col $from), (Col $to))
}

function RR([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = [Math]::Max($r * 2, 0.01)
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $p
}

function TilePath($s) {
    $m = [Math]::Round($s * 0.03)
    RR $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.22)
}

# Rounded square background with a diagonal gradient.
function Tile($g, $s, [string]$from, [string]$to) {
    $g.FillPath((Gradient $s $from $to), (TilePath $s))
}

# Points are given as fractions of the icon size: x1, y1, x2, y2, ...
function Points($s, [double[]]$xy) {
    $pts = for ($i = 0; $i -lt $xy.Count; $i += 2) { [System.Drawing.PointF]::new($xy[$i] * $s, $xy[$i + 1] * $s) }
    , [System.Drawing.PointF[]]$pts  # leading comma stops PowerShell unrolling the array
}

function Arrow($g, $brush, $s, $cx, $top, $bottom, $w) {
    $head = $w * 0.85; $shaft = $w * 0.38
    $g.FillPolygon($brush, (Points $s @(
        $cx, $top, ($cx + $w / 2), ($top + $head), ($cx + $shaft / 2), ($top + $head),
        ($cx + $shaft / 2), $bottom, ($cx - $shaft / 2), $bottom, ($cx - $shaft / 2), ($top + $head),
        ($cx - $w / 2), ($top + $head))))
}

$Designs = [ordered]@{
    # A maximized window above a taskbar.
    window   = {
        param($g, $s)
        Tile $g $s '#3B82F6' '#1E40AF'
        $white = Fill '#FFFFFF'
        $wx = $s * 0.17; $wy = $s * 0.16; $ww = $s * 0.66; $wh = $s * 0.46
        $g.DrawPath((Stroke '#FFFFFF' ([Math]::Max(1.0, $s * 0.06))), (RR $wx $wy $ww $wh ($s * 0.05)))
        $g.FillRectangle($white, $wx, $wy, $ww, [Math]::Max(2.0, $s * 0.11))
        $bx = $s * 0.13; $by = $s * 0.71; $bw = $s * 0.74; $bh = $s * 0.15
        $g.FillPath($white, (RR $bx $by $bw $bh ($bh * 0.35)))
        if ($s -ge 24) {
            $dot = $bh * 0.5
            foreach ($i in 0..3) {
                $g.FillRectangle((Fill '#2563EB'), ($bx + $bw * (0.2 + 0.2 * $i) - $dot / 2), ($by + ($bh - $dot) / 2), $dot, $dot)
            }
        }
    }

    # Apps shooting up out of the taskbar.
    launch   = {
        param($g, $s)
        Tile $g $s '#FB923C' '#DB2777'
        $white = Fill '#FFFFFF'
        $g.FillPath($white, (RR ($s * 0.16) ($s * 0.74) ($s * 0.68) ($s * 0.12) ($s * 0.05)))
        if ($s -ge 32) {
            Arrow $g (Fill '#FFFFFF' 170) $s 0.27 0.34 0.65 0.17
            Arrow $g (Fill '#FFFFFF' 170) $s 0.73 0.34 0.65 0.17
            Arrow $g $white $s 0.50 0.13 0.65 0.26
        }
        else {
            Arrow $g $white $s 0.50 0.12 0.65 0.42
        }
    }

    # Full-screen corners around a row of apps.
    maximize = {
        param($g, $s)
        Tile $g $s '#475569' '#0F172A'
        $pen = Stroke '#FFFFFF' ([Math]::Max(1.6, $s * 0.075))
        $a = 0.2; $b = 0.8; $l = 0.2
        foreach ($c in @(
                @($a, ($a + $l), $a, $a, ($a + $l), $a),
                @(($b - $l), $a, $b, $a, $b, ($a + $l)),
                @($b, ($b - $l), $b, $b, ($b - $l), $b),
                @(($a + $l), $b, $a, $b, $a, ($b - $l)))) {
            $g.DrawLines($pen, (Points $s $c))
        }
        if ($s -ge 32) {
            $r = $s * 0.06
            $i = 0
            foreach ($hex in '#60A5FA', '#34D399', '#FBBF24') {
                $cx = $s * (0.37 + 0.13 * $i); $i++
                $g.FillEllipse((Fill $hex), ($cx - $r), ($s * 0.5 - $r), (2 * $r), (2 * $r))
            }
        }
        else {
            $r = $s * 0.11
            $g.FillEllipse((Fill '#60A5FA'), ($s * 0.5 - $r), ($s * 0.5 - $r), (2 * $r), (2 * $r))
        }
    }

    # A fan of windows opening at once.
    fan      = {
        param($g, $s)
        Tile $g $s '#2DD4BF' '#0F766E'
        if ($s -lt 24) {
            $g.FillPath((Fill '#FFFFFF'), (RR ($s * 0.18) ($s * 0.24) ($s * 0.64) ($s * 0.52) ($s * 0.08)))
            $g.FillRectangle((Fill '#0D9488'), ($s * 0.28), ($s * 0.34), ($s * 0.44), [Math]::Max(1.5, $s * 0.08))
            return
        }
        $g.SetClip((TilePath $s))
        foreach ($angle in -20, 20, 0) {
            $state = $g.Save()
            $g.TranslateTransform($s * 0.5, $s * 0.96)
            $g.RotateTransform($angle)
            $cw = $s * 0.46; $ch = $s * 0.36; $x = - $cw / 2; $y = - $s * 0.74
            $alpha = if ($angle -eq 0) { 255 } else { 170 }
            $g.FillPath((Fill '#FFFFFF' $alpha), (RR $x $y $cw $ch ($s * 0.05)))
            $g.FillRectangle((Fill '#0D9488' $alpha), ($x + $s * 0.06), ($y + $s * 0.06), ($cw - $s * 0.12), ($s * 0.05))
            $g.Restore($state)
        }
    }

    # A lightning bolt: everything starts in one click.
    bolt     = {
        param($g, $s)
        Tile $g $s '#A78BFA' '#6D28D9'
        $g.FillPolygon((Fill '#FFFFFF'), (Points $s @(0.50, 0.10, 0.27, 0.56, 0.46, 0.56, 0.38, 0.90, 0.75, 0.40, 0.56, 0.40, 0.71, 0.10)))
    }

    # A play button on a stack of apps.
    play     = {
        param($g, $s)
        Tile $g $s '#4ADE80' '#15803D'
        if ($s -ge 24) {
            $g.FillPath((Fill '#FFFFFF' 90), (RR ($s * 0.32) ($s * 0.14) ($s * 0.52) ($s * 0.42) ($s * 0.06)))
            $g.FillPath((Fill '#FFFFFF' 160), (RR ($s * 0.24) ($s * 0.23) ($s * 0.52) ($s * 0.42) ($s * 0.06)))
            $fx = 0.16; $fy = 0.32; $fw = 0.56; $fh = 0.46
        }
        else {
            $fx = 0.14; $fy = 0.20; $fw = 0.72; $fh = 0.60
        }
        $g.FillPath((Fill '#FFFFFF'), (RR ($s * $fx) ($s * $fy) ($s * $fw) ($s * $fh) ($s * 0.07)))
        $cx = $fx + $fw * 0.53; $cy = $fy + $fh * 0.5; $t = $fh * 0.5
        $g.FillPolygon((Fill '#16A34A'), (Points $s @(($cx - $t * 0.4), ($cy - $t * 0.5), ($cx + $t * 0.55), $cy, ($cx - $t * 0.4), ($cy + $t * 0.5))))
    }

    # No tile: a taskbar pill with an "open" chevron above it.
    dock     = {
        param($g, $s)
        $g.FillPath((Gradient $s '#3B82F6' '#1E40AF'), (RR ($s * 0.04) ($s * 0.56) ($s * 0.92) ($s * 0.36) ($s * 0.14)))
        if ($s -ge 24) {
            foreach ($x in 0.145, 0.415, 0.685) {
                $g.FillPath((Fill '#FFFFFF'), (RR ($s * $x) ($s * 0.655) ($s * 0.17) ($s * 0.17) ($s * 0.045)))
            }
        }
        $g.DrawLines((Stroke '#3B82F6' ([Math]::Max(2.0, $s * 0.12))), (Points $s @(0.24, 0.42, 0.5, 0.16, 0.76, 0.42)))
    }
}

function Render([string]$name, [int]$s) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    & $Designs[$name] $g $s
    $g.Dispose()
    $bmp
}

function PngBytes($bmp) {
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    , $ms.ToArray()
}

if ($PreviewDir) {
    New-Item -ItemType Directory -Force $PreviewDir | Out-Null
    foreach ($name in $Designs.Keys) {
        foreach ($s in 16, 24, 32, 48, 64, 256) {
            $bmp = Render $name $s
            $bmp.Save((Join-Path $PreviewDir "$name-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)
            $bmp.Dispose()
        }
    }
    Write-Host "Wrote previews of $($Designs.Count) designs to $PreviewDir"
    return
}

if (-not $Designs.Contains($Design)) { throw "Unknown design '$Design'. Choose one of: $($Designs.Keys -join ', ')" }

$images = foreach ($s in $IconSizes) { $bmp = Render $Design $s; , (PngBytes $bmp); $bmp.Dispose() }

# ICO container holding one PNG per size.
$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$IconSizes.Count)
$offset = 6 + 16 * $IconSizes.Count
for ($i = 0; $i -lt $IconSizes.Count; $i++) {
    $s = $IconSizes[$i]; $data = $images[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $images) { $w.Write($data) }
$w.Flush()

$dest = Join-Path $PSScriptRoot '..\assets\app.ico'
New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
[System.IO.File]::WriteAllBytes($dest, $out.ToArray())
Write-Host "Wrote $dest ($Design)"
