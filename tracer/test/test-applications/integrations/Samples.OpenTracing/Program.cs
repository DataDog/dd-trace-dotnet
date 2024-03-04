using Datadog.Trace.OpenTracing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using OpenTracing;
using OpenTracing.Propagation;
using ITracer = OpenTracing.ITracer;

if (args.Length == 0)
{
    throw new InvalidOperationException("Must provide an argument to define the scenario");
}

var scenario = args[0];

var scenarioToRun = scenario switch
{
    "MinimalSpan" => MinimalSpan(),
    "CustomServiceName" => CustomServiceName(),
    "Utf8Everywhere" => Utf8Everywhere(),
    "OpenTracingSpanBuilderTests" => OpenTracingSpanBuilderTests(),
    "OpenTracingSpanTests" => OpenTracingSpanTests(),
    "OpenTracingTracerTests" => OpenTracingTracerTests(),
    "HttpHeaderCodecTests" => HttpHeaderCodecTests(),
    _ => throw new InvalidOperationException("Unknown scenario: " + scenario),
};

await scenarioToRun;

return 0;

ITracer GetWrappedTracer()
{
#pragma warning disable CS0618 // Type or member is obsolete
    return OpenTracingTracerFactory.WrapTracer(Datadog.Trace.Tracer.Instance);
#pragma warning restore CS0618
}

ITracer CreateTracer(string defaultServiceName)
{
#pragma warning disable CS0618 // Type or member is obsolete
    return OpenTracingTracerFactory.CreateTracer(defaultServiceName: defaultServiceName);
#pragma warning restore CS0618
}

// Integration tests
Task MinimalSpan()
{
    var tracer = GetWrappedTracer();
    var span = tracer.BuildSpan("Operation")
                     .Start();
    span.Finish();
    return Task.CompletedTask;
}

Task CustomServiceName()
{
    var tracer = GetWrappedTracer();
    var span = tracer.BuildSpan("Operation")
                     .WithTag(DatadogTags.ResourceName, "This is a resource")
                     .WithTag(DatadogTags.ServiceName, "MyService")
                     .Start();
    span.Finish();
    return Task.CompletedTask;
}

Task Utf8Everywhere()
{
    var tracer = GetWrappedTracer();
    var span = tracer.BuildSpan("Aᛗᚪᚾᚾᚪ")
                     .WithTag(DatadogTags.ResourceName, "η γλώσσα μου έδωσαν ελληνική")
                     .WithTag(DatadogTags.ServiceName, "На берегу пустынных волн")
                     .WithTag("யாமறிந்த", "ნუთუ კვლა")
                     .Start();
    span.Finish();
    return Task.CompletedTask;
}

