// <copyright file="AttemptToFixRetryBehavior.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal readonly struct AttemptToFixRetryBehavior : IRetryBehavior
{
    public AttemptToFixRetryBehavior(ITestOptimization testOptimization)
    {
        RemainingRetries = testOptimization.TestManagementFeature?.TestManagementAttemptToFixRetryCount - 1 ?? TestOptimizationTestManagementFeature.TestManagementAttemptToFixRetryCountDefault;
    }

    public int RemainingRetries { get; }

    public string RetryMode => "AttemptToFix";

    // A single failure proves the fix did not work.
    public bool ShouldRetry(ITestResult result) => result.ResultState?.Status != TestStatus.Failed;

    public ITestResult ResultChanges(ITestResult result) => result;
}
