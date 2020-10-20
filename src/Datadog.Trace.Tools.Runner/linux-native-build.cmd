@echo off
SETLOCAL

echo *** Building Native ClrProfiler for linux-x64 ***
docker build -f ..\..\build\docker\linux-build.dockerfile --target native-linux-binary --tag native-linux-binary --output type=local,dest=home/linux-x64 --platform=linux ..\..\
docker run -it --rm -v %cd%\home\linux-x64:/home/linux-x64 native-linux-binary:latest
ENDLOCAL