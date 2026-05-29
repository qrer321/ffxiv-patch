param(
    [Parameter(Mandatory = $true)]
    [string]$Global,

    [Parameter(Mandatory = $true)]
    [string]$Korea,

    [string]$Output,

    [string]$TargetLanguage = "ja",

    [string]$BaseIndexDirectory,

    [string]$FontPackDir,

    [string]$FailedOutput,

    [string]$ReleasePath,

    [string]$Configuration = "Release",

    [switch]$BuildRelease,

    [switch]$SkipGenerate,

    [switch]$RunInGameCritical,

    [switch]$SkipKnownFailureCheck,

    [switch]$SkipCrashLogCheck,

    [switch]$AllowPatchedGlobal,

    [switch]$AllowVersionMismatch,

    [switch]$FailOnAnyNewCrash
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $repoRoot ".tmp\release-hd-crash-focus-$TargetLanguage"
}

if ([string]::IsNullOrWhiteSpace($FontPackDir)) {
    $FontPackDir = Join-Path $repoRoot "FFXIVPatchGenerator\FontPatchAssets"
}

if ([string]::IsNullOrWhiteSpace($ReleasePath)) {
    $ReleasePath = Join-Path $repoRoot "Release\Public\FFXIVKoreanPatch.exe"
}

function Resolve-LatestBaseIndexDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Language
    )

    $root = Join-Path $env:LOCALAPPDATA "FFXIVKoreanPatch\restore-baseline\$Language"
    if (!(Test-Path -LiteralPath $root)) {
        throw "Restore baseline root was not found: $root"
    }

    $candidate = Get-ChildItem -LiteralPath $root -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($candidate -eq $null) {
        throw "No restore baseline directories were found under: $root"
    }

    return $candidate.FullName
}

function Resolve-KnownFailedOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Language
    )

    $root = Join-Path $env:LOCALAPPDATA "FFXIVKoreanPatch\generated-release\$Language"
    if (!(Test-Path -LiteralPath $root)) {
        return ""
    }

    $candidate = Get-ChildItem -LiteralPath $root -Directory -Recurse |
        Where-Object { $_.Name -eq "20260528-003930" } |
        Select-Object -First 1
    if ($candidate -eq $null) {
        return ""
    }

    return $candidate.FullName
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,

        [Parameter(Mandatory = $true)]
        [string]$File,

        [string[]]$Arguments
    )

    Write-Host "[$Label] $File $($Arguments -join ' ')"
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

function Invoke-CapturedNative {
    param(
        [Parameter(Mandatory = $true)]
        [string]$File,

        [string[]]$Arguments
    )

    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $lines = & $File @Arguments 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
    }

    return @{
        ExitCode = $code
        Lines = @($lines | ForEach-Object { $_.ToString() })
    }
}

if ([string]::IsNullOrWhiteSpace($BaseIndexDirectory)) {
    $BaseIndexDirectory = Resolve-LatestBaseIndexDirectory -Language $TargetLanguage
}

if ([string]::IsNullOrWhiteSpace($FailedOutput)) {
    $FailedOutput = Resolve-KnownFailedOutput -Language $TargetLanguage
}

$focusedChecks = "lobby-coverage-glyphs,lobby-runtime-font-safety,lobby-multitexture-font-set,lobby-runtime-scale-font-routes,lobby-hangul-visibility,font-runtime-glyph-bounds"

