// <copyright file="RaspMetricsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Tagging;

#nullable enable
namespace Datadog.Trace.AppSec.Rasp;

internal sealed class RaspMetricsHelper
{
    private ulong _raspWafDuration;
    private ulong _raspWafAndBindingsDuration;
    private uint _raspRuleEval;
    private bool _raspTimeout;

    // the totals are running totals of the request, which can be reported out of order
    public void AddRaspSpanMetrics(ulong totalDuration, ulong totalDurationWithBindings, bool timeout)
    {
        _raspWafDuration = Math.Max(_raspWafDuration, totalDuration);
        _raspWafAndBindingsDuration = Math.Max(_raspWafAndBindingsDuration, totalDurationWithBindings);

        if (timeout)
        {
            _raspTimeout = true;
        }

        _raspRuleEval++;
    }

    public void GenerateRaspSpanMetricTags(ITags tags)
    {
        if (_raspRuleEval > 0)
        {
            tags.SetMetric(Metrics.RaspRuleEval, _raspRuleEval);
            tags.SetMetric(Metrics.RaspWafDuration, _raspWafDuration);
            tags.SetMetric(Metrics.RaspWafAndBindingsDuration, _raspWafAndBindingsDuration);

            if (_raspTimeout)
            {
                tags.SetMetric(Metrics.RaspWafTimeout, 1);
            }
        }
    }
}
