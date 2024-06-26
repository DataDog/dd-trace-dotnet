// <copyright file="AppSecRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec;

internal class AppSecRequestContext
{
    private const string StackKey = "_dd.stack";
    private const string ExploitStackKey = "exploit";
    private readonly object _sync = new();
    private readonly List<object> _wafSecurityEvents = new();
    private Dictionary<string, List<Dictionary<string, object>>>? _raspStackTraces = null;
    private RaspTelemetryHelper? _raspTelemetryHelper = Security.Instance.RaspEnabled ? new RaspTelemetryHelper() : null;

    internal void CloseWebSpan(TraceTagCollection tags, Span span)
    {
        lock (_sync)
        {
            if (_wafSecurityEvents.Count > 0)
            {
                var triggers = JsonConvert.SerializeObject(_wafSecurityEvents);
                tags.SetTag(Tags.AppSecJson, "{\"triggers\":" + triggers + "}");
            }

            if (_raspStackTraces?.Count > 0)
            {
                span.SetMetaStruct(StackKey, MetaStructHelper.ObjectToByteArray(_raspStackTraces));
            }

            _raspTelemetryHelper?.GenerateRaspSpanMetricTags(span.Tags);
        }
    }

    internal void AddRaspSpanMetrics(ulong duration, ulong durationWithBindings)
    {
        lock (_sync)
        {
            _raspTelemetryHelper?.AddRaspSpanMetrics(duration, durationWithBindings);
        }
    }

    internal void AddWafSecurityEvents(IReadOnlyCollection<object> events)
    {
        lock (_sync)
        {
            _wafSecurityEvents.AddRange(events);
        }
    }

    internal void AddRaspStackTrace(Dictionary<string, object> stackTrace, int maxStackTraces)
    {
        lock (_sync)
        {
            _raspStackTraces ??= new();

            if (!_raspStackTraces.ContainsKey(ExploitStackKey))
            {
                _raspStackTraces.Add(ExploitStackKey, new());
            }
            else if (maxStackTraces > 0 && _raspStackTraces[ExploitStackKey].Count >= maxStackTraces)
            {
                return;
            }

            _raspStackTraces[ExploitStackKey].Add(stackTrace);
        }
    }
}
