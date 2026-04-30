param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "FfxivKoreanPatch.sln"
$testDir = Join-Path $repoRoot "Release\Test"
$patchGeneratorBuild = Join-Path $repoRoot "FFXIVPatchGenerator\build.ps1"
$msbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

if (!(Test-Path $msbuild)) {
    throw "MSBuild was not found: $msbuild"
}

& powershell -ExecutionPolicy Bypass -File $patchGeneratorBuild -Configuration Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $msbuild $solutionPath /restore "/p:Configuration=$Configuration" /m
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$resolvedTestDir = [System.IO.Path]::GetFullPath($testDir)
$resolvedRepoRoot = [System.IO.Path]::GetFullPath($repoRoot)
if (!$resolvedTestDir.StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Test path escaped repository root: $resolvedTestDir"
}

New-Item -ItemType Directory -Force -Path $testDir | Out-Null

$uiBin = Join-Path $repoRoot "FFXIVPatchUI\bin\$Configuration"
$mappings = @{
    "FFXIVKoreanPatch.exe" = "FFXIVKoreanPatch.Test.exe"
    "FFXIVKoreanPatch.exe.config" = "FFXIVKoreanPatch.Test.exe.config"
    "FFXIVPatchGenerator.exe" = "FFXIVPatchGenerator.exe"
    "TTMPD.mpd" = "TTMPD.mpd"
    "TTMPL.mpl" = "TTMPL.mpl"
}

$runningProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -eq "FFXIVKoreanPatch" -or $_.ProcessName -eq "FFXIVKoreanPatch.Test" -or $_.ProcessName -eq "FFXIVPatchGenerator"
}

if ($runningProcesses) {
    $processSummary = ($runningProcesses | ForEach-Object { "$($_.ProcessName)($($_.Id))" }) -join ", "
    throw "Test output files may be locked by running processes: $processSummary. Close the app/generator and run the build again."
}

foreach ($sourceName in $mappings.Keys) {
    $source = Join-Path $uiBin $sourceName
    if (Test-Path $source) {
        $destination = Join-Path $testDir $mappings[$sourceName]
        if (Test-Path $destination) {
            Remove-Item -LiteralPath $destination -Force
        }

        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

Write-Host "Test files written to $testDir"
