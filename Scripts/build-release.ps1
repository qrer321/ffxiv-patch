param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "FfxivKoreanPatch.sln"
$releaseDir = Join-Path $repoRoot "Release\Public"
$patchGeneratorBuild = Join-Path $repoRoot "FFXIVPatchGenerator\build.ps1"
$msbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

if (!(Test-Path $msbuild)) {
    throw "MSBuild was not found: $msbuild"
}

& powershell -ExecutionPolicy Bypass -File $patchGeneratorBuild -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $msbuild $solutionPath /restore "/p:Configuration=$Configuration" /m
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$resolvedReleaseDir = [System.IO.Path]::GetFullPath($releaseDir)
$resolvedRepoRoot = [System.IO.Path]::GetFullPath($repoRoot)
if (!$resolvedReleaseDir.StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release path escaped repository root: $resolvedReleaseDir"
}

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$uiBin = Join-Path $repoRoot "FFXIVPatchUI\bin\$Configuration"

# Old upstream self-updater output is no longer part of this local-generator based patcher.
$legacyFiles = @(
    "FFXIVKoreanPatchUpdater.exe",
    "FFXIVKoreanPatchUpdater.exe.config"
)

foreach ($legacyFile in $legacyFiles) {
    $legacyPath = Join-Path $releaseDir $legacyFile
    if (Test-Path $legacyPath) {
        Remove-Item -LiteralPath $legacyPath -Force
    }
}

$files = @(
    "FFXIVKoreanPatch.exe"
)

$obsoleteSidecarFiles = @(
    "FFXIVKoreanPatch.exe.config",
    "FFXIVPatchGenerator.exe",
    "TTMPD.mpd",
    "TTMPL.mpl"
)

$runningProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -eq "FFXIVKoreanPatch" -or $_.ProcessName -eq "FFXIVPatchGenerator"
}

if ($runningProcesses) {
    $processSummary = ($runningProcesses | ForEach-Object { "$($_.ProcessName)($($_.Id))" }) -join ", "
    throw "Release output files may be locked by running processes: $processSummary. Close the app/generator and run the build again."
}

foreach ($obsoleteFile in $obsoleteSidecarFiles) {
    $obsoletePath = Join-Path $releaseDir $obsoleteFile
    if (Test-Path $obsoletePath) {
        Remove-Item -LiteralPath $obsoletePath -Force
    }
}

foreach ($file in $files) {
    $source = Join-Path $uiBin $file
    if (Test-Path $source) {
        $destination = Join-Path $releaseDir $file
        if (Test-Path $destination) {
            Remove-Item -LiteralPath $destination -Force
        }

        Copy-Item -LiteralPath $source -Destination $releaseDir -Force
    }
}

Write-Host "Release files written to $releaseDir"
