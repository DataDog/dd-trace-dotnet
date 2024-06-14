# Run locally to build Linux artifacts from Windows.
# Find the output in shared/bin/monitoring-home on the Docker host.
[CmdletBinding()]
Param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = "Stop"

$ROOT_DIR = "$PSScriptRoot/.."
$BUILD_DIR = "$ROOT_DIR/tracer/build/_build"
$IMAGE_NAME = "dd-trace-dotnet/debian-local-builder"
$OUTPUT_DIR_REL = "shared/bin/monitoring-home"
$OUTPUT_DIR_ABS = "$ROOT_DIR/$OUTPUT_DIR_REL"

docker build `
    --build-arg DOTNETSDK_VERSION=8.0.100 `
    --tag $IMAGE_NAME `
    --file "$BUILD_DIR/docker/debian.dockerfile" `
    --target local_builder `
    "$ROOT_DIR"

docker run -it --rm `
    --mount "type=bind,source=$OUTPUT_DIR_ABS,target=/src/$OUTPUT_DIR_REL" `
    --env NUKE_TELEMETRY_OPTOUT=1 `
    $IMAGE_NAME `
    dotnet /src/tracer/build/_build/bin/Debug/_build.dll $BuildArguments