Task OpenTracingSpanBuilderTests()
{
    const string OpenTracingSpanBuilderTestsServiceName = "OpenTracingSpanBuilderTests";

    Start_NoServiceName_DefaultServiceNameIsSet();
    Start_NoParentProvided_RootSpan();
    Start_AsChildOfSpan_ChildReferencesParent();
    Start_AsChildOfSpanContext_ChildReferencesParent();
    Start_ReferenceAsChildOf_ChildReferencesParent();
    Start_WithTags_TagsAreProperlySet();
    Start_SettingService_ServiceIsSet();
    Start_SettingServiceInParent_ChildDoesNotInheritServiceName();
    Start_SettingServiceInParent_ExplicitChildDoesNotInheritServiceName();
    Start_SettingServiceInParent_NotChildDontInheritServiceName();
    Start_SettingServiceInChild_ServiceNameOverrideParent();
    Start_SettingResource_ResourceIsSet();
    Start_SettingType_TypeIsSet();
    Start_SettingError_ErrorIsSet();
    Start_WithStartTimeStamp_TimeStampProperlySet();
    Start_SetOperationName_OperationNameProperlySet();

    void Start_NoServiceName_DefaultServiceNameIsSet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null).Start();
        span
           .GetDdTraceCustomSpan()
           .ServiceName.Should()
           .Be(OpenTracingSpanBuilderTestsServiceName);
    }

    void Start_NoParentProvided_RootSpan()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null).Start();
        var context = span.GetDdTraceCustomSpanContext();

        context.Should().NotBeNull();
        context.SpanId.Should().NotBe(0);
        context.TraceId.Should().NotBe(0);
    }

    void Start_AsChildOfSpan_ChildReferencesParent()
    {
        var tracer = GetWrappedTracer();
        var root = tracer.BuildSpan(null).Start();
        var child = tracer.BuildSpan(null)
                          .AsChildOf(root)
                          .Start();

        AssertChildReferencesParent(root, child);
    }

    void Start_AsChildOfSpanContext_ChildReferencesParent()
    {
        var tracer = GetWrappedTracer();
        var root = tracer.BuildSpan(null).Start();
        var child = tracer.BuildSpan(null)
                          .AsChildOf(root.Context)
                          .Start();

        AssertChildReferencesParent(root, child);
    }

    void Start_ReferenceAsChildOf_ChildReferencesParent()
    {
        var tracer = GetWrappedTracer();
        var root = tracer.BuildSpan(null).Start();
        var child = tracer.BuildSpan(null)
                          .AddReference(References.ChildOf, root.Context)
                          .Start();

        AssertChildReferencesParent(root, child);
    }

    void Start_WithTags_TagsAreProperlySet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null)
                         .WithTag("StringKey", "What's tracing")
                         .WithTag("IntKey", 42)
                         .WithTag("DoubleKey", 1.618)
                         .WithTag("BoolKey", true)
                         .Start();

        var ddSpan = span.GetDdTraceCustomSpan();
        ddSpan.GetTag("StringKey").Should().Be("What's tracing");
        ddSpan.GetTag("IntKey").Should().Be("42");
        ddSpan.GetTag("DoubleKey").Should().Be("1.618");
        ddSpan.GetTag("BoolKey").Should().Be("True");
    }

    void Start_SettingService_ServiceIsSet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null)
                         .WithTag(DatadogTags.ServiceName, "MyService")
                         .Start();

        span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyService");
    }

    void Start_SettingServiceInParent_ChildDoesNotInheritServiceName()
    {
        var tracer = GetWrappedTracer();
        var root = tracer.BuildSpan(null)
                         .WithTag(DatadogTags.ServiceName, "MyService")
                         .StartActive(finishSpanOnDispose: true);
        var child = tracer.BuildSpan(null)
                          .StartActive(finishSpanOnDispose: true);

        root.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyService");
        child.Span.GetDdTraceCustomSpan().ServiceName.Should().Be(OpenTracingSpanBuilderTestsServiceName);
    }

    void Start_SettingServiceInParent_ExplicitChildDoesNotInheritServiceName()
    {
        var tracer = GetWrappedTracer();
        var root = tracer.BuildSpan(null)
                         .WithTag(DatadogTags.ServiceName, "MyService")
                         .StartActive(finishSpanOnDispose: true);
        var child = tracer.BuildSpan(null)
                          .AsChildOf(root.Span)
                          .StartActive(finishSpanOnDispose: true);

        root.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyService");
        child.Span.GetDdTraceCustomSpan().ServiceName.Should().Be(OpenTracingSpanBuilderTestsServiceName);
    }

    void Start_SettingServiceInParent_NotChildDontInheritServiceName()
    {
        var tracer = GetWrappedTracer();
        var span1 = tracer.BuildSpan(null)
                          .WithTag(DatadogTags.ServiceName, "MyService")
                          .Start();
        var root = tracer.BuildSpan(null)
                         .StartActive(finishSpanOnDispose: true);

        span1.GetDdTraceCustomSpan().ServiceName.Should().Be("MyService");
        root.Span.GetDdTraceCustomSpan().ServiceName.Should().Be(OpenTracingSpanBuilderTestsServiceName);
    }

    void Start_SettingServiceInChild_ServiceNameOverrideParent()
    {
        var tracer = GetWrappedTracer();

        var root = tracer.BuildSpan(null)
                         .WithTag(DatadogTags.ServiceName, "MyService")
                         .Start();
        var child = tracer.BuildSpan(null)
                          .WithTag(DatadogTags.ServiceName, "AnotherService")
                          .Start();

        root.GetDdTraceCustomSpan().ServiceName.Should().Be("MyService");
        child.GetDdTraceCustomSpan().ServiceName.Should().Be("AnotherService");
    }

    void Start_SettingResource_ResourceIsSet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null)
                         .WithTag("resource.name", "MyResource")
                         .Start();

        span.GetDdTraceCustomSpan().ResourceName.Should().Be("MyResource");
    }

    void Start_SettingType_TypeIsSet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null)
                         .WithTag("span.type", "web")
                         .Start();

        span.GetDdTraceCustomSpan().Type.Should().Be("web");
    }

    void Start_SettingError_ErrorIsSet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan(null)
                         .WithTag(global::OpenTracing.Tag.Tags.Error.Key, true)
                         .Start();

        span.GetDdTraceCustomSpan().Error.Should().BeTrue();
    }

    void Start_WithStartTimeStamp_TimeStampProperlySet()
    {
        var tracer = GetWrappedTracer();
        var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
        var span = tracer.BuildSpan(null)
                         .WithStartTimestamp(startTime)
                         .Start();

        var ddSpan = span.GetDdTraceCustomSpan();
        ddSpan.GetInternalProperties().StartTime.Should().Be(startTime);
    }

    void Start_SetOperationName_OperationNameProperlySet()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan("Op1")
                         .Start();

        span.GetDdTraceCustomSpan().OperationName.Should().Be("Op1");
    }

    return Task.CompletedTask;
    
    void AssertChildReferencesParent(ISpan span, ISpan child1)
    {
        var rootContext = span.GetDdTraceCustomSpanContext();
        var childContext = child1.GetDdTraceCustomSpanContext();

        rootContext.TraceId.Should().Be(childContext.TraceId);
        rootContext.SpanId.Should().NotBe(0);
        childContext.SpanId.Should().NotBe(0);

        var rootParentId = rootContext.GetParentId();
        rootParentId.Should().BeNull();

        var childParentId = childContext.GetParentId();
        childParentId.Should().Be(rootContext.SpanId);
    }
}

