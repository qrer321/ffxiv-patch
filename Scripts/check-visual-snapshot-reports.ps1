param(
    [Parameter(Mandatory = $true)]
    [string]$SnapshotDir,

    [int]$MaxSevereOverlap = 3,

    [int]$MinSevereGap = -2,

    [double]$MaxFringeRatio = 1.05
)

$ErrorActionPreference = "Stop"

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
    foreach ($row in $Rows) {
        $overlap = To-Int $row.overlap
        $minGap = To-Int $row.min_gap 999
        $fringe = To-Double $row.fringe_ratio

        if ($overlap -gt $MaxSevereOverlap -or $minGap -lt $MinSevereGap -or $fringe -gt $MaxFringeRatio) {
            $severe += $row
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

    Write-Host ("  OK   {0}: rows={1}, overlapRows={2}, negativeGapRows={3}, maxOverlap={4}, minGap={5}" -f `
        $Name,
        $Rows.Count,
        $overlapRows.Count,
        $negativeGapRows.Count,
        $maxOverlap,
        $minGap)
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
