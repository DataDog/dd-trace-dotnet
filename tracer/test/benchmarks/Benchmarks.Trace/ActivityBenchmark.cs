#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe;
using ActivityIdFormat = System.Diagnostics.ActivityIdFormat;
using ActivityKind = Datadog.Trace.Activity.DuckTypes.ActivityKind;
using ActivityListener = System.Diagnostics.ActivityListener;
using ActivitySamplingResult = System.Diagnostics.ActivitySamplingResult;
using ActivitySource = System.Diagnostics.ActivitySource;
using ActivityStatusCode = Datadog.Trace.Activity.DuckTypes.ActivityStatusCode;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkAgent6]
[BenchmarkCategory(Constants.TracerCategory)]
public class ActivityBenchmark
{
    private const string SourceName = "BenchmarkSource";
    private static readonly Datadog.Trace.Activity.DuckTypes.ActivitySource _duckSource;
    private static readonly DateTime _startTime = DateTimeOffset.FromUnixTimeSeconds(0).UtcDateTime;
    private static readonly DateTime _endTime = DateTimeOffset.FromUnixTimeSeconds(5).UtcDateTime;

    private static readonly ActivitySource _source;

    static ActivityBenchmark()
    {
        _source = new ActivitySource(SourceName);

        var activityListener = new ActivityListener { ShouldListenTo = _ => true, Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData };

        ActivitySource.AddActivityListener(activityListener);

        _duckSource = new Datadog.Trace.Activity.DuckTypes.ActivitySource { Name = _source.Name, Version = _source.Version ?? string.Empty };
    }

    [Benchmark]
    public void StartStopWithChild()
    {
        // unfortunately this tests some creation/setup for activity themselves
        using var parent = CreateActivity();
        using var child = CreateActivity(parent);
        var parentMock = new MockActivity6(parent, null, _duckSource);
        var childMock = new MockActivity6(child, parentMock, _duckSource);
        var handler = new DefaultActivityHandler();
        handler.ActivityStarted(SourceName, parentMock);
        handler.ActivityStarted(SourceName, childMock);
        child.Stop();
        handler.ActivityStopped(SourceName, childMock);
        parent.Stop();
        handler.ActivityStopped(SourceName, parentMock);
    }

    private static Activity CreateActivity(Activity? parent = null)
    {
        var activity = parent is null
                           ? _source.CreateActivity("parent", System.Diagnostics.ActivityKind.Internal)
                           : _source.CreateActivity("child", System.Diagnostics.ActivityKind.Internal, parent!.Context);

        if (activity is null)
        {
            throw new Exception("Failed to create an activity");
        }

        activity.SetStartTime(_startTime);
        activity.SetIdFormat(ActivityIdFormat.W3C); // we have more logic for TraceId/SpanId activities

        activity.Start(); // creates necessary TraceId/SpanId that we need

        activity.SetEndTime(_endTime);

        activity.AddTag("tag", "value");
        activity.AddTag("tag[]", new bool[2] { true, false });

        return activity;
    }
}

#region MockActivity Implementations
internal class MockActivity6 : MockActivity5, IActivity6
{
    private readonly Activity _activity;

    public MockActivity6(Activity activity, IActivity? parent, Datadog.Trace.Activity.DuckTypes.ActivitySource source)
        : base(activity, parent, source)
    {
        _activity = activity;
    }

    public ActivityStatusCode Status => Convert(_activity.Status);
    public string StatusDescription => _activity.StatusDescription ?? string.Empty;

    private static ActivityStatusCode Convert(System.Diagnostics.ActivityStatusCode code)
    {
        return code switch
        {
            System.Diagnostics.ActivityStatusCode.Unset => ActivityStatusCode.Unset,
            System.Diagnostics.ActivityStatusCode.Ok => ActivityStatusCode.Ok,
            System.Diagnostics.ActivityStatusCode.Error => ActivityStatusCode.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
        };
    }

    public void Stop()
    {
        _activity.Stop();
    }
}

internal class MockActivity5 : MockW3CActivity, IActivity5
{
    private readonly Activity _activity;

