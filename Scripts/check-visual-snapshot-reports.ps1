param(
    [Parameter(Mandatory = $true)]
    [string]$SnapshotDir,

    [int]$MaxSevereOverlap = 3,

    [int]$MinSevereGap = -2,

    [double]$MaxFringeRatio = 1.05,

    [int]$MinInkPixels = 8,

    [switch]$FailOnAnyRawOverlap
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (!(Test-Path -LiteralPath $SnapshotDir)) {
    throw "Snapshot directory was not found: $SnapshotDir"
}

function Import-Report {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $path = Join-Path $SnapshotDir $Name
    if (!(Test-Path -LiteralPath $path)) {
        throw "Visual snapshot report was not found: $path"
    }

    return @(Import-Csv -LiteralPath $path -Delimiter "`t" -Encoding UTF8)
}

function To-Int {
    param([string]$Value, [int]$Default = 0)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Default
    }

    return [int]$Value
}

function To-Double {
    param([string]$Value, [double]$Default = 0.0)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Default
    }

    return [double]::Parse($Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Require-ReportId {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Rows,

        [Parameter(Mandatory = $true)]
        [string]$Id
    )

    if (!($Rows | Where-Object { $_.id -eq $Id } | Select-Object -First 1)) {
        throw "Required visual snapshot id was not found: $Id"
    }
}

function Require-ReportIdLike {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Rows,

        [Parameter(Mandatory = $true)]
        [string]$Pattern
    )

    if (!($Rows | Where-Object { $_.id -like $Pattern } | Select-Object -First 1)) {
        throw "Required visual snapshot id pattern was not found: $Pattern"
    }
}

function Test-SnapshotPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Id,

        [Parameter(Mandatory = $true)]
        [string]$Column,

        [Parameter(Mandatory = $true)]
        [object]$Row,

        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    if (!(Test-Path -LiteralPath $Path)) {
        throw "$Name row '$Id' references missing ${Column}: $Path"
    }

    $item = Get-Item -LiteralPath $Path
    if ($item.Length -le 0) {
        throw "$Name row '$Id' references empty ${Column}: $Path"
    }

    $bitmap = $null
    try {
        $bitmap = [System.Drawing.Bitmap]::FromFile($item.FullName)
        if ($bitmap.Width -le 0 -or $bitmap.Height -le 0) {
            throw "$Name row '$Id' references invalid ${Column} dimensions: $Path"
        }

        $expectedWidth = To-Int $Row.width
        $expectedHeight = To-Int $Row.height
        if ($expectedWidth -gt 0 -and $bitmap.Width -lt $expectedWidth) {
            throw "$Name row '$Id' ${Column} width is smaller than report width: image=$($bitmap.Width), report=$expectedWidth, path=$Path"
        }

        if ($expectedHeight -gt 0 -and $bitmap.Height -lt $expectedHeight) {
            throw "$Name row '$Id' ${Column} height is smaller than report height: image=$($bitmap.Height), report=$expectedHeight, path=$Path"
        }

        $background = $bitmap.GetPixel(0, 0)
        $inkPixels = 0
        for ($y = 0; $y -lt $bitmap.Height; $y++) {
            for ($x = 0; $x -lt $bitmap.Width; $x++) {
                $pixel = $bitmap.GetPixel($x, $y)
                $delta =
                    [Math]::Abs([int]$pixel.A - [int]$background.A) +
                    [Math]::Abs([int]$pixel.R - [int]$background.R) +
                    [Math]::Abs([int]$pixel.G - [int]$background.G) +
                    [Math]::Abs([int]$pixel.B - [int]$background.B)
                if ($delta -gt 2) {
                    $inkPixels++
                    if ($inkPixels -ge $MinInkPixels) {
                        break
                    }
                }
            }

            if ($inkPixels -ge $MinInkPixels) {
                break
            }
        }

        if ($inkPixels -lt $MinInkPixels) {
            throw "$Name row '$Id' ${Column} has too few non-background pixels: ink=$inkPixels, required=$MinInkPixels, path=$Path"
        }
    }
    finally {
        if ($bitmap -ne $null) {
            $bitmap.Dispose()
        }
    }

    return $true
}

