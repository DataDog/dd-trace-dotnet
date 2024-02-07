// <copyright file="UnmanagedMemoryPoolFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Util;

/// <summary>
/// Beware that this type is not thread safe and should be used with [ThreadStatic]
/// </summary>
internal class UnmanagedMemoryPoolFactory
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(UnmanagedMemoryPoolFactory));

    // Statistics
    private static int _fastPoolCount = 0;
    private static int _slowPoolCount = 0;

    public static IUnmanagedMemoryPool GetPool(int blockSize, int poolSize)
    {
        if (_fastPoolCount < WafConstants.MaxUnmanagedPools)
        {
            Interlocked.Increment(ref _fastPoolCount);
            Log.Debug<int, int>("Created fast WAF unmanaged pool. Current pools -> Fast: {PoolCount}  Slow: {SlowPoolCount}", _fastPoolCount, _slowPoolCount);
            TelemetryFactory.Metrics.RecordGaugePoolCount(_fastPoolCount);

            return new UnmanagedMemoryPool(blockSize, poolSize);
        }
        else
        {
            Interlocked.Increment(ref _slowPoolCount);
            Log.Debug<int, int>("Created slow WAF unmanaged pool. Current pools -> Fast: {PoolCount}  Slow: {SlowPoolCount}", _fastPoolCount, _slowPoolCount);
            TelemetryFactory.Metrics.RecordGaugePoolSlowCount(_slowPoolCount);

            return new UnmanagedMemoryPoolSlow(blockSize);
        }
    }

    public static void OnPoolDestroyed(IUnmanagedMemoryPool pool)
    {
        if (pool is UnmanagedMemoryPoolSlow)
        {
            Interlocked.Decrement(ref _slowPoolCount);
            TelemetryFactory.Metrics.RecordGaugePoolSlowCount(_slowPoolCount);
        }
        else
        {
            Interlocked.Decrement(ref _fastPoolCount);
            TelemetryFactory.Metrics.RecordGaugePoolCount(_fastPoolCount);
        }
    }
}
