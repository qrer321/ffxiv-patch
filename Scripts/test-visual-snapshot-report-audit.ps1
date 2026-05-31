param(
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot (".tmp\visual-snapshot-audit-selftest\{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$checkScript = Join-Path $PSScriptRoot "check-visual-snapshot-reports.ps1"

function New-TestPng {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [int]$Width = 20,

        [int]$Height = 12,

        [switch]$Blank
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(255, 24, 24, 24))
            if (!$Blank) {
                $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 230, 230, 230))
                try {
                    $graphics.FillRectangle($brush, 3, 3, 8, 4)
                    $graphics.FillRectangle($brush, 12, 5, 4, 3)
                }
                finally {
                    $brush.Dispose()
                }
            }
        }
        finally {
            $graphics.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }
}

function New-Row {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Id,

        [Parameter(Mandatory = $true)]
        [string]$Png,

        [string]$Kind = "snapshot",

        [string]$Png2 = "",

        [int]$Overlap = 0,

        [int]$MinGap = 0
    )

    [pscustomobject]@{
        kind = $Kind
        id = $Id
        font = "common/font/AXIS_12.fdt"
        phrase = $Id
        scale = "1"
        width = "10"
        height = "8"
        visible = "24"
        fringe_ratio = "0.5"
        layout_width = "10"
        min_gap = $MinGap.ToString([Globalization.CultureInfo]::InvariantCulture)
        min_pair = "n/a"
        overlap = $Overlap.ToString([Globalization.CultureInfo]::InvariantCulture)
        diff = ""
        png = $Png
        png2 = $Png2
    }
}

function Write-Tsv {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object[]]$Rows
    )

    $Rows | Export-Csv -LiteralPath $Path -Delimiter "`t" -NoTypeInformation -Encoding UTF8
}

function New-VisualReportSet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory,

        [string]$OmitLobbyId,

        [switch]$BlankLargeUiPng,

        [switch]$SeverePvpOverlap
    )

    New-Item -ItemType Directory -Force -Path $Directory | Out-Null

    $lobbyIds = @(
        "settings-axis12-ui150-result",
        "settings-axis14-ui150-fhd",
        "settings-axis14-ui200-result",
        "settings-axis14-ui300-result",
        "character-trump23-race",
        "character-trump34-race",
        "dc-axis12-elemental",
        "dc-axis14-pandaemonium"
    ) | Where-Object { $_ -ne $OmitLobbyId }

    $largeUiIds = @(
        "large-ui-00-instant",
        "large-ui-01-second",
        "large-ui-02-120.00-second",
        "large-ui-03-1.50-second",
        "large-ui-06-system",
        "large-ui-07-crystalline"
    )

    $pvpIds = @(
        "pvp-profile-02-crystalline",
        "pvp-profile-00-PvP-profile",
        "pvp-profile-05-PvP-action"
    )

    $lobbyRows = @()
    foreach ($id in $lobbyIds) {
        $png = Join-Path $Directory ("lobby-$id.png")
        New-TestPng $png
        $lobbyRows += New-Row -Id $id -Png $png
    }

    $largeRows = @()
    foreach ($id in $largeUiIds) {
        $png = Join-Path $Directory ("large-$id.png")
        New-TestPng $png -Blank:($BlankLargeUiPng -and $id -eq "large-ui-00-instant")
        $row = New-Row -Id $id -Png $png
        $largeRows += [pscustomobject]@{
            id = $row.id
            font = $row.font
            phrase = $row.phrase
            width = $row.width
            height = $row.height
            visible = $row.visible
            fringe_ratio = $row.fringe_ratio
            layout_width = $row.layout_width
            min_gap = $row.min_gap
            min_pair = $row.min_pair
            overlap = $row.overlap
            png = $row.png
        }
    }

    $pvpRows = @()
    foreach ($id in $pvpIds) {
        $png = Join-Path $Directory ("pvp-$id.png")
        New-TestPng $png
        $overlap = 0
        if ($SeverePvpOverlap -and $id -eq "pvp-profile-02-crystalline") {
            $overlap = 4
        }

        $row = New-Row -Id $id -Png $png -Overlap $overlap
        $pvpRows += [pscustomobject]@{
            id = $row.id
            font = $row.font
            phrase = $row.phrase
            width = $row.width
            height = $row.height
            visible = $row.visible
            fringe_ratio = $row.fringe_ratio
            layout_width = $row.layout_width
            min_gap = $row.min_gap
            min_pair = $row.min_pair
            overlap = $row.overlap
            png = $row.png
        }
    }

    Write-Tsv (Join-Path $Directory "lobby-render-report.tsv") $lobbyRows
    Write-Tsv (Join-Path $Directory "large-ui-render-report.tsv") $largeRows
    Write-Tsv (Join-Path $Directory "pvp-profile-render-report.tsv") $pvpRows
}

function Invoke-AuditExpectPass {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory
    )

    & $checkScript -SnapshotDir $Directory | Out-Host
}

function Invoke-AuditExpectFail {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    try {
        & $checkScript -SnapshotDir $Directory | Out-Host
    }
    catch {
        Write-Host "  OK   expected failure for ${Label}: $($_.Exception.Message)"
        return
    }

    throw "Visual snapshot audit unexpectedly passed for ${Label}: $Directory"
}

$validDir = Join-Path $OutputRoot "valid"
New-VisualReportSet $validDir
Invoke-AuditExpectPass $validDir

$missingIdDir = Join-Path $OutputRoot "missing-required-id"
New-VisualReportSet $missingIdDir -OmitLobbyId "settings-axis12-ui150-result"
Invoke-AuditExpectFail $missingIdDir "missing required id"

$blankPngDir = Join-Path $OutputRoot "blank-png"
New-VisualReportSet $blankPngDir -BlankLargeUiPng
Invoke-AuditExpectFail $blankPngDir "blank PNG"

$severeOverlapDir = Join-Path $OutputRoot "severe-overlap"
New-VisualReportSet $severeOverlapDir -SeverePvpOverlap
Invoke-AuditExpectFail $severeOverlapDir "severe overlap"

Write-Host "RESULT: PASS"
Write-Host "  artifacts: $OutputRoot"