function Test-ReportRows {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [object[]]$Rows
    )

    if ($Rows.Count -eq 0) {
        throw "$Name is empty"
    }

    $severe = @()
    $pngCount = 0
    foreach ($row in $Rows) {
        $overlap = To-Int $row.overlap
        $minGap = To-Int $row.min_gap 999
        $fringe = To-Double $row.fringe_ratio

        if ($overlap -gt $MaxSevereOverlap -or $minGap -lt $MinSevereGap -or $fringe -gt $MaxFringeRatio) {
            $severe += $row
        }

        if (Test-SnapshotPath $Name $row.id "png" $row $row.png) {
            $pngCount++
        }

        if (Get-Member -InputObject $row -Name "png2" -MemberType NoteProperty -ErrorAction SilentlyContinue) {
            if (Test-SnapshotPath $Name $row.id "png2" $row $row.png2) {
                $pngCount++
            }
        }
    }

    if ($severe.Count -gt 0) {
        $details = $severe |
            Select-Object -First 10 id, font, phrase, min_gap, overlap, fringe_ratio, png |
            Format-List |
            Out-String
        throw "$Name has severe visual report anomalies:`n$details"
    }

    $overlapRows = @($Rows | Where-Object { (To-Int $_.overlap) -gt 0 })
    $negativeGapRows = @($Rows | Where-Object { (To-Int $_.min_gap 999) -lt 0 })
    $maxOverlap = ($Rows | ForEach-Object { To-Int $_.overlap } | Measure-Object -Maximum).Maximum
    $minGap = ($Rows | ForEach-Object { To-Int $_.min_gap 999 } | Measure-Object -Minimum).Minimum

    $rawAnomalies = @($Rows | Where-Object { (To-Int $_.overlap) -gt 0 -or (To-Int $_.min_gap 999) -lt 0 })
    if ($FailOnAnyRawOverlap -and $rawAnomalies.Count -gt 0) {
        $details = $rawAnomalies |
            Select-Object -First 10 id, font, phrase, min_gap, min_pair, overlap, png |
            Format-List |
            Out-String
        throw "$Name has raw visual overlap/gap anomalies under strict mode:`n$details"
    }

    if ($pngCount -lt $Rows.Count) {
        throw "$Name has fewer PNG references than rows: pngs=$pngCount rows=$($Rows.Count)"
    }

    Write-Host ("  OK   {0}: rows={1}, pngs={2}, overlapRows={3}, negativeGapRows={4}, maxOverlap={5}, minGap={6}" -f `
        $Name,
        $Rows.Count,
        $pngCount,
        $overlapRows.Count,
        $negativeGapRows.Count,
        $maxOverlap,
        $minGap)

    if ($rawAnomalies.Count -gt 0) {
        $summary = $rawAnomalies |
            Select-Object -First 5 id, font, phrase, min_gap, min_pair, overlap |
            Format-Table -AutoSize |
            Out-String
        Write-Host "  INFO $Name bounded raw visual anomalies:"
        $summary.TrimEnd() -split "`r?`n" | ForEach-Object { Write-Host "       $_" }
    }
}

$lobby = Import-Report "lobby-render-report.tsv"
$largeUi = Import-Report "large-ui-render-report.tsv"
$pvp = Import-Report "pvp-profile-render-report.tsv"

Require-ReportId $lobby "settings-axis12-ui150-result"
Require-ReportId $lobby "settings-axis14-ui150-fhd"
Require-ReportId $lobby "settings-axis14-ui200-result"
Require-ReportId $lobby "settings-axis14-ui300-result"
Require-ReportId $lobby "character-trump23-race"
Require-ReportId $lobby "character-trump34-race"
Require-ReportId $lobby "dc-axis12-elemental"
Require-ReportId $lobby "dc-axis14-pandaemonium"

Require-ReportIdLike $largeUi "large-ui-00-*"
Require-ReportIdLike $largeUi "large-ui-01-*"
Require-ReportIdLike $largeUi "large-ui-02-120.00*"
Require-ReportIdLike $largeUi "large-ui-03-1.50*"
Require-ReportIdLike $largeUi "large-ui-06-*"
Require-ReportIdLike $largeUi "large-ui-07-*"

Require-ReportIdLike $pvp "pvp-profile-02-*"
Require-ReportIdLike $pvp "pvp-profile-00-PvP*"
Require-ReportIdLike $pvp "pvp-profile-05-PvP*"

Test-ReportRows "lobby-render-report.tsv" $lobby
Test-ReportRows "large-ui-render-report.tsv" $largeUi
Test-ReportRows "pvp-profile-render-report.tsv" $pvp

Write-Host "RESULT: PASS"
