# Migration to .NET Tracer v2

## .NET Tracer v2.0 contents

The .NET Tracer v2.0:

- Fixes a long standing bug where traces could be disconnected when automatic and custom tracing were using two different versions of the tracer.
- Adds support for .NET 6 and ends support of .NET Framework versions older than 4.6.1. If you are currently targeting version < 4.6.1, we suggest you upgrade in line with Microsoft's guidance.
- Refactored our APIs for an easier and safer use.

For a more complete overview, please refer to our [release notes on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases).

## Upgrading from 1.x to 2.0

### Code changes

Most of our api changes do not require any changes to your code, but some patterns are no longer supported or recommended. Please refer to [Datadog.Trace documentation](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#upgrading-from-1x-to-20) for more information.

### What if you are relying on .NET Framework lower than 4.6.1

If you are currently targeting version < 4.6.1, we suggest you upgrade in line with [Microsoft's guidance](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework). On our end, we will support fixing major bugs on top of Tracer v1.31.

That said, automatic instrumentation relies only on the CLR (runtime) version, not the compile-time target. Even if an application is built for .NET Framework older than 4.6.1, as long as it runs on a supported CLR version, automatic instrumentation will work normally.

Regarding custom instrumentation, NuGet package version 2.0 and above will no longer include binaries for versions of .NET Framework older than 4.6.1. The NuGet package will only work correctly with applications that target (at compile time):

- .NET Framework 4.6.1 or above
- .NET Core 3.1 or above
- .NET 5 or above
- .NET Standard 2.0 or above (includes .NET Core 2.0 and above)

### Rollout strategy when using automatic and custom instrumentation

If your application uses both automatic and custom instrumentation, your application traces may be disconnected until both components are upgraded to v2.x. Follow the steps below to upgrade the components in the recommended order.

**Note:** To minimize application overhead, align the versions of the automatic and custom instrumentation. Using two different versions of the 2.x .NET Tracer will still result in complete traces but it requires additional overhead, so Datadog recommends minimizing the amount of time that the versions are mismatched.

#### .NET Core and .NET 5+

##### If you were using v1.28.8 or greater

On .NET Core, to avoid disconnected traces during the upgrade process:

1. Upgrade the nuget package to the latest version of the tracer
2. Upgrade the automatic instrumentation package to the matching version of the tracer

Indeed, on .NET Core, if the assembly version from the nuget package is newer, the CLR can load that one instead of the assembly from the tracer's home folder.

##### If you were using v1.28.7 or lower

In that case, the migration will create disconnected traces one last time. **You should update the Tracer Home (ie serverside) first though**, then the nuget.

#### .NET Framework

On .NET Framework there will be disconnected traces between automatic and custom instrumentation until both packages are upgraded to 2.x. In this upgrade scenario, Datadog recommends upgrading the automatic and custom instrumentation in whichever order is easiest for your application.
