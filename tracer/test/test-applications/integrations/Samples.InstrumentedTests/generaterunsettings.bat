set projectFolder=%~dp0
set file=%projectFolder%iast.runsettings
set solutionFolder=%projectFolder%..\..\..\..\..\
echo ^<^?xml version="1.0" encoding="utf-8" ^?^> > %file%
echo ^<RunSettings^>^<RunConfiguration^>^<EnvironmentVariables^> >> %file%
echo ^<CORECLR_ENABLE_PROFILING^>1^</CORECLR_ENABLE_PROFILING^> >> %file%
echo ^<CORECLR_PROFILER^>{846F5F1C-F9AE-4B07-969E-05C26BC060D8}^</CORECLR_PROFILER^> >> %file%
echo ^<CORECLR_PROFILER_PATH_64^>%solutionFolder%shared\bin\monitoring-home\win-x64\Datadog.Trace.ClrProfiler.Native.dll^</CORECLR_PROFILER_PATH_64^> >> %file%
echo ^<CORECLR_PROFILER_PATH_32^>%solutionFolder%shared\bin\monitoring-home\win-x86\Datadog.Trace.ClrProfiler.Native.dll^</CORECLR_PROFILER_PATH_32^> >> %file%
echo ^<COR_ENABLE_PROFILING^>1^</COR_ENABLE_PROFILING^> >> %file%
echo ^<COR_PROFILER^>{846F5F1C-F9AE-4B07-969E-05C26BC060D8}^</COR_PROFILER^> >> %file%
echo ^<COR_PROFILER_PATH_64^>%solutionFolder%shared\bin\monitoring-home\win-x64\Datadog.Trace.ClrProfiler.Native.dll^</COR_PROFILER_PATH_64^> >> %file%
echo ^<COR_PROFILER_PATH_32^>%solutionFolder%shared\bin\monitoring-home\win-x86\Datadog.Trace.ClrProfiler.Native.dll^</COR_PROFILER_PATH_32^> >> %file%
echo ^<DD_DOTNET_TRACER_HOME^>%solutionFolder%shared\bin\monitoring-home^</DD_DOTNET_TRACER_HOME^> >> %file%
echo ^<DD_VERSION^>1.0.0^</DD_VERSION^> >> %file%
echo ^<DD_TRACE_DEBUG^>1^</DD_TRACE_DEBUG^> >> %file%
echo ^<DD_IAST_ENABLED^>1^</DD_IAST_ENABLED^> >> %file%
echo ^<DD_CIVISIBILITY_ENABLED^>0^</DD_CIVISIBILITY_ENABLED^> >> %file%
echo ^<DD_IAST_DEDUPLICATION_ENABLED^>0^</DD_IAST_DEDUPLICATION_ENABLED^> >> %file%
echo ^</EnvironmentVariables^>^</RunConfiguration^>^</RunSettings^> >> %file%