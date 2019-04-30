@echo off

REM Makes copies of the profiler assemblies for single project use (relies on post-build-events-cpp.bat)
REM Other assemblies can test against the profiler without locking the bin of the profiler project

echo PostBuildEvents for %2%
REM echo $(SolutionDir) is %1
REM echo $(ProjectDir) is %2

set source_dir="%~1\ProfilerAssemblies\*"
set output_dir="%~2\bin\Profiler\"

if not exist %output_dir% (mkdir %output_dir%)

xcopy "%source_dir%" "%output_dir%" /i /s /y >NUL

echo Finished xcopy for %2%

REM IF EXIST $(SolutionDir)copy-profiler-assemblies.bat CALL $(SolutionDir)copy-profiler-assemblies.bat "$(SolutionDir)" "$(ProjectDir)"