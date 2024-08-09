# Datadog.Trace NuGet package

This package contains the Datadog .NET APM tracer for configuring custom instrumentation.

⚠ Starting with version 3.0.0, this package requires that you also configure automatic instrumentation.
Using this package without automatic instrumentation is no longer supported  

> If you are only using automatic instrumentation, **you do not need this package**. Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for details on how to install the tracer for automatic instrumentation.

> If you are using automatic instrumentation and would like to interact with APM only through C# attributes, see the [Datadog.Trace.Annotations](https://www.nuget.org/packages/Datadog.Trace.Annotations/) NuGet package.

Please note that Datadog does not support tracing (manual or automatic) in partial-trust environments.

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. Configure automatic instrumentation [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/?tab=windows#install-the-tracer).
3. Configure custom instrumentation, as shown below
4. [View your live data on Datadog](https://app.datadoghq.com/apm/traces).

### Configuring Datadog in code

There are multiple ways to configure your application: using environment variables, a `web.config` file, or a `datadog.json` file, [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/#configuration). This NuGet package also allows you to configure settings in code.

To override configuration settings, create an instance of `TracerSettings`, and pass it to the static `Tracer.Configure()` method:

```csharp
using Datadog.Trace;

// Create a settings object using the existing
// environment variables and config sources
var settings = TracerSettings.FromDefaultSources();

// Override a value
settings.GlobalTags.Add("SomeKey", "SomeValue");

// Replace the tracer configuration
Tracer.Configure(settings);
```

Calling `Tracer.Configure()` will replace the settings for all subsequent traces, both for custom instrumentation and for automatic instrumentation.

> :warning: Replacing the configuration should be done once, as early as possible in your application.

 ### Create custom traces

To create and activate a custom span, use `Tracer.Instance.StartActive()`. If a trace is already active (when created by automatic instrumentation, for example), the span will be part of the current trace. If there is no current trace, a new one will be started.

> :warning: Ensure you dispose of the scope returned from StartActive. Disposing the scope will close the span, and ensure the trace is flushed to Datadog once all its spans are closed.

```csharp
using Datadog.Trace;

// Start a new span
using (var scope = Tracer.Instance.StartActive("custom-operation"))
{
    // Do something
}
```

## Release Notes

You can view the [notes for the latest release on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases).

## Upgrading from 2.x to 3.0

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

For a full list of changes and a guide to migrating your application, please see [the migration guide](https://github.com/DataDog/dd-trace-dotnet/blob/master/docs/MIGRATING.md).

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).

