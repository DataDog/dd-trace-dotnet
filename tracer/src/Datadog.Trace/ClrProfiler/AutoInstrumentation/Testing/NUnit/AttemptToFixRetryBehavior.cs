// <copyright file="AttemptToFixRetryBehavior.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Net;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal readonly struct AttemptToFixRetryBehavior : IRetryBehavior
{
    private readonly TestOptimizationClient.TestManagementResponseTestPropertiesAttributes _testProperties;

    public AttemptToFixRetryBehavior(ITestOptimization testOptimization, TestOptimizationClient.TestManagementResponseTestPropertiesAttributes testProperties)
    {
        RemainingRetries = testOptimization.TestManagementFeature?.TestManagementAttemptToFixRetryCount - 1 ?? TestOptimizationTestManagementFeature.TestManagementAttemptToFixRetryCountDefault;
        _testProperties = testProperties;
    }

    public int RemainingRetries { get; }

    public string RetryMode => "AttemptToFix";

    public bool ShouldRetry(ITestResult result) => true;

    public ITestResult ResultChanges(ITestResult result)
    {
        if (_testProperties is { Quarantined: true } or { Disabled: true })
        {
            Common.Log.Debug("AttemptToFixRetryBehavior: Test is quarantined or disabled by Datadog.");
            result.SetResult(result.ResultState.StaticIgnored, "Flaky test is quarantined or disabled by Datadog.", string.Empty);
        }

        return result;
    }
}
