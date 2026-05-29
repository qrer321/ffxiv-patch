param(
    [string]$ReleasePath,

    [string]$LogDir,

    [string]$Signature = "AtkFontAnalyzerRenderer",

    [switch]$FailOnAnyNewCrash
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($ReleasePath)) {
    $ReleasePath = Join-Path $repoRoot "Release\Public\FFXIVKoreanPatch.exe"
}

if ([string]::IsNullOrWhiteSpace($LogDir)) {
    $LogDir = Join-Path $env:APPDATA "XIVLauncher"
}

function Get-RegisterValue {
    param(
        [string[]]$Lines,

        [Parameter(Mandatory = $true)]
        [string]$Register
    )

    foreach ($line in $Lines) {
        if ($line -match "^\s*$Register\s*:\s*([0-9A-Fa-f]+)") {
            return $Matches[1]
        }
    }

    return ""
}

$releaseItem = Get-Item -LiteralPath $ReleasePath
$releaseHash = (Get-FileHash -LiteralPath $releaseItem.FullName -Algorithm SHA256).Hash

Write-Host "Release: $($releaseItem.FullName)"
Write-Host "Release time: $($releaseItem.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
Write-Host "Release SHA256: $releaseHash"
Write-Host "Crash log dir: $LogDir"
Write-Host "Signature: $Signature"

if (!(Test-Path -LiteralPath $LogDir)) {
    Write-Host "RESULT: PASS"
    Write-Host "No crash log directory exists."
    exit 0
}

$logs = @(Get-ChildItem -LiteralPath $LogDir -Filter "dalamud_appcrash_*.log" | Sort-Object LastWriteTime -Descending)
if ($logs.Count -eq 0) {
    Write-Host "RESULT: PASS"
    Write-Host "No Dalamud appcrash logs found."
    exit 0
}

$newLogs = @($logs | Where-Object { $_.LastWriteTime -gt $releaseItem.LastWriteTime })
$newSignatureLogs = New-Object System.Collections.Generic.List[System.IO.FileInfo]

foreach ($log in $newLogs) {
    if (Select-String -LiteralPath $log.FullName -Pattern $Signature -Quiet) {
        $newSignatureLogs.Add($log)
    }
}

$latestSignatureLog = $null
foreach ($log in $logs) {
    if (Select-String -LiteralPath $log.FullName -Pattern $Signature -Quiet) {
        $latestSignatureLog = $log
        break
    }
}

Write-Host "Latest logs:"
foreach ($log in ($logs | Select-Object -First 5)) {
    $hasSignature = Select-String -LiteralPath $log.FullName -Pattern $Signature -Quiet
    $marker = if ($hasSignature) { "signature" } else { "other" }
    Write-Host ("  {0}  {1}  {2}" -f $log.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'), $marker, $log.Name)
}

if ($latestSignatureLog -ne $null) {
    $lines = @(Get-Content -LiteralPath $latestSignatureLog.FullName)
    $r10 = Get-RegisterValue -Lines $lines -Register "R10"
    $r11 = Get-RegisterValue -Lines $lines -Register "R11"
    $rax = Get-RegisterValue -Lines $lines -Register "RAX"
    $rcx = Get-RegisterValue -Lines $lines -Register "RCX"

    Write-Host "Latest matching signature log:"
    Write-Host "  $($latestSignatureLog.FullName)"
    Write-Host "  time=$($latestSignatureLog.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')) RAX=$rax RCX=$rcx R10=$r10 R11=$r11"
}

if ($newSignatureLogs.Count -gt 0) {
    Write-Host "RESULT: FAIL"
    Write-Host "New crash logs after the release contain the target signature:"
    foreach ($log in $newSignatureLogs) {
        Write-Host "  $($log.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')) $($log.FullName)"
    }
    exit 1
}

if ($FailOnAnyNewCrash -and $newLogs.Count -gt 0) {
    Write-Host "RESULT: FAIL"
    Write-Host "New appcrash logs exist after the release:"
    foreach ($log in $newLogs) {
        Write-Host "  $($log.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss')) $($log.FullName)"
    }
    exit 1
}

Write-Host "RESULT: PASS"
if ($newLogs.Count -gt 0) {
    Write-Host "No new post-release appcrash log contains the target signature. New non-matching logs: $($newLogs.Count)"
}
else {
    Write-Host "No appcrash logs are newer than the release."
}
