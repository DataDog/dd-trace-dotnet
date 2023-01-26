#nullable enable

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace NetActivitySdk;

public static class Program
{
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

        using (var rootSpan = _source.StartActivity("RootSpan")) // 1 span (total 1)
        {
            RunActivityAddTags(); // 1 span (total 2)
            RunActivitySetTags(); // 1 span (total 3)
            RunActivityAddEvent(); // 5 spans (total 8)
            RunActivityAddBaggage(); // 2 spans (total 10)
        }

        // needs to be outside of the above root span as we don't want a parent here
        RunActivityConstructors(); // 4 spans (total 14)
        RunActivityUpdate(); //  9 spans (total 23)
        //RunActivityLink();
        await Task.Delay(1000);
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

        span?.AddTag("set-string", "test");
        span?.SetTag("set-string", "str");

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
        using var span1 = _source.StartActivity("SomeUnrelatedSpan");
        //var context1 = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
        //var context2 = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
        //var contexts = new[] { context1, context2 };

        //var links = contexts.Select(context => new ActivityLink(context));
        List<ActivityLink> links;
        if(span1 is not null)
        {
            links = new List<ActivityLink>() { new ActivityLink(span1.Context) };
            using var span2 = _source.StartActivity(name: "ActivityLinks", kind: ActivityKind.Internal, links: links);
        }

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
        using var ctor1 = _source.StartActivity("Ctor1", ActivityKind.Server);

        var someContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
        using var ctor2 = _source.StartActivity("Ctor2", ActivityKind.Server, someContext);

        var tags = new List<KeyValuePair<string, object?>>
        {
            new KeyValuePair<string, object?>("attribute-string", "str"),
            new KeyValuePair<string, object?>("attribute-int", 1)
        };
        using var ctor3 = _source.StartActivity("Ctor3", ActivityKind.Server, someContext, tags);

        var links = new List<ActivityLink>();
        links.Add(new ActivityLink(someContext));

        using var ctor4 = _source.StartActivity("Ctor4", ActivityKind.Server, default(ActivityContext), tags, links);
    }

    private static IEnumerable<KeyValuePair<string, object?>> GenerateKeyValuePairs()
    {
        var keyValuePairs = new Dictionary<string, object?>(); // TODO enable nullable

        keyValuePairs.Add("key-str", "str");
        keyValuePairs.Add("key-int", 5);

        return keyValuePairs;
    }

    private static void EnsureAutomaticInstrumentationEnabled()
    {
        var process = new Process();
        return;
    }
}
