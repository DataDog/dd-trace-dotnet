using System.Diagnostics;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace NetActivitySdk;

public static class Program
{
    private static ActivitySource _source;

    private static string SpanLinkTraceId1;
    private static string SpanLinkTraceId2;

    private static string SpanLinkSpanId1;
    private static string SpanLinkSpanId2;

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"SpanId 1: {SpanLinkSpanId1} SpanId 2: {SpanLinkSpanId2}");
        _source = new ActivitySource("Samples.NetActivitySdk");

        var activityListener = new ActivityListener
        {
            //ActivityStarted = activity => Console.WriteLine($"{activity.DisplayName} - Started"),
            ActivityStopped = activity => PrintSpanStoppedInformation(activity),
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(activityListener);

        RunCreateSpanLinkSpans();

        RunActivityLinks();

        using (var rootSpan = _source.StartActivity("RootSpan")) // 1 span (total 1)
        {
            RunActivityAddTags(); // 1 span (total 2)
            RunActivitySetTags(); // 1 span (total 3)
            RunActivityAddEvent(); // 5 spans (total 8)
            RunActivityAddBaggage(); // 2 spans (total 10)
            RunActivityOperationName(); // 21 spans (total 31)
        }

        // needs to be outside of the above root span as we don't want a parent here
        RunActivityConstructors(); // 4 spans (total 35)
        RunActivityUpdate(); //  9 spans (total 44)
        RunNonW3CId(); // 2 spans (total 46)
        RunActivityReservedAttributes(); // 1 span (47 total)

        RunManuallyUpdatedStartTime(); // 3 spans (50 total)

        await Task.Delay(1000);
    }

    private static void PrintSpanStoppedInformation(Activity activity)
    {
        if (activity is null)
        {
            Console.WriteLine("ERROR: activity was null");
            return;
        }

        Console.Write("\n*****\n");
        Console.WriteLine($"Activity.DisplayName: {activity.DisplayName} Stopped");
        Console.WriteLine($"Activity.Id: {activity.Id}");
        Console.WriteLine($"Activity.SpanId: {activity.SpanId}");
        Console.WriteLine($"Activity.ParentId: {activity.ParentId}");
        Console.WriteLine($"Activity.Kind: {activity.Kind}");
        Console.WriteLine($"Activity.Status: {activity.Status}");
        Console.WriteLine($"Activity.StatusDescription: {activity.StatusDescription}");
        Console.WriteLine($"Activity.TraceStateString: {activity.TraceStateString}");
        Console.WriteLine($"Activity.Source.Name: {activity.Source.Name}");
        Console.WriteLine($"Activity.STartTime: {activity.StartTimeUtc}");
        Console.WriteLine("Tags:");
        foreach(var tag in activity.TagObjects)
        {
            Console.WriteLine($"{tag.Key}: {tag.Value}");
        }
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
        var event1 = new ActivityEvent("event-1", new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var event2 = new ActivityEvent("event-2", new DateTimeOffset(1970, 1, 1, 0, 0, 1, TimeSpan.Zero));
        span5?.AddEvent(event1);
        span5?.AddEvent(event2);
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

    private static void RunNonW3CId()
    {
        using (var parent = new Activity("Parent-NonW3CId"))
        {
            parent.SetIdFormat(ActivityIdFormat.Hierarchical);
            parent.Start();
            using (var child = new Activity("Child-NonW3CId"))
            {
                child.SetIdFormat(ActivityIdFormat.Hierarchical);
                child.Start();
            }
        }
    }

    private static void RunManuallyUpdatedStartTime()
    {
        using (var parent = new Activity("TimeParent"))
        {
            parent.SetIdFormat(ActivityIdFormat.Hierarchical);
            parent.SetStartTime(new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            parent.Start();
            using (var manuallySetStartTime = new Activity("TimeTrigger"))
            {
                manuallySetStartTime.SetIdFormat(ActivityIdFormat.Hierarchical);
                manuallySetStartTime.Start();
                // ISSUE: Activity's can have their start time modified after they are started
                //        We use the Activity.Parent.StartTime as a basis to check whether to 
                //        nest an Activity as a child node. 
                //        This code then clears/updates various Span/Trace ID values on the Activity
                //        and resets the Activity.Id value.
                //        The issue here is that for Hierarchical IDs we were setting a Span/Trace ID
                //        when we shouldn't have been and then clearing out the Activity.Id.
                //        The Activity.Id would then be null and would cause issues for us and the customer's
                //        application if they did anything with the ID.
                manuallySetStartTime.SetStartTime(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                using (var child = new Activity("TimeChild"))
                {
                    child.SetIdFormat(ActivityIdFormat.Hierarchical);
                    child.Start();

                    // Without the fix, this child.Id.Substring(1) call would throw an NRE
                    // as we cleared out the ID wrongly.
                    Console.WriteLine(child.Id.Substring(1));
                }
            }
        }
    }

    private static void RunActivityUpdate()
    {
        using var errorSpan = _source.StartActivity("ErrorSpan");
        errorSpan?.SetStatus(ActivityStatusCode.Error, "SetStatus-Error");

        using var okSpan = _source.StartActivity("OkSpan");
        okSpan?.SetStatus(ActivityStatusCode.Ok);

        using var unsetStatusSpan = _source.StartActivity("UnsetStatusSpan");
        unsetStatusSpan?.SetStatus(ActivityStatusCode.Unset);

        var parentId = ActivityTraceId.CreateRandom();
        using var parentSpan = _source.StartActivity("ParentSpan");
        // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.setparentid?view=net-7.0
        var childSpan = new Activity("ChildSpan"); // can't create with StartActivity because ParentId is automatically set
        childSpan.SetParentId(parentSpan.Id);
        childSpan.Start();
        childSpan.Stop();

        // W3C overload
        using var parentSpan2 = _source.StartActivity("W3CParentSpan");
        var childSpan2 = new Activity("W3CChildSpan");
        childSpan2.SetParentId(parentSpan2.TraceId, parentSpan2.SpanId);
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

        var tags = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("attribute-string", "str"),
            new KeyValuePair<string, object>("attribute-int", 1)
        };
        using var ctor3 = _source.StartActivity("Ctor3", ActivityKind.Server, someContext, tags);

        var links = new List<ActivityLink>();
        links.Add(new ActivityLink(someContext));

        using var ctor4 = _source.StartActivity("Ctor4", ActivityKind.Server, default(ActivityContext), tags, links);
        ctor4.DisplayName = "Ctor4DisplayName";
    }

    /// <summary>
    /// Note: These test cases were copy/pasted from parametric tests (with some find/replace to make it work here)
    /// </summary>
    private static IEnumerable<object[]> OperationNameData =>
    new List<object[]>
    {
                // expected_operation_name, span_kind, tags_related_to_operation_name
                new object[] { "http.server.request", ActivityKind.Server, new Dictionary<string, object>() { { "http.request.method", "GET" } } },
                new object[] { "http.client.request", ActivityKind.Client, new Dictionary<string, object>() { { "http.request.method", "GET" } } },
                new object[] { "redis.query", ActivityKind.Client, new Dictionary<string, object>() { { "db.system", "Redis" } } },
                new object[] { "kafka.receive", ActivityKind.Client, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", ActivityKind.Server, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", ActivityKind.Producer, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "kafka.receive", ActivityKind.Consumer, new Dictionary<string, object>() { { "messaging.system", "Kafka" }, { "messaging.operation", "Receive" } } },
                new object[] { "aws.s3.request", ActivityKind.Client, new Dictionary<string, object>() { { "rpc.system", "aws-api" }, { "rpc.service", "S3" } } },
                new object[] { "aws.client.request", ActivityKind.Client, new Dictionary<string, object>() { { "rpc.system", "aws-api" } } },
                new object[] { "grpc.client.request", ActivityKind.Client, new Dictionary<string, object>() { { "rpc.system", "GRPC" } } },
                new object[] { "grpc.server.request", ActivityKind.Server, new Dictionary<string, object>() { { "rpc.system", "GRPC" } } },
                new object[] { "aws.my-function.invoke", ActivityKind.Client, new Dictionary<string, object>() { { "faas.invoked_provider", "aws" }, { "faas.invoked_name", "My-Function" } } },
                new object[] { "datasource.invoke", ActivityKind.Server, new Dictionary<string, object>() { { "faas.trigger", "Datasource" } } },
                new object[] { "graphql.server.request", ActivityKind.Server, new Dictionary<string, object>() { { "graphql.operation.type", "query" } } },
                new object[] { "amqp.server.request", ActivityKind.Server, new Dictionary<string, object>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "server.request", ActivityKind.Server, new Dictionary<string, object>() },
                new object[] { "amqp.client.request", ActivityKind.Client, new Dictionary<string, object>() { { "network.protocol.name", "Amqp" } } },
                new object[] { "client.request", ActivityKind.Client, new Dictionary<string, object>() },
                new object[] { "internal", ActivityKind.Internal, new Dictionary<string, object>() },
                new object[] { "consumer", ActivityKind.Consumer, new Dictionary<string, object>() },
                new object[] { "producer", ActivityKind.Producer, new Dictionary<string, object>() },
                // new object[] { "otel_unknown", null, new Dictionary<string, object>() }, // always should have a span kind for Activity5+
    };

    private static void RunActivityOperationName()
    {
        foreach(var data in OperationNameData)
        {
            using var activity = _source.StartActivity(name: $"operation name should be-> {data[0]}", kind: (ActivityKind)data[1], tags: (Dictionary<string, object>)data[2]);
        }
    }

    private static void RunActivityReservedAttributes()
    {
        var tags = new Dictionary<string, object>();
        tags.Add("http.request.method", "GET"); // operation name would be "http.server.request" (without the override)
        tags.Add("resource.name", "ResourceNameOverride");
        tags.Add("operation.name", "OperationNameOverride");
        tags.Add("service.name", "ServiceNameOverride");
        tags.Add("span.type", "SpanTypeOverride");
        tags.Add("analytics.event", "true"); // metric->  _dd1.sr.eausr: 1.0
        using var activity = _source.StartActivity(name: "This name should not be in the snapshot", kind: ActivityKind.Server, tags: tags);
    }

    private static void RunCreateSpanLinkSpans()
    {
        using var activity1 = _source.StartActivity("SpanLinkSpan1", ActivityKind.Server);
        SpanLinkTraceId1 = activity1?.TraceId.ToHexString();
        SpanLinkSpanId1 = activity1?.SpanId.ToHexString();

        using var activity2 = _source.StartActivity("SpanLinkSpan2", ActivityKind.Server);
        SpanLinkTraceId2 = activity2?.TraceId.ToHexString();
        SpanLinkSpanId2 = activity2?.SpanId.ToHexString();
    }

    private static void RunActivityLinks()
    {
        var activityTags = new ActivityTagsCollection();

        activityTags["some_tag"] = "value";

        var activityLinks = new List<ActivityLink>();

        var activityLinkTags1 = new ActivityTagsCollection();
        activityLinkTags1.Add("some_unserializeable_object", null); // can't serialize
        activityLinkTags1.Add("some_string", "five");
        activityLinkTags1.Add("some_string[]",new [] { "a", "b", "c" });
        activityLinkTags1.Add("some_bool", false);
        activityLinkTags1.Add("some_bool[]", new [] { true, false });
        activityLinkTags1.Add("some_int", 5);
        activityLinkTags1.Add("some_int[]", new [] { 5, 55, 555 } );
        activityLinkTags1.Add("some_int[][]", new [,] {{5, 55}, {555, 5555}}); // can't serialize

        // basic linked context
        var context1 = new ActivityContext(
            ActivityTraceId.CreateFromString(SpanLinkTraceId1.AsSpan()),
            ActivitySpanId.CreateFromString(SpanLinkSpanId1.AsSpan()),
            ActivityTraceFlags.None);

        // basic linked context - with flat set to 1
        var context2 = new ActivityContext(
            ActivityTraceId.CreateFromString(SpanLinkTraceId2.AsSpan()),
            ActivitySpanId.CreateFromString(SpanLinkSpanId2.AsSpan()),
            ActivityTraceFlags.Recorded,
            "foo=1,dd=t.dm:-4;s:2,bar=baz",
            true);

        activityLinks.Add(new ActivityLink(context1, activityLinkTags1));
        activityLinks.Add(new ActivityLink(context2));

        using var activity = _source.StartActivity(
            "ActivityWithLinks",
            ActivityKind.Server,
            default(ActivityContext),
            activityTags,
            activityLinks);
    }

    private static IEnumerable<KeyValuePair<string, object>> GenerateKeyValuePairs()
    {
        var keyValuePairs = new Dictionary<string, object>();

        keyValuePairs.Add("key-str", "str");
        keyValuePairs.Add("key-int", 5);

        return keyValuePairs;
    }
}
