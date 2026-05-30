param(
    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter(Mandatory = $true)]
    [string]$AppliedGame,

    [string]$BackupRoot,

    [switch]$SkipText,

    [switch]$SkipFont,

    [switch]$SkipUi
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $BackupRoot = Join-Path $repoRoot ".tmp\applied-route-backups"
}

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

    New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
    $backupDir = Join-Path $BackupRoot $Timestamp
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

    $copied = New-Object System.Collections.Generic.List[string]
    foreach ($file in ($Files | Select-Object -Unique)) {
        $sourcePath = Join-Path $SqpackDir $file
        if (Test-Path -LiteralPath $sourcePath) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $backupDir $file) -Force
            [void]$copied.Add($file)
        }
    }

    $info = New-Object System.Collections.Generic.List[string]
    [void]$info.Add("FFXIV generated output apply backup")
    [void]$info.Add("Created: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
    [void]$info.Add("Sqpack: $SqpackDir")
    [void]$info.Add("Files:")
    foreach ($file in $copied) {
        [void]$info.Add("  $file")
    }

    $info | Set-Content -LiteralPath (Join-Path $backupDir "backup-info.txt") -Encoding UTF8
    return $backupDir
}

$outputDir = (Resolve-Path -LiteralPath $Output).Path
$gameDir = (Resolve-Path -LiteralPath $AppliedGame).Path
$sqpackDir = Join-Path $gameDir "sqpack\ffxiv"
if (!(Test-Path -LiteralPath $sqpackDir)) {
    throw "Applied sqpack folder was not found: $sqpackDir"
}

$files = @()
if (!$SkipText) {
    $files += $textPatchFiles
}
if (!$SkipFont) {
    $files += $fontPatchFiles
}
if (!$SkipUi) {
    $files += $uiPatchFiles
}

$files = @($files | Select-Object -Unique)
if ($files.Count -eq 0) {
    throw "No files were selected to apply."
}

foreach ($file in $files) {
    $sourcePath = Join-Path $outputDir $file
    if (!(Test-Path -LiteralPath $sourcePath)) {
        throw "Generated patch file was not found: $sourcePath"
    }
}

Assert-NoGameProcesses

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupDir = Backup-AppliedFiles -SqpackDir $sqpackDir -Files $files -Timestamp $timestamp
Write-Host "Applied file backup: $backupDir"

foreach ($file in $files) {
    $sourcePath = Join-Path $outputDir $file
    $destinationPath = Join-Path $sqpackDir $file
    Copy-Item -LiteralPath $sourcePath -Destination $destinationPath -Force

    $sourceHash = Get-FileSha1 -Path $sourcePath
    $destinationHash = Get-FileSha1 -Path $destinationPath
    if ($sourceHash -ne $destinationHash) {
        throw "Applied file hash mismatch after copy: $file"
    }
}

Write-Host ("Applied generated output files: " + ($files -join ", "))
Write-Host "RESULT: PASS"
