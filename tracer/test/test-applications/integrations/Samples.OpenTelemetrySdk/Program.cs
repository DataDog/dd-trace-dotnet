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

        // Confusingly, the new span is still a child of the active span. I expect this behavior to change later because this doesn't make sense.
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
            { "baz", new int[] { 1, 2, 3 } }
        });

        span.AddEvent("event-message");
        span.AddEvent("event-messageWithDateTime", DateTimeOffset.Now);
        span.AddEvent("event-messageWithAttributes", attributes);
        span.AddEvent("event-messageWithDateTimeAndAttributes", DateTimeOffset.Now, attributes);
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
            innerSpan.RecordException(new ArgumentException("Example argument exception"));
            innerSpan.UpdateName("InnerSpanUpdated");
        }
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
