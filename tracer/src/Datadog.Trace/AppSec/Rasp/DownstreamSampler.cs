// <copyright file="DownstreamSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Trace.AppSec.Rasp;

internal sealed class DownstreamSampler : IDownstreamSampler
{
    private const long KnuthFactor = 1111111111111111111L;
    private readonly long _threshold;
    private long _globalRequestCount = 0;

    public DownstreamSampler(double rate)
    {
        double sanitizedRate = rate < 0.0 ? 0 : (rate > 1.0 ? 1 : rate);
        _threshold = SamplingCutoff(sanitizedRate);
    }

    private static long SamplingCutoff(double rate)
    {
        // Maps a rate in [0.0, 1.0] to a threshold in [long.MinValue, long.MaxValue].
        // The hashed counter (counter * KnuthFactor) + long.MinValue is uniformly distributed
        // over all longs, so ~rate fraction of values will be <= the returned threshold.
        //
        // ulong.MaxValue as double rounds up to 2^64, so rate * ulong.MaxValue (as double)
        // can exceed long.MaxValue when rate >= 0.5. To avoid overflow when casting back to long,
        // subtract long.MinValue (= -2^63) in double arithmetic first, bringing the value into
        // [0, ~2^63), then cast to long.
        if (rate >= 1.0)
        {
            return long.MaxValue;
        }

        // rate * 2^64 can exceed long.MaxValue for rate >= 0.5, so we subtract 2^63 first.
        // long.MinValue == -2^63, so: (long)(rate * 2^64 - 2^63) == (long)(rate * 2^64) + long.MinValue
        // Both branches are equivalent; using double subtraction first keeps us in range.
        return (long)((rate * (double)ulong.MaxValue) + long.MinValue);
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
