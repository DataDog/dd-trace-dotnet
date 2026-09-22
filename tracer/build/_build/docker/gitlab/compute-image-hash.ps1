# Emits a short content-addressed hash for the Windows GitLab build image.
#
# Hashes every file in this directory (including this script itself), so any
# change to the Dockerfile, install scripts, entrypoint, .dockerignore, or the
# hashing logic itself produces a new tag. Used by both the producer job
# (build-windows-ci-image) and the consumer job (build) in .gitlab-ci.yml to
# derive the same image reference from the same source of truth.

$ErrorActionPreference = 'Stop'

try {
    $files = Get-ChildItem -Path $PSScriptRoot -File -Force | Sort-Object -Property Name
    $combined = ($files | ForEach-Object { (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash }) -join ''
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($combined)
    $stream = [System.IO.MemoryStream]::new($bytes)
    try {
        $digest = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
    }
    finally {
        $stream.Dispose()
    }

    Write-Output $digest.Substring(0, 12).ToLowerInvariant()
    exit 0
}
catch {
    [Console]::Error.WriteLine("compute-image-hash.ps1: $_")
    exit 1
}
