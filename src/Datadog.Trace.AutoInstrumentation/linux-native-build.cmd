@echo off
SETLOCAL

SET CurrentDir=%~dp0
SET RootDir=%CurrentDir%..\..
echo RootDir = %RootDir%

echo *** Building Native ClrProfiler for linux-x64 ***

docker build ^
    --build-arg DOTNETSDK_VERSION=5.0 ^
    --tag dd-trace-dotnet/debian-builder ^
    --target builder ^
    --file "%RootDir%/build/_build/docker/debian.dockerfile" ^
    "%RootDir%/build/_build"

docker run --rm ^
    --mount type=bind,source="%RootDir%",target=/project ^
    --env NugetPackageDirectory=/project/packages ^
    --env tracerHome=/project/src/bin/linux-tracer-home-debian ^
    --env artifacts=/project/src/bin/artifacts ^
    dd-trace-dotnet/debian-builder ^
    dotnet /build/bin/Debug/_build.dll BuildTracerHome ZipTracerHome

copy %RootDir%\src\bin\linux-tracer-home-debian\Datadog.Trace.ClrProfiler.Native.so %RootDir%\src\Datadog.Trace.AutoInstrumentation\home\linux-x64\Datadog.Trace.ClrProfiler.Native.so
ENDLOCAL