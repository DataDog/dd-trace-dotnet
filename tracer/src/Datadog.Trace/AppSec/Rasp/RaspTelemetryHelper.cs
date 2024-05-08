// <copyright file="RaspTelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using static Datadog.Trace.Telemetry.Metrics.MetricTags;

#nullable enable
namespace Datadog.Trace.AppSec.Rasp;

internal class RaspTelemetryHelper
{
    private ulong _raspWafDuration = 0;
    private ulong _raspWafAndBindingsDuration = 0;
    private int _raspRuleEval = 0;
    private object _metricsLock = new();

    public void AddRaspWafAndBindingsDuration(ulong duration, ulong durationWithBindings)
    {
        lock (_metricsLock)
        {
            _raspWafDuration += duration;
            _raspWafAndBindingsDuration += durationWithBindings;
            _raspRuleEval++;
        }
    }

    public void GenerateMetricTags(ITags tags)
    {
        lock (_metricsLock)
        {
            tags.SetMetric(Metrics.RaspRuleEval, _raspRuleEval);
            tags.SetMetric(Metrics.RaspWafDuration, _raspWafDuration);
            tags.SetMetric(Metrics.RaspWafAndBindingsDuration, _raspWafAndBindingsDuration);
        }
    }
}
