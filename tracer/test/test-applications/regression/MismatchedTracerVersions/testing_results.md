In the following tables, the versions are:
- 1.27.1: [existing release](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.27.1) _before_ merging assemblies
- 1.28.0: [existing release](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.0) _before_ merging assemblies
- 1.30.0: fake future version after merging assemblies, for testing purposes
- 1.31.0: fake future version after merging assemblies, for testing purposes
- 2.0.0:  fake new version after merging assemblies, to test with a major version bump

TL;DR There is no regression in any of the combinations of tracer home directory (e.g. msi installer) and nuget package. The cases where automatic and manual spans are separated into multiple traces will be fixed in a future version of the tracer.

## .NET 5

On .NET Core and .NET 5, if the assembly version from the nuget package is newer, the CLR can load that one instead of the assembly from the tracer's home folder. As long as there are no breaking changes, a newer nuget package and be combined with an older msi. This "feature" can be used when updating both at the same time is not possible: update the nuget first and deploy the app, then update the msi.

If the assembly version from the nuget package is older, the CLR will load multiple versions of the assembly. Mixing assemblies should never break an application, but automatic and manual spans will be split into multiple traces unless the assembly versions match. This will be fixed in a future version of the tracer.

| Tracer Home | NuGet          | Notes                                                                                                           |
| ----------- | -------------- | --------------------------------------------------------------------------------------------------------------- |
| 1.27.1      | 1.27.1 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                               |
| 1.27.1      | 1.28.0 (newer) | :green_circle: Both automatic and manual spans in a single trace. Only the newer `Datadog.Trace.dll` is loaded. |
|             |                |                                                                                                                 |
| 1.28.0      | 1.27.1 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.    |
| 1.28.0      | 1.28.0 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                               |
| 1.28.0      | 1.30.0 (newer) | :green_circle: Both automatic and manual spans in a single trace. Only the newer `Datadog.Trace.dll` is loaded. |
| 1.28.0      | 2.0.0  (newer) | :green_circle: Both automatic and manual spans in a single trace. Only the newer `Datadog.Trace.dll` is loaded. |
|             |                |                                                                                                                 |
| 1.30.0      | 1.28.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.    |
| 1.30.0      | 1.30.0 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                               |
| 1.30.0      | 1.31.0 (newer) | :green_circle: Both automatic and manual spans in a single trace. Only the newer `Datadog.Trace.dll` is loaded. |
| 1.30.0      | 2.0.0  (newer) | :green_circle: Both automatic and manual spans in a single trace. Only the newer `Datadog.Trace.dll` is loaded. |
|             |                |                                                                                                                 |
| 1.31.0      | 1.30.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.    |
| 1.31.0      | 1.31.0 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                               |
|             |                |                                                                                                                 |
| 2.0.0       | 1.28.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.    |
| 2.0.0       | 1.30.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded.    |
| 2.0.0       | 2.0.0 (same)   | :green_circle: Both automatic and manual spans in a single trace.                                               |


## .NET Framework 4.6.1

Since the assemblies have strong names, strict assembly binding kicks in on .NET Framework. The CLR is able to load multiple versions of the same assembly. Mixing assemblies should never break an application, but automatic and manual spans will be split into multiple traces unless the assembly versions match. This will be fixed in a future version of the tracer.

| Tracer Home | NuGet          | Notes                                                                                                        |
| ----------- | -------------- | ------------------------------------------------------------------------------------------------------------ |
| 1.27.1      | 1.27.1 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                            |
| 1.27.1      | 1.28.0 (newer) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
|             |                |
| 1.28.0      | 1.27.1 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 1.28.0      | 1.28.0 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                            |
| 1.28.0      | 1.30.0 (newer) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 1.28.0      | 2.0.0  (newer) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
|             |                |
| 1.30.0      | 1.28.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 1.30.0      | 1.30.0 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                            |
| 1.30.0      | 1.31.0 (newer) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 1.30.0      | 2.0.0  (newer) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
|             |                |
| 1.31.0      | 1.30.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 1.31.0      | 1.31.0 (same)  | :green_circle: Both automatic and manual spans in a single trace.                                            |
|             |                |
| 2.0.0       | 1.28.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 2.0.0       | 1.30.0 (older) | :red_square: Automatic and manual spans in separate traces. Both versions of `Datadog.Trace.dll` are loaded. |
| 2.0.0       | 2.0.0 (same)   | :green_circle: Both automatic and manual spans in a single trace.                                            |
