# Run locally to build Linux artifacts from Windows.
# Find the output in shared/bin/monitoring-home on the Docker host.
[CmdletBinding()]
Param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = "Stop"

$ROOT_DIR = "$PSScriptRoot/.."
$IMAGE_NAME = "dd-trace-dotnet/debian-local-builder"
$OUTPUT_DIR_REL = "shared/bin/monitoring-home"
$OUTPUT_DIR_ABS = "$ROOT_DIR/$OUTPUT_DIR_REL"

# Build the local builder image, and pre-build the Nuke project
docker build `
    --build-arg DOTNETSDK_VERSION=8.0.100 `
    --tag $IMAGE_NAME `
    --file "$ROOT_DIR/tracer/build/_build/docker/debian.dockerfile" `
    --target local_builder `
    "$ROOT_DIR"

# Run Nuke with build arguments
docker run -it --rm `
    --mount "type=bind,source=$OUTPUT_DIR_ABS,target=/project/$OUTPUT_DIR_REL" `
    --env artifacts=/project/tracer/bin/artifacts `
    --env NUKE_TELEMETRY_OPTOUT=1 `
    $IMAGE_NAME `
    dotnet /project/tracer/build/_build/bin/Debug/_build.dll $BuildArguments
