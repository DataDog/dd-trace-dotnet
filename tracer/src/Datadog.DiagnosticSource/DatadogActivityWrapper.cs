// <copyright file="DatadogActivityWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.DiagnosticSource
{
    internal class DatadogActivityWrapper : IDatadogActivity
    {
        private Activity _activity;

        internal DatadogActivityWrapper(Activity activity)
        {
            _activity = activity;
        }

        public ActivityTraceFlags ActivityTraceFlags
        {
            get => _activity.ActivityTraceFlags;
            set => _activity.ActivityTraceFlags = value;
        }

        public IEnumerable<KeyValuePair<string, string?>> Baggage => _activity.Baggage;

        public ActivityContext Context => _activity.Context;

        public string DisplayName
        {
            get => _activity.DisplayName;
            set => _activity.DisplayName = value;
        }

        public TimeSpan Duration => _activity.Duration;

        public IEnumerable<ActivityEvent> Events => _activity.Events;

        public string? Id => _activity.Id;

        public ActivityIdFormat IdFormat => _activity.IdFormat;

        public bool IsAllDataRequested
        {
            get => _activity.IsAllDataRequested;
            set => _activity.IsAllDataRequested = value;
        }

        public ActivityKind Kind => _activity.Kind;

        public IEnumerable<ActivityLink> Links => _activity.Links;

        public string OperationName => _activity.OperationName;

        public Activity? Parent => _activity.Parent;

        public string? ParentId => _activity.ParentId;

        public ActivitySpanId ParentSpanId => _activity.ParentSpanId;

        public bool Recorded => _activity.Recorded;

        public string? RootId => _activity.RootId;

        public ActivitySource Source => _activity.Source;

        public ActivitySpanId SpanId => _activity.SpanId;

        public DateTime StartTimeUtc => _activity.StartTimeUtc;

        public IEnumerable<KeyValuePair<string, object?>> TagObjects => _activity.TagObjects;

        public IEnumerable<KeyValuePair<string, string?>> Tags => _activity.Tags;

        public ActivityTraceId TraceId => _activity.TraceId;

        public string? TraceStateString
        {
            get => _activity.TraceStateString;
            set => _activity.TraceStateString = value;
        }

        public IDatadogActivity AddBaggage(string key, string? value)
        {
            _activity.AddBaggage(key, value);
            return this;
        }

        public IDatadogActivity AddEvent(ActivityEvent e)
        {
            _activity.AddEvent(e);
            return this;
        }

        public IDatadogActivity AddTag(string key, string? value)
        {
            _activity.AddTag(key, value);
            return this;
        }

        public IDatadogActivity AddTag(string key, object? value)
        {
            _activity.AddTag(key, value);
            return this;
        }

        public void Dispose()
        {
            _activity.Dispose();
        }

        public string? GetBaggageItem(string key) => _activity.GetBaggageItem(key);

        public object? GetCustomProperty(string propertyName) => _activity.GetCustomProperty(propertyName);

        public void SetCustomProperty(string propertyName, object? propertyValue) => _activity.SetCustomProperty(propertyName, propertyValue);

        public IDatadogActivity SetEndTime(DateTime endTimeUtc)
        {
            _activity.SetEndTime(endTimeUtc);
            return this;
        }

        public IDatadogActivity SetIdFormat(ActivityIdFormat format)
        {
            _activity.SetIdFormat(format);
            return this;
        }

        public IDatadogActivity SetParentId(string parentId)
        {
            _activity.SetParentId(parentId);
            return this;
        }

        public IDatadogActivity SetParentId(ActivityTraceId traceId, ActivitySpanId spanId, ActivityTraceFlags activityTraceFlags = ActivityTraceFlags.None)
        {
            _activity.SetParentId(traceId, spanId, activityTraceFlags);
            return this;
        }

        public IDatadogActivity SetStartTime(DateTime startTimeUtc)
        {
            _activity.SetStartTime(startTimeUtc);
            return this;
        }

        public IDatadogActivity SetTag(string key, object? value)
        {
            _activity.SetTag(key, value);
            return this;
        }

        public IDatadogActivity Start()
        {
            _activity.Start();
            return this;
        }

        public void Stop()
        {
            _activity.Stop();
        }
    }
}
