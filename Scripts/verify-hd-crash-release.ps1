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

    [string[]]$AdditionalFailedOutput,

    [string]$ReleasePath,

    [string]$AppliedGame,

    [string]$Configuration = "Release",

    [switch]$BuildRelease,

    [switch]$SkipGenerate,

    [switch]$SkipFocusedChecks,

    [switch]$RunInGameCritical,

    [switch]$RunFullFontObjective,

    [switch]$SkipKnownFailureCheck,

    [switch]$SkipCrashLogCheck,

    [switch]$AllowPatchedGlobal,

    [switch]$AllowVersionMismatch,

    [switch]$FailOnAnyNewCrash,

    [switch]$DumpVisualSnapshots
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$gateLogDir = Join-Path $repoRoot ".tmp\hd-crash-release-gate-logs"
New-Item -ItemType Directory -Force -Path $gateLogDir | Out-Null

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

function Add-KnownFailedOutput {
    param(
        [System.Collections.Generic.List[string]]$Outputs,

        [string]$Path
    )

    if ($Outputs -eq $null -or [string]::IsNullOrWhiteSpace($Path) -or !(Test-Path -LiteralPath $Path)) {
        return
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    for ($i = 0; $i -lt $Outputs.Count; $i++) {
        if ([string]::Equals($Outputs[$i], $resolved, [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }
    }

    $Outputs.Add($resolved)
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

function New-VerifierArguments {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$Checks,

        [switch]$UseAppliedGame
    )

    $arguments = @(
        "--output", $OutputPath,
        "--global", $Global,
        "--korea", $Korea,
        "--target-language", $TargetLanguage,
        "--font-pack-dir", $FontPackDir,
        "--checks", $Checks,
        "--no-glyph-dump"
    )

    if ($UseAppliedGame -and ![string]::IsNullOrWhiteSpace($AppliedGame)) {
        $arguments += @("--applied-game", $AppliedGame)

        $cleanFontIndex = Join-Path $OutputPath "orig.000000.win32.index"
        if (Test-Path -LiteralPath $cleanFontIndex) {
            $arguments += @("--clean-font-index", $cleanFontIndex)
        }

        $cleanUiIndex = Join-Path $OutputPath "orig.060000.win32.index"
        if (Test-Path -LiteralPath $cleanUiIndex) {
            $arguments += @("--clean-ui-index", $cleanUiIndex)
        }
    }

    return $arguments
}

function Invoke-CapturedVerifierCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,

        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [Parameter(Mandatory = $true)]
        [string]$Checks,

        [switch]$UseAppliedGame
    )

    $verifierExe = Join-Path $repoRoot "Tools\PatchRouteVerifier\bin\$Configuration\PatchRouteVerifier.exe"
    $safeLabel = $Label -replace "[^A-Za-z0-9_.-]", "_"
    $logPath = Join-Path $gateLogDir ("{0}-{1}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $safeLabel)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $appliedSuffix = if ($UseAppliedGame -and ![string]::IsNullOrWhiteSpace($AppliedGame)) { " --applied-game $AppliedGame" } else { "" }
    Write-Host "[$Label] $verifierExe --checks $Checks$appliedSuffix"
    Write-Host "[$Label] log: $logPath"

    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $verifierArguments = New-VerifierArguments -OutputPath $OutputPath -Checks $Checks -UseAppliedGame:$UseAppliedGame
        & $verifierExe @verifierArguments *> $logPath
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldPreference
        $stopwatch.Stop()
    }

    $important = @(Select-String -LiteralPath $logPath -Pattern "^\s+(FAIL|WARN) ","^Checks:","^RESULT:" | ForEach-Object { $_.Line })

    if ($important.Count -gt 0) {
        $important | Select-Object -First 80 | ForEach-Object { Write-Host $_ }
        if ($important.Count -gt 80) {
            Write-Host "... verifier summary truncated"
        }
    }

    Write-Host ("[$Label] elapsed: {0:n1}s" -f $stopwatch.Elapsed.TotalSeconds)

    if ($exitCode -ne 0) {
        $sample = (Get-Content -LiteralPath $logPath -First 120) -join "`n"
        throw "$Label failed with exit code $exitCode. Log: $logPath`nFirst lines:`n$sample"
    }
}

if ([string]::IsNullOrWhiteSpace($BaseIndexDirectory)) {
    $BaseIndexDirectory = Resolve-LatestBaseIndexDirectory -Language $TargetLanguage
}

$failedOutputs = New-Object 'System.Collections.Generic.List[string]'
$resolvedOutputPath = [System.IO.Path]::GetFullPath($Output)
if ([string]::IsNullOrWhiteSpace($FailedOutput)) {
    Add-KnownFailedOutput -Outputs $failedOutputs -Path (Resolve-KnownFailedOutput -Language $TargetLanguage)
}
else {
    Add-KnownFailedOutput -Outputs $failedOutputs -Path $FailedOutput
}

$postReleaseR33Output = Join-Path $repoRoot ".tmp\release-hd-crash-head-$TargetLanguage"
if (![string]::Equals([System.IO.Path]::GetFullPath($postReleaseR33Output), $resolvedOutputPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Add-KnownFailedOutput -Outputs $failedOutputs -Path $postReleaseR33Output
}

$postReleaseR34GeneratedOutput = Join-Path $env:LOCALAPPDATA "FFXIVKoreanPatch\generated-release\$TargetLanguage\2026.05.01.0000.0000\20260530-002104"
if (![string]::Equals([System.IO.Path]::GetFullPath($postReleaseR34GeneratedOutput), $resolvedOutputPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Add-KnownFailedOutput -Outputs $failedOutputs -Path $postReleaseR34GeneratedOutput
}

if ($AdditionalFailedOutput -ne $null) {
    for ($i = 0; $i -lt $AdditionalFailedOutput.Length; $i++) {
        Add-KnownFailedOutput -Outputs $failedOutputs -Path $AdditionalFailedOutput[$i]
    }
}

$pvpProfileFailedOutputs = New-Object 'System.Collections.Generic.List[string]'
$pvpOversizedStartVariantOutput = Join-Path $repoRoot ".tmp\start-variant-$TargetLanguage-r3"
if (![string]::Equals([System.IO.Path]::GetFullPath($pvpOversizedStartVariantOutput), $resolvedOutputPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    Add-KnownFailedOutput -Outputs $pvpProfileFailedOutputs -Path $pvpOversizedStartVariantOutput
}

$focusedChecks = "lobby-coverage-glyphs,lobby-runtime-font-safety,lobby-multitexture-font-set,lobby-runtime-scale-font-routes,lobby-hangul-visibility,font-runtime-glyph-bounds,start-screen-glyph-variants,lobby-render-snapshots,action-detail-scale-layouts,pvp-profile-font-routes,combat-flytext-damage-glyphs,third-party-game-font-safety,party-list-self-marker"
$visualSnapshotChecks = "lobby-render-snapshots,action-detail-scale-layouts,pvp-profile-font-routes"
$verifyFocusedAgainstAppliedGame = ![string]::IsNullOrWhiteSpace($AppliedGame)
$runAppliedFocusedChecks = $verifyFocusedAgainstAppliedGame -and !$RunFullFontObjective -and !$SkipFocusedChecks

if ($SkipFocusedChecks -and !$SkipGenerate) {
    throw "-SkipFocusedChecks is only valid with -SkipGenerate; generation still needs a verification check set."
}

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
if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
    Write-Host "  applied game: $AppliedGame"
}
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

    if (!$verifyFocusedAgainstAppliedGame -and !$SkipFocusedChecks) {
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
}

if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
    $appliedBaseArgs = @(
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"),
        "-Output", $Output,
        "-Global", $Global,
        "-Korea", $Korea,
        "-TargetLanguage", $TargetLanguage,
        "-FontPackDir", $FontPackDir,
        "-Configuration", $Configuration,
        "-AppliedGame", $AppliedGame,
        "-NoGlyphDump"
    )

    $cleanFontIndex = Join-Path $Output "orig.000000.win32.index"
    if (Test-Path -LiteralPath $cleanFontIndex) {
        $appliedBaseArgs += @("-CleanFontIndex", $cleanFontIndex)
    }

    $cleanUiIndex = Join-Path $Output "orig.060000.win32.index"
    if (Test-Path -LiteralPath $cleanUiIndex) {
        $appliedBaseArgs += @("-CleanUiIndex", $cleanUiIndex)
    }

    Invoke-Checked `
        -Label "verify-applied-files" `
        -File "powershell" `
        -Arguments ($appliedBaseArgs + @("-Checks", "applied-output-files"))

    if ($runAppliedFocusedChecks) {
        Invoke-Checked `
            -Label "verify-applied-focused" `
            -File "powershell" `
            -Arguments ($appliedBaseArgs + @("-Checks", $focusedChecks))
    }

    Invoke-Checked `
        -Label "verify-applied-routes" `
        -File "powershell" `
        -Arguments ($appliedBaseArgs + @("-Checks", "applied-lobby-routes,lobby-runtime-font-safety,font-runtime-glyph-bounds"))
}

if ($DumpVisualSnapshots) {
    $snapshotDir = Join-Path $gateLogDir ("{0}-visual-snapshots" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
    Write-Host "  visual snapshots: $snapshotDir"
    if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
        $snapshotArgs = @($appliedBaseArgs | Where-Object { $_ -ne "-NoGlyphDump" }) + @(
            "-Checks", $visualSnapshotChecks,
            "-GlyphDumpDir", $snapshotDir
        )
    }
    else {
        $snapshotArgs = @(
            "-ExecutionPolicy", "Bypass",
            "-File", (Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"),
            "-Output", $Output,
            "-Global", $Global,
            "-Korea", $Korea,
            "-TargetLanguage", $TargetLanguage,
            "-FontPackDir", $FontPackDir,
            "-Configuration", $Configuration,
            "-Checks", $visualSnapshotChecks,
            "-GlyphDumpDir", $snapshotDir
        )
    }

    Invoke-Checked `
        -Label "verify-visual-snapshots" `
        -File "powershell" `
        -Arguments $snapshotArgs
}

if ($RunInGameCritical) {
    if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
        Invoke-Checked `
            -Label "verify-applied-ingame-critical" `
            -File "powershell" `
            -Arguments ($appliedBaseArgs + @("-Checks", "ingame-critical"))
    }
    else {
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
}

if ($RunFullFontObjective) {
    $objectiveChecks = @(
        @{
            Label = "objective-lobby-critical"
            Checks = "lobby-critical,4k-lobby-phrase-layouts,start-main-menu-phrase-layouts"
        },
        @{
            Label = "objective-ingame-bounds"
            Checks = "font-runtime-glyph-bounds,ingame-clean-ascii-glyphs,numeric-glyphs"
        },
        @{
            Label = "objective-ingame-markers"
            Checks = "party-list-self-marker,combat-flytext-damage-glyphs"
        },
        @{
            Label = "objective-shared-font-safety"
            Checks = "third-party-game-font-safety,reported-ingame-hangul-phrases"
        },
        @{
            Label = "objective-large-ui"
            Checks = "action-detail-scale-layouts,pvp-profile-font-routes"
        },
        @{
            Label = "objective-source-preservation"
            Checks = "hangul-source-preservation"
        },
        @{
            Label = "objective-texture-neighborhoods"
            Checks = "ingame-ttmp-texture-neighborhoods"
        }
    )

    for ($objectiveIndex = 0; $objectiveIndex -lt $objectiveChecks.Count; $objectiveIndex++) {
        Invoke-CapturedVerifierCheck `
            -Label $objectiveChecks[$objectiveIndex].Label `
            -OutputPath $Output `
            -Checks $objectiveChecks[$objectiveIndex].Checks `
            -UseAppliedGame:(![string]::IsNullOrWhiteSpace($AppliedGame))
    }
}

if (!$SkipKnownFailureCheck) {
    if ($failedOutputs.Count -eq 0) {
        throw "Known failed output was not found. Pass -SkipKnownFailureCheck or -FailedOutput explicitly."
    }

    $verifierExe = Join-Path $repoRoot "Tools\PatchRouteVerifier\bin\$Configuration\PatchRouteVerifier.exe"
    for ($failedIndex = 0; $failedIndex -lt $failedOutputs.Count; $failedIndex++) {
        $failedOutputPath = $failedOutputs[$failedIndex]
        $knownFailureResult = Invoke-CapturedNative `
            -File $verifierExe `
            -Arguments @(
                "--output", $failedOutputPath,
                "--global", $Global,
                "--korea", $Korea,
                "--target-language", $TargetLanguage,
                "--font-pack-dir", $FontPackDir,
                "--checks", "lobby-runtime-font-safety",
                "--no-glyph-dump"
            )

        if ($knownFailureResult.ExitCode -eq 0) {
            throw "Known failed output unexpectedly passed: $failedOutputPath"
        }

        $expectedFailure = @($knownFailureResult.Lines | Select-String -Pattern "unsafe lobby texture route.*font_lobby3\.tex")
        if ($expectedFailure.Count -eq 0) {
            $sample = ($knownFailureResult.Lines | Select-Object -First 20) -join "`n"
            throw "Known failed output failed for an unexpected reason. First lines:`n$sample"
        }

        Write-Host "Known failed output rejected as expected: $failedOutputPath"
    }

    for ($pvpFailedIndex = 0; $pvpFailedIndex -lt $pvpProfileFailedOutputs.Count; $pvpFailedIndex++) {
        $pvpFailedOutputPath = $pvpProfileFailedOutputs[$pvpFailedIndex]
        $pvpKnownFailureResult = Invoke-CapturedNative `
            -File $verifierExe `
            -Arguments @(
                "--output", $pvpFailedOutputPath,
                "--global", $Global,
                "--korea", $Korea,
                "--target-language", $TargetLanguage,
                "--font-pack-dir", $FontPackDir,
                "--checks", "pvp-profile-font-routes",
                "--no-glyph-dump"
            )

        if ($pvpKnownFailureResult.ExitCode -eq 0) {
            throw "Known PvP profile failed output unexpectedly passed: $pvpFailedOutputPath"
        }

        $expectedPvpFailure = @($pvpKnownFailureResult.Lines | Select-String -Pattern "Jupiter_16\.fdt PvP phrase .*outside")
        if ($expectedPvpFailure.Count -eq 0) {
            $sample = ($pvpKnownFailureResult.Lines | Select-Object -First 20) -join "`n"
            throw "Known PvP profile failed output failed for an unexpected reason. First lines:`n$sample"
        }

        Write-Host "Known PvP profile failed output rejected as expected: $pvpFailedOutputPath"
    }
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
