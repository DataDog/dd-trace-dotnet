@echo off
SETLOCAL

SET CurrentDir=%~dp0
SET RootDir=%CurrentDir%..\..
echo RootDir = %RootDir%

echo *** Building Native ClrProfiler for linux-musl-x64 ***

docker build ^
    --build-arg DOTNETSDK_VERSION=5.0 ^
    --tag dd-trace-dotnet/alpine-builder ^
    --target builder ^
    --file "../../build/_build/docker/alpine.dockerfile" ^
    "../../build/_build"

docker run --rm ^
    --mount type=bind,source="%RootDir%",target=/project ^
    --env NugetPackageDirectory=/project/packages ^
    --env tracerHome=/project/src/bin/linux-tracer-home-alpine ^
    --env artifacts=/project/src/bin/artifacts ^
    dd-trace-dotnet/alpine-builder ^
    dotnet /build/bin/Debug/_build.dll Clean BuildTracerHome ZipTracerHome

copy ..\..\src\bin\linux-tracer-home-alpine\Datadog.Trace.ClrProfiler.Native.so .\home\linux-musl-x64\Datadog.Trace.ClrProfiler.Native.so
ENDLOCAL