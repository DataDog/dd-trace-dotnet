using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Samples.OpenTelemetrySdk;

public static class Program
{
    internal static readonly string _additionalActivitySourceName = "AdditionalActivitySource";

    private static SpanContext _previousSpanContext;
    private static Tracer _tracer;
    private static readonly ActivitySource _additionalActivitySource = new(_additionalActivitySourceName);

    public static async Task Main(string[] args)
    {
        var serviceName = "MyServiceName";
        var serviceVersion = "1.0.x";

        var otherLibraryName = "OtherLibrary";
        var otherLibraryVersion = "4.0.0";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .AddSource(otherLibraryName)
            .AddActivitySourceIfEnvironmentVariablePresent()
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .AddConsoleExporter()
            .AddOtlpExporterIfEnvironmentVariablePresent()
            .Build();

        _tracer = tracerProvider.GetTracer(serviceName); // The version is omitted so the ActivitySource.Version / otel.library.version is not set
        var _otherLibraryTracer = tracerProvider.GetTracer(otherLibraryName, version: otherLibraryVersion);

        TelemetrySpan span = null;
        using (span = _tracer.StartActiveSpan("SayHello"))
        {
            // Change tracestate before print statement so previous<->current comparison is accurate
            Activity.Current.TraceStateString = "app=hello";
            PrintSpanStartedInformation(span);

            await RunStartSpanOverloadsAsync(span);
            RunSetAttributeOverloads(span);
            RunAddEventOverloads(span);
            RunSpanUpdateMethods(span);
            RunSpecialTagRemappers(span);

            TelemetrySpan otherSpan = null;
            using (otherSpan = _otherLibraryTracer.StartActiveSpan("Response"))
            {
                PrintSpanStartedInformation(otherSpan);
            }

            PrintSpanClosedInformation(otherSpan);
        }

        PrintSpanClosedInformation(span);

        // There is no active span, so the default behavior will result in a new trace
        // Note: StartSpan does not update the active span, so when the call returns there will still be no active span
        using (var span2 = _tracer.StartSpan("SayHello2", SpanKind.Internal, parentContext: default, links: new Link[] { new(span.Context, new SpanAttributes()) }))
        {
            // There is no active span, so the default behavior will result in a new trace
            // Note: StartSpan does not update the active span, so when the call returns there will still be no active span
            using var span3 = _tracer.StartSpan("SayHello3", SpanKind.Internal, parentSpan: default, links: new Link[] { new(span.Context, new SpanAttributes()), new(span2.Context, new SpanAttributes()) });
        }

        // Use the built-in System.Diagnostics.ActivitySource to generate an Activity (and a respective Datadog span)
        //
        // Note: If the ActivitySource is added to the TracerProviderBuilder, the service name will be set to the OTEL service.name resource.
        // Otherwise, the service name will be set to the Datadog Tracer's service name (can be configured with DD_SERVICE)
        using (var localActivity = _additionalActivitySource.StartActivity(name: "Transform"))
        {
            Thread.Sleep(100);
        }

        using (var operationNameRoot = _tracer.StartActiveSpan("OperationNameRootSpan"))
        {
            RunSpanOperationName();
            RunSpanReservedAttributes();
        }

        using var missingServiceTracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MissingServiceName")
            .AddActivitySourceIfEnvironmentVariablePresent()
            .AddConsoleExporter()
            .AddOtlpExporterIfEnvironmentVariablePresent()
            .Build();

        var missingServiceTracer = missingServiceTracerProvider.GetTracer("MissingServiceName");

        using (var unknownServiceSpan = missingServiceTracer.StartRootSpan("service.name should be the DefaultServiceName value"))
        {
            Thread.Sleep(100);
        }

    }

    private static async Task RunStartSpanOverloadsAsync(TelemetrySpan span)
    {
        // Test the following span creation methods:
        // - StartActiveSpan
        // - StartSpan
        // - StartRootSpan

        // This call does not change the active span, so any new spans created without an explicit parent will not become child spans of this one
        using var nonActiveSpan = _tracer.StartSpan("StartSpan");
        PrintSpanStartedInformation(nonActiveSpan);

        // This call does not change the active span, so any new spans created without an explicit parent will not become child spans of this one
        using var nonActiveChildSpan = _tracer.StartSpan("StartSpan2", SpanKind.Internal);
        PrintSpanStartedInformation(nonActiveChildSpan);

        // Prior to v1.7.0 the new span is still a child of the active span - this behavior appears to be a bug in OpenTelemetry.
        // After v1.7.0 the new span is no longer a child of the active span.
        using var rootSpan = _tracer.StartRootSpan("StartRootSpan");
        PrintSpanStartedInformation(rootSpan);

        // Use StartActiveSpan with a parent TelemetrySpan. There are two things to note here:
        // 1) The parent span will be the specified span instead of the currently active span
        // 2) Upon leaving this scope, the active span should be reset back to the previous active span, NOT to the parent span
        var childSpan = _tracer.StartActiveSpan("StartActiveSpan.Child", SpanKind.Internal, nonActiveSpan);
        using (childSpan)
        {
            PrintSpanStartedInformation(childSpan);
            await Task.Delay(100);
        }

        PrintSpanClosedInformation(childSpan);
    }


