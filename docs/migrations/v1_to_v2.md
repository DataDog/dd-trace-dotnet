# Migration to .NET Tracer v2

## .NET Tracer v2.0 content

The .NET Tracer v2.0:

- Fixes a long standing bug where traces could be disconnected when automatic and custom tracing were using two different versions of the tracer.
- Adds support to .NET 6 and drops the support of .NET Framework 4.5 to .NET Framework 4.6.1. If you are currently targeting version < 4.6.1, we suggest you upgrade in line with Microsoft's guidance.
- Refactored our APIs for an easier and safer use.

For a more complete overview, please refer to our [release notes on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases).

## Upgrading from 1.x to 2.0

### Code changes

Most of our api changes do not require any changes to your code, but some patterns are no longer supported or recommended. Please refer to [Datadog.Trace documentation](https://github.com/DataDog/dd-trace-dotnet/tree/v2.0.1/docs/Datadog.Trace#upgrading-from-1x-to-20) for more information.

### What if you are relying on .NET Framework lower than 4.6.0

If you are currently targeting version < 4.6.1, we suggest you upgrade in line with [Microsoft's guidance](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework). On our end, we will support fixing major bugs on top of Tracer v1.31.

That said, automatic instrumentation relies only on the CLR (runtime) version, not the compile-time target. Even if an application is built for .NET Framework older than 4.6.1, as long as it runs on a supported CLR version, automatic instrumentation will work normally.

Regarding custom instrumentation, NuGet package version 2.0 and above will no longer include binaries for versions of .NET Framework older than 4.6.1. The NuGet package will only work correctly with applications that target (at compile time):

- .NET Framework 4.6.1 or above
- .NET Core 3.1 or above
- .NET Standard 2.0 or above (includes .NET Core 2.0 and above)

### Rollout strategy when mixing automatic and custom instrumentation

**Note that** even though you won't get disconnected traces when using two different v2 versions of the tracer, **you should aim at aligning the versions of the automatic and manual tracers**. Indeed, there's a bigger overhead when there's a version mismatch, so you should aim at making this situation temporary.

#### .NET Core

##### If you were using v1.28.8 or greater

On .NET Core, to avoid disconnected traces during the upgrade process:

- Upgrade the nuget package to the latest version of the tracer
- Then upgrade serverside

Indeed, on .NET Core, if the assembly version from the nuget package is newer, the CLR can load that one instead of the assembly from the tracer's home folder.

##### If you were using v1.28.7 or lower

In that case, the migration will create disconnected traces one last time. **You should update the Tracer Home (ie serverside) first though**, then the nuget.

#### .NET Framework

If you rely on the .NET Framework, there are no scenarios where you could avoid disconnected traces between automatic and custome instrumentation. In that case, you should do what's easier for you.
