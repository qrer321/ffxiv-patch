$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    throw "C# compiler not found: $csc"
}

$testDir = Join-Path $repoRoot "Tools\SqPackIndexCheckTests"
$outDir = Join-Path $testDir "bin"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @(
    (Join-Path $repoRoot "FFXIVPatchGenerator\SqPackIndexCheck.cs"),
    (Join-Path $testDir "SqPackIndexCheckTests.cs")
)

$exePath = Join-Path $outDir "SqPackIndexCheckTests.exe"
& $csc /nologo /target:exe "/out:$exePath" $sources
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

# Synthetic index/dat fixtures are written under bin\fixtures and rebuilt on every run.
& $exePath (Join-Path $outDir "fixtures")
exit $LASTEXITCODE
