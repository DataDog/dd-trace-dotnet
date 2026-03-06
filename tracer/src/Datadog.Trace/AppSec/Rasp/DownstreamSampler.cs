// <copyright file="DownstreamSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.AppSec.Rasp;

internal sealed class DownstreamSampler : IDownstreamSampler
{
    private const long KnuthFactor = 1111111111111111111L;
    private readonly double _threshold;
    private long _globalRequestCount = 0;

    public DownstreamSampler(double rate)
    {
        double sanitizedRate = rate < 0.0 ? 0 : (rate > 1.0 ? 1 : rate);
        _threshold = SamplingCutoff(sanitizedRate);
    }

    private static double SamplingCutoff(double rate)
    {
        const double max = 18446744073709551615.0;

        if (rate < 0.5)
        {
            return (long)(rate * max) + long.MinValue;
        }
        else if (rate < 1.0)
        {
            return (long)((rate * max) + long.MinValue);
        }

        return long.MaxValue;
    }

    public bool SampleHttpClientRequest(AppSecRequestContext ctx, ulong requestId)
    {
        long counter = UpdateRequestCount();

        unchecked
        {
            if ((counter * KnuthFactor) + long.MinValue > _threshold)
            {
                return false;
            }
        }

        return true;
    }

    private long UpdateRequestCount()
    {
        long initial, computed;
        do
        {
            initial = Interlocked.Read(ref _globalRequestCount);
            computed = (initial == long.MaxValue) ? 0L : initial + 1L;
        }
        while (Interlocked.CompareExchange(ref _globalRequestCount, computed, initial) != initial);

        return computed;
    }
}
