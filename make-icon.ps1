Add-Type -AssemblyName System.Drawing

function Make-Icon([int]$sz, [string]$outPath) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $sz / 32.0

    # ── colours ──────────────────────────────────────────────────────────────
    $cTopCap  = [System.Drawing.Color]::FromArgb(255,  72, 160, 230)
    $cBotCap  = [System.Drawing.Color]::FromArgb(255,  14,  72, 130)
    $cBodyL   = [System.Drawing.Color]::FromArgb(255,  50, 140, 210)
    $cBodyR   = [System.Drawing.Color]::FromArgb(255,  12,  80, 160)
    $cOutline = [System.Drawing.Color]::FromArgb(180,  10,  55, 110)
    $cArrow   = [System.Drawing.Color]::FromArgb(255,  30, 185,  70)
    $cWhite   = [System.Drawing.Color]::White

    # ── cylinder geometry ────────────────────────────────────────────────────
    $cx = [float](3  * $s)
    $cy = [float](9  * $s)
    $cw = [float](20 * $s)
    $ch = [float](20 * $s)
    $eh = [float](6  * $s)

    $bodyY = $cy + $eh / 2.0
    $bodyH = $ch - $eh

    $bodyRect = [System.Drawing.RectangleF]::new($cx, $bodyY, $cw, $bodyH)

    # gradient body
    $gb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $bodyRect, $cBodyL, $cBodyR,
        [System.Drawing.Drawing2D.LinearGradientMode]::Horizontal)
    $g.FillRectangle($gb, $bodyRect)

    # bottom cap
    $botRect = [System.Drawing.RectangleF]::new($cx, $cy + $ch - $eh, $cw, $eh)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($cBotCap)), $botRect)

    # top cap
    $topRect = [System.Drawing.RectangleF]::new($cx, $cy, $cw, $eh)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($cTopCap)), $topRect)

    # row stripes
    for ($row = 1; $row -le 2; $row++) {
        $ry = $bodyY + $bodyH * $row / 3.0
        $sp = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(50, 255, 255, 255), ([float]($s)))
        $g.DrawLine($sp, $cx, $ry, $cx + $cw, $ry)
        $sp.Dispose()
    }

    # outline
    $op = New-Object System.Drawing.Pen($cOutline, ([float]($s * 0.8)))
    $g.DrawRectangle($op, $cx, $bodyY, $cw, $bodyH)
    $g.DrawEllipse($op, $botRect)
    $g.DrawEllipse($op, $topRect)
    $op.Dispose()

    # ── restore-arrow badge (top-right corner) ───────────────────────────────
    $bx  = [float](17 * $s)
    $by  = [float](0  * $s)
    $bsz = [float](15 * $s)

    # white circle background with subtle shadow
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 0, 0, 0))
    $g.FillEllipse($shadowBrush, $bx + 1, $by + 1, $bsz, $bsz)
    $shadowBrush.Dispose()

    $g.FillEllipse((New-Object System.Drawing.SolidBrush($cWhite)), $bx, $by, $bsz, $bsz)

    # green arc
    $strokeW  = [float]($sz / 10.5)
    $arcInset = [float]($bsz * 0.19)
    $arcRect  = [System.Drawing.RectangleF]::new($bx + $arcInset, $by + $arcInset,
                                                  $bsz - 2*$arcInset, $bsz - 2*$arcInset)
    $arcPen = New-Object System.Drawing.Pen($cArrow, $strokeW)
    $arcPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($arcPen, $arcRect, -60.0, 290.0)
    $arcPen.Dispose()

    # arrowhead at arc end (240 deg from centre)
    $acx    = $bx + $bsz / 2.0
    $acy    = $by + $bsz / 2.0
    $ar     = ($bsz / 2.0) - $arcInset
    $endRad = ((-60 + 290) * [Math]::PI / 180.0)   # 230 deg
    $ex     = $acx + $ar * [Math]::Cos($endRad)
    $ey     = $acy + $ar * [Math]::Sin($endRad)

    $hl = [float]($sz / 9.0)
    # perpendicular & radial offsets for a tidy triangle
    $normX =  [Math]::Sin($endRad)
    $normY = -[Math]::Cos($endRad)
    $tipPts = @(
        [System.Drawing.PointF]::new($ex + $normX * $hl,        $ey + $normY * $hl),
        [System.Drawing.PointF]::new($ex - $normX * $hl,        $ey - $normY * $hl),
        [System.Drawing.PointF]::new($ex + [Math]::Cos($endRad) * $hl * 1.3,
                                     $ey + [Math]::Sin($endRad) * $hl * 1.3)
    )
    $g.FillPolygon((New-Object System.Drawing.SolidBrush($cArrow)), $tipPts)

    # subtle badge outline
    $badgePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 100, 100, 100), ([float](0.6 * $s)))
    $g.DrawEllipse($badgePen, $bx, $by, $bsz, $bsz)
    $badgePen.Dispose()

    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Saved $outPath ($sz x $sz)"
}

$dir = 'D:\Project\SsmsQuickRestore\src\Resources'
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

Make-Icon 16 "$dir\SsmsRestoreDrop_16.png"
Make-Icon 32 "$dir\SsmsRestoreDrop_32.png"
Make-Icon 64 "$dir\SsmsRestoreDrop_64.png"

Write-Host "Done."
