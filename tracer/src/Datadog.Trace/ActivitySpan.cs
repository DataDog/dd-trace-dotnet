// <copyright file="ActivitySpan.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace;

internal class ActivitySpan
{
    private System.Diagnostics.Activity _activity;
    private ActivitySpanContext _context;

    public ActivitySpan(System.Diagnostics.Activity activity)
    {
        _activity = activity;
        _context = new ActivitySpanContext(_activity.Context);
    }

    public ActivitySpan(ActivitySpanContext parentContext, TraceContext traceContext, string serviceName, ulong? traceId = null, ulong? spanId = null, string? rawTraceId = null, string? rawSpanId = null, int? samplingPriority = null)
    {

        var activityContext = new System.Diagnostics.ActivityContext()
    }

    public ulong SpanId { get; }

    public ulong TraceId { get; }


    public ActivitySpanContext Context
    {
        get
        {
            return _context;
        }
    }

}
