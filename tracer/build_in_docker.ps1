# Run locally to build Linux artifacts from Windows.
# Find the output in shared/bin/monitoring-home on the Docker host.
[CmdletBinding()]
Param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = "Stop"

$ROOT_DIR = "$PSScriptRoot/.."
$MONITORING_HOME_DIR = "shared/bin/monitoring-home"
$ARTIFACTS_DIR = "tracer/bin/artifacts"
$IMAGE_NAME = "dd-trace-dotnet/debian-local-builder"

# Build the local builder image, and pre-build the Nuke project
docker build `
    --build-arg DOTNETSDK_VERSION=8.0.100 `
    --tag $IMAGE_NAME `
    --file "$ROOT_DIR/tracer/build/_build/docker/debian.dockerfile" `
    --target local-builder `
    "$ROOT_DIR"

# Run Nuke with build arguments
docker run -it --rm `
    --mount "type=bind,source=$ROOT_DIR/$MONITORING_HOME_DIR,target=/project/$MONITORING_HOME_DIR" `
    --mount "type=bind,source=$ROOT_DIR/$ARTIFACTS_DIR,target=/project/$ARTIFACTS_DIR" `
    --env NUKE_TELEMETRY_OPTOUT=1 `
    $IMAGE_NAME `
    dotnet /project/tracer/build/_build/bin/Debug/_build.dll $BuildArguments
