@echo off

set file=%1.windows.%2.json

if exist %file% (
   %GOPATH%\\bin\\timeit.exe %file%
) else (
   echo missing %file%
)