Task OpenTracingSpanTests()
{
    SetTag_Tags_TagsAreProperlySet();
    SetTag_SpecialTags_ServiceNameSetsService();
    SetTag_SpecialTags_ServiceVersionSetsVersion();
    SetOperationName_ValidOperationName_OperationNameIsProperlySet();
    Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed();
    Finish_EndTimeProvided_SpanWritenWithCorrectDuration();
    Finish_EndTimeInThePast_DurationIs0();
    Dispose_ExitUsing_SpanWriten();
    Context_TwoCalls_ContextStaysEqual();

    void SetTag_Tags_TagsAreProperlySet()
    {
        var span = GetScope("Op1").Span;

        span.SetTag("StringKey", "What's tracing");
        span.SetTag("IntKey", 42);
        span.SetTag("DoubleKey", 1.618);
        span.SetTag("BoolKey", true);

        var ddSpan = span.GetDdTraceCustomSpan();
        ddSpan.GetTag("StringKey").Should().Be("What's tracing");
        ddSpan.GetTag("IntKey").Should().Be("42");
        ddSpan.GetTag("DoubleKey").Should().Be("1.618");
        ddSpan.GetTag("BoolKey").Should().Be("True");
    }

    void SetTag_SpecialTags_ServiceNameSetsService()
    {
        var span = GetScope("Op1").Span;
        const string value = "value";

        span.SetTag(DatadogTags.ServiceName, value);

        span.GetDdTraceCustomSpan().ServiceName.Should().Be(value);
    }

    void SetTag_SpecialTags_ServiceVersionSetsVersion()
    {
        var span = GetScope("Op1").Span;
        const string value = "value";

        span.SetTag(DatadogTags.ServiceVersion, value);


        var ddSpan = span.GetDdTraceCustomSpan();
        ddSpan.GetTag(Datadog.Trace.Tags.Version).Should().Be(value);
        ddSpan.GetTag(DatadogTags.ServiceVersion).Should().Be(value);
    }

    void SetOperationName_ValidOperationName_OperationNameIsProperlySet()
    {
        var span = GetScope("Op0").Span;

        span.SetOperationName("Op1");

        span.GetDdTraceCustomSpan().OperationName.Should().Be("Op1");
    }

    void Finish_StartTimeInThePastWithNoEndTime_DurationProperlyComputed()
    {
        TimeSpan expectedDuration = TimeSpan.FromMinutes(1);
        var startTime = DateTimeOffset.UtcNow - expectedDuration;

        var span = GetScope("Op1", startTime).Span;
        span.Finish();

        var precision = TimeSpan.FromSeconds(1);
        span.GetDdTraceCustomSpan()
            .GetInternalProperties()
            .Duration
            .Should()
            .BeCloseTo(expectedDuration, precision);
    }

    void Finish_EndTimeProvided_SpanWritenWithCorrectDuration()
    {
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMilliseconds(10);

        var span = GetScope("Op1", startTime).Span;
        span.Finish(endTime);

        span.GetDdTraceCustomSpan()
            .GetInternalProperties()
            .Duration
            .Should()
            .Be(endTime - startTime);
    }

    void Finish_EndTimeInThePast_DurationIs0()
    {
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddMilliseconds(-10);

        var span = GetScope("Op1", startTime).Span;
        span.Finish(endTime);

        span.GetDdTraceCustomSpan()
            .GetInternalProperties()
            .Duration
            .Should()
            .Be(TimeSpan.Zero);
    }

    void Dispose_ExitUsing_SpanWriten()
    {
        Datadog.Trace.ISpan span;

        using (var scope = GetScope("Op1"))
        {
            span = scope.Span.GetDdTraceCustomSpan();
        }

        span
           .GetInternalProperties()
           .Duration
           .Should()
           .BeGreaterThan(TimeSpan.Zero);
    }

    void Context_TwoCalls_ContextStaysEqual()
    {
        global::OpenTracing.ISpan span;
        global::OpenTracing.ISpanContext firstContext;

        using (var scope = GetScope("Op1"))
        {
            span = scope.Span;
            firstContext = span.Context;
        }

        var secondContext = span.Context;

        secondContext.Should().BeSameAs(firstContext);
    }

    global::OpenTracing.IScope GetScope(string operationName, DateTimeOffset? startTime = null)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var tracer = OpenTracingTracerFactory.WrapTracer(Datadog.Trace.Tracer.Instance);
#pragma warning restore CS0618

        ISpanBuilder spanBuilder = OpenTracingHelpers.CreateOpenTracingSpanBuilder(tracer, operationName);

        if (startTime != null)
        {
            spanBuilder = spanBuilder.WithStartTimestamp(startTime.Value);
        }

        return spanBuilder.StartActive(finishSpanOnDispose: true);
    }

    return Task.CompletedTask;
}

