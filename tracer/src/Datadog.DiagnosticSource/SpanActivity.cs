// <copyright file="SpanActivity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace;

namespace Datadog.DiagnosticSource
{
    internal class SpanActivity : IDatadogActivity
    {
        private ISpan _span;

        internal SpanActivity(ISpan span)
        {
            _span = span;
        }

        public TimeSpan Duration => throw new NotImplementedException();

        public string OperationName => _span.OperationName;

        public string DisplayName
        {
            get => _span.ResourceName ?? _span.OperationName;
            set => _span.ResourceName = value;
        }

        public ActivityTraceFlags ActivityTraceFlags { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IEnumerable<KeyValuePair<string, string?>> Baggage => throw new NotImplementedException();

        public ActivityContext Context => throw new NotImplementedException();

        public IEnumerable<ActivityEvent> Events => throw new NotImplementedException();

        public string? Id => throw new NotImplementedException();

        public ActivityIdFormat IdFormat => throw new NotImplementedException();

        public bool IsAllDataRequested { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public ActivityKind Kind => throw new NotImplementedException();

        public IEnumerable<ActivityLink> Links => throw new NotImplementedException();

        public Activity? Parent => throw new NotImplementedException();

        public string? ParentId => throw new NotImplementedException();

        public ActivitySpanId ParentSpanId => throw new NotImplementedException();

        public bool Recorded => throw new NotImplementedException();

        public string? RootId => throw new NotImplementedException();

        public ActivitySource Source => throw new NotImplementedException();

        public ActivitySpanId SpanId => throw new NotImplementedException();

        public DateTime StartTimeUtc => throw new NotImplementedException();

        public IEnumerable<KeyValuePair<string, object?>> TagObjects => throw new NotImplementedException();

        public IEnumerable<KeyValuePair<string, string?>> Tags => throw new NotImplementedException();

        public ActivityTraceId TraceId => throw new NotImplementedException();

        public string? TraceStateString { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IDatadogActivity AddBaggage(string key, string? value)
        {
            return this;
        }

        public IDatadogActivity AddEvent(ActivityEvent e)
        {
            return this;
        }

        public IDatadogActivity AddTag(string key, string? value)
        {
            throw new NotImplementedException();
        }

        public IDatadogActivity AddTag(string key, object? value)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string? GetBaggageItem(string key)
        {
            return null;
        }

        public object? GetCustomProperty(string propertyName)
        {
            return null;
        }

        public void SetCustomProperty(string propertyName, object? propertyValue)
        {
        }

        public IDatadogActivity SetEndTime(DateTime endTimeUtc)
        {
            throw new NotImplementedException();
        }

        public IDatadogActivity SetIdFormat(ActivityIdFormat format)
        {
            throw new NotImplementedException();
        }

        public IDatadogActivity SetParentId(string parentId)
        {
            throw new NotImplementedException();
        }

        public IDatadogActivity SetParentId(ActivityTraceId traceId, ActivitySpanId spanId, ActivityTraceFlags activityTraceFlags = ActivityTraceFlags.None)
        {
            throw new NotImplementedException();
        }

        public IDatadogActivity SetStartTime(DateTime startTimeUtc)
        {
            throw new NotImplementedException();
        }

        public IDatadogActivity SetTag(string key, object? value)
        {
            _span.SetTag(key, value?.ToString());
            return this;
        }

        public IDatadogActivity Start()
        {
            return this;
        }

        public void Stop()
        {
        }
    }
}
