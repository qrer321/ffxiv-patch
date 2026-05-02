param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (!(Test-Path $csc)) {
    throw "C# compiler not found: $csc"
}

$outDir = Join-Path $PSScriptRoot "bin\$Configuration"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = @(
    "Program.cs",
    "TextPatchGenerator.cs",
    "FontPatchGenerator.cs",
    "UiPatchGenerator.cs",
    "SqPack.cs",
    "Excel.cs",
    "ExdStringPatcher.cs",
    "QuestChatPhraseAnonymizer.cs",
    "HashAndEndian.cs",
    "PatchPolicy.cs"
) | ForEach-Object { Join-Path $PSScriptRoot $_ }

$exePath = Join-Path $outDir "FFXIVPatchGenerator.exe"
& $csc /nologo /optimize+ /target:exe "/out:$exePath" /r:System.IO.Compression.dll /r:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $outDir\FFXIVPatchGenerator.exe"