async Task OpenTracingTracerTests()
{
    BuildSpan_NoParameter_DefaultParameters();
    BuildSpan_OneChild_ChildParentProperlySet();
    BuildSpan_2ChildrenOfRoot_ChildrenParentProperlySet();
    BuildSpan_2LevelChildren_ChildrenParentProperlySet();
    await BuildSpan_AsyncChildrenCreation_ChildrenParentProperlySet();
    Inject_HttpHeadersFormat_CorrectHeaders();
    Inject_TextMapFormat_CorrectHeaders();
    Inject_UnknownFormat_Throws();
    Extract_HttpHeadersFormat_HeadersProperlySet_SpanContext();
    Extract_TextMapFormat_HeadersProperlySet_SpanContext();
    Extract_UnknownFormat_Throws();
    StartActive_NoServiceName_DefaultServiceName();
    SetDefaultServiceName();
    SetServiceName_WithTag();
    SetServiceName_SetTag();
    OverrideDefaultServiceName_WithTag();
    OverrideDefaultServiceName_SetTag();
    DoesNotInheritParentServiceName_WithTag();
    DoesNotInheritParentServiceName_SetTag();
    Parent_OverrideDefaultServiceName_WithTag();
    Parent_OverrideDefaultServiceName_SetTag();
    
    void BuildSpan_NoParameter_DefaultParameters()
    {
        var tracer = GetWrappedTracer();
        var builder = tracer.BuildSpan("Op1");
        var span = builder.Start();

        var ddSpan = span.GetDdTraceCustomSpan();
        ddSpan.ServiceName.Should().Be("OpenTracingTracerTests");
        ddSpan.OperationName.Should().Be("Op1");
    }

    void BuildSpan_OneChild_ChildParentProperlySet()
    {
        var tracer = GetWrappedTracer();
        var root = tracer
                  .BuildSpan("Root")
                  .StartActive(finishSpanOnDispose: true);
        var child = tracer
                   .BuildSpan("Child")
                   .StartActive(finishSpanOnDispose: true);

        var ddRoot = root.Span.GetDdTraceCustomSpan();
        var ddChild = child.Span.GetDdTraceCustomSpan();

        ddRoot.Context.SpanId.Should().Be(ddChild.Context.GetParentId());
        ddRoot.Context.GetTraceContext().Should().BeSameAs(ddChild.Context.GetTraceContext());
    }

    void BuildSpan_2ChildrenOfRoot_ChildrenParentProperlySet()
    {
        var tracer = GetWrappedTracer();

        var root = tracer
                  .BuildSpan("Root")
                  .StartActive(finishSpanOnDispose: true);

        var child1 = tracer
                    .BuildSpan("Child1")
                    .StartActive(finishSpanOnDispose: true);

        child1.Dispose();

        var child2 = tracer
                    .BuildSpan("Child2")
                    .StartActive(finishSpanOnDispose: true);

        var ddRoot = root.Span.GetDdTraceCustomSpan();
        var ddChild1 = child1.Span.GetDdTraceCustomSpan();
        var ddChild2 = child2.Span.GetDdTraceCustomSpan();

        ddRoot.Context.GetTraceContext()
              .Should()
              .BeSameAs(ddChild1.Context.GetTraceContext())
              .And.BeSameAs(ddChild2.Context.GetTraceContext());

        ddRoot.Context.SpanId
              .Should()
              .Be(ddChild1.Context.GetParentId())
              .And.Be(ddChild2.Context.GetParentId());
    }

    void BuildSpan_2LevelChildren_ChildrenParentProperlySet()
    {
        var tracer = GetWrappedTracer();

        var root = tracer
                  .BuildSpan("Root")
                  .StartActive(finishSpanOnDispose: true);
        var child1 = tracer
                    .BuildSpan("Child1")
                    .StartActive(finishSpanOnDispose: true);
        var child2 = tracer
                    .BuildSpan("Child2")
                    .StartActive(finishSpanOnDispose: true);

        var ddRoot = root.Span.GetDdTraceCustomSpan();
        var ddChild1 = child1.Span.GetDdTraceCustomSpan();
        var ddChild2 = child2.Span.GetDdTraceCustomSpan();

        ddRoot.Context.GetTraceContext()
              .Should()
              .BeSameAs(ddChild1.Context.GetTraceContext())
              .And.BeSameAs(ddChild2.Context.GetTraceContext());

        ddRoot.Context.SpanId
              .Should()
              .Be(ddChild1.Context.GetParentId())
              .And.Be(ddChild2.Context.GetParentId());
    }

    async Task BuildSpan_AsyncChildrenCreation_ChildrenParentProperlySet()
    {
        var tracer = GetWrappedTracer();
        var tcs = new TaskCompletionSource<bool>();

        var root = tracer
                  .BuildSpan("Root")
                  .StartActive(finishSpanOnDispose: true);

        Func<OpenTracing.ITracer, Task<OpenTracing.ISpan>> createSpanAsync = async (t) =>
        {
            await tcs.Task;
            return tracer.BuildSpan("AsyncChild").Start();
        };
        var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(tracer)).ToArray();

        var syncChild = tracer.BuildSpan("SyncChild").Start();
        tcs.SetResult(true);

        var ddRoot = root.Span.GetDdTraceCustomSpan();
        var ddSyncChild = syncChild.GetDdTraceCustomSpan();

        ddRoot.Context.GetTraceContext().Should().BeSameAs(ddSyncChild.Context.GetTraceContext());
        ddRoot.Context.SpanId.Should().Be(ddSyncChild.Context.GetParentId());

        foreach (var task in tasks)
        {
            var span = await task;
            var ddSpan = span.GetDdTraceCustomSpan();
            
            ddRoot.Context.GetTraceContext().Should().BeSameAs(ddSpan.Context.GetTraceContext());
            ddRoot.Context.SpanId.Should().Be(ddSpan.Context.GetParentId());
        }
    }

    void Inject_HttpHeadersFormat_CorrectHeaders()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan("Span").Start();
        var headers = new MockTextMap();

        tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, headers);

        headers.Get(Datadog.Trace.HttpHeaderNames.TraceId).Should().Be(span.Context.TraceId);
        headers.Get(Datadog.Trace.HttpHeaderNames.ParentId).Should().Be(span.Context.SpanId);
    }

    void Inject_TextMapFormat_CorrectHeaders()
    {
        var tracer = GetWrappedTracer();
        var span = tracer.BuildSpan("Span").Start();
        var headers = new MockTextMap();

        tracer.Inject(span.Context, BuiltinFormats.TextMap, headers);

        headers.Get(Datadog.Trace.HttpHeaderNames.TraceId).Should().Be(span.Context.TraceId);
        headers.Get(Datadog.Trace.HttpHeaderNames.ParentId).Should().Be(span.Context.SpanId);
    }

    void Inject_UnknownFormat_Throws()
    {
        var tracer = GetWrappedTracer();

        var span = tracer.BuildSpan("Span").Start();
        var headers = new MockTextMap();
        var unknownFormat = new UnknownFormat();

        var inject = () => tracer.Inject(span.Context, unknownFormat, headers);
        inject.Should().Throw<NotSupportedException>();
    }

    void Extract_HttpHeadersFormat_HeadersProperlySet_SpanContext()
    {
        var tracer = GetWrappedTracer();

        const ulong parentId = 10;
        const ulong traceId = 42;
        var headers = new MockTextMap();
        headers.Set(Datadog.Trace.HttpHeaderNames.ParentId, parentId.ToString());
        headers.Set(Datadog.Trace.HttpHeaderNames.TraceId, traceId.ToString());

        var otSpanContext = tracer.Extract(BuiltinFormats.HttpHeaders, headers);

        var ddSpanContext = otSpanContext.GetDdTraceCustomSpanContext();
        ddSpanContext.SpanId.Should().Be(parentId);
        ddSpanContext.TraceId.Should().Be(traceId);
    }

    void Extract_TextMapFormat_HeadersProperlySet_SpanContext()
    {
        var tracer = GetWrappedTracer();

        const ulong parentId = 10;
        const ulong traceId = 42;
        var headers = new MockTextMap();
        headers.Set(Datadog.Trace.HttpHeaderNames.ParentId, parentId.ToString());
        headers.Set(Datadog.Trace.HttpHeaderNames.TraceId, traceId.ToString());

        var otSpanContext = tracer.Extract(BuiltinFormats.TextMap, headers);

        var ddSpanContext = otSpanContext.GetDdTraceCustomSpanContext();
        ddSpanContext.SpanId.Should().Be(parentId);
        ddSpanContext.TraceId.Should().Be(traceId);
    }

    void Extract_UnknownFormat_Throws()
    {
        var tracer = GetWrappedTracer();

        const ulong parentId = 10;
        const ulong traceId = 42;
        var headers = new MockTextMap();
        headers.Set(Datadog.Trace.HttpHeaderNames.ParentId, parentId.ToString());
        headers.Set(Datadog.Trace.HttpHeaderNames.TraceId, traceId.ToString());
        var unknownFormat = new UnknownFormat();

        var inject = () => tracer.Extract(unknownFormat, headers);
        inject.Should().Throw<NotSupportedException>();
    }

    void StartActive_NoServiceName_DefaultServiceName()
    {
        var tracer = GetWrappedTracer();

        var scope = tracer.BuildSpan("Operation")
                          .StartActive();

        scope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("OpenTracingTracerTests");
    }

    void SetDefaultServiceName()
    {
        var tracer = CreateTracer(defaultServiceName: "DefaultServiceName");

        var scope = tracer.BuildSpan("Operation")
                          .StartActive();

        scope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("DefaultServiceName");
    }

    void SetServiceName_WithTag()
    {
        var tracer = GetWrappedTracer();

        var scope = tracer.BuildSpan("Operation")
                          .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                          .StartActive();

        scope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
    }

    void SetServiceName_SetTag()
    {
        var tracer = GetWrappedTracer();

        var scope = tracer.BuildSpan("Operation")
                          .StartActive();

        scope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");
        
        scope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
    }

    void OverrideDefaultServiceName_WithTag()
    {
        var tracer = CreateTracer(defaultServiceName: "DefaultServiceName");

        var scope = tracer.BuildSpan("Operation")
                          .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                          .StartActive();

        scope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
    }

    void OverrideDefaultServiceName_SetTag()
    {
        var tracer = CreateTracer(defaultServiceName: "DefaultServiceName");

        var scope = tracer.BuildSpan("Operation")
                          .StartActive();

        scope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

        scope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
    }

    void DoesNotInheritParentServiceName_WithTag()
    {
        var tracer = GetWrappedTracer();

        var parentScope = tracer.BuildSpan("ParentOperation")
                                .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                                .StartActive();

        var childScope = tracer.BuildSpan("ChildOperation")
                               .AsChildOf(parentScope.Span)
                               .StartActive();

        parentScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
        childScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("OpenTracingTracerTests");
    }

    void DoesNotInheritParentServiceName_SetTag()
    {
        var tracer = GetWrappedTracer();

        var parentScope = tracer.BuildSpan("ParentOperation")
                                .StartActive();

        parentScope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

        var childScope = tracer.BuildSpan("ChildOperation")
                               .AsChildOf(parentScope.Span)
                               .StartActive();

        parentScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
        childScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("OpenTracingTracerTests");
    }

    void Parent_OverrideDefaultServiceName_WithTag()
    {
        const string defaultServiceName = "DefaultServiceName";
        var tracer = CreateTracer(defaultServiceName: defaultServiceName);

        var parentScope = tracer.BuildSpan("ParentOperation")
                                .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                                .StartActive();

        var childScope = tracer.BuildSpan("ChildOperation")
                               .AsChildOf(parentScope.Span)
                               .StartActive();

        parentScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
        childScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("OpenTracingTracerTests");
    }

    void Parent_OverrideDefaultServiceName_SetTag()
    {
        const string defaultServiceName = "DefaultServiceName";
        var tracer = CreateTracer(defaultServiceName: defaultServiceName);

        var parentScope = tracer.BuildSpan("ParentOperation")
                                .StartActive();

        parentScope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

        var childScope = tracer.BuildSpan("ChildOperation")
                               .AsChildOf(parentScope.Span)
                               .StartActive();

        parentScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("MyAwesomeService");
        childScope.Span.GetDdTraceCustomSpan().ServiceName.Should().Be("OpenTracingTracerTests");
    }
}

