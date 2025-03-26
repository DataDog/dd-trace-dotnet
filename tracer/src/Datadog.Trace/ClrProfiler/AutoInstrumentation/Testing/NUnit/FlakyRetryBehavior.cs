// <copyright file="FlakyRetryBehavior.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading;
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal readonly struct FlakyRetryBehavior : IRetryBehavior
{
    private static int _totalRetries = -1;

    public FlakyRetryBehavior(ITestOptimization testOptimization)
    {
        RemainingRetries = testOptimization.FlakyRetryFeature?.FlakyRetryCount ?? TestOptimizationFlakyRetryFeature.FlakyRetryCountDefault;
        Interlocked.CompareExchange(ref _totalRetries, testOptimization.FlakyRetryFeature?.TotalFlakyRetryCount ?? TestOptimizationFlakyRetryFeature.TotalFlakyRetryCountDefault, -1);
    }

    public int RemainingRetries { get; }

    public string RetryMode => "FlakyRetry";

    public bool ShouldRetry(ITestResult result)
        => result.ResultState.Status == TestStatus.Failed && Interlocked.Decrement(ref _totalRetries) > 0;

    public ITestResult ResultChanges(ITestResult result) => result;
}
