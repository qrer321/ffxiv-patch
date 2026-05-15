param(
    [Parameter(Mandatory = $true)]
    [string]$Output,

    [Parameter(Mandatory = $true)]
    [string]$Global,

    [string]$GlobalText,

    [string]$GlobalFont,

    [string]$GlobalUi,

    [string]$Korea,

    [string]$AppliedGame,

    [string]$TargetLanguage = "ja",

    [string]$FontPackDir,

    [string]$GlyphDumpDir,

    [switch]$NoGlyphDump,

    [string]$Checks,

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
if (![string]::IsNullOrWhiteSpace($GlobalText)) {
    $arguments += @("--global-text", $GlobalText)
}
if (![string]::IsNullOrWhiteSpace($GlobalFont)) {
    $arguments += @("--global-font", $GlobalFont)
}
if (![string]::IsNullOrWhiteSpace($GlobalUi)) {
    $arguments += @("--global-ui", $GlobalUi)
}
if (![string]::IsNullOrWhiteSpace($Korea)) {
    $arguments += @("--korea", $Korea)
}
if (![string]::IsNullOrWhiteSpace($AppliedGame)) {
    $arguments += @("--applied-game", $AppliedGame)
}
if (![string]::IsNullOrWhiteSpace($FontPackDir)) {
    $arguments += @("--font-pack-dir", $FontPackDir)
}
if (![string]::IsNullOrWhiteSpace($GlyphDumpDir)) {
    $arguments += @("--glyph-dump-dir", $GlyphDumpDir)
}
if ($NoGlyphDump) {
    $arguments += "--no-glyph-dump"
}
if (![string]::IsNullOrWhiteSpace($Checks)) {
    $arguments += @("--checks", $Checks)
}

& $toolExe @arguments
exit $LASTEXITCODE
