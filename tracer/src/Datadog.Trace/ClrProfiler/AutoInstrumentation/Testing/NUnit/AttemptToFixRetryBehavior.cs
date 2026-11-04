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
            if (NUnitIntegration.TryHideResult(result, "Flaky test is quarantined or disabled by Datadog."))
            {
                Common.Log.Debug("AttemptToFixRetryBehavior: Test is quarantined or disabled by Datadog.");
            }
            else
            {
                Common.Log.Debug("AttemptToFixRetryBehavior: Quarantined or disabled test did not fail and is a prerequisite, keeping its result for the dependent tests.");
            }
        }

        return result;
    }
}
