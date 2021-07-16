```
.\build-tracer.ps1 -TracerVersions 1.30.0 -SolutionDirectory $env:DD_TRACE_SLN_DIRECTORY -DestinationDirectory C:\temp\dd-trace-dotnet
.\run.ps1 -HomeVersion 1.30.0 -NuGetVersion 1.30.0 -TracerHomesDirectory C:\Temp\dd-trace-dotnet
```

In the following table, the versions are:
- 1.27.1, past release, _before_ merging assemblies
- 1.28.0, past release, _before_ merging assemblies
- 1.30.0, fake new version after merging assemblies, for testing purposes
- 1.31.0, fake new version after merging assemblies, for testing purposes
- 2.0.0, fake new version after merging assemblies, to test a major version bump

.NET 5.0

| Tracer Home | NuGet          | Notes                                                                                                                                                                                                                                                                                 |
| ----------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.27.1      | 1.27.1 (same)  | Both automatic and manual spans in a single trace.                                                                                                                                                                                                                                    |
| 1.27.1      | 1.28.0 (newer) | Both automatic and manual spans in a single trace. `Datadog.Trace.dll` is newer.                                                                                                                                                                                                      |
|             |                |                                                                                                                                                                                                                                                                                       |
| 1.28.0      | 1.27.1 (older) | <font color="red">Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.</font>                                                                                                                                                              |
| 1.28.0      | 1.28.0 (same)  | Both automatic and manual spans in a single trace.                                                                                                                                                                                                                                    |
| 1.28.0      | 1.30.0 (newer) | Both automatic and manual spans in a single trace. `Datadog.Trace.dll` is newer.                                                                                                                                                                                                      |
| 1.28.0      | 2.0.0  (newer) | Both automatic and manual spans in a single trace. `Datadog.Trace.dll` is newer.                                                                                                                                                                                                      |
|             |                |                                                                                                                                                                                                                                                                                       |
| 1.30.0      | 1.28.0 (older) | <font color="red">Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.</font>                                                                                                                                                              |
| 1.30.0      | 1.30.0 (same)  | Both automatic and manual spans in a single trace.                                                                                                                                                                                                                                    |
| 1.30.0      | 1.31.0 (newer) | <font color="red">`DiagnosticSource` and manual spans in a single trace. No automatic instrumentation. `Datadog.Trace.dll` is newer. `CallTarget_RewriterCallback() skipping method: Method replacement found but the managed profiler has not yet been loaded into AppDomain`</font> |
| 1.30.0      | 2.0.0  (newer) | <font color="red">`DiagnosticSource` and manual spans in a single trace. No automatic instrumentation. `Datadog.Trace.dll` is newer. `CallTarget_RewriterCallback() skipping method: Method replacement found but the managed profiler has not yet been loaded into AppDomain`</font> |
|             |                |                                                                                                                                                                                                                                                                                       |
| 1.31.0      | 1.30.0 (older) |
| 1.31.0      | 1.31.0 (same)  |
|             |                |                                                                                                                                                                                                                                                                                       |
| 2.0.0       | 1.28.0 (older) |
| 2.0.0       | 1.30.0 (older) |
| 2.0.0       | 2.0.0 (same)   | Both automatic and manual spans in a single trace.                                                                                                                                                                                                                                    |


Before merging assemblies (older home, newer nuget):
```
Datadog.Trace, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Emit.DynamicAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
Datadog.Trace.ClrProfiler.Managed, Version=1.27.1.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed.Loader, Version=1.27.1.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
```

Before merging assemblies (newer home, older nuget):
```
Datadog.Trace, Version=1.27.1.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Emit.DynamicAssembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
Datadog.Trace.ClrProfiler.Managed, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed.Loader, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
```

After merging assemblies (older home, newer nuget):
```
Datadog.Trace, Version=1.30.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed.Loader, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
```

After merging assemblies (newer home, older nuget):
```
Datadog.Trace, Version=1.28.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
Datadog.Trace.ClrProfiler.Managed.Loader, Version=1.30.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb
```


.NET 5
- TODO

.NET Framework 4.6.1
- TODO
