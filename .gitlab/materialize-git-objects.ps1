$ErrorActionPreference = "Stop"

# GitLab's checkout may borrow objects from a runner-local cache. The cache is
# available to this host job, but it is outside the repository mounted into the
# Windows build container. SourceLink needs to inspect the repository in that
# container, so copy the borrowed objects into the checkout first.
$alternatesFile = git rev-parse --git-path objects/info/alternates
if ($LASTEXITCODE -ne 0) {
  throw "Could not locate the Git alternates file"
}

if (Test-Path -LiteralPath $alternatesFile) {
  Write-Output "Materializing Git objects for the Windows sample-build container"
  git repack -a -d
  if ($LASTEXITCODE -ne 0) {
    throw "Could not materialize the Git object database"
  }

  Remove-Item -LiteralPath $alternatesFile
}

