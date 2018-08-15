$Env:COR_ENABLE_PROFILING = 1
$Env:COR_PROFILER = {846F5F1C-F9AE-4B07-969E-05C26BC060D8}
$Env:COR_PROFILER_PATH = (Get-Item -Path ".\src\Datadog.Trace.ClrProfiler.Native\x64\Debug\Datadog.Trace.ClrProfiler.Native.dll").FullName

Start-Process -FilePath ".\samples\Samples.ConsoleFramework\bin\Debug\Samples.ConsoleFramework.exe"
