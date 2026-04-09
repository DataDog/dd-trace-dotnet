# Datadog.Trace NuGet package

This package contains the Datadog .NET APM tracer for configuring custom instrumentation.

> **Starting with version 3.0.0**, this package requires that you also configure automatic instrumentation.
> Using this package without automatic instrumentation is no longer supported.

> If you are only using automatic instrumentation, **you do not need this package**. Please [read our documentation](https://docs.datadoghq.com/tracing/setup/dotnet) for details on how to install the tracer for automatic instrumentation.

> If you are using automatic instrumentation and would like to interact with APM only through C# attributes, see the [Datadog.Trace.Annotations](https://www.nuget.org/packages/Datadog.Trace.Annotations/) NuGet package.

Please note that Datadog does not support tracing (manual or automatic) in partial-trust environments.

## Getting Started

1. Configure the Datadog agent for APM [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core#configure-the-datadog-agent-for-apm).
2. Configure automatic instrumentation [as described in our documentation](https://docs.datadoghq.com/tracing/setup_overview/setup/dotnet-core/?tab=windows#install-the-tracer).
3. Configure custom instrumentation, as shown below.
4. [View your live data on Datadog](https://app.datadoghq.com/apm/traces).

## Configuring Datadog in code

There are multiple ways to configure your application: using environment variables, a `web.config` file, or a `datadog.json` file, [as described in our documentation](https://docs.datadoghq.com/tracing/trace_collection/library_config/dotnet-core/). This NuGet package also allows you to configure settings in code.

To override configuration settings, create an instance of `TracerSettings`, and pass it to the static `Tracer.Configure()` method:

```csharp
using Datadog.Trace;
using Datadog.Trace.Configuration;

// Create a settings object using the existing
// environment variables and config sources
var settings = TracerSettings.FromDefaultSources();

// Override settings in code
settings.ServiceName = "my-web-app";
settings.Environment = "production";
settings.ServiceVersion = "1.0.0";
settings.GlobalTags.Add("team", "checkout");

// Replace the tracer configuration
Tracer.Configure(settings);
```

Calling `Tracer.Configure()` will replace the settings for all subsequent traces, both for custom instrumentation and for automatic instrumentation.

> :warning: Replacing the configuration should be done once, as early as possible in your application.

## Create custom traces

To create and activate a custom span, use `Tracer.Instance.StartActive()`. If a trace is already active (when created by automatic instrumentation, for example), the span will be part of the current trace. If there is no current trace, a new one will be started.

> :warning: Ensure you dispose of the scope returned from `StartActive`. Disposing the scope will close the span, and ensure the trace is flushed to Datadog once all its spans are closed.

```csharp
using Datadog.Trace;

// Start a new span
using (var scope = Tracer.Instance.StartActive("custom-operation"))
{
    var span = scope.Span;

    // Set the resource name (what this specific call is doing)
    span.ResourceName = "ProcessOrder";

    // Set the service name (defaults to the application's service name)
    span.ServiceName = "order-service";

    // Set the span type (e.g. "web", "sql", "custom")
    span.Type = "custom";

    // Do your work here
    ProcessOrder(orderId);
}
```

## Add custom tags and metrics to spans

Tags let you attach key-value metadata to spans for filtering and grouping in Datadog.

### String tags

Use `SetTag()` on the span to add string tags:

```csharp
using Datadog.Trace;

using (var scope = Tracer.Instance.StartActive("checkout.process"))
{
    var span = scope.Span;
    span.ResourceName = "ProcessCheckout";

    // Add custom business tags
    span.SetTag("user.id", userId);
    span.SetTag("order.id", orderId);
    span.SetTag("payment.method", "credit_card");
    span.SetTag("cart.item_count", itemCount.ToString());

    // Use built-in tag constants for standard fields
    span.SetTag(Tags.HttpMethod, "POST");
    span.SetTag(Tags.HttpUrl, "/api/checkout");

    ProcessCheckout(userId, orderId);
}
```

### Numeric metrics

Use the `SetTag()` extension method with a `double?` parameter to add numeric metrics:

```csharp
using Datadog.Trace;

using (var scope = Tracer.Instance.StartActive("payment.charge"))
{
    var span = scope.Span;
    span.ResourceName = "ChargePayment";

    // Add numeric metrics
    span.SetTag("payment.amount", 99.95);
    span.SetTag("payment.items", 3.0);
    span.SetTag("payment.discount_percent", 15.0);

    ChargePayment(orderId, amount);
}
```

## Instrument a database query

When automatic instrumentation doesn't cover a specific database client, you can manually wrap database calls in spans:

```csharp
using Datadog.Trace;
using System.Data.SqlClient;

public int ExecuteQuery(string connectionString, string query)
{
    using (var scope = Tracer.Instance.StartActive("sql.query"))
    {
        var span = scope.Span;
        span.ResourceName = query;
        span.Type = SpanTypes.Sql;
        span.SetTag(Tags.DbType, "sql-server");
        span.SetTag(Tags.SqlQuery, query);
        span.SetTag(Tags.DbName, "OrdersDb");

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand(query, connection);
            int rowsAffected = command.ExecuteNonQuery();

            span.SetTag(Tags.SqlRows, rowsAffected.ToString());
            return rowsAffected;
        }
        catch (Exception ex)
        {
            span.SetException(ex);
            throw;
        }
    }
}
```

> **Note**: ADO.NET libraries like `System.Data.SqlClient`, `Microsoft.Data.SqlClient`, `Npgsql`, and `MySql.Data` are automatically instrumented when automatic instrumentation is enabled. You only need manual instrumentation for unsupported database clients or when you want to add custom tags.

## Create child spans (parent-child relationships)

### Automatic parenting

When you nest `StartActive()` calls, child spans are automatically linked to their parent:

```csharp
using Datadog.Trace;

// Parent span
using (var outerScope = Tracer.Instance.StartActive("web.request"))
{
    outerScope.Span.ResourceName = "GET /api/orders";

    // This span automatically becomes a child of "web.request"
    using (var innerScope = Tracer.Instance.StartActive("sql.query"))
    {
        innerScope.Span.ResourceName = "SELECT * FROM orders";
        innerScope.Span.Type = SpanTypes.Sql;

        // Execute the query
    }

    // This span is also a child of "web.request"
    using (var cacheScope = Tracer.Instance.StartActive("cache.lookup"))
    {
        cacheScope.Span.ResourceName = "GET order:123";
    }
}
```

### Explicit parent context

To explicitly set a parent span, use `SpanCreationSettings`:

```csharp
using Datadog.Trace;

// Use a previously captured span context as parent
ISpanContext parentContext = previousScope.Span.Context;
var settings = new SpanCreationSettings { Parent = parentContext };

using (var scope = Tracer.Instance.StartActive("child-operation", settings))
{
    scope.Span.ResourceName = "ChildWork";
    // This span is a child of the specified parent, regardless of the active scope
}
```

### Create a root span (no parent)

To create a span that is **not** a child of the currently active span, use `SpanContext.None`:

```csharp
using Datadog.Trace;

// This span will start a new trace, even if there's an active span
var settings = new SpanCreationSettings { Parent = SpanContext.None };

using (var scope = Tracer.Instance.StartActive("background-job", settings))
{
    scope.Span.ResourceName = "CleanupExpiredSessions";
    // This is a root span in its own trace
}
```

## Error handling

Mark a span as an error and attach exception details:

```csharp
using Datadog.Trace;

using (var scope = Tracer.Instance.StartActive("risky-operation"))
{
    var span = scope.Span;
    span.ResourceName = "ProcessPayment";

    try
    {
        ProcessPayment(orderId);
    }
    catch (Exception ex)
    {
        // Record the exception on the span (sets error flag, message, type, and stack trace)
        span.SetException(ex);
        throw;
    }
}
```

You can also set the error flag manually without an exception:

```csharp
using Datadog.Trace;

using (var scope = Tracer.Instance.StartActive("validation"))
{
    var span = scope.Span;

    var result = ValidateInput(input);
    if (!result.IsValid)
    {
        span.Error = true;
        span.SetTag(Tags.ErrorMsg, result.ErrorMessage);
    }
}
```

## Manual sampling control

Override the automatic sampling decision for a specific trace:

```csharp
using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;

using (var scope = Tracer.Instance.StartActive("critical-operation"))
{
    var span = scope.Span;

    // Force this trace to be kept (sent to Datadog), regardless of sampling rules
    span.SetTraceSamplingPriority(SamplingPriority.UserKeep);

    ProcessCriticalTransaction();
}
```

You can also use tag-based sampling control:

```csharp
using Datadog.Trace;

using (var scope = Tracer.Instance.StartActive("operation"))
{
    // Keep this trace
    scope.Span.SetTag(Tags.ManualKeep, "true");

    // Or drop this trace
    // scope.Span.SetTag(Tags.ManualDrop, "true");
}
```

## Trace context propagation for unsupported libraries

The tracer automatically propagates trace context for [supported libraries](https://docs.datadoghq.com/tracing/trace_collection/compatibility/dotnet-core/). For libraries that are not automatically instrumented (such as custom message queues), you can manually inject and extract trace context using `SpanContextInjector` and `SpanContextExtractor`.

### Injecting context (producer/sender side)

When sending a message, inject the current trace context into the message headers:

```csharp
using Datadog.Trace;

public void SendMessage(MyMessage message)
{
    using (var scope = Tracer.Instance.StartActive("queue.produce"))
    {
        var span = scope.Span;
        span.ResourceName = $"Produce {message.Topic}";
        span.Type = "queue";
        span.SetTag("messaging.system", "custom-queue");
        span.SetTag("messaging.destination", message.Topic);

        // Inject trace context into message headers
        var injector = new SpanContextInjector();
        injector.Inject(
            message.Headers,
            (headers, key, value) => headers[key] = value,
            span.Context);

        _queue.Send(message);
    }
}
```

### Extracting context (consumer/receiver side)

When receiving a message, extract the trace context from the message headers and use it as the parent:

```csharp
using Datadog.Trace;

public void HandleMessage(MyMessage message)
{
    // Extract trace context from message headers
    var extractor = new SpanContextExtractor();
    var parentContext = extractor.Extract(
        message.Headers,
        (headers, key) => headers.TryGetValue(key, out var value)
            ? new[] { value }
            : Enumerable.Empty<string?>());

    // Create a span with the extracted context as parent
    var settings = new SpanCreationSettings { Parent = parentContext };

    using (var scope = Tracer.Instance.StartActive("queue.consume", settings))
    {
        var span = scope.Span;
        span.ResourceName = $"Consume {message.Topic}";
        span.Type = "queue";
        span.SetTag("messaging.system", "custom-queue");
        span.SetTag("messaging.destination", message.Topic);

        ProcessMessage(message);
    }
}
```

This pattern enables end-to-end distributed tracing across services connected by any messaging system, even when the tracer doesn't natively support it.

## Flushing traces

When your application is shutting down or running in a short-lived process, call `ForceFlushAsync()` to ensure all pending traces are sent:

```csharp
using Datadog.Trace;

// Flush all pending traces before shutdown
await Tracer.Instance.ForceFlushAsync();
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
