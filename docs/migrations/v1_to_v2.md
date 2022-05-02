# Migration to .NET Tracer v2

## .NET Tracer v2.0 contents

The .NET Tracer v2.0:

- Fixes a long standing bug where traces could be disconnected when automatic and custom tracing were using two different versions of the tracer.
- Adds support for .NET 6 and ends support of .NET Framework versions older than 4.6.1. If you are currently targeting version < 4.6.1, we suggest you upgrade in line with Microsoft's guidance.
- Refactored our APIs for an easier and safer use.

For a more complete overview, please refer to our [release notes on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases).

## Upgrading from 1.x to 2.0

### Code changes

Most of our api changes do not require any changes to your code, but some patterns are no longer supported or recommended. Please refer to [Datadog.Trace documentation](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#upgrading-from-1x-to-20) for full details:
- [Singleton `Tracer` instances](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#supported-net-versions)
- [Immutable `Tracer.Settings`](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#immutable-tracersettings)
- [Exporter settings](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#exporter-settings)
- [Configure ADO.NET integrations individually](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#configure-adonet-integrations-individually)
- [`ElasticsearchNet5` integration ID removed](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#elasticsearchnet5-integration-id-removed)
- [Obsolete APIs have been removed](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#obsolete-apis-have-been-removed)
- [Introduction of interfaces `ISpan`, `IScope`, and `ITracer`](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#elasticsearchnet5-integration-id-removed)
- [Simplification of the tracer interface](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#simplification-of-the-tracer-interface)
- [Incorrect integration names are ignored](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#incorrect-integration-names-are-ignored)
- [Automatic instrumentation changes](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#automatic-instrumentation-changes)

### What if you are relying on .NET Framework lower than 4.6.1

If you are currently targeting version < 4.6.1, we suggest you upgrade in line with [Microsoft's guidance](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework). On our end, we will support fixing major bugs on top of Tracer v1.31.

That said, automatic instrumentation relies only on the CLR (runtime) version, not the compile-time target. Even if an application is built for .NET Framework older than 4.6.1, as long as it runs on a supported CLR version, automatic instrumentation will work normally.

Regarding custom instrumentation, NuGet package version 2.0 and above will no longer include binaries for versions of .NET Framework older than 4.6.1. The NuGet package will only work correctly with applications that target (at compile time):

- .NET Framework 4.6.1 or above
- .NET Core 3.1 or above
- .NET 5 or above
- .NET Standard 2.0 or above (includes .NET Core 2.0 and above)

### Rollout strategy when using automatic and custom instrumentation

If your application uses both automatic and custom instrumentation, your application traces may be disconnected until both components are upgraded to v2.x. If you are unable to upgrade both components simultaneously, follow the steps below to upgrade the components in the recommended order.

**Note:** To minimize application overhead, align the versions of the automatic and custom instrumentation. Using two different versions of the 2.x .NET Tracer will still result in complete traces but it requires additional overhead, so Datadog recommends minimizing the amount of time that the versions are mismatched.

#### .NET Core and .NET 5+

##### If you were using v1.28.8 or greater

On .NET Core, to avoid disconnected traces during the upgrade process:

1. Upgrade _Datadog.Trace_ NuGet package to the latest version of the tracer
2. Upgrade the automatic instrumentation package to the matching version of the tracer

##### If you were using v1.28.7 or lower

In this upgrade scenario **you should upgrade the automatic instrumentation first** and then upgrade the _Datadog.Trace_ NuGet package. If the components are not upgraded in the correct order, the automatic instrumentation may fail to produce traces.

#### .NET Framework

On .NET Framework there will be disconnected traces between automatic and custom instrumentation until both packages are upgraded to 2.x. In this upgrade scenario, Datadog recommends upgrading the MSI and NuGet package in whichever order is easiest for your application.
