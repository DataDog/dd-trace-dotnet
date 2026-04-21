// <copyright file="AppSecRequestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.AppSec.Rasp;

namespace Datadog.Trace.AppSec;

internal sealed class AppSecRequestContext
{
    private const string StackKey = "_dd.stack";
    private const string VulnerabilityStackKey = "vulnerability";
    private readonly object _sync = new();
    private Dictionary<string, List<Dictionary<string, object>>>? _stackTraces;

    internal void CloseWebSpan(Span span)
    {
        lock (_sync)
        {
            if (_stackTraces?.Count > 0)
            {
                span.SetMetaStruct(StackKey, MetaStructHelper.ObjectToByteArray(_stackTraces));
            }
        }
    }

    internal void AddVulnerabilityStackTrace(Dictionary<string, object> stackTrace, int maxStackTraces)
    {
        AddStackTrace(VulnerabilityStackKey, stackTrace, maxStackTraces);
    }

    internal void AddStackTrace(string stackCategory, Dictionary<string, object> stackTrace, int maxStackTraces)
    {
        lock (_sync)
        {
            _stackTraces ??= new();

            if (!_stackTraces.TryGetValue(stackCategory, out var value))
            {
                _stackTraces.Add(stackCategory, new());
            }
            else if (maxStackTraces > 0 && value.Count >= maxStackTraces)
            {
                return;
            }

            _stackTraces[stackCategory].Add(stackTrace);
        }
    }
}
