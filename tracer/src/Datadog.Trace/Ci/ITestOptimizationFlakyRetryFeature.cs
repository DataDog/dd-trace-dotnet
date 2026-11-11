// <copyright file="ITestOptimizationFlakyRetryFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Ci;

internal interface ITestOptimizationFlakyRetryFeature : ITestOptimizationFeature
{
    int FlakyRetryCount { get; }

    int TotalFlakyRetryCount { get; }

    /// <summary>
    /// Gets the number of retries for a failed test whose first execution took <paramref name="firstExecutionDuration"/>.
    /// It is <see cref="FlakyRetryCount"/> unless DD_CIVISIBILITY_DYNAMIC_ATR_ENABLED picks it from the duration.
    /// </summary>
    int GetFlakyRetryCount(TimeSpan firstExecutionDuration);
}
