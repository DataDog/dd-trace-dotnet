param(
    [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Branch,
    [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Sha
)

$ErrorActionPreference = "Stop"

$versionProps = Join-Path $PSScriptRoot "_build/version.props"

$Branch = $Branch -creplace '^refs/heads/', ''
$lowerBranch = $Branch.ToLowerInvariant()
if ($lowerBranch -eq "" -or $lowerBranch -match '^(refs/|master$|main$|release|hotfix)') {
    Write-Output "Not setting a CI dev version suffix for branch '$Branch'"
    exit 0
}

$Sha = $Sha.ToLowerInvariant()
if ($Sha -notmatch '^[0-9a-f]{8,}$') {
    throw "Expected a commit SHA, but got '$Sha'"
}

$content = [System.IO.File]::ReadAllText($versionProps)
$match = [regex]::Match($content, '<DD_VersionSuffix>([^<]*)</DD_VersionSuffix>')
if (-not $match.Success) {
    throw "Could not find <DD_VersionSuffix> in $versionProps"
}

if ($match.Groups[1].Value -ne "") {
    Write-Output "Not setting a CI dev version suffix: the version suffix is already '$($match.Groups[1].Value)'"
    exit 0
}

$suffix = "-dev.ci.g" + $Sha.Substring(0, 8)
$content = $content.Replace('<DD_VersionSuffix></DD_VersionSuffix>', "<DD_VersionSuffix>$suffix</DD_VersionSuffix>")
[System.IO.File]::WriteAllText($versionProps, $content, (New-Object System.Text.UTF8Encoding $false))

Write-Output "Set the version suffix to '$suffix' for branch '$Branch'"
