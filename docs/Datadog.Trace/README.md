# Datadog.Trace NuGet package

This package contains the Datadog .NET APM tracer for configuring custom instrumentation.

> If you are only using automatic tracing, **you do not need this package**. Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for details on how to install the tracer for automatic instrumentation. 

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. For automatic instrumentation, install and enable the tracer [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/?tab=windows#install-the-tracer). 
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

## Upgrading from 1.x to 2.0

.NET Tracer 2.0 introduces a number of breaking changes to the API which allow various performance improvements, add new features, and deprecate problematic ways of using the package. Most of these changes do not require any changes to your code, but some patterns are no longer supported or recommended.

This section describes some of the most important breaking changes. For full details see [the release notes on GitHub](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v2.0.0).

### Supported .NET versions

.NET Tracer 2.0 adds support for .NET 6.0 and raises the minimum supported version of .NET Framework from .NET Framework 4.5 to .NET Framework 4.6.1. If you are currently targeting version < 4.6.1, we suggest you upgrade [in line with Microsoft's guidance](https://docs.microsoft.com/en-us/lifecycle/products/microsoft-net-framework).

For full details of supported versions, see [our documentation on .NET Framework compatibility requirements](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-framework) and [.NET/.NET Core compatibility requirements](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-core).

### Singleton Tracer instances

In version 1.x, you could create new `Tracer` instances with different settings for each instance, using `new Tracer()`. In version 2.0 this constructor is marked `[Obsolete]` and it is no longer possible to create `Tracer` instances with different settings. This was done to avoid multiple problematic patterns that were hard for users to detect.

Update your code to the following:

```csharp
using Datadog.Trace;

// Create your settings as before
var settings = new TracerSettings();

// var tracer = new Tracer(settings) // <- Delete this line
Tracer.Configure(settings);          // <- Add this line
```

### TracerSettings are immutable

In version 1.x the `TracerSettings` object passed to a `Tracer` instance could be modified later. Depending on the changes, the tracer may or may respect them. In version 2.0, an `ImmutableTracerSettings` object is created when the `Tracer` instance is configured. Subsequent changes to the `TracerSettings` instance will not be observed by `Tracer`.

This change should not require changes to your code, but the property `Tracer.Settings` now returns `ImmutableTracerSettings`, not `TracerSettings`.

```csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();
settings.TraceEnabled = false;   // TracerSettings are mutable 

Tracer.Configure(settings);

// All properties on Tracer.Settings are now read-only 
// Tracer.Instance.Settings.TraceEnabled = false; // <- DOES NOT COMPILE
```

### Exporter settings

Exporter-related settings were grouped into the `TracerSettings.Exporter` property.

csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();
// settings.AgentUri = "http://localhost:8126";        // <- Delete this line
settings.Exporter.AgentUri = "http://localhost:8126";  // <- Add this line

### Configure ADO.NET integrations individually

In version 1.x, you could configure automatic instrumentation of all ADO.NET integrations using the `AdoNet` integration ID. In version 2.0, you can now also disable specific integrations using the following integration IDs:

* `MySql`
* `Npgsql` (PostgreSQL)
* `Oracle`
* `SqlClient` (SQL Server)
* `Sqlite`

This allows you to disable specific ADO.NET integrations if required. [See our documentation for a complete list of supported integration IDS](https://docs.datadoghq.com/tracing/setup_overview/compatibility_requirements/dotnet-core/#integrations). Note that you can still disable _all_ ADO.NET integrations using the `AdoNet` integration ID.

This change also removes the now-obsolete `TracerSettings.AdoNetExcludedTypes` setting and the corresponding environment variable `DD_TRACE_ADONET_EXCLUDED_TYPES`. Replace usages of these with `TracerSettings.Integrations["<INTEGRATION_NAME>"].Enabled` and `DD_TRACE_<INTEGRATION_NAME>_ENABLED`, respectively:

```csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();

// settings.AdoNetExcludedTypes.Add("MySql");      // <- Delete this line
settings.Integrations["MySql"].Enabled = false; // <- Add this line
```

### `ElasticsearchNet5` integration ID has been removed

In version 1.x, the integration ID for version 5.x of `Elasticsearch.Net` was `ElasticsearchNet5`, and the integration ID for version 6.x+ was `ElasticsearchNet`. In version 2.0, `ElasticsearchNet5` has been removed. Use `ElasticsearchNet` for all versions of `Elasticsearch.Net`.

Replace usages of `ElasticsearchNet5` with `ElasticsearchNet`

```csharp
using Datadog.Trace;

var settings = TracerSettings.FromDefaultSources();

settings.Integrations["ElasticsearchNet5"].Enabled = false; // <- Delete this line
settings.Integrations["ElasticsearchNet"].Enabled = false;  // <- Add this line
```

### Obsolete APIs have been removed

The following deprecated APIs were removed.

* `TracerSettings.DebugEnabled` was removed. Set the `DD_TRACE_DEBUG` environment variable to `1` to enable debug mode.
* `Tags.ForceDrop` and `Tags.ForceKeep` were removed. Use `Tags.ManualDrop` and `Tags.ManualKeep` respectively instead.
* `SpanTypes` associated with automatic instrumentation spans, such as `MongoDb` and `Redis`, were removed.
* `Tags` associated with automatic instrumentation spans, such as `AmqpCommand` and `CosmosDbContainer`, were removed.
* `Tracer.Create()` was removed. Use `Tracer.Configure()` instead.
* `TracerSettings.AdoNetExcludedTypes` was removed. Use `TracerSettings.Integrations` to configure ADO.NET automatic instrumentation.
* Various internal APIs not intended for public consumption were removed.

In addition, some settings were marked obsolete in 2.0:

* Environment variables `DD_TRACE_ANALYTICS_ENABLED`, `DD_TRACE_{0}_ANALYTICS_ENABLED`, and `DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE` for controlling App Analytics are obsolete. App Analytics has been replaced with Tracing Without Limits. See [our documentation for details](https://docs.datadoghq.com/tracing/legacy_app_analytics/).
* `TracerSettings.AnalyticsEnabled`, `IntegrationSettings.AnalyticsEnabled`, and `IntegrationSettings.AnalyticsSampleRate` were marked obsolete.
* Environment variable `DD_TRACE_LOG_PATH` is deprecated. Use `DD_TRACE_LOG_DIRECTORY` instead.

### Introduction of `ISpan` and `IScope`

Version 2.0 makes the public `Scope` and `Span` classes internal. Instead, we now expose public `IScope` and `ISpan` interfaces and the `Tracer` API was updated accordingly. If you are currently using explicit types (instead of interring types with `var`), replace usages of `Scope` with `IScope` and `Span` with `ISpan`:

```csharp
using Datadog.Trace;

// No changes required here
using (var scope = Tracer.Instance.StartActive("my-operation"))
{
}

// No Longer compiles (incorrect usage of Scope)
using (Scope scope = Tracer.Instance.StartActive("my-operation"))
{
}

// Correct usage with explicit type
using (IScope scope = Tracer.Instance.StartActive("my-operation"))
{
}
```

### Simplification of the tracer interface

In addition to returning `IScope`, the `Tracer.StartActive` method was simplified by replacing several parameters with a single `SpanCreationSettings`. We have also removed the possibility to override the service name from `Tracer.StartActive`. Instead, set `Span.ServiceName` after creating the span.


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

### Automatic instrumentation changes

#### Default value of feature flag `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` is now `true`

Version 1.26.0 added support for the `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` feature flag, which enabled improved span names for ASP.NET and ASP.NET Core automatic instrumentation spans, an additional span for ASP.NET Core requests, and additional tags.

In version 2.0, `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` is **enabled** by default. Due to the change in span names, you may need to update your monitors and dashboards to use the new resource names.

If you do not wish to take advantage of the improved route names, you can disabled the feature by setting the `DD_TRACE_ROUTE_TEMPLATE_RESOURCE_NAMES_ENABLED` environment variable to `0`.

#### Call-site instrumentation removed

Call-site automatic instrumentation was removed in .NET Tracer 2.0 and replaced with call-target instrumentation. This was the default mode [since version 1.28.0](https://github.com/DataDog/dd-trace-dotnet/releases/tag/v1.28.0). Call-target instrumentation provides performance and reliability improvements over call-site instrumentation, but it does not support instrumenting custom implementations of `DbCommand` for now. If you find spans are missing from your traces after upgrading, please raise an issue on GitHub, or contact [support](https://docs.datadoghq.com/help).

#### `DD_INTEGRATIONS` environment variable no longer needed

The `integrations.json` file is no longer required for instrumentation. You can remove references to this file, for example by deleting the `DD_INTEGRATIONS` environment variable.

## Get in touch

If you have questions, feedback, or feature requests, reach our [support](https://docs.datadoghq.com/help).
