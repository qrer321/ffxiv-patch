$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    throw "C# compiler not found: $csc"
}

$testDir = Join-Path $repoRoot "Tools\SayQuestPhraseTests"
$outDir = Join-Path $testDir "bin"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @(
    (Join-Path $repoRoot "FFXIVPatchGenerator\SayQuestPhraseLocalizer.cs"),
    (Join-Path $repoRoot "FFXIVPatchGenerator\ExdRowRewriter.cs"),
    (Join-Path $repoRoot "FFXIVPatchGenerator\Excel.cs"),
    (Join-Path $repoRoot "FFXIVPatchGenerator\HashAndEndian.cs"),
    (Join-Path $testDir "SayQuestPhraseTests.cs")
)

$exePath = Join-Path $outDir "SayQuestPhraseTests.exe"
& $csc /nologo /target:exe "/out:$exePath" $sources
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $exePath
exit $LASTEXITCODE
