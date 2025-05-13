# Migration guide

This document includes high level steps describing how to migrate between high level versions of the Datadog .NET Client Library (.NET Tracer)

- [Migrating from v2.x.x to v3.x.x](#migrating-from-v2xx-to-v3xx)
- [Migrating from v1.x.x to v2.x.x](#migrating-from-v1xx-to-v2xx)


## Migrating from v2.x.x to v3.x.x

The .NET tracer v3.0.0 includes breaking changes that you must be aware of before upgrading your applications. The most important high-level changes are listed below, and described in more detail later in this document

- Breaking changes
	- **Custom-only tracing (using the _Datadog.Trace_ NuGet package), _without_ any automatic tracing, is no longer supported**. Custom instrumentation with the  _Datadog.Trace_ NuGet where you have _also_ configured [automatic-instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/) is still supported as it was in v2.x.x.
	- **The public API surface has changed** in the *Datadog.Trace* NuGet package. A number of previously obsolete APIs have been removed, and some other APIs have been marked obsolete. Most changes are related to how you create `TracerSettings`  and `Tracer` instances.
	- **Changes to default settings**. The default values of some settings have changed, and others have been removed. See below for more details.
	- **Changes in behavior**. The semantic requirements and meaning of some settings have changed, as have some of the tags added to traces.  See below for more details.
	- **The 32-bit MSI installer will no longer be available**. The 64-bit MSI installer already includes support for tracing 32-bit processes, so you should use this installer instead. 
    - **The client library will still be injected when `DD_TRACE_ENABLED=0`**. In v2.x.x, setting `DD_TRACE_ENABLED=0` would prevent the client library from being injected into the application completely. In v3.0.0+, the client library will still be injected, but tracing will be disabled.
    - **Referencing the `Datadog.Trace.AspNet` module is no longer supported**. In v1.x.x and 2.x.x ASP.NET support allowed adding a reference to the `Datadog.Trace.AspNet` module in your web.config. This is no longer supported in v3.x.x.
- Deprecation notices
	- **.NET Core 2.1 is marked EOL** in v3.0.0+ of the tracer. That means versions 2.0, 2.1, 2.2 and 3.0 of .NET Core are now EOL. These versions may still work with v3.0.0+, but they will no longer receive significant testing and you will receive limited support for issues arising with EOL versions.
	- **Datadog.Trace.OpenTracing is now obsolete**. OpenTracing is considered deprecated, and so _Datadog.Trace.OpenTracing_ is considered deprecated. See the following details on future deprecation.
	- **macOS 11 is no longer supported for CI Visibility** in v3.0.0+. Only macOS 12 and above are supported.
- Major version policy and future deprecation
	- **Announcing a major version roadmap**. We intend to make yearly major releases, starting from v3.0.0 in 2024, and v4.0.0 in 2025. We clearly will aim for minimal breaking changes, with the primary focus being on maintaining support for new versions of .NET and removal of EOL frameworks and operating systems.
	- **Planned removal of support for .NET Core 2.x and .NET Core 3.0** in version v4.0.0+. We intend to completely remove support for .NET Core 2.x and .NET Core 3.0 in v4.0.0. .NET Framework 4.6.1+ will continue to be supported.
	- **Planned removal of support for some linux distributions**. In version v4.0.0, we intend to drop support for CentOS 7, RHEL 7, and CentOS Stream 8.
	- **Planned remove of support for App Analytics**. In version v4.0.0, we intend to drop support for App Analytics and associated settings.

> [!CAUTION]
> **Breaking Changes**: The following section describes the important breaking changes introduced in v3.0.0+ of the tracer, whether you will be affected, and how to handle them.

###  Custom-only tracing is no longer supported

#### What changed?
In version 2.x.x of the tracer, you can add tracing to your application in two ways:
- Using [automatic instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/). You will automatically receive traces for common libraries and frameworks.
- Using [custom instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/dd_libraries/dotnet-core/?tab=windows#custom-instrumentation) by referencing the [_Datadog.Trace_ ](https://www.nuget.org/packages/Datadog.Trace) or [Datadog.Trace.OpenTracing](https://www.nuget.org/packages/Datadog.Trace.OpenTracing) NuGet packages.

In version 3.0.0+, you will _always_ need to configure automatic instrumentation. You can still use custom instrumentation with the  the [_Datadog.Trace_ ](https://www.nuget.org/packages/Datadog.Trace) or [Datadog.Trace.OpenTracing](https://www.nuget.org/packages/Datadog.Trace.OpenTracing) NuGet packages, but you must _also_ configure automatic instrumentation.

#### Why did we change it?
This change was introduced for technical reasons to tackle the situation where the version of your NuGet package is different to the version of the automatic instrumentation tracer. In version 2.0.0, we included a partial solution to the problem, but the solution came with other performance implications, and caused issues for other features such as remote configuration and dynamic instrumentation. The proposed solution has none of those limitations.

#### What action should you take?
If you're already using any of the [automatic instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation) approaches, no action is required other than to ensure you have updated to version 3.0.0+. You must also update your *Datadog.Trace* NuGet package to 3.0.0+.

If you are currently _only_ using the *Datadog.Trace* NuGet package, then you must configure [automatic instrumentation](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/) to continue receiving traces. 

Using the Datadog.Trace or Datadog.Trace.OpenTracing NuGet packages without automatic instrumentation (when developing locally, for example) should not have adverse side affects, but none of the tracer library features will be available or running.

### *Datadog.Trace* public APIs have changed

#### What changed?
Many obsolete APIs have been removed from the public API in the [_Datadog.Trace_ NuGet package](https://www.nuget.org/packages/Datadog.Trace), some rarely-used APIs have been removed, and some APIs have been deprecated.

#### Why did we change it?
We removed obsolete APIs that could cause problematic usage patterns and restructured some APIs to solve the version-mismatch scenario described in the previous section. 

#### What action should you take?
If you are _not_ using the [_Datadog.Trace_ NuGet package](https://www.nuget.org/packages/Datadog.Trace), no action is required. 

If you *are* using the NuGet package and any of the following APIs, then you should adjust your usage as described below.

- `TracerSettings` can no longer be created from an `IConfigurationSource`. 
	- Instead use `TracerSettings.FromDefaultSources()` to load from all [default sources](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-framework)and then configure the `TracerSettings` object using properties
	- Alternatively, use `new TracerSettings()` to retrieve an "unitialized" instance
- `TracerSettings` no longer exposes `Build()` method
	- The output of this method could not be used previously, so it has been removed as unnecessary
- The `ExporterSettings` object is marked `[Obsolete]`, and most settings have been removed.
	- Get or set the `TracerSettings.AgentUri` property instead.
	- If you wish to set other exporter setting values, you should use one of the standard configuration sources instead of using configuration in code
- `GlobalSettings` no longer exposes a `Reload()` or `FromDefaultSources()` method.
	- There is no replacement for these methods, but you may still call `SetDebugEnabled(enabled)`
- `Tracer` no longer has public constructors and `Tracer.Instance` has no setter
	- These methods were marked `[Obsolete]` in 2.0.0 as they lead to problematic code patterns
	- Update your code to use the `Configure()` pattern shown below. Note that you should only call `Configure()` infrequently, i.e. on application startup.
- `ISpan` interfaces exposes a new property, `RawSpanId`, which allows you to retrieve the 128-bit TraceID as a hex-encoded `string`.

```csharp
using Datadog.Trace;

// Create your settings as before
var settings = new TracerSettings();

// var tracer = new Tracer(settings) // <- Delete this line
Tracer.Configure(settings);          // <- Add this line
```

Also note that the implementation types from all public APIs have changed. For example, `ISpan.Context` returns an `ISpanContext` instance, but this is no longer `SpanContext`.

### Changes to default settings

#### What changed and why?

Several settings have changed their default values:
- `DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED` now defaults to `true`. This setting provides a better experience in the majority of cases.
- `DD_TRACE_WCF_WEB_HTTP_RESOURCE_NAMES_ENABLED` now defaults to `true`. This setting provides a better experience in the majority of cases.
- `DD_TRACE_SAMPLING_RULES_FORMAT` now defaults to `glob` instead of `regex`. This setting is consistent with other language client libraries. 

Some settings have been removed
- `DD_TRACE_OTEL_LEGACY_OPERATION_NAME_ENABLED` has been removed. This setting was not widely used and was not necessary for most users, thanks to an improved operation name calculation.

#### What action should you take?
We recommend using the new settings if possible. If this is not possible, you can manually change the value for each setting to its previous value, for example: `DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED=false`

### Changes in behavior

#### What changed and why?
Some settings have changed their behavior:
- `DD_TRACE_HEADER_TAGS` [no longer replaces periods or spaces in names](https://github.com/DataDog/dd-trace-dotnet/pull/4599). The new behavior is consistent with the behavior of other tracer languages.
- `DD_TRACE_HEADER_TAGS` [now considers trailing `:` in entries as invalid, and splits key-value pairs on the last `:` in the entry](https://github.com/DataDog/dd-trace-dotnet/pull/5438). The new behavior is consistent with the behavior of other tracer languages.
- `DD_APPSEC_HTTP_BLOCKED_TEMPLATE_JSON` now refers to an absolute file path instead of providing the template content directly. This makes it easier to provide custom values.
- `DD_APPSEC_HTTP_BLOCKED_TEMPLATE_HTML` now refers to an absolute file path instead of providing the template content directly. This makes it easier to provide custom values.
- `DD_API_SECURITY_REQUEST_SAMPLING ` now requires a value from 0 to 1.0, not a percentage from 0 to 100.

There are also changes to some reported tags added to spans:
- The `language` tag is [now added to added to all spans](https://github.com/DataDog/dd-trace-dotnet/pull/4839) with the value `dotnet`. Previously, some spans were not tagged, but were subsequently tagged with the value `.NET`. This change removes the inconsistency.

#### What action should you take?
If you require the previous `DD_TRACE_HEADER_TAGS` normalization behavior, you must apply this normalization yourself, replacing periods and spaces with underscores in the value you pass to `DD_TRACE_HEADER_TAGS`.

Review any cases where you are setting `DD_TRACE_HEADER_TAGS` with entries that end in `:` or which contain multiple `:` values. For example, in `key1:, key2:key3:value4`, `key1:` is considered an invalid value in v3.0.0 whereas it was valid in v2.x.x. The final entry will be split into `key2:key3` and `value4` in v3.0.0, whereas in v2.x.x it was split into `key2` and `key3:value4`.  

If you are currently using `DD_APPSEC_HTTP_BLOCKED_TEMPLATE_JSON` or `DD_APPSEC_HTTP_BLOCKED_TEMPLATE_HTML`, you should move that content to a file, and provide the absolute file path in the settings.

If you are currently setting `DD_API_SECURITY_REQUEST_SAMPLING`, divide the value you are providing by 100. For example, if you are currently setting `DD_API_SECURITY_REQUEST_SAMPLING=10` (i.e. 10%)., then you should now use `DD_API_SECURITY_REQUEST_SAMPLING=0.10`.


### Windows 32-bit MSI installer is no longer available

#### What changed?
We will no longer be producing the 32-bit MSI installer as of v3.0.0. We will still produce the 64-bit MSI installer which is capable of instrumenting both 64-bit and 32-bit applications.

#### Why did we change it?
The 32-bit MSI installer should _only_ be used on 32-bit versions of Windows; the 64-bit MSI should normally be used, and allows tracing both 32-bit and 64-bit processes. Windows Server 2008 was the last version of Windows Server to support 32-bit operating systems, and [was announced EOL by Microsoft in 2020](https://learn.microsoft.com/en-us/troubleshoot/windows-server/windows-server-eos-faq/end-of-support-windows-server-2008-2008r2).  We are removing the 32-bit MSI to reduce confusion so that customers don't install the wrong version.

#### What action should you take?
If you _are_ running a 32-bit version of Windows (for example, a 32-bit version of Windows 7), you will no longer be able to enable Datadog .NET automatic instrumentation via the MSI. Consider using one of the other automatic instrumentation approaches, such as [the Datadog.Trace.Bundle approach](https://docs.datadoghq.com/tracing/trace_collection/automatic_instrumentation/dd_libraries/dotnet-framework/?tab=nuget#install-the-tracer).

### The client library will still be injected when `DD_TRACE_ENABLED=0`

#### What changed?

In version 2.x.x of the tracer, setting the environment variable `DD_TRACE_ENABLED` to `0` or `false` would prevent the client library being injected into your application. In version 3.0.0+, this is no longer the case: the client library will still be injected, but tracing will be disabled. 

#### Why did we change it?
The Datadog client library is no longer focused solely on APM tracing, but disabling the client library entirely could prevent these features from working correctly. There is already a mechanism to prevent instrumenting an application completely, so this additional switch also added confusion.

#### What action should you take?
If you are currently using `DD_TRACE_ENABLED=0` to avoid instrumenting an application, you should instead use `CORECLR_ENABLE_PROFILING=0` (for .NET and .NET Core applications) or `COR_ENABLE_PROFILING=0` (for .NET Framework applications). These variables are provided by the .NET runtime, and ensures the client library is not injected into your process.

If you don't set `COR_ENABLE_PROFILING=0`/`CORECLR_ENABLE_PROFILING=1` and continue to set `DD_TRACE_ENABLED=0`, the client library will be injected but tracing will be disabled and you will not receive traces. 

### Referencing the `Datadog.Trace.AspNet` module is no longer supported

#### What changed?

In version 1.x.x and 2.x.x of the tracer, it was possible to reference the `Datadog.Trace.AspNet` module in your application's `web.config` file, although this wasn't required for ASP.NET support in general. In version 3.x.x, referencing the `Datadog.Trace.AspNet` in your application's `web.config` will cause an error, and may cause your application to fail to start, with the error:

> "Could not load file or assembly 'Datadog.Trace.AspNet' or one of its dependencies. The system cannot find the file specified."

#### Why did we change it?
The `Datadog.Trace.AspNet` module is obsolete and is not required for tracing ASP.NET applications. We are removing the `Datadog.Trace.AspNet` module to remove a point of failure when upgrading the .NET client library. Referencing the module in an application's `web.config` file, and the required installation into the [Global Assembly Cache (GAC)](https://learn.microsoft.com/en-us/dotnet/framework/app-domains/gac), can make the update experience harder; by removing the module, we remove this constraint. 

#### What action should you take?
Remove any references to the `Datadog.Trace.AspNet` module in your application's `web.config file` and anywhere else it is referenced in your IIS configuration. For example, if you have code in the `system.webServer/modules` element that references `Datadog.Trace.AspNet`:

```xml
<system.webServer>
  <modules>
    <remove name="FormsAuthentication" />
    <remove name="SomeOtherModule" />
    <!--  👇 Remove this line  -->
    <add name="DatadogModule" type="Datadog.Trace.AspNet.TracingHttpModule, Datadog.Trace.AspNet"/>
    <add name="MyCustomModule" type="ExampleOrg.MyCustomModule, ExampleOrg.Modules"/>
  </modules>
</system.webServer>
```

Then you should remove the `<add>` line that references `Datadog.Trace.AspNet`:

```xml
<system.webServer>
  <modules>
    <remove name="FormsAuthentication" />
    <remove name="SomeOtherModule" />
    <add name="MyCustomModule" type="ExampleOrg.MyCustomModule, ExampleOrg.Modules"/>
  </modules>
</system.webServer>
```

> [!WARNING]
> **Deprecated APIs and platforms**: The following section describes some of the APIs, supported platforms, and runtimes that are considered deprecated as of version 3.0.0 of the .NET tracer.

### .NET Core 2.1 is now EOL
 
#### What changed?
.NET Core 2.1 is considered EOL as of version 3.0.0 of the .NET tracer. You can continue to use v3.0.0  with applications that run on .NET Core 2.1, but this runtime will receive limited support and testing. With this change, versions 2.0, 2.1, 2.2 and 3.0 of .NET Core are now EOL. There are no changes to supported .NET Framework versions.

#### Why did we change it?
.NET Core 2.1 was marked EOL by Microsoft in August 2021. Similarly .NET Core 2.0, .NET Core 2.2, and .NET Core 3.0 are already considered EOL. This change applies a consistent policy across older runtimes,  allows us to continue to support newer runtimes, and has limited use across customers.

#### What action should you take?
If you're currently running applications on .NET Core 2.1, you can still do so with v3.0.0 of the .NET tracer. However, we strongly suggest updating to [a supported version of .NET Core/.NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#lifecycle). In a future major version of the .NET tracer we intend to remove support for .NET Core 2.1 and other EOL runtimes completely, as described in the following section.

### Datadog.Trace.OpenTracing is now obsolete

#### What changed?
The [Datadog.Trace.OpenTracing](https://www.nuget.org/packages/Datadog.Trace.OpenTracing) NuGet package is considered obsolete, and may be removed in a future major version of .NET Core. The `OpenTracingTracerFactory.CreateTracer()` and `OpenTracingTracerFactory.WrapTracer` methods have been marked `[Obsolete]` to reflect this policy change.

#### Why did we change it?
[The OpenTracing project](https://opentracing.io/) is archived and considered deprecated at this point. Instead, you should consider [moving to OpenTelemetry](https://www.datadoghq.com/knowledge-center/opentelemetry/).

#### What action should you take?
You may continue to use the  [Datadog.Trace.OpenTracing](https://www.nuget.org/packages/Datadog.Trace.OpenTracing) package with v3.0.0+ of the .NET tracer, but you may need to [suppress the `[Obsolete]` compiler warnings](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/preprocessor-directives#pragma-warning). You should consider moving your project to using OpenTelemetry instead of OpenTracing. 

.NET has built in support for OpenTelemetry by way of the `Activity` class, which is supported by Datadog's automatic instrumentation by setting `DD_TRACE_OTEL_ENABLED=true`. Alternatively, you can use vendor agnostic tooling to send telemetry signals to [the OpenTelemetry collector and use the Datadog Exporter](https://docs.datadoghq.com/opentelemetry/collector_exporter) to forward this information to Datadog.

### macOS 11 is no longer supported

#### What changed?
CI Visibility previously supported macOS 11+. In v3.0.0+ CI Visibility will only support macOS 12+.

#### Why did we change it?
macOS 11 no longer receives updates from Apple, and [is being dropped from continuous integration providers](https://learn.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=azure-devops&tabs=yaml#recent-updates) in June 2024.

#### What action should you take?
If you are using CI Visibility with macOS 11, we strongly suggest upgrading to a newer version of macOS. If you cannot upgrade, you can continue to use the 2.x.x version of the .NET tracer but you will receive no feature updates or bug fixes.

> [!IMPORTANT]
> **Future major version policy and plans**: The following section describes our policy around future major versions, and gives advanced warning of the intention to drop various platforms and frameworks.


### Future major version roadmap

#### What changed?
With this major release (v3.0.0) we're announcing the intention to follow a yearly cadence for major version releases of the .NET tracer. For example
- v3.0.0 of the .NET tracer was released in 2024
- v4.0.0 of the .NET tracer will be released in 2025
- v5.0.0 of the .NET tracer will be released in 2026

#### Why did we change it?
Modern .NET follows [a yearly major release cadence](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#cadence). Sometimes a major .NET release requires significant or breaking changes to the .NET tracer to support the latest version. By similarly adopting a yearly cycle for the .NET tracer, we are able to quickly react to any new requirements. It also ensures customers know when to expect major versions of the tracer, so as to incorporate updates into their future roadmap.

Note that this policy does _not_ mean the bar for making breaking changes has been lowered. We will always strive to provide the smoothest upgrade experience for users.

### Planned removal of support for .NET Core 2.x and .NET Core 3.0

#### What changed?
In the next version of the .NET tracer (v4.0.0) we plan to drop support for the following .NET versions:
- .NET Core 2.0
- .NET Core 2.1
- .NET Core 2.2
- .NET Core 3.0

The .NET tracer will no longer instrument applications using these runtimes.

#### Why did we change it?
[These .NET runtime versions are all unsupported by Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core#cadence). By removing support for these runtimes in the Datadog .NET tracer we can reduce overheads for all users and focus on supporting modern frameworks.

### Planned removal of support for some linux distributions

#### What changed?
In the next version of the .NET tracer (v4.0.0) we plan to drop support for the following Linux distributions
- [CentOS 7](https://www.redhat.com/en/topics/linux/centos-linux-eol)
- [CentOS 8](https://www.centos.org/centos-stream/)
- [CentOS Stream 8](https://www.centos.org/centos-stream/)
- [RHEL 7](https://access.redhat.com/support/policy/updates/errata)

The .NET tracer will no longer function on these distributions or any distribution with a [glibc](https://www.gnu.org/software/libc/) version <2.28 (Version 2.0.0 of the .NET tracer supports glibc 2.17)

#### Why did we change it?
The above distributions are either no longer supported or have ceased receiving updates. Similarly, .NET 8 dropped support for these distributions and will no longer run. We are adopting a similar approach to Microsoft to ensure we can take advantage of newer versions of glibc that benefit all our customers.

### Planned removal of support for Datadog.Trace.OpenTracing

#### What changed?
In the next version of the .NET tracer (v4.0.0) we plan to stop producing the [Datadog.Trace.OpenTracing](https://www.nuget.org/packages/Datadog.Trace.OpenTracing) NuGet package. Consider [moving to OpenTelemetry](https://www.datadoghq.com/knowledge-center/opentelemetry/) instead. 

#### Why did we change it?
[The OpenTracing project](https://opentracing.io/) is archived and considered deprecated at this point.

### Planned removal of support for AppAnalytics

#### What changed?
In version v4.0.0, the .NET tracer will no longer include settings for configuring [App Analytics](https://docs.datadoghq.com/tracing/legacy_app_analytics/).

The following settings will be removed:
- `DD_TRACE_ANALYTICS_ENABLED` and corresponding `TracerSettings` property
- `DD_TRACE_<INTEGRATION>_ANALYTICS_ENABLED` and corresponding `IntegrationSettings` property
- `DD_TRACE_<INTEGRATION>_ANALYTICS_SAMPLE_RATE` and corresponding `IntegrationSettings` property

#### Why did we change it?
[App Analytics](https://docs.datadoghq.com/tracing/legacy_app_analytics/) is deprecated. To have full control over your traces, use [ingestion controls and retention filters](https://docs.datadoghq.com/tracing/trace_pipeline) instead.

--- 

## Migrating from v1.x.x to v2.x.x

### .NET Tracer v2.0 contents

The .NET Tracer v2.0:

- Fixes a long standing bug where traces could be disconnected when automatic and custom tracing were using two different versions of the tracer.
- Adds support for .NET 6 and ends support of .NET Framework versions older than 4.6.1. If you are currently targeting version < 4.6.1, we suggest you upgrade in line with Microsoft's guidance.
- Refactored our APIs for an easier and safer use.

For a more complete overview, please refer to our [release notes on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases).

### Upgrading from 1.x to 2.0

#### Code changes

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

#### What if you are relying on .NET Framework lower than 4.6.1

If you are currently targeting version < 4.6.1, we suggest you upgrade in line with [Microsoft's guidance](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework). On our end, we will support fixing major bugs on top of Tracer v1.31.

That said, automatic instrumentation relies only on the CLR (runtime) version, not the compile-time target. Even if an application is built for .NET Framework older than 4.6.1, as long as it runs on a supported CLR version, automatic instrumentation will work normally.

Regarding custom instrumentation, NuGet package version 2.0 and above will no longer include binaries for versions of .NET Framework older than 4.6.1. The NuGet package will only work correctly with applications that target (at compile time):

- .NET Framework 4.6.1 or above
- .NET Core 3.1 or above
- .NET 5 or above
- .NET Standard 2.0 or above (includes .NET Core 2.0 and above)

#### Rollout strategy when using automatic and custom instrumentation

If your application uses both automatic and custom instrumentation, your application traces may be disconnected until both components are upgraded to v2.x. If you are unable to upgrade both components simultaneously, follow the steps below to upgrade the components in the recommended order.

**Note:** To minimize application overhead, align the versions of the automatic and custom instrumentation. Using two different versions of the 2.x .NET Tracer will still result in complete traces but it requires additional overhead, so Datadog recommends minimizing the amount of time that the versions are mismatched.

##### .NET Core and .NET 5+

###### If you were using v1.28.8 or greater

On .NET Core, to avoid disconnected traces during the upgrade process:

1. Upgrade _Datadog.Trace_ NuGet package to the latest version of the tracer
2. Upgrade the automatic instrumentation package to the matching version of the tracer

###### If you were using v1.28.7 or lower

In this upgrade scenario **you should upgrade the automatic instrumentation first** and then upgrade the _Datadog.Trace_ NuGet package. If the components are not upgraded in the correct order, the automatic instrumentation may fail to produce traces.

##### .NET Framework

On .NET Framework there will be disconnected traces between automatic and custom instrumentation until both packages are upgraded to 2.x. In this upgrade scenario, Datadog recommends upgrading the MSI and NuGet package in whichever order is easiest for your application.
