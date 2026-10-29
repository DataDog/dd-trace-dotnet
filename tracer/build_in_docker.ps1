[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = "Stop"

$ROOT_DIR="$PSScriptRoot/.."
$BUILD_DIR="$ROOT_DIR/tracer/build/_build"
$IMAGE_NAME="dd-trace-dotnet/alpine-base"

&docker build `
   --build-arg DOTNETSDK_VERSION=11.0.100-rc.2.26504.105 `
   --tag $IMAGE_NAME `
   --file "$BUILD_DIR/docker/alpine.dockerfile" `
   "$BUILD_DIR"

# Allocate a TTY only for interactive invocations.
$ttyFlags = if ([Console]::IsInputRedirected) { @('-i') } else { @('-i','-t') }
$hostPort = if ($env:BUILD_DOCKER_PORT) { $env:BUILD_DOCKER_PORT } else { '5003' }

&docker run @ttyFlags --rm `
    --mount "type=bind,source=$ROOT_DIR,target=/project" `
    --env NugetPackageDirectory=/project/packages `
    --env artifacts=/project/tracer/bin/artifacts `
    --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 `
    --env NUKE_TELEMETRY_OPTOUT=1 `
    -p "${hostPort}:5003" `
    -v /ddlogs:/var/log/datadog/dotnet `
    $IMAGE_NAME `
    dotnet /build/bin/Debug/_build.dll $BuildArguments
