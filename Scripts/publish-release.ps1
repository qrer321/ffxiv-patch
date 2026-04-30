param(
    [string]$TagName = "",
    [string]$ReleaseTitle = "",
    [string]$RemoteName = "origin",
    [string]$BranchName = "main",
    [switch]$Publish,
    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$SkipBuild,
    [switch]$AllowDirty,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$releasePublicDir = Join-Path $repoRoot "Release\Public"
$releaseRoot = Join-Path $repoRoot "Release\GitHub"

function Resolve-ToolPath {
    param(
        [string]$Name,
        [string[]]$FallbackPaths
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    foreach ($path in $FallbackPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    throw "$Name was not found. Install it first or restart the terminal so PATH is refreshed."
}

function Assert-ChildPath {
    param(
        [string]$Path,
        [string]$Parent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (!$resolvedPath.StartsWith($resolvedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped expected parent. Path: $resolvedPath, Parent: $resolvedParent"
    }
}

function Get-SafeTagPart {
    param([string]$Value)

    $safe = $Value.Trim()
    foreach ($invalid in [System.IO.Path]::GetInvalidFileNameChars()) {
        $safe = $safe.Replace($invalid, '-')
    }

    return $safe.Replace('/', '-').Replace('\', '-')
}

function Get-PreviousTag {
    param([string]$Git)

    $tags = @(& $Git tag --sort=-creatordate)
    if ($tags -and $tags.Count -gt 0 -and ![string]::IsNullOrWhiteSpace($tags[0])) {
        return $tags[0].Trim()
    }

    return ""
}

function Write-ReleaseNotes {
    param(
        [string]$Path,
        [string]$Git,
        [string]$Tag,
        [string]$Title,
        [string]$ArtifactName,
        [string]$Sha256,
        [string[]]$Files
    )

    $commit = (& $Git rev-parse --short HEAD).Trim()
    $previousTag = Get-PreviousTag $Git
    $range = if ([string]::IsNullOrWhiteSpace($previousTag)) { "-10" } else { "$previousTag..HEAD" }
    $changes = & $Git log $range --pretty=format:"- %s (%h)"

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine("# $Title")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Summary")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- Tag: ``$Tag``")
    [void]$builder.AppendLine("- Commit: ``$commit``")
    [void]$builder.AppendLine("- Artifact: ``$ArtifactName``")
    [void]$builder.AppendLine("- SHA256: ``$Sha256``")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Included Files")
    [void]$builder.AppendLine()
    foreach ($file in $Files) {
        [void]$builder.AppendLine("- ``$file``")
    }

    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Changes")
    [void]$builder.AppendLine()
    if ($changes) {
        foreach ($change in $changes) {
            [void]$builder.AppendLine($change)
        }
    }
    else {
        [void]$builder.AppendLine("- No changes found.")
    }

    [void]$builder.AppendLine()
    [void]$builder.AppendLine("## Pre-Publish Checklist")
    [void]$builder.AppendLine()
    [void]$builder.AppendLine("- Confirm the release build completed with 0 warnings and 0 errors.")
    [void]$builder.AppendLine("- Confirm the GitHub Release asset is ``FFXIVKoreanPatch.exe``.")
    [void]$builder.AppendLine("- Smoke test preflight, full patch, font patch, and patch removal flows.")

    Set-Content -LiteralPath $Path -Value $builder.ToString() -Encoding UTF8
}

function Test-GitClean {
    param([string]$Git)

    $status = & $Git status --porcelain
    return [string]::IsNullOrWhiteSpace(($status -join [Environment]::NewLine))
}

$git = Resolve-ToolPath "git" @(
    "C:\Program Files\Git\cmd\git.exe",
    "C:\Program Files\Git\bin\git.exe"
)

$gh = Resolve-ToolPath "gh" @(
    "C:\Program Files\GitHub CLI\gh.exe"
)

Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($TagName)) {
        $TagName = "v" + (Get-Date -Format "yyyy.MM.dd.HHmm")
    }

    if ([string]::IsNullOrWhiteSpace($ReleaseTitle)) {
        $ReleaseTitle = "FFXIV Korean Patch $TagName"
    }

    if (!$AllowDirty -and !(Test-GitClean $git)) {
        throw "Working tree is not clean. Commit or stash changes first, or pass -AllowDirty for prepare-only runs."
    }

    if (!$SkipBuild) {
        & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-release.ps1")
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    if (!(Test-Path $releasePublicDir)) {
        throw "Release\Public was not found. Run Scripts\build-release.ps1 first or omit -SkipBuild."
    }

    $requiredFiles = @(
        "FFXIVKoreanPatch.exe"
    )

    $missingRequired = @()
    foreach ($file in $requiredFiles) {
        if (!(Test-Path (Join-Path $releasePublicDir $file))) {
            $missingRequired += $file
        }
    }

    if ($missingRequired.Count -gt 0) {
        throw "Release\Public is missing required files: $($missingRequired -join ', ')"
    }

    New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
    $safeTag = Get-SafeTagPart $TagName
    $stagingDir = Join-Path $releaseRoot $safeTag
    Assert-ChildPath $stagingDir $releaseRoot

    if (Test-Path $stagingDir) {
        if (!$Force) {
            throw "Release staging directory already exists: $stagingDir. Pass -Force to recreate it."
        }

        Remove-Item -LiteralPath $stagingDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
    $artifactName = "FFXIVKoreanPatch.exe"
    $artifactPath = Join-Path $stagingDir $artifactName
    Copy-Item -LiteralPath (Join-Path $releasePublicDir $artifactName) -Destination $artifactPath -Force

    $hash = Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256
    $shaPath = Join-Path $stagingDir "$artifactName.sha256.txt"
    Set-Content -LiteralPath $shaPath -Value "$($hash.Hash)  $artifactName" -Encoding ASCII

    $notesPath = Join-Path $stagingDir "release-notes.md"
    Write-ReleaseNotes -Path $notesPath -Git $git -Tag $TagName -Title $ReleaseTitle -ArtifactName $artifactName -Sha256 $hash.Hash -Files @($artifactName)

    Write-Host "Release asset prepared:"
    Write-Host "  Tag:      $TagName"
    Write-Host "  Exe:      $artifactPath"
    Write-Host "  SHA:      $shaPath"
    Write-Host "  Notes:    $notesPath"

    if (!$Publish) {
        Write-Host ""
        Write-Host "Prepare-only mode. No git tag or GitHub Release was created."
        Write-Host "To publish:"
        Write-Host "  .\Scripts\publish-release.ps1 -TagName $TagName -Publish"
        return
    }

    & $gh auth status | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not logged in. Run 'gh auth login' first."
    }

    if (!(Test-GitClean $git)) {
        throw "Working tree changed during release preparation. Commit or stash changes before publishing."
    }

    $currentBranch = (& $git rev-parse --abbrev-ref HEAD).Trim()
    if ($currentBranch -ne $BranchName) {
        throw "Current branch is $currentBranch, expected $BranchName."
    }

    $head = (& $git rev-parse HEAD).Trim()
    $remoteHeadLine = & $git ls-remote $RemoteName "refs/heads/$BranchName"
    $remoteHead = if ($remoteHeadLine) { $remoteHeadLine.Split("`t")[0].Trim() } else { "" }
    if ([string]::IsNullOrWhiteSpace($remoteHead) -or $head -ne $remoteHead) {
        throw "Local HEAD is not equal to $RemoteName/$BranchName. Push source changes before publishing a release."
    }

    & $git rev-parse -q --verify "refs/tags/$TagName" *> $null
    if ($LASTEXITCODE -eq 0) {
        throw "Local tag already exists: $TagName"
    }

    $remoteTag = & $git ls-remote --tags $RemoteName "refs/tags/$TagName"
    if ($remoteTag) {
        throw "Remote tag already exists: $TagName"
    }

    & $git tag -a $TagName -m $ReleaseTitle
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & $git push $RemoteName $TagName
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $releaseArgs = @(
        "release", "create", $TagName,
        $artifactPath,
        $shaPath,
        "--title", $ReleaseTitle,
        "--notes-file", $notesPath
    )

    if ($Draft) {
        $releaseArgs += "--draft"
    }

    if ($Prerelease) {
        $releaseArgs += "--prerelease"
    }

    & $gh @releaseArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host "GitHub Release published for $TagName"
}
finally {
    Pop-Location
}
