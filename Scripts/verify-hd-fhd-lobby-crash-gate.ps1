param(
    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter(Mandatory = $true)]
    [string]$Global,

    [string]$Korea,

    [string]$TargetLanguage = "ja",

    [string]$FontPackDir,

    [string[]]$KnownBadOutput,

    [string]$Checks = "lobby-multitexture-font-set,lobby-source-cell-conflicts,lobby-render-snapshots",

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$verifyScript = Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"

if ([string]::IsNullOrWhiteSpace($FontPackDir)) {
    $FontPackDir = Join-Path $repoRoot "FFXIVPatchGenerator\bin\$Configuration"
}

if ($KnownBadOutput -eq $null -or $KnownBadOutput.Length -eq 0) {
    $KnownBadOutput = @(
        (Join-Path $repoRoot ".tmp\release-hd-crash-head-ja"),
        (Join-Path $repoRoot ".tmp\release-hd-crash-r34-ja"),
        (Join-Path $repoRoot ".tmp\hd-fhd-current-main-ja")
    )
}

function Invoke-RouteVerifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $arguments = @(
        "-Output", $OutputPath,
        "-Global", $Global,
        "-TargetLanguage", $TargetLanguage,
        "-FontPackDir", $FontPackDir,
        "-NoGlyphDump",
        "-Checks", $Checks
    )

    if (![string]::IsNullOrWhiteSpace($Korea)) {
        $arguments += @("-Korea", $Korea)
    }

    $processArguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $verifyScript
    ) + $arguments

    $lines = & powershell.exe @processArguments 2>&1
    $code = $LASTEXITCODE

    return @{
        ExitCode = $code
        Lines = @($lines | ForEach-Object { $_.ToString() })
    }
}

function Test-ExpectedFailure {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Result
    )

    if ($Result.ExitCode -eq 0) {
        return $false
    }

    foreach ($line in $Result.Lines) {
        if ($line -match "RESULT:\s+FAIL" -or $line -match "^\s*FAIL\s+") {
            return $true
        }
    }

    return $false
}

Write-Host "[HD/FHD lobby crash gate] positive output: $Output"
$positive = Invoke-RouteVerifier -OutputPath $Output
$positive.Lines | Select-String -Pattern "FAIL|WARN|RESULT|unsafe|bottom edge|conflict|render snapshot|lobby FDT|lobby texture" | ForEach-Object { $_.ToString() }
if ($positive.ExitCode -ne 0) {
    throw "Positive HD/FHD lobby crash gate failed for: $Output"
}

$checkedKnownBad = 0
foreach ($bad in $KnownBadOutput) {
    if ([string]::IsNullOrWhiteSpace($bad) -or !(Test-Path -LiteralPath $bad)) {
        Write-Host "[HD/FHD lobby crash gate] known-bad skipped: $bad"
        continue
    }

    Write-Host "[HD/FHD lobby crash gate] known-bad output: $bad"
    $negative = Invoke-RouteVerifier -OutputPath $bad
    $negative.Lines | Select-String -Pattern "FAIL|WARN|RESULT|unsafe|bottom edge|conflict|render snapshot|lobby FDT|lobby texture|exception" | Select-Object -First 80 | ForEach-Object { $_.ToString() }
    if (!(Test-ExpectedFailure -Result $negative)) {
        throw "Known-bad output did not fail with a verifier assertion: $bad"
    }

    $checkedKnownBad++
}

if ($checkedKnownBad -eq 0) {
    throw "No known-bad HD/FHD lobby outputs were checked."
}

Write-Host "[HD/FHD lobby crash gate] PASS: positive output passed and $checkedKnownBad known-bad output(s) failed."
