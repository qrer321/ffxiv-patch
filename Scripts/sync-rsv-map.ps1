param(
    [string]$Destination,
    [string]$SourcePath = "",
    [string]$SourceUrl = "https://raw.githubusercontent.com/Bing-su/my-ffxiv-toolkit/main/rsv.json"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Destination)) {
    throw "Destination is required."
}

$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$destinationDir = [System.IO.Path]::GetDirectoryName($destinationPath)
New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null

$tempPath = $destinationPath + ".tmp"
if (Test-Path $tempPath) {
    Remove-Item -LiteralPath $tempPath -Force
}

if (![string]::IsNullOrWhiteSpace($SourcePath)) {
    $resolvedSource = [System.IO.Path]::GetFullPath($SourcePath)
    if (!(Test-Path $resolvedSource)) {
        throw "RSV source file was not found: $resolvedSource"
    }

    Copy-Item -LiteralPath $resolvedSource -Destination $tempPath -Force
}
else {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $SourceUrl -OutFile $tempPath -UseBasicParsing
}

$content = Get-Content -LiteralPath $tempPath -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($content) -or $content.IndexOf("_rsv_", [System.StringComparison]::Ordinal) -lt 0) {
    throw "Downloaded RSV map does not look valid: $tempPath"
}

try {
    $null = $content | ConvertFrom-Json
}
catch {
    throw "Downloaded RSV map is not valid JSON: $($_.Exception.Message)"
}

if (Test-Path $destinationPath) {
    Remove-Item -LiteralPath $destinationPath -Force
}

Move-Item -LiteralPath $tempPath -Destination $destinationPath
$hash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
Write-Host "RSV map written to $destinationPath"
Write-Host "RSV map SHA256: $hash"
