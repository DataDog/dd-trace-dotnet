
REM SET SOLUTION_DIR=C:\Github\dd-trace-dotnet

SET TOOL_BUILD_CONFIG=Release

SET BASE_TRACE_PROJ=%SOLUTION_DIR%\Datadog.Trace.proj
SET TRACE_PROJ=%SOLUTION_DIR%\src\Datadog.Trace\Datadog.Trace.csproj
SET INTEGRATIONS_PROJ=%SOLUTION_DIR%\src\Datadog.Trace.ClrProfiler.Managed\Datadog.Trace.ClrProfiler.Managed.csproj
REM SET NATIVE_PROJ=%SOLUTION_DIR%\src\Datadog.Trace.ClrProfiler.Native\Datadog.Trace.ClrProfiler.Native.vcxproj

SET NATIVE_BIN=%SOLUTION_DIR%\src\Datadog.Trace.ClrProfiler.Native\bin
SET OUTPUT_DIR=%SOLUTION_DIR%\tools\PrepareRelease\bin\tracer-home

dotnet restore %BASE_TRACE_PROJ%

dotnet build %BASE_TRACE_PROJ% /t:BuildCpp /p:Configuration=%TOOL_BUILD_CONFIG%;Platform=x64
mkdir "%OUTPUT_DIR%\x64"
copy "%NATIVE_BIN%\%TOOL_BUILD_CONFIG%\x64\Datadog.Trace.ClrProfiler.Native.dll" "%OUTPUT_DIR%\x64\Datadog.Trace.ClrProfiler.Native.dll"
if "%TOOL_BUILD_CONFIG%"=="Debug" (
    copy "%NATIVE_BIN%\%TOOL_BUILD_CONFIG%\x64\Datadog.Trace.ClrProfiler.Native.pdb" "%OUTPUT_DIR%\x64\Datadog.Trace.ClrProfiler.Native.pdb"
)

dotnet build %BASE_TRACE_PROJ% /t:BuildCpp /p:Configuration=%TOOL_BUILD_CONFIG%;Platform=x86
mkdir "%OUTPUT_DIR%\x86\"
copy "%NATIVE_BIN%\%TOOL_BUILD_CONFIG%\x86\Datadog.Trace.ClrProfiler.Native.dll" "%OUTPUT_DIR%\x86\Datadog.Trace.ClrProfiler.Native.dll"
if "%TOOL_BUILD_CONFIG%"=="Debug" (
    copy "%NATIVE_BIN%\%TOOL_BUILD_CONFIG%\x86\Datadog.Trace.ClrProfiler.Native.pdb" "%OUTPUT_DIR%\x86\Datadog.Trace.ClrProfiler.Native.pdb"
)

dotnet publish %INTEGRATIONS_PROJ% -c %TOOL_BUILD_CONFIG% -f net45 -o "%OUTPUT_DIR%\net45"
dotnet publish %INTEGRATIONS_PROJ% -c %TOOL_BUILD_CONFIG% -f net461 -o "%OUTPUT_DIR%\net461"
dotnet publish %INTEGRATIONS_PROJ% -c %TOOL_BUILD_CONFIG% -f netstandard2.0 -o "%OUTPUT_DIR%\netstandard2.0"
