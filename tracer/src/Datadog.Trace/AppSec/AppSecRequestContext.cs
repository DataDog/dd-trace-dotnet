// <copyright file="AppSecRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec;

internal class AppSecRequestContext
{
    private readonly object _sync = new();
    private readonly List<object> _wafSecurityEvents = new();

    internal void CloseWebSpan(TraceTagCollection tags)
    {
        lock (_sync)
        {
            if (_wafSecurityEvents.Count > 0)
            {
                var triggers = JsonConvert.SerializeObject(_wafSecurityEvents);
                tags.SetTag(Tags.AppSecJson, "{\"triggers\":" + triggers + "}");
            }
        }
    }

    internal void AddWafSecurityEvents(IReadOnlyCollection<object> events)
    {
        lock (_sync)
        {
            _wafSecurityEvents.AddRange(events);
        }
    }
}
