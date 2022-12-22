using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace NetActivitySdk;

public static class Program
{
    private static SpanContext _previousSpanContext;
    private static ActivitySource _source;

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

        _source = new ActivitySource(serviceName, serviceVersion);

        using (var rootSpan = _source.StartActivity("RootSpan"))
        {
            RunActivityAddTags();
            RunActivitySetTags();
            RunActivityAddEvent();
            RunActivityAddBaggage();
            RunActivityLink();
        }

        // needs to be outside of the above root span as we don't want a parent here
        RunActivityUpdate();
    }

    private static void RunActivityAddTags()
    {
        using var span = _source.StartActivity("AddTagsActivity");

        span?.AddTag("operation.name", "Saying hello!");

        span?.AddTag("attribute-string", "str");
        span?.AddTag("attribute-int", 1);
        span?.AddTag("attribute-bool", true);
        span?.AddTag("attribute-double", 2.0);
        span?.AddTag("attribute-stringArray", new string[] { "str1", "str2", "str3" });
        span?.AddTag("attribute-stringArrayEmpty", new string[] { });
        span?.AddTag("attribute-intArray", new int[] { 1, 2, 3 });
        span?.AddTag("attribute-intArrayEmpty", new int[] { });
        span?.AddTag("attribute-boolArray", new bool[] { false, true, false });
        span?.AddTag("attribute-boolArrayEmpty", new bool[] { });
        span?.AddTag("attribute-doubleArray", new double[] { 4.1, 5.0, 6.0 });
        span?.AddTag("attribute-doubleArrayEmpty", new double[] { });
    }

    private static void RunActivitySetTags()
    {
        using var span = _source.StartActivity("SetTagsActivity");
        // SetTag will update an existing key or add it if it doesn't exist

        // first add tags that we want
        span?.AddTag("set-string", "test");
        span?.SetTag("set-string", "str");

        // TODO should we attempt to change these as well?
        span?.SetTag("attribute-string", "str");
        span?.SetTag("attribute-int", 1);
        span?.SetTag("attribute-bool", true);
        span?.SetTag("attribute-double", 2.0);
        span?.SetTag("attribute-stringArray", new string[] { "str1", "str2", "str3" });
        span?.SetTag("attribute-stringArrayEmpty", new string[] { });
        span?.SetTag("attribute-intArray", new int[] { 1, 2, 3 });
        span?.SetTag("attribute-intArrayEmpty", new int[] { });
        span?.SetTag("attribute-boolArray", new bool[] { false, true, false });
        span?.SetTag("attribute-boolArrayEmpty", new bool[] { });
        span?.SetTag("attribute-doubleArray", new double[] { 4.1, 5.0, 6.0 });
        span?.SetTag("attribute-doubleArrayEmpty", new double[] { });
    }

    private static void RunActivityAddEvent()
    {
        using var span1 = _source.StartActivity("NameEvent");
        var nameEvent = new ActivityEvent("name");
        span1?.AddEvent(nameEvent);

        using var span2 = _source.StartActivity("NameDateEvent");
        var nameDateEvent = new ActivityEvent("name-date", DateTimeOffset.Now);
        span2?.AddEvent(nameDateEvent);

        using var span3 = _source.StartActivity("EmptyTagsEvent");
        var emptyTags = new ActivityTagsCollection();
        var emptyTagsEvent = new ActivityEvent("event-empty-tags", DateTimeOffset.Now, emptyTags);
        span3?.AddEvent(emptyTagsEvent);

        using var span4 = _source.StartActivity("TagsEvent");
        var tags = new ActivityTagsCollection(GenerateKeyValuePairs());
        var tagsEvent = new ActivityEvent("event-tags", DateTimeOffset.Now, tags);
        span4?.AddEvent(tagsEvent);

        using var span5 = _source.StartActivity("MultipleEvents");
        var event1 = new ActivityEvent("event-1", DateTimeOffset.Now);
        var event2 = new ActivityEvent("event-2", DateTimeOffset.Now);
        span5?.AddEvent(event1);
        span5?.AddEvent(event2);
    }

    private static void RunActivityLink()
    {
        var context1 = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
        var context2 = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
        var contexts = new[] { context1, context2 };

        using var span = _source.StartActivity(name: "ActivityLinks", kind: ActivityKind.Internal, links: contexts.Select(context => new ActivityLink(context)));
    }

    private static void RunActivityAddBaggage()
    {
        using var addBaggageSpan = _source.StartActivity("AddBaggage");
        addBaggageSpan?.AddBaggage("string-value", "str");
        addBaggageSpan?.AddBaggage("null-value", null);

        using var setBaggageSpan = _source.StartActivity("SetBaggage");
        setBaggageSpan?.AddBaggage("string-value", "string-1");
        setBaggageSpan?.SetBaggage("string-value", "string-2"); // change existing
        setBaggageSpan?.SetBaggage("new-string", "new-string"); // create new
    }

    private static void RunActivityUpdate()
    {
        using var errorSpan = _source.StartActivity("ErrorSpan");
        errorSpan?.SetStatus(ActivityStatusCode.Error, "SetStatus-Error");

        using var okSpan = _source.StartActivity("OkSpan");
        okSpan?.SetStatus(ActivityStatusCode.Ok);

        using var unsetStatusSpan = _source.StartActivity("UnsetStatusSpan");
        unsetStatusSpan?.SetStatus(ActivityStatusCode.Unset);

        var parentId = ActivityTraceId.CreateRandom(); // TODO maybe need to create this from a string if we are going to snapshot
        using var parentSpan = _source.StartActivity("ParentSpan");
        // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.setparentid?view=net-7.0
        var childSpan = new Activity("ChildSpan"); // can't create with StartActivity because ParentId is automatically set
        childSpan.SetParentId(parentSpan.Id);
        childSpan.Start();
        childSpan.Stop();

        // W3C overload
        using var parentSpan2 = _source.StartActivity("W3CParentSpan");
        var childSpan2 = new Activity("W3CChildSpan");
        childSpan2.SetParentId(parentSpan2.TraceId, parentSpan2.SpanId); // TODO should ActivityTraceFlags be added? (None by default)
        childSpan2.Start();
        childSpan2.Stop();

        // other misc properties that work with StartActivity
        using var miscSpan = _source.StartActivity("MiscSpan");
        miscSpan.DisplayName = "IAmMiscSpan";
        miscSpan.TraceStateString = "app=hello";
        miscSpan.SetCustomProperty("CustomPropertyKey", "CustomPropertyValue");
        miscSpan.SetStartTime(new DateTime(2000, 1, 1, 1, 1, 1).ToUniversalTime());
        miscSpan.SetEndTime(new DateTime(2000, 1, 1, 1, 1, 2).ToUniversalTime());

        // other misc properties that don't work with StartActivity
        var miscSpan2 = new Activity("MiscSpan2");
        miscSpan2.IsAllDataRequested = true; // https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.isalldatarequested
        miscSpan2.SetIdFormat(ActivityIdFormat.W3C);
        miscSpan2.Start();
        miscSpan2.Stop();
    }

    private static void RunActivityConstructors()
    {
        using var nameOnly = _source.StartActivity("NameOnlyActivity");

        var tags = new List<KeyValuePair<string, object?>>();
        tags.Add(new KeyValuePair<string, object?>("attribute-string", "str"));
        tags.Add(new KeyValuePair<string, object?>("attribute-int", 1));
        using var nameOnly2 = _source.StartActivity(ActivityKind.Client, nameOnly.Context, tags, null, DateTimeOffset.Now, "OperationName");


    }

    private static void RunAddBaggage(Activity span)
    {
        span.AddBaggage("AddBaggage-null", null);
        span.AddBaggage("AddBaggage-empty", string.Empty);
        span.AddBaggage("AddBaggage-str", "str");
    }

    private static void RunSetBaggage(Activity span)
    {
        // update an existing key
        span.AddBaggage("SetBaggage", "");
        span.SetBaggage("SetBaggage", "set-baggage");
        // create new keys
        span.SetBaggage("SetBaggage-null", null);
        span.SetBaggage("SetBaggage-empty", string.Empty);
        span.SetBaggage("SetBaggage-str", "str");
    }

    private static IEnumerable<KeyValuePair<string, object?>> GenerateKeyValuePairs()
    {
        var keyValuePairs = new Dictionary<string, object?>(); // TODO enable nullable

        keyValuePairs.Add("key-str", "str");
        keyValuePairs.Add("key-int", 5);

        return keyValuePairs;
    }

    private static void PrintSpanStartedInformation(Activity span)
    {
        Console.WriteLine($"[Main] Started span with span_id: {span?.Context.SpanId}");
        PrintCurrentSpanUpdateIfNeeded();
        Console.WriteLine();
    }

    private static void EnsureAutomaticInstrumentationEnabled()
    {
        var process = new Process();
        return;
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
