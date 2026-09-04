[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $InputPath,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [ValidateSet('Key', 'Opaque16x9', 'Validate')]
    [string] $Mode = 'Key',

    [ValidatePattern('^#[0-9A-Fa-f]{6}$')]
    [string] $Chroma = '#00ff00',

    [ValidateRange(0, 255)]
    [int] $TransparentDistance = 20,

    [ValidateRange(1, 442)]
    [int] $OpaqueDistance = 125,

    [ValidateRange(0, 128)]
    [int] $Padding = 10,

    [switch] $SquareCanvas,

    [ValidateRange(16, 8192)]
    [int] $Width = 1920,

    [ValidateRange(16, 8192)]
    [int] $Height = 1080
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

function Convert-HexToRgb {
    param([string] $Hex)

    return @(
        [Convert]::ToInt32($Hex.Substring(1, 2), 16),
        [Convert]::ToInt32($Hex.Substring(3, 2), 16),
        [Convert]::ToInt32($Hex.Substring(5, 2), 16)
    )
}

function Copy-ToArgbBitmap {
    param([System.Drawing.Image] $Image)

    $copy = [System.Drawing.Bitmap]::new(
        $Image.Width,
        $Image.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($copy)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.DrawImage($Image, 0, 0, $Image.Width, $Image.Height)
    }
    finally {
        $graphics.Dispose()
    }

    return $copy
}

function Invoke-ChromaKey {
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [int[]] $KeyRgb,
        [int] $InnerDistance,
        [int] $OuterDistance
    )

    if ($OuterDistance -le $InnerDistance) {
        throw 'OpaqueDistance must be greater than TransparentDistance.'
    }

    $bounds = [System.Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    $data = $Bitmap.LockBits(
        $bounds,
        [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $stride = [Math]::Abs($data.Stride)
        $pixels = [byte[]]::new($stride * $Bitmap.Height)
        [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)

        $minX = $Bitmap.Width
        $minY = $Bitmap.Height
        $maxX = -1
        $maxY = -1
        $keyR = $KeyRgb[0]
        $keyG = $KeyRgb[1]
        $keyB = $KeyRgb[2]
        $distanceRange = [double]($OuterDistance - $InnerDistance)

        for ($y = 0; $y -lt $Bitmap.Height; $y++) {
            $row = $y * $stride
            for ($x = 0; $x -lt $Bitmap.Width; $x++) {
                $offset = $row + ($x * 4)
                $blue = [double]$pixels[$offset]
                $green = [double]$pixels[$offset + 1]
                $red = [double]$pixels[$offset + 2]
                $sourceAlpha = [double]$pixels[$offset + 3] / 255.0

                $deltaR = $red - $keyR
                $deltaG = $green - $keyG
                $deltaB = $blue - $keyB
                $distance = [Math]::Sqrt(
                    ($deltaR * $deltaR) + ($deltaG * $deltaG) + ($deltaB * $deltaB))

                $alpha = ($distance - $InnerDistance) / $distanceRange
                $alpha = [Math]::Max(0.0, [Math]::Min(1.0, $alpha))
                $alpha = $alpha * $alpha * (3.0 - (2.0 * $alpha))

                # Distance alone can classify a dark antialiased outline mixed with green as
                # opaque. Refine the matte using key-channel dominance while retaining the
                # distance-derived soft edge required for neutral and bright subject colours.
                if (($keyG -gt $keyR) -and ($keyG -gt $keyB)) {
                    $keyDominance = $green - [Math]::Max($red, $blue)
                }
                else {
                    $keyDominance = [Math]::Min($red, $blue) - $green
                }
                $dominanceAlpha = (190.0 - $keyDominance) / 182.0
                $dominanceAlpha = [Math]::Max(0.0, [Math]::Min(1.0, $dominanceAlpha))
                $dominanceAlpha = $dominanceAlpha * $dominanceAlpha * (3.0 - (2.0 * $dominanceAlpha))
                $alpha = [Math]::Min($alpha, $dominanceAlpha)
                $alpha *= $sourceAlpha

                # Generated chroma fields can contain tiny compression-like colour noise.
                # Removing imperceptible matte coverage prevents straight-RGB recovery from
                # amplifying that noise into bright opposite-colour speckles on dark UI.
                if ($alpha -le 0.06) {
                    $pixels[$offset] = 0
                    $pixels[$offset + 1] = 0
                    $pixels[$offset + 2] = 0
                    $pixels[$offset + 3] = 0
                    continue
                }

                if (($alpha -ge 0.25) -and ($alpha -lt 0.999)) {
                    # Recover straight RGB from a subject pixel composited over the known key.
                    $inverseAlpha = 1.0 - $alpha
                    $red = ($red - ($inverseAlpha * $keyR)) / $alpha
                    $green = ($green - ($inverseAlpha * $keyG)) / $alpha
                    $blue = ($blue - ($inverseAlpha * $keyB)) / $alpha

                }

                # Suppress the key channel's residual dominance on every translucent pixel.
                # Very-low-alpha pixels deliberately keep their observed RGB before despill;
                # deconvolving them is numerically unstable and creates bright edge flecks.
                if ($alpha -lt 0.999) {
                    if (($keyG -gt $keyR) -and ($keyG -gt $keyB)) {
                        $green = [Math]::Min($green, [Math]::Max($red, $blue) + 6.0)
                    }
                    elseif (($keyR -gt $keyG) -and ($keyB -gt $keyG)) {
                        $edgeNeutral = $green + 6.0
                        $red = [Math]::Min($red, $edgeNeutral)
                        $blue = [Math]::Min($blue, $edgeNeutral)
                    }
                }

                $pixels[$offset] = [byte][Math]::Round([Math]::Max(0.0, [Math]::Min(255.0, $blue)))
                $pixels[$offset + 1] = [byte][Math]::Round([Math]::Max(0.0, [Math]::Min(255.0, $green)))
                $pixels[$offset + 2] = [byte][Math]::Round([Math]::Max(0.0, [Math]::Min(255.0, $red)))
                $pixels[$offset + 3] = [byte][Math]::Round($alpha * 255.0)

                if ($pixels[$offset + 3] -gt 4) {
                    $minX = [Math]::Min($minX, $x)
                    $minY = [Math]::Min($minY, $y)
                    $maxX = [Math]::Max($maxX, $x)
                    $maxY = [Math]::Max($maxY, $y)
                }
            }
        }

        [Runtime.InteropServices.Marshal]::Copy($pixels, 0, $data.Scan0, $pixels.Length)
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    if ($maxX -lt 0) {
        throw 'No foreground pixels survived chroma keying.'
    }

    return [System.Drawing.Rectangle]::FromLTRB(
        [Math]::Max(0, $minX - $Padding),
        [Math]::Max(0, $minY - $Padding),
        [Math]::Min($Bitmap.Width, $maxX + $Padding + 1),
        [Math]::Min($Bitmap.Height, $maxY + $Padding + 1))
}

function Save-CroppedBitmap {
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [System.Drawing.Rectangle] $CropBounds,
        [string] $Destination,
        [bool] $UseSquareCanvas
    )

    $outputWidth = $CropBounds.Width
    $outputHeight = $CropBounds.Height
    if ($UseSquareCanvas) {
        $squareSize = [Math]::Max($outputWidth, $outputHeight)
        $outputWidth = $squareSize
        $outputHeight = $squareSize
    }

    $cropped = [System.Drawing.Bitmap]::new(
        $outputWidth,
        $outputHeight,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($cropped)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $destinationX = [int][Math]::Floor(($outputWidth - $CropBounds.Width) / 2.0)
        $destinationY = [int][Math]::Floor(($outputHeight - $CropBounds.Height) / 2.0)
        $graphics.DrawImage(
            $Bitmap,
            [System.Drawing.Rectangle]::new(
                $destinationX,
                $destinationY,
                $CropBounds.Width,
                $CropBounds.Height),
            $CropBounds,
            [System.Drawing.GraphicsUnit]::Pixel)
        $cropped.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $cropped.Dispose()
    }
}

function Save-OpaqueCover {
    param(
        [System.Drawing.Image] $Image,
        [string] $Destination,
        [int] $TargetWidth,
        [int] $TargetHeight
    )

    $output = [System.Drawing.Bitmap]::new(
        $TargetWidth,
        $TargetHeight,
        [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($output)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(255, 15, 10, 45))
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

        $scale = [Math]::Max(
            [double]$TargetWidth / $Image.Width,
            [double]$TargetHeight / $Image.Height)
        $drawWidth = [int][Math]::Ceiling($Image.Width * $scale)
        $drawHeight = [int][Math]::Ceiling($Image.Height * $scale)
        $drawX = [int][Math]::Floor(($TargetWidth - $drawWidth) / 2.0)
        $drawY = [int][Math]::Floor(($TargetHeight - $drawHeight) / 2.0)
        $graphics.DrawImage($Image, $drawX, $drawY, $drawWidth, $drawHeight)
        $output.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $output.Dispose()
    }
}

function Test-KeyedBitmap {
    param(
        [string] $Path,
        [int[]] $KeyRgb
    )

    $loaded = [System.Drawing.Image]::FromFile($Path)
    try {
        $bitmap = Copy-ToArgbBitmap -Image $loaded
    }
    finally {
        $loaded.Dispose()
    }

    try {
        $bounds = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
        $data = $bitmap.LockBits(
            $bounds,
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $stride = [Math]::Abs($data.Stride)
            $pixels = [byte[]]::new($stride * $bitmap.Height)
            [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
            [long]$transparent = 0
            [long]$translucent = 0
            [long]$opaque = 0
            [long]$fringe = 0

            for ($y = 0; $y -lt $bitmap.Height; $y++) {
                $row = $y * $stride
                for ($x = 0; $x -lt $bitmap.Width; $x++) {
                    $offset = $row + ($x * 4)
                    $blue = [int]$pixels[$offset]
                    $green = [int]$pixels[$offset + 1]
                    $red = [int]$pixels[$offset + 2]
                    $alpha = [int]$pixels[$offset + 3]
                    if ($alpha -eq 0) {
                        $transparent++
                    }
                    elseif ($alpha -eq 255) {
                        $opaque++
                    }
                    else {
                        $translucent++
                    }

                    if ($alpha -gt 0) {
                        $isEdge = ($x -eq 0) -or ($y -eq 0) -or `
                            ($x -eq ($bitmap.Width - 1)) -or ($y -eq ($bitmap.Height - 1))
                        if (-not $isEdge) {
                            $leftAlpha = [int]$pixels[$offset - 1]
                            $rightAlpha = [int]$pixels[$offset + 7]
                            $upAlpha = [int]$pixels[$offset - $stride + 3]
                            $downAlpha = [int]$pixels[$offset + $stride + 3]
                            $isEdge = ($leftAlpha -eq 0) -or ($rightAlpha -eq 0) -or `
                                ($upAlpha -eq 0) -or ($downAlpha -eq 0)
                        }

                        if ($isEdge) {
                            if (($KeyRgb[1] -gt $KeyRgb[0]) -and ($KeyRgb[1] -gt $KeyRgb[2])) {
                                if (($green - [Math]::Max($red, $blue)) -gt 24) { $fringe++ }
                            }
                            elseif (($KeyRgb[0] -gt $KeyRgb[1]) -and ($KeyRgb[2] -gt $KeyRgb[1])) {
                                if (([Math]::Min($red, $blue) - $green) -gt 24) { $fringe++ }
                            }
                        }
                    }
                }
            }

            $edgeSamples = [Math]::Max(1, $translucent)
            return [pscustomobject]@{
                Path = (Resolve-Path $Path).Path
                Width = $bitmap.Width
                Height = $bitmap.Height
                TransparentPixels = $transparent
                TranslucentPixels = $translucent
                OpaquePixels = $opaque
                SuspectFringePixels = $fringe
                SuspectFringePercent = [Math]::Round((100.0 * $fringe) / $edgeSamples, 4)
                Passed = ($transparent -gt 0 -and $opaque -gt 0 -and $fringe -eq 0)
            }
        }
        finally {
            $bitmap.UnlockBits($data)
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force $outputDirectory | Out-Null
}
$absoluteOutput = [System.IO.Path]::GetFullPath($OutputPath)
$keyRgb = Convert-HexToRgb -Hex $Chroma

if ($Mode -eq 'Validate') {
    Test-KeyedBitmap -Path $resolvedInput -KeyRgb $keyRgb | ConvertTo-Json -Depth 3
    return
}

$source = [System.Drawing.Image]::FromFile($resolvedInput)
try {
    if ($Mode -eq 'Opaque16x9') {
        Save-OpaqueCover -Image $source -Destination $absoluteOutput -TargetWidth $Width -TargetHeight $Height
        [pscustomobject]@{
            Path = $absoluteOutput
            Width = $Width
            Height = $Height
            Mode = $Mode
        } | ConvertTo-Json
        return
    }

    $working = Copy-ToArgbBitmap -Image $source
}
finally {
    $source.Dispose()
}

try {
    $crop = Invoke-ChromaKey `
        -Bitmap $working `
        -KeyRgb $keyRgb `
        -InnerDistance $TransparentDistance `
        -OuterDistance $OpaqueDistance
    Save-CroppedBitmap `
        -Bitmap $working `
        -CropBounds $crop `
        -Destination $absoluteOutput `
        -UseSquareCanvas $SquareCanvas.IsPresent
}
finally {
    $working.Dispose()
}

Test-KeyedBitmap -Path $absoluteOutput -KeyRgb $keyRgb | ConvertTo-Json -Depth 3
