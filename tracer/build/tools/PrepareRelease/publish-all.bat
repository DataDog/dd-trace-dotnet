RMDIR "%OUTPUT_DIR%" /S /Q
dotnet msbuild "%SOLUTION_DIR%\Datadog.Trace.proj" /t:PublishManagedProfilerOnDisk /p:Configuration=Release;TracerHomeDirectory=%OUTPUT_DIR%
