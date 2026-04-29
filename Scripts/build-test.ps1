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

if (Test-Path $testDir) {
    Remove-Item -LiteralPath $testDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $testDir | Out-Null

$uiBin = Join-Path $repoRoot "FFXIVPatchUI\bin\$Configuration"
$mappings = @{
    "FFXIVKoreanPatch.exe" = "FFXIVKoreanPatch.Test.exe"
    "FFXIVKoreanPatch.exe.config" = "FFXIVKoreanPatch.Test.exe.config"
    "FFXIVPatchGenerator.exe" = "FFXIVPatchGenerator.exe"
}

foreach ($sourceName in $mappings.Keys) {
    $source = Join-Path $uiBin $sourceName
    if (Test-Path $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $testDir $mappings[$sourceName]) -Force
    }
}

Write-Host "Test files written to $testDir"
