
REM SET SOLUTION_DIR=C:\Github\dd-trace-dotnet

SET TOOL_BUILD_CONFIG=Release
SET INTEGRATIONS_PROJ=%SOLUTION_DIR%\src\Datadog.Trace.ClrProfiler.Managed\Datadog.Trace.ClrProfiler.Managed.csproj
SET OUTPUT_DIR=%SOLUTION_DIR%\tools\PrepareRelease\bin\tracer-home

RMDIR "%OUTPUT_DIR%" /S /Q

dotnet publish %INTEGRATIONS_PROJ% -c %TOOL_BUILD_CONFIG% -f net45 -o "%OUTPUT_DIR%\net45"
dotnet publish %INTEGRATIONS_PROJ% -c %TOOL_BUILD_CONFIG% -f net461 -o "%OUTPUT_DIR%\net461"
dotnet publish %INTEGRATIONS_PROJ% -c %TOOL_BUILD_CONFIG% -f netstandard2.0 -o "%OUTPUT_DIR%\netstandard2.0"
