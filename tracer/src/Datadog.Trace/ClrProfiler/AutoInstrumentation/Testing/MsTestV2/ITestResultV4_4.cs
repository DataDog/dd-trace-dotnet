// <copyright file="ITestResultV4_4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2;

internal interface ITestResultV4_4 : ITestResult
{
    int RetryAttemptNumber { get; set; }

    bool IsSupersededRetryAttempt { get; set; }
}