Task HttpHeaderCodecTests()
{
    // The values are duplicated here to make sure that if they are changed it will break tests
    const string httpHeaderTraceId = "x-datadog-trace-id";
    const string httpHeaderParentId = "x-datadog-parent-id";
    const string httpHeaderSamplingPriority = "x-datadog-sampling-priority";

    Inject_SpanContext_HeadersWithCorrectInfo();
    Extract_ValidParentAndTraceId_ProperSpanContext();
    Extract_WrongHeaderCase_ExtractionStillWorks();

    return Task.CompletedTask;

    void Inject_SpanContext_HeadersWithCorrectInfo()
    {
        var tracer = GetWrappedTracer();

        const ulong spanId = 10;
        const ulong traceId = 7;
        var samplingPriority = Datadog.Trace.SamplingPriority.UserKeep;

        var ddSpanContext = new Datadog.Trace.SpanContext(traceId, spanId, samplingPriority);
        var spanContext = OpenTracingHelpers.CreateOpenTracingSpanContext(ddSpanContext);
        var headers = new Dictionary<string, string>();

        tracer.Inject(spanContext, BuiltinFormats.HttpHeaders, new TextMapInjectAdapter(headers));

        headers[httpHeaderParentId].Should().Be(spanId.ToString());
        headers[httpHeaderTraceId].Should().Be(traceId.ToString());
        headers[httpHeaderSamplingPriority].Should().Be(((int)samplingPriority).ToString());
    }

    void Extract_ValidParentAndTraceId_ProperSpanContext()
    {
        var tracer = GetWrappedTracer();

        const ulong traceId = 10;
        const ulong parentId = 120;

        var headers = new Dictionary<string, string>();
        headers[httpHeaderTraceId] = traceId.ToString();
        headers[httpHeaderParentId] = parentId.ToString();

        var context = tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(headers));

        context.Should().NotBeNull();
        context.SpanId.Should().Be(parentId.ToString());
        context.TraceId.Should().Be(traceId.ToString());
    }

    void Extract_WrongHeaderCase_ExtractionStillWorks()
    {
        var tracer = GetWrappedTracer();

        const ulong traceId = 10;
        const ulong parentId = 120;
        const int samplingPriority = 2;

        
        var headers = new Dictionary<string, string>();
        headers[httpHeaderTraceId.ToUpper()] = traceId.ToString();
        headers[httpHeaderParentId.ToUpper()] = parentId.ToString();
        headers[httpHeaderSamplingPriority.ToUpper()] = samplingPriority.ToString();

        var context = tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(headers));

        context.Should().NotBeNull();
        context.SpanId.Should().Be(parentId.ToString());
        context.TraceId.Should().Be(traceId.ToString());
    }
}

public class MockTextMap : ITextMap
{
    private readonly Dictionary<string, string> _dictionary = new();

    public string Get(string key)
    {
        _dictionary.TryGetValue(key, out string value);
        return value;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    public void Set(string key, string value)
    {
        _dictionary[key] = value;
    }
}

public class UnknownFormat : IFormat<ITextMap>
{
}
