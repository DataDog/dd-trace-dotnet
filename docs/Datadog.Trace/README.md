# Datadog.Trace NuGet package

This package contains the Datadog .NET APM tracer for configuring custom instrumentation.

> If you are only using automatic tracing, **you do not need this package**. Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for details on how to install the tracer for automatic instrumentation. 

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. For automatic instrumentation, install and enable the tracer [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/?tab=windows#install-the-tracer). 
3. Configure custom instrumentation, as shown below
4. [View your live data on Datadog](https://app.datadoghq.com/apm/traces).

### Configuring Datadog in code

There are multiple ways to configure your application, for example using Environment variables, _web.config_, or _datadog.json_, [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/#configuration). This NuGet package also allows you to configure settings in code.

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
using (var scope = Tracer.Instance.StartActive("custom-operation")
{
    // Do something
}
```

## Release Notes

You can view the [notes for the latest release on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases).  

## Upgrading from 1.x to 2.0

Version 2.0 of this package introduced a number of breaking changes to the API which allowed various performance improvements, added new features, and deprecated problematic ways of using the package. Most of these changes will not require any changes to your code, but some patterns are no longer supported or recommended.

This section describes some of the most important breaking changes. For full details see [the release notes on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.0.0).

## Updates for supported .NET versions

Version 2.0 of _Datadog.Trace_ added support for .NET Core 6.0 and raised the minimum supported version of .NET Framework from .NET Framework 4.5 to .NET Framework 4.6.1. If you are currently targeting version < 4.6.1, we suggest you upgrade [in line with Microsoft's guidance](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework).

For full details of supported versions, see [our documentation on .NET Framework compatibility requirements](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-framework) and [.NET/.NET Core compatibility requirements](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-core).  

### Singleton Tracer instances

In version 1.x, you could create new `Tracer` instances with different settings for each instance, using `new Tracer()`. From version 2.0 this constructor is marked `[Obsolete]`, and it is no longer possible to create `Tracer` instances with different settings. This was done to avoid multiple problematic patterns that were hard for users to detect. 

Update your code to the following:

```csharp
using Datadog.Trace;

// Create your settings as before
var settings = new TracerSettings();

// var tracer = new Tracer(settings) // <- Delete this line
Tracer.Configure(settings);          // <- Add this line
```

### TracerSettings are immutable

In version 1.x the `TracerSettings` object passed to a `Tracer` instance could be subsequently modified. Depending on the changes, these may or may not have been respected. In version 2.0, an `ImmutableTracerSettings` object is created when the `Tracer` instance is configured. Subsequent changes to the `TracerSettings` instance will not be observed by `Tracer`. 

This change should not generally require changes to your code, but the type of `Tracer.Settings` is now an `ImmutableTracerSettings` instance, not a `TracerSettings` instance.

```csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();
settings.TraceEnabled = false;   // TracerSettings are mutable 

Tracer.Configure(settings);

// All properties on Settings are now read-only 
// Tracer.Instance.Settings.TraceEnabled = false; // <- DOES NOT COMPILE
```

### ADO.NET integrations can be disabled individually

In version 1.x, you could disable automatic instrumentation of all ADO.NET integrations using the `AdoNet` integration ID. From version 2.0, this integration ID has been removed and replaced with the following integration IDs:

* `MySql`
* `Npgsql` (PostreSQL)
* `Oracle`
* `SqlClient` (SQL Server)
* `Sqlite`

This enables you to disable specific ADO.NET integrations if required. [See our documentation for a complete list of supported integration IDS](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-core/#integrations).

This change also removes the now-obsolete `TracerSettings.AdoNetExcludedTypes` setting, and corresponding environment variable `DD_TRACE_ADONET_EXCLUDED_TYPES`. Replace usages of these (for an example integration ID `MyIntegrationId`) with `TracerSettings.Integrations["MyIntegrationId"].Enabled` and `DD_TRACE_MyIntegrationId_ENABLED` respectively: 

```csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();

settings.AdoNetExcludedTypes.Add("MySql");      // <- Delete this line
settings.Integrations["MySql"].Enabled = false; // <- Add this line
```

### ElasticsearchNet5 integration ID has been removed

In version 1.x, the integration ID for `Elasticsearch.Net` in version 5.x was `ElasticsearchNet5`, and for version 6.x+ was `ElasticsearchNet`. From version 2.0, support for the ID `ElasticsearchNet5` has been removed, and instead `ElasticsearchNet` now can be used to  

Replace usages of `ElasticsearchNet5` with `ElasticsearchNet`

```csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();

settings.Integrations["ElasticsearchNet5"].Enabled = false; // <- Delete this line
settings.Integrations["ElasticsearchNet"].Enabled = false;  // <- Add this line
```

### Obsolete APIs have been removed

The following deprecated APIs have been removed.

* `TracerSettings.DebugEnabled`: Enable debug mode by setting the `DD_TRACE_DEBUG` environment variable to `1`
* `Tags.ForceDrop` and `Tags.ForceKeep` have been removed. Use `Tags.ManualDrop` and `Tags.ManualKeep` respectively instead.
* `SpanTypes` associated with automatic instrumentation spans, such as `MongoDb` and `Redis` have been removed.  
* `Tags` associated with automatic instrumentation spans, such as `AmqpCommand` and `CosmosDbContainer` have been removed.
* `Tracer.Create()` has been removed. Use `Tracer.Configure()` instead.
* `TracerSettings.AdoNetExcludedTypes` has been removed. Use `TracerSettings.Integrations` to disable automatic instrumentation for ADO.NET integrations in the same way as other integrations.
* Various internal APIs that were not intended for public consumption have been removed. 

In addition, some settings have been marked obsolete:

* Environment variables `DD_TRACE_ANALYTICS_ENABLED`, `DD_TRACE_{0}_ANALYTICS_ENABLED`, and `DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE` for controlling App Analytics are obsolete. App Analytics has been replaced with Tracing Without Limits. See [our documentation for details](https://docs.datadoghq.com/tracing/legacy_app_analytics/).
* Setting `TracerSettings.AnalyticsEnabled` has similarly been marked obsolete.
* Environment variable `DD_TRACE_LOG_PATH` is deprecated. Use `DD_TRACE_LOG_DIRECTORY` instead.

### Introduction of ISpan and IScope

In version 2.0 the public `Tracer` API has been updated to expose `IScope` and `ISpan` instead of `Scope` and `Span`. In most cases, this should not require any changes to your code, but if you are currently using explicit types (instead of inferred types), then you will need to replace usages of `Scope` with `IScope` and `Span` with `ISpan`:

```csharp
using Datadog.Trace;

// no changes required here
using (var scope = Tracer.Instance.StartActive("my-operation")
{
}

// No Longer compiles (incorrect usage of Scope)
using (Scope scope = Tracer.Instance.StartActive("my-operation")
{
}

// Correct usage with explicit type
using (IScope scope = Tracer.Instance.StartActive("my-operation")
{
}
```

### Incorrect integration names are ignored

In version 2.0, any changes made to an `IntegrationSettings` object for an unknown `IntegrationId` will not be persisted. Instead, a warning will be logged describing the invalid access.

```csharp
using Datadog.Trace;

var tracerSettings = TracerSettings.FromDefaultSources();

// Accessing settings for an unknown integration will log a warning
var settings = tracerSettings .Integrations["MyRandomIntegration"];

// changes are not persisted
settings.Enabled = false; 

// isEnabled is null, not false
bool? isEnabled = tracerSettings.Integrations["MyRandomIntegration"].Enabled;
```

### Default value of `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` is now `true`

Version 1.26.0 added support for the `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` feature flag, which enabled improved span names for ASP.NET and ASP.NET Core automatic instrumentation spans, an additional span for ASP.NET Core requests, and additional tags. 

In version 2.0, `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` is **enabled** by default. Due to the change in span names, you may need to update your monitors and dashboards to use the new resource names. 

If you do not wish to take advantage of the improved route names, you can disabled the feature by setting the `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` environment variable to `0`.

### CallSite instrumentation has been removed

From version 2.0, callsite automatic instrumentation has been removed and replaced with calltarget instrumentation. This has been the default mode [since version 1.28.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.0). Calltarget provides performance improvements over callsite, but we no longer support instrumenting some custom implementations of `DbCommand`. If you find spans are missing from your traces after upgrading, please raise an issue on GitHub, or contact [support](https://docs.datadoghq.com/help).

### Integrations.json has been removed

The _integrations.json_ file is no required for instrumentation. You can remove references to this file, for example by deleting the `DD_INTEGRATIONS` environment variable.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
