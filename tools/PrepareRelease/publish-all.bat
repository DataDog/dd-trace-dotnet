
REM SET SOLUTION_DIR=C:/Github/dd-trace-dotnet

SET MANAGED_PROJ=%SOLUTION_DIR%/src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj
SET OUTPUT_DIR=%SOLUTION_DIR%/tools/PrepareRelease/bin/tracer-home

dotnet publish %MANAGED_PROJ% -c Release -f net45 -o "%OUTPUT_DIR%/net45"
dotnet publish %MANAGED_PROJ% -c Release -f net461 -o "%OUTPUT_DIR%/net461"
dotnet publish %MANAGED_PROJ% -c Release -f netstandard2.0 -o "%OUTPUT_DIR%/netstandard2.0"
