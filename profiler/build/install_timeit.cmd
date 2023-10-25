@echo off

dotnet tool update -g timeitsharp --version 0.0.17

rem Add %USERPROFILE%\.dotnet\tools to Path if it is not already there
path|find /i "%USERPROFILE%\.dotnet\tools" >nul || set path=%path%;"%USERPROFILE%\.dotnet\tools"