    private static void RunSetAttributeOverloads(TelemetrySpan span)
    {
        span.SetAttribute("operation.name", "Saying hello!");

        span.SetAttribute("attribute-string", "\"str");
        span.SetAttribute("attribute-int", 1);
        span.SetAttribute("attribute-bool", true);
        span.SetAttribute("attribute-double", 2.0);
        span.SetAttribute("attribute-stringArray", new string[] { "\"str1\"", "str2", "str3" });
        span.SetAttribute("attribute-stringArrayEmpty", new string[] { });
        span.SetAttribute("attribute-intArray", new int[] { 1, 2, 3 });
        span.SetAttribute("attribute-intArrayEmpty", new int[] { });
        span.SetAttribute("attribute-boolArray", new bool[] { false, true, false });
        span.SetAttribute("attribute-boolArrayEmpty", new bool[] { });
        span.SetAttribute("attribute-doubleArray", new double[] { 4.1, 5.0, 6.0 });
        span.SetAttribute("attribute-doubleArrayEmpty", new double[] { });
    }

    private static void RunAddEventOverloads(TelemetrySpan span)
    {
        SpanAttributes attributes = new(new Dictionary<string, object>
        {
            { "foo", 1 },
            { "bar", "Hello, World!" },
            { "baz", new int[] { 1, 2, 3 } },
            { "strings", new string[] { "str", "1" } },
            { "ignored_array_of_object", new object[] { "str", 2 } },
            { "ignored_array_of_arrays", new string[][] { new string[] { "arr1_val1"}, new string[] { "arr2_val1" } } },
            { "ignored_dict", new Dictionary<string, string> { { "ignored_key", "ignored_value" } } }
        });

        span.AddEvent("event-message");
        span.AddEvent("event-messageWithDateTime", new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero));
        span.AddEvent("event-messageWithAttributes", attributes);
        span.AddEvent("event-messageWithDateTimeAndAttributes", new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero), attributes);
    }

    private static void RunSpecialTagRemappers(TelemetrySpan span)
    {
        // these tags should go to the "meta" tags
        // this is the current tag name
        using (var httpSpan = _tracer.StartActiveSpan("SomeHttpSpan"))
        {
            httpSpan.SetAttribute("http.response.status_code", 404);
        }

        // this is the deprecated tag name
        using (var httpSpanDeprecated = _tracer.StartActiveSpan("DeprecatedHttpStatusTagName"))
        {
            httpSpanDeprecated.SetAttribute("http.status_code", 404);
        }

    }

    private static void RunSpanUpdateMethods(TelemetrySpan span)
    {
        // Test the following span update methods:
        // - SetStatus
        // - RecordException
        // - UpdateName
        using (var innerSpan = _tracer.StartActiveSpan("InnerSpanOk"))
        {
            innerSpan.SetStatus(Status.Ok);
        }

        using (var innerSpan = _tracer.StartActiveSpan("InnerSpanError"))
        {
            innerSpan.SetStatus(Status.Error.WithDescription("Something went wrong"));
        }

        using (var innerSpan = _tracer.StartActiveSpan("InnerSpan"))
        {
            // if we don't set the span as an error we won't check it for exception event
            // so if we only do RecordException, we wouldn't copy that info over as the span isn't marked as an Error
            // see OpenTelemetry's example for .RecordException:
            //  https://github.com/open-telemetry/opentelemetry-dotnet/tree/2916b2de80522d4b1cafe353b3fda3fd629ddb00/docs/trace/reporting-exceptions#option-4---use-activityrecordexception
            try
            {
                throw new ArgumentException("Example argument exception");
            }
            catch (Exception ex)
            {
                innerSpan.SetStatus(Status.Error.WithDescription(ex.Message));
                innerSpan.RecordException(ex);
                innerSpan.UpdateName("InnerSpanUpdated");
            }
        }
    }

    /// <summary>
    /// Note: These test cases were copy/pasted from parametric tests (with some find/replace to make it work here)
    /// </summary>
    private static IEnumerable<object[]> OperationNameData =>
        new List<object[]>
        {
                // expected_operation_name, span_kind, tags_related_to_operation_name
                new object[] { "http.server.request", SpanKind.Server, new Dictionary<string, object>() { { "http.request.method", "GET" } } },
                new object[] { "http.client.request", SpanKind.Client, new Dictionary<string, object>() { { "http.request.method", "GET" } } },
                new object[] { "redis.query", SpanKind.Client, new Dictionary<string, object>() { { "db.system", "Redis" } } },
                new object[] { "kafka.receive", SpanKind.Client, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", SpanKind.Server, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", SpanKind.Producer, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", SpanKind.Consumer, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "aws.s3.request", SpanKind.Client, new Dictionary<string, object>() { { "rpc.system", "aws-api" }, { "rpc.service", "S3" } } },
                new object[] { "aws.client.request", SpanKind.Client, new Dictionary<string, object>() { { "rpc.system", "aws-api" } } },
                new object[] { "grpc.client.request", SpanKind.Client, new Dictionary<string, object>() { { "rpc.system", "GRPC" } } },
                new object[] { "grpc.server.request", SpanKind.Server, new Dictionary<string, object>() { { "rpc.system", "GRPC" } } },
                new object[] { "aws.my-function.invoke", SpanKind.Client, new Dictionary<string, object>() { { "faas.invoked_provider", "aws" }, { "faas.invoked_name", "My-Function" } } },
                new object[] { "datasource.invoke", SpanKind.Server, new Dictionary<string, object>() { { "faas.trigger", "Datasource" } } },
                new object[] { "graphql.server.request", SpanKind.Server, new Dictionary<string, object>() { { "graphql.operation.type", "query" } } },
                new object[] { "amqp.server.request", SpanKind.Server, new Dictionary<string, object>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "server.request", SpanKind.Server, new Dictionary<string, object>() },
                new object[] { "amqp.client.request", SpanKind.Client, new Dictionary<string, object>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "client.request", SpanKind.Client, new Dictionary<string, object>() },
                new object[] { "internal", SpanKind.Internal, new Dictionary<string, object>() },
                new object[] { "consumer", SpanKind.Consumer, new Dictionary<string, object>() },
                new object[] { "producer", SpanKind.Producer, new Dictionary<string, object>() },
                // new object[] { "otel_unknown", null, new Dictionary<string, object>() }, // always should have a span kind due to Activity5+
        };

    private static void RunSpanOperationName()
    {
        foreach(var data in OperationNameData)
        {
            SpanAttributes attributes = new SpanAttributes((Dictionary<string, object>)data[2]);
            using var span = _tracer.StartActiveSpan("ResourceName", kind: (SpanKind)data[1], initialAttributes: attributes);
        }
    }

    private static void RunSpanReservedAttributes()
    {
        var tags = new Dictionary<string, object>();
        tags.Add("http.request.method", "GET"); // operation name would be "http.server.request" (without the override)
        tags.Add("resource.name", "ResourceNameOverride");
        tags.Add("operation.name", "OperationNameOverride");
        tags.Add("service.name", "ServiceNameOverride");
        tags.Add("span.type", "SpanTypeOverride");
        tags.Add("analytics.event", "true"); // metric->  _dd1.sr.eausr: 1.0
        SpanAttributes attributes = new SpanAttributes(tags);

        using var activity = _tracer.StartActiveSpan(name: "This name should not be in the snapshot", kind: SpanKind.Server, initialAttributes: attributes);
    }

    private static void PrintSpanStartedInformation(TelemetrySpan span)
    {
        Console.WriteLine($"[Main] Started span with span_id: {span.Context.SpanId}");
        PrintCurrentSpanUpdateIfNeeded();
        Console.WriteLine();
    }

    private static void PrintSpanClosedInformation(TelemetrySpan span)
    {
        Console.WriteLine($"[Main] Closed span with span_id: {span.Context.SpanId}");
        PrintCurrentSpanUpdateIfNeeded();
        Console.WriteLine();
    }

    private static void PrintCurrentSpanUpdateIfNeeded()
    {
        // Console.WriteLine($"Created span with span_id: {span.Context.SpanId}");
        var currentSpanContext = Tracer.CurrentSpan.Context;
        if (_previousSpanContext != currentSpanContext)
        {
            var displayName = Activity.Current?.DisplayName ?? "(null)";
            var spanId = Tracer.CurrentSpan.Context.SpanId;

            Console.WriteLine($"       ===> Active span has changed: Activity.Current.DisplayName={displayName}, Tracer.CurrentSpan span_id={spanId}");
        }

        _previousSpanContext = currentSpanContext;
    }
}
