// <copyright file="DynamicFlakyRetryBehavior.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal readonly struct DynamicFlakyRetryBehavior : IRetryBehavior
{
    public DynamicFlakyRetryBehavior(ITestOptimization testOptimization, int remainingRetries)
    {
        // Share the same _totalRetries counter as FlakyRetryBehavior so that
        // pre-close budget checks in TestOptimizationTestCommand see the same value.
        RemainingRetries = remainingRetries;
        FlakyRetryBehavior.EnsureTotalRetriesInitialized(testOptimization);
    }

    public int RemainingRetries { get; }

    public string RetryMode => "DynamicFlakyRetry";

    public bool ShouldRetry(ITestResult result)
        => result.ResultState.Status == TestStatus.Failed && FlakyRetryBehavior.DecrementTotalRetries() > 0;

    public ITestResult ResultChanges(ITestResult result) => result;
}
