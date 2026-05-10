param(
    [Parameter(Mandatory = $true)]
    [string]$Global,

    [Parameter(Mandatory = $true)]
    [string]$Korea,

    [Parameter(Mandatory = $true)]
    [string]$Output,

    [string]$AppliedGame,

    [string]$TargetLanguage = "ja",

    [string]$FontPackDir,

    [string]$BaseIndexDirectory,

    [string]$Checks = "applied-output-files,data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld,lobby-scale-font-sources,system-settings-mixed-scale-layouts,clean-ascii-font-routes,start-main-menu-phrase-layouts,4k-lobby-font-derivations,numeric-glyphs",

    [string]$Configuration = "Release",

    [switch]$AllowPatchedGlobal,

    [switch]$AllowVersionMismatch,

    [switch]$SkipFont,

    [switch]$GlyphDump,

    [switch]$VerboseLogs
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$generatorBuild = Join-Path $repoRoot "FFXIVPatchGenerator\build.ps1"
$generatorExe = Join-Path $repoRoot "FFXIVPatchGenerator\bin\$Configuration\FFXIVPatchGenerator.exe"
$verifyScript = Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"
$logDir = Join-Path $repoRoot ".tmp\applied-route-verification-logs"

if ([string]::IsNullOrWhiteSpace($FontPackDir)) {
    $FontPackDir = Join-Path $repoRoot "FFXIVPatchGenerator\FontPatchAssets"
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Write-MatchingLines {
    param(
        [AllowEmptyString()]
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string[]]$Patterns,

        [int]$First = 200
    )

    $count = 0
    foreach ($line in $Lines) {
        foreach ($pattern in $Patterns) {
            if ($line -match $pattern) {
                Write-Host $line
                $count++
                break
            }
        }

        if ($count -ge $First) {
            Write-Host "... output truncated; see log for full details"
            return
        }
    }
}

& powershell -ExecutionPolicy Bypass -File $generatorBuild -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$outputFullPath = [System.IO.Path]::GetFullPath($Output)
$generatorArgs = @(
    "--global", $Global,
    "--korea", $Korea,
    "--target-language", $TargetLanguage,
    "--output", $outputFullPath
)

if (!$SkipFont) {
    $generatorArgs += "--include-font"
    $generatorArgs += @("--font-pack-dir", $FontPackDir)
}

if ($AllowPatchedGlobal) {
    $generatorArgs += "--allow-patched-global"
}

if ($AllowVersionMismatch) {
    $generatorArgs += "--allow-version-mismatch"
}

if (![string]::IsNullOrWhiteSpace($BaseIndexDirectory)) {
    $basePairs = @(
        @{ Argument = "--base-index"; FileName = "orig.0a0000.win32.index" },
        @{ Argument = "--base-index2"; FileName = "orig.0a0000.win32.index2" },
        @{ Argument = "--base-font-index"; FileName = "orig.000000.win32.index" },
        @{ Argument = "--base-font-index2"; FileName = "orig.000000.win32.index2" },
        @{ Argument = "--base-ui-index"; FileName = "orig.060000.win32.index" },
        @{ Argument = "--base-ui-index2"; FileName = "orig.060000.win32.index2" }
    )

    foreach ($pair in $basePairs) {
        $basePath = Join-Path $BaseIndexDirectory $pair.FileName
        if (!(Test-Path $basePath)) {
            throw "Base index file was not found: $basePath"
        }

        $generatorArgs += @($pair.Argument, $basePath)
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$generatorLog = Join-Path $logDir "$timestamp-generator.log"
$verifierLog = Join-Path $logDir "$timestamp-verifier.log"

Write-Host "Generating patch output: $outputFullPath"
$generatorOutput = & $generatorExe @generatorArgs 2>&1
$generatorExitCode = $LASTEXITCODE
$generatorLines = $generatorOutput | ForEach-Object { $_.ToString() }
$generatorLines | Set-Content -LiteralPath $generatorLog -Encoding UTF8
if ($VerboseLogs) {
    $generatorLines
}
else {
    Write-MatchingLines `
        -Lines $generatorLines `
        -Patterns @("^Using base", "^Writing output:", "^Patch build completed\.", "^\s+EXD rows patched:", "^\s+Font files patched:", "^\s+UI files patched:", "^\s+Output:", "^\s+Diagnostics:", "^Warnings:")
}
if ($generatorExitCode -ne 0) {
    Write-Host "Generator log: $generatorLog"
    exit $generatorExitCode
}

$verifyArgs = @(
    "-Output", $outputFullPath,
    "-Global", $Global,
    "-TargetLanguage", $TargetLanguage,
    "-FontPackDir", $FontPackDir,
    "-Configuration", $Configuration
)

if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
    $verifyArgs += @("-AppliedGame", $AppliedGame)
}

if (![string]::IsNullOrWhiteSpace($Checks)) {
    $verifyArgs += @("-Checks", $Checks)
}

if (!$GlyphDump) {
    $verifyArgs += "-NoGlyphDump"
}

Write-Host "Verifying patch output"
$verifierOutput = & powershell -ExecutionPolicy Bypass -File $verifyScript @verifyArgs 2>&1
$verifierExitCode = $LASTEXITCODE
$verifierLines = $verifierOutput | ForEach-Object { $_.ToString() }
$verifierLines | Set-Content -LiteralPath $verifierLog -Encoding UTF8
if ($VerboseLogs) {
    $verifierLines
}
else {
    $failureCount = @($verifierLines | Where-Object { $_ -match "^\s+FAIL " }).Count
    Write-MatchingLines `
        -Lines $verifierLines `
        -Patterns @("^\[", "^\s+FAIL .*applied", "^\s+FAIL .*generated output", "^\s+FAIL .*differs from generated output", "^RESULT:")
    Write-Host "Verifier failures: $failureCount"
}

Write-Host "Generator log: $generatorLog"
Write-Host "Verifier log:  $verifierLog"
exit $verifierExitCode
