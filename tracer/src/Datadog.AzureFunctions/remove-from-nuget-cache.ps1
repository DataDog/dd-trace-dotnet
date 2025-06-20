param (
    [string]$PackageId = "Datadog.AzureFunctions"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "Removing `"$PackageId`" from NuGet cache."

# Get the global packages folder path
$globalPackagesPath = & dotnet nuget locals global-packages --list | ForEach-Object {
    if ($_ -match "global-packages:\s*(.+)$") { $matches[1] }
}

if (-not $globalPackagesPath)
{
    Write-Warning "Warning: Failed to find the global packages folder."
}
else
{
    $packagePath = Join-Path -Path $globalPackagesPath -ChildPath $PackageId

    if (Test-Path $packagePath)
    {
        Write-Host "Deleting `"$packagePath`"."
        Remove-Item -Path $packagePath -Recurse -Force -ErrorAction SilentlyContinue
    }
    else
    {
        Write-Host "Package `"$packagePath`" not found in the NuGet cache."
    }
}
