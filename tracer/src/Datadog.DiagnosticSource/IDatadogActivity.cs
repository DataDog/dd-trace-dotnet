// <copyright file="IDatadogActivity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.DiagnosticSource
{
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CS1591 // Elements should be documented
    public interface IDatadogActivity : IDisposable
    {
        ActivityTraceFlags ActivityTraceFlags { get; set; }

        IEnumerable<KeyValuePair<string, string?>> Baggage { get; }

        ActivityContext Context { get; }

        string DisplayName { get; set; }

        TimeSpan Duration { get; }

        IEnumerable<ActivityEvent> Events { get; }

        string? Id { get; }

        ActivityIdFormat IdFormat { get; }

        bool IsAllDataRequested { get; set; }

        ActivityKind Kind { get; }

        IEnumerable<ActivityLink> Links { get; }

        string OperationName { get; }

        Activity? Parent { get; }

        string? ParentId { get; }

        ActivitySpanId ParentSpanId { get; }

        bool Recorded { get; }

        string? RootId { get; }

        ActivitySource Source { get; }

        ActivitySpanId SpanId { get; }

        DateTime StartTimeUtc { get; }

        IEnumerable<KeyValuePair<string, object?>> TagObjects { get; }

        IEnumerable<KeyValuePair<string, string?>> Tags { get; }

        ActivityTraceId TraceId { get; }

        string? TraceStateString { get; set; }

        IDatadogActivity AddEvent(ActivityEvent e);

        IDatadogActivity AddBaggage(string key, string? value);

        IDatadogActivity AddTag(string key, string? value);

        IDatadogActivity AddTag(string key, object? value);

        string? GetBaggageItem(string key);

        object? GetCustomProperty(string propertyName);

        void SetCustomProperty(string propertyName, object? propertyValue);

        IDatadogActivity SetEndTime(DateTime endTimeUtc);

        IDatadogActivity SetIdFormat(ActivityIdFormat format);

        IDatadogActivity SetParentId(string parentId);

        IDatadogActivity SetParentId(ActivityTraceId traceId, ActivitySpanId spanId, ActivityTraceFlags activityTraceFlags = ActivityTraceFlags.None);

        IDatadogActivity SetStartTime(DateTime startTimeUtc);

        IDatadogActivity SetTag(string key, object? value);

        IDatadogActivity Start();

        void Stop();
    }
}
