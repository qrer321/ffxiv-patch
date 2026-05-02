param(
    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter(Mandatory = $true)]
    [string]$Global,

    [string]$AppliedGame,

    [string]$TargetLanguage = "ja",

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$toolProject = Join-Path $repoRoot "Tools\PatchRouteVerifier\PatchRouteVerifier.csproj"
$toolExe = Join-Path $repoRoot "Tools\PatchRouteVerifier\bin\$Configuration\PatchRouteVerifier.exe"
$msbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"

if (!(Test-Path $msbuild)) {
    throw "MSBuild was not found: $msbuild"
}

& $msbuild $toolProject /restore "/p:Configuration=$Configuration" /m
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$arguments = @("--output", $Output, "--global", $Global, "--target-language", $TargetLanguage)
if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
    $arguments += @("--applied-game", $AppliedGame)
}

& $toolExe @arguments
exit $LASTEXITCODE
