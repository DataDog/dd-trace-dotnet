using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenTelemetrySdk;

public static class Program
{
    private static SpanContext _previousSpanContext;
    private static Tracer _tracer;

    public static async Task Main(string[] args)
    {
        EnsureAutomaticInstrumentationEnabled();

        var serviceName = "MyServiceName";
        var serviceVersion = "1.0.x";

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .AddConsoleExporter()
            .AddOtlpExporterIfEnvironmentVariablePresent()
            .Build();

        _tracer = tracerProvider.GetTracer(serviceName);
        using var span = _tracer.StartActiveSpan("SayHello");
        PrintSpanStartedInformation(span);
        Activity.Current.TraceStateString = "app=hello";

        await RunStartSpanOverloadsAsync(span);
        RunSetAttributeOverloads(span);
        RunAddEventOverloads(span);
        RunSpanUpdateMethods(span);
    }

    private static void EnsureAutomaticInstrumentationEnabled()
    {
        var process = new Process();
        return;
    }

    private static async Task RunStartSpanOverloadsAsync(TelemetrySpan span)
    {
        // Test the following span creation methods:
        // - StartActiveSpan
        // - StartSpan
        // - StartRootSpan
        using var nonActiveSpan = _tracer.StartSpan("StartSpan");
        PrintSpanStartedInformation(nonActiveSpan);

        using var nonActiveChildSpan = _tracer.StartSpan("StartSpan2", SpanKind.Internal);
        PrintSpanStartedInformation(nonActiveChildSpan);

        using var rootSpan = _tracer.StartRootSpan("StartRootSpan");
        PrintSpanStartedInformation(rootSpan);

        // Use StartActiveSpan which will make the new span Active for the time-being. Note two oddities being tested here:
        // 1) The parent span is not the currently Active span
        // 2) Upon leaving this scope, the Active span should be reset back to the previous Active span, NOT to the parent span
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

        span.SetAttribute("attribute-string", "str");
        span.SetAttribute("attribute-int", 1);
        span.SetAttribute("attribute-bool", true);
        span.SetAttribute("attribute-double", 2.0);
        span.SetAttribute("attribute-stringArray", new string[] { "str1", "str2", "str3" });
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
            Console.WriteLine($"       ===> Active span has changed: Activity.Current.DisplayName={Activity.Current.DisplayName}, Tracer.CurrentSpan span_id={Tracer.CurrentSpan.Context.SpanId}");
        }

        _previousSpanContext = currentSpanContext;
    }
}
