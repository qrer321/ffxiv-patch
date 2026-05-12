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

    [string]$Checks = "applied-output-files,data-center-title-uld,data-center-worldmap-uld,start-system-settings-uld,lobby-ttmp-payloads,korean-lobby-font-sources,system-settings-mixed-scale-layouts,clean-ascii-font-routes,start-main-menu-phrase-layouts,numeric-glyphs",

    [string]$Configuration = "Release",

    [switch]$AllowPatchedGlobal,

    [switch]$AllowVersionMismatch,

    [switch]$SkipFont,

    [switch]$ApplyToAppliedGame,

    [switch]$GlyphDump,

    [switch]$VerboseLogs
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$generatorBuild = Join-Path $repoRoot "FFXIVPatchGenerator\build.ps1"
$generatorExe = Join-Path $repoRoot "FFXIVPatchGenerator\bin\$Configuration\FFXIVPatchGenerator.exe"
$verifyScript = Join-Path $repoRoot "Scripts\verify-patch-routes.ps1"
$logDir = Join-Path $repoRoot ".tmp\applied-route-verification-logs"
$backupRoot = Join-Path $repoRoot ".tmp\applied-route-backups"

if ([string]::IsNullOrWhiteSpace($FontPackDir)) {
    $FontPackDir = Join-Path $repoRoot "FFXIVPatchGenerator\FontPatchAssets"
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$textPatchFiles = @(
    "0a0000.win32.dat1",
    "0a0000.win32.index",
    "0a0000.win32.index2"
)

$fontPatchFiles = @(
    "000000.win32.dat1",
    "000000.win32.index",
    "000000.win32.index2"
)

$uiPatchFiles = @(
    "060000.win32.dat4",
    "060000.win32.index",
    "060000.win32.index2"
)

$patchFiles = @($textPatchFiles)
if (!$SkipFont) {
    $patchFiles += $fontPatchFiles
    $patchFiles += $uiPatchFiles
}

function Get-FileSha1 {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA1).Hash
}

function Assert-NoGameProcesses {
    $blocked = @(
        "ffxivboot", "ffxivboot64",
        "ffxivlauncher", "ffxivlauncher64",
        "ffxiv", "ffxiv_dx11",
        "XIVLauncher", "XIVLauncherCN"
    )

    $running = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $blocked -contains $_.ProcessName
    }

    if ($running) {
        $summary = ($running | ForEach-Object { "$($_.ProcessName)($($_.Id))" }) -join ", "
        throw "Game or launcher processes are running and may lock sqpack files: $summary"
    }
}

function Backup-AppliedFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SqpackDir,

        [Parameter(Mandatory = $true)]
        [string[]]$Files,

        [Parameter(Mandatory = $true)]
        [string]$Timestamp
    )

    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
    $backupDir = Join-Path $backupRoot $Timestamp
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

    $copied = New-Object System.Collections.Generic.List[string]
    foreach ($file in ($Files | Select-Object -Unique)) {
        $sourcePath = Join-Path $SqpackDir $file
        if (Test-Path $sourcePath) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $backupDir $file) -Force
            [void]$copied.Add($file)
        }
    }

    $info = New-Object System.Collections.Generic.List[string]
    [void]$info.Add("FFXIV applied route backup")
    [void]$info.Add("Created: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
    [void]$info.Add("Sqpack: $SqpackDir")
    [void]$info.Add("Files:")
    foreach ($file in $copied) {
        [void]$info.Add("  $file")
    }

    $info | Set-Content -LiteralPath (Join-Path $backupDir "backup-info.txt") -Encoding UTF8
    return $backupDir
}

function Apply-GeneratedPatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputDir,

        [Parameter(Mandatory = $true)]
        [string]$GameDir,

        [Parameter(Mandatory = $true)]
        [string[]]$Files,

        [Parameter(Mandatory = $true)]
        [string]$Timestamp
    )

    if ([string]::IsNullOrWhiteSpace($GameDir)) {
        throw "-ApplyToAppliedGame requires -AppliedGame."
    }

    $sqpackDir = Join-Path $GameDir "sqpack\ffxiv"
    if (!(Test-Path $sqpackDir)) {
        throw "Applied sqpack folder was not found: $sqpackDir"
    }

    Assert-NoGameProcesses

    foreach ($file in ($Files | Select-Object -Unique)) {
        $sourcePath = Join-Path $OutputDir $file
        if (!(Test-Path $sourcePath)) {
            throw "Generated patch file was not found: $sourcePath"
        }
    }

    $backupDir = Backup-AppliedFiles -SqpackDir $sqpackDir -Files $Files -Timestamp $Timestamp
    Write-Host "Applied file backup: $backupDir"

    foreach ($file in ($Files | Select-Object -Unique)) {
        $sourcePath = Join-Path $OutputDir $file
        $destinationPath = Join-Path $sqpackDir $file
        Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force

        $sourceHash = Get-FileSha1 -Path $sourcePath
        $destinationHash = Get-FileSha1 -Path $destinationPath
        if ($sourceHash -ne $destinationHash) {
            throw "Applied file hash mismatch after copy: $file"
        }
    }

    Write-Host ("Applied generated patch files: " + (($Files | Select-Object -Unique) -join ", "))
}

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

if ($ApplyToAppliedGame) {
    Write-Host "Applying generated output to: $AppliedGame"
    Apply-GeneratedPatch -OutputDir $outputFullPath -GameDir $AppliedGame -Files $patchFiles -Timestamp $timestamp
}

$verifyArgs = @(
    "-Output", $outputFullPath,
    "-Global", $Global,
    "-Korea", $Korea,
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
