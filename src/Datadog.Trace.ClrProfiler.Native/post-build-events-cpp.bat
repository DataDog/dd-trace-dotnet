@echo off

REM Makes copies of the profiler assemblies for convenience
REM Other assemblies can test against the profiler without locking the bin of the profiler project

echo PostBuildEvents 
echo  $(SolutionDir) is %1

set profiler_bin="%~dp0\bin\*"
set copy_dir="%~1\ProfilerAssemblies"

if not exist %copy_dir% (mkdir %copy_dir%)

xcopy "%profiler_bin%" "%copy_dir%" /i /s /y

REM IF EXIST $(ProjectDir)post-build-events-cpp.bat CALL $(ProjectDir)post-build-events-cpp.bat "$(SolutionDir)"