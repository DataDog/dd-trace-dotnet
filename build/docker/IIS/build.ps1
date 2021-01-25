$ProgressPreference = 'SilentlyContinue'

$nuget_found = [bool] (Get-Command -ErrorAction Ignore -Type Application nuget)
if (!$nuget_found)
{
    Write-Error 'nuget not found in $env:PATH. Exiting.' -ErrorAction Stop
}

$msbuild_found = [bool] (Get-Command -ErrorAction Ignore -Type Application msbuild)
if (!$msbuild_found)
{
    Write-Error 'msbuild not found in $env:PATH. Exiting.' -ErrorAction Stop
}

$docker_compose_found = [bool] (Get-Command -ErrorAction Ignore -Type Application docker-compose)
if (!$docker_compose_found)
{
    Write-Error 'docker-compose not found in $env:PATH. Exiting.' -ErrorAction Stop
}

$repo_root = Resolve-Path ../../..
$trace_solution = Join-Path $repo_root Datadog.Trace.sln
$trace_proj = Join-Path $repo_root Datadog.Trace.proj
$solution = Join-Path $repo_root test/test-applications/aspnet/samples-iis.sln

# Build IIS samples
nuget restore $solution
msbuild $solution /p:DeployOnBuild=true /p:PublishProfile=FolderProfile.pubxml

# Build Datadog MSI's
nuget restore $trace_solution
msbuild $trace_proj /t:BuildCsharp /p:Configuration=Release

msbuild $trace_proj /t:BuildCpp /p:"Configuration=Release;Platform=x64"
msbuild $trace_proj /t:BuildCpp /p:"Configuration=Release;Platform=x86"

msbuild $trace_proj /t:msi /p:"Configuration=Release;Platform=x64"
msbuild $trace_proj /t:msi /p:"Configuration=Release;Platform=x86"

# Build IIS container
pushd $repo_root
$relative_msi = "src/WindowsInstaller/bin/Release/x64/en-us/*.msi"
docker-compose build --build-arg DOTNET_TRACER_MSI=$relative_msi IntegrationTests.IIS.LoaderOptimizationRegKey
popd