// <copyright file="TraceClock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Util;

namespace Datadog.Trace;

internal sealed class TraceClock
{
    private static readonly CancellationTokenSource TokenSource;
    private static TraceClock _instance;

    private readonly DateTimeOffset _utcStart;
    private readonly long _timestamp;

    static TraceClock()
    {
        TokenSource = new CancellationTokenSource();
        LifetimeManager.Instance.AddShutdownTask(_ => TokenSource.Cancel());
        _instance = new TraceClock();
        _ = UpdateClockAsync();
    }

    private TraceClock()
    {
        _utcStart = DateTimeOffset.UtcNow;
        _timestamp = Stopwatch.GetTimestamp();

        // The following is to prevent the case of GC hitting between _utcStart and _timestamp set
        var retries = 3;
        while ((DateTimeOffset.UtcNow - UtcNow).TotalMilliseconds > 16 && retries-- > 0)
        {
            _utcStart = DateTimeOffset.UtcNow;
            _timestamp = Stopwatch.GetTimestamp();
        }
    }

    public static TraceClock Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _instance;
    }

    public DateTimeOffset UtcNow
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _utcStart.Add(Elapsed);
    }

    private TimeSpan Elapsed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => StopwatchHelpers.GetElapsed(Stopwatch.GetTimestamp() - _timestamp);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ElapsedSince(DateTimeOffset date) => UtcNow - date;

    private static async Task UpdateClockAsync()
    {
        var token = TokenSource.Token;
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                break;
            }

            _instance = new TraceClock();
        }
    }
}
