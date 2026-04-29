param(
    [string]$RepoName = "ffxiv-patch-main",
    [ValidateSet("private", "public", "internal")]
    [string]$Visibility = "private",
    [string]$Description = "FFXIV Korean patch UI and generator",
    [string]$RemoteName = "origin",
    [string]$CommitMessage = "Initial source release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

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

$git = Resolve-ToolPath "git" @(
    "C:\Program Files\Git\cmd\git.exe",
    "C:\Program Files\Git\bin\git.exe"
)

$gh = Resolve-ToolPath "gh" @(
    "C:\Program Files\GitHub CLI\gh.exe"
)

Push-Location $repoRoot
try {
    & $gh auth status | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI is not logged in. Run 'gh auth login' first."
    }

    if (!(Test-Path ".git")) {
        & $git init -b main
    }

    $name = & $git config user.name
    $email = & $git config user.email
    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($email)) {
        throw "Git user.name or user.email is missing. Set them with git config before publishing."
    }

    & $git add .

    $hasHead = $true
    & $git rev-parse --verify HEAD *> $null
    if ($LASTEXITCODE -ne 0) {
        $hasHead = $false
    }

    if (!$hasHead) {
        & $git commit -m $CommitMessage
    }
    else {
        $pending = & $git status --porcelain
        if ($pending) {
            & $git commit -m $CommitMessage
        }
    }

    $remote = & $git remote get-url $RemoteName 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($remote)) {
        & $gh repo create $RepoName "--$Visibility" --description $Description --source . --remote $RemoteName --push
    }
    else {
        & $git push -u $RemoteName main
    }
}
finally {
    Pop-Location
}