if ($BuildRelease) {
    Invoke-Checked `
        -Label "build-release" `
        -File "powershell" `
        -Arguments @("-ExecutionPolicy", "Bypass", "-File", (Join-Path $repoRoot "Scripts\build-release.ps1"))
}

$releaseItem = Get-Item -LiteralPath $ReleasePath
$releaseHash = (Get-FileHash -LiteralPath $releaseItem.FullName -Algorithm SHA256).Hash

Write-Host "HD crash release gate"
Write-Host "  release: $($releaseItem.FullName)"
Write-Host "  release time: $($releaseItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
Write-Host "  release SHA256: $releaseHash"
Write-Host "  output: $Output"
Write-Host "  base index: $BaseIndexDirectory"
Write-Host "  focused checks: $focusedChecks"

if (!$SkipGenerate) {
    if (Test-Path -LiteralPath $Output) {
        Remove-Item -LiteralPath $Output -Recurse -Force
    }

    $generateArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $repoRoot "Scripts\generate-and-verify-patch-routes.ps1"),
        "-Global", $Global,
        "-Korea", $Korea,
        "-Output", $Output,
        "-TargetLanguage", $TargetLanguage,
        "-BaseIndexDirectory", $BaseIndexDirectory,
        "-Checks", $focusedChecks,
        "-Configuration", $Configuration
    )

    if ($AllowPatchedGlobal) {
        $generateArgs += "-AllowPatchedGlobal"
    }

    if ($AllowVersionMismatch) {
        $generateArgs += "-AllowVersionMismatch"
    }

    Invoke-Checked -Label "generate-focused-output" -File "powershell" -Arguments $generateArgs
}
else {
    if (!(Test-Path -LiteralPath $Output)) {
        throw "Output does not exist for -SkipGenerate: $Output"
    }

    Invoke-Checked `
        -Label "verify-focused-output" `
        -File "powershell" `
        -Arguments @(
            "-ExecutionPolicy", "Bypass",
            "-File", (Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"),
            "-Output", $Output,
            "-Global", $Global,
            "-Korea", $Korea,
            "-TargetLanguage", $TargetLanguage,
            "-FontPackDir", $FontPackDir,
            "-Configuration", $Configuration,
            "-Checks", $focusedChecks,
            "-NoGlyphDump"
        )
}

if ($RunInGameCritical) {
    Invoke-Checked `
        -Label "verify-ingame-critical" `
        -File "powershell" `
        -Arguments @(
            "-ExecutionPolicy", "Bypass",
            "-File", (Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"),
            "-Output", $Output,
            "-Global", $Global,
            "-Korea", $Korea,
            "-TargetLanguage", $TargetLanguage,
            "-FontPackDir", $FontPackDir,
            "-Configuration", $Configuration,
            "-Checks", "ingame-critical",
            "-NoGlyphDump"
        )
}

if (!$SkipKnownFailureCheck) {
    if ([string]::IsNullOrWhiteSpace($FailedOutput) -or !(Test-Path -LiteralPath $FailedOutput)) {
        throw "Known failed output was not found. Pass -SkipKnownFailureCheck or -FailedOutput explicitly."
    }

    $verifierExe = Join-Path $repoRoot "Tools\PatchRouteVerifier\bin\$Configuration\PatchRouteVerifier.exe"
    $knownFailureResult = Invoke-CapturedNative `
        -File $verifierExe `
        -Arguments @(
            "--output", $FailedOutput,
            "--global", $Global,
            "--korea", $Korea,
            "--target-language", $TargetLanguage,
            "--font-pack-dir", $FontPackDir,
            "--checks", "lobby-runtime-font-safety",
            "--no-glyph-dump"
        )

    if ($knownFailureResult.ExitCode -eq 0) {
        throw "Known failed output unexpectedly passed: $FailedOutput"
    }

    $expectedFailure = @($knownFailureResult.Lines | Select-String -Pattern "unsafe lobby texture route.*font_lobby3\.tex")
    if ($expectedFailure.Count -eq 0) {
        $sample = ($knownFailureResult.Lines | Select-Object -First 20) -join "`n"
        throw "Known failed output failed for an unexpected reason. First lines:`n$sample"
    }

    Write-Host "Known failed output rejected as expected: $FailedOutput"
}

if (!$SkipCrashLogCheck) {
    $crashArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $repoRoot "Scripts\check-hd-crash-logs.ps1"),
        "-ReleasePath", $ReleasePath
    )

    if ($FailOnAnyNewCrash) {
        $crashArgs += "-FailOnAnyNewCrash"
    }

    Invoke-Checked -Label "check-hd-crash-logs" -File "powershell" -Arguments $crashArgs
}

Write-Host "RESULT: PASS"
