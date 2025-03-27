// <copyright file="EarlyFlakeDetectionRetryBehavior.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit;

internal readonly struct EarlyFlakeDetectionRetryBehavior : IRetryBehavior
{
    public EarlyFlakeDetectionRetryBehavior(TimeSpan duration)
    {
        RemainingRetries = Common.GetNumberOfExecutionsForDuration(duration) - 1;
    }

    public int RemainingRetries { get; }

    public string RetryMode => "EarlyFlakeDetection";

    public bool ShouldRetry(ITestResult result) => true;

    public ITestResult ResultChanges(ITestResult result) => result;
}
