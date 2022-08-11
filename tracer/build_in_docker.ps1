[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

# in case we are being run from outside this directory
Set-Location $PSScriptRoot

$ROOT_DIR="$PSScriptRoot/.."
$BUILD_DIR="$ROOT_DIR/tracer/build/_build"
$IMAGE_NAME="dd-trace-dotnet/alpine-base"

&docker build `
   --build-arg DOTNETSDK_VERSION=6.0.100 `
   --tag $IMAGE_NAME `
   --file "$BUILD_DIR/docker/alpine.dockerfile" `
   "$BUILD_DIR"

&docker run -it --rm `
    --mount type=bind,source="$ROOT_DIR",target=/project `
    --env NugetPackageDirectory=/project/packages `
    --env artifacts=/project/tracer/bin/artifacts `
    --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 `
    -p 5003:5003 `
    -v /ddlogs:/var/log/datadog/dotnet `
    $IMAGE_NAME `
    dotnet /build/bin/Debug/_build.dll $BuildArguments