    public MockActivity5(Activity activity, IActivity? parent, Datadog.Trace.Activity.DuckTypes.ActivitySource source)
        : base(activity, parent)
    {
        _activity = activity;
        Source = source;
    }

    public string DisplayName => _activity.DisplayName;

    public bool IsAllDataRequested
    {
        get => _activity.IsAllDataRequested;
        set => _activity.IsAllDataRequested = value;
    }

    public ActivityKind Kind => ConvertActivityKind(_activity.Kind);
    public IEnumerable<KeyValuePair<string, object>> TagObjects => _activity.TagObjects!;
    public Datadog.Trace.Activity.DuckTypes.ActivitySource Source { get; }
    public IEnumerable Events => _activity.Events;
    public IEnumerable Links => _activity.Links;

    object IActivity5.AddTag(string key, object value)
    {
        return _activity.AddTag(key, value);
    }

    private static ActivityKind ConvertActivityKind(System.Diagnostics.ActivityKind kind)
    {
        return kind switch
        {
            System.Diagnostics.ActivityKind.Internal => ActivityKind.Internal,
            System.Diagnostics.ActivityKind.Server => ActivityKind.Server,
            System.Diagnostics.ActivityKind.Client => ActivityKind.Client,
            System.Diagnostics.ActivityKind.Producer => ActivityKind.Producer,
            System.Diagnostics.ActivityKind.Consumer => ActivityKind.Consumer,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}

internal class MockW3CActivity : MockActivity, IW3CActivity
{
    private readonly Activity _activity;
    private string? _traceId;
    private string? _spanId;
    private string? _parentSpanId;
    private string? _traceStateString;

    public MockW3CActivity(Activity activity, IActivity? parent)
        : base(activity, parent)
    {
        _activity = activity;
    }

    public string TraceId
    {
        get => _traceId ?? _activity.TraceId.ToString();
        set => _traceId = value;
    }

    public string SpanId
    {
        get => _spanId ?? _activity.SpanId.ToString();
        set => _spanId = value;
    }

    public string ParentSpanId
    {
        get => _parentSpanId ?? _activity.ParentSpanId.ToString();
        set => _parentSpanId = value;
    }

    public string? RawId { get; set; }
    public string? RawParentId { get; set; }

    public string? TraceStateString
    {
        get => _traceStateString ?? _activity.TraceStateString;
        set => _traceStateString = value;
    }
}

internal class MockActivity : IActivity
{
    private readonly Activity _activity;
    private readonly IActivity? _parent;

    public MockActivity(Activity activity, IActivity? parent)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _parent = parent;
    }

    public object Instance => _activity;
    public Type Type => typeof(Activity);
    public string Id => _activity.Id ?? string.Empty;
    public string ParentId => _activity.ParentId!;

    public string RootId => _activity.RootId!;

    public TimeSpan Duration => _activity.Duration;
    public string OperationName => _activity.OperationName;
    public IActivity? Parent => _parent;
    public DateTime StartTimeUtc => _activity.StartTimeUtc;
    public IEnumerable<KeyValuePair<string, string>> Baggage => _activity.Baggage!;
    public IEnumerable<KeyValuePair<string, string>> Tags => _activity.Tags!;

    public object AddBaggage(string key, string value)
    {
        return _activity.AddBaggage(key, value);
    }

    public object AddTag(string key, string value)
    {
        return _activity.AddTag(key, value);
    }

    public string GetBaggageItem(string key)
    {
        return _activity.GetBaggageItem(key)!;
    }

    public object SetEndTime(DateTime endTimeUtc)
    {
        return _activity.SetEndTime(endTimeUtc);
    }

    public object SetParentId(string parentId)
    {
        return _activity.SetParentId(parentId);
    }

    public object SetStartTime(DateTime startTimeUtc)
    {
        return _activity.SetStartTime(startTimeUtc);
    }

    public ref TReturn GetInternalDuckTypedInstance<TReturn>()
    {
        return ref Unsafe.As<Activity, TReturn>(ref Unsafe.AsRef(in _activity));
    }

    string IDuckType.ToString()
    {
        return _activity.ToString() ?? string.Empty;
    }
}
#endregion
