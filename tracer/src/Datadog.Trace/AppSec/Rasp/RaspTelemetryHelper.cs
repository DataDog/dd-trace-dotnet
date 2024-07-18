// <copyright file="RaspTelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tagging;

#nullable enable
namespace Datadog.Trace.AppSec.Rasp;

internal class RaspTelemetryHelper
{
    private ulong _raspWafDuration = 0;
    private ulong _raspWafAndBindingsDuration = 0;
    private uint _raspRuleEval = 0;

    public void AddRaspSpanMetrics(ulong duration, ulong durationWithBindings)
    {
        _raspWafDuration += duration;
        _raspWafAndBindingsDuration += durationWithBindings;
        _raspRuleEval++;
    }

    public void GenerateRaspSpanMetricTags(ITags tags)
    {
        if (_raspRuleEval > 0)
        {
            tags.SetMetric(Metrics.RaspRuleEval, _raspRuleEval);
            tags.SetMetric(Metrics.RaspWafDuration, _raspWafDuration);
            tags.SetMetric(Metrics.RaspWafAndBindingsDuration, _raspWafAndBindingsDuration);
        }
    }
}
