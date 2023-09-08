// <copyright file="TelemetryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.Telemetry;

internal static class TelemetryHelper
{
    /// <summary>
    /// Gets the CIVisibilityTestFramework enum from testing framework string
    /// </summary>
    /// <param name="testingFramework">Testing framework string</param>
    /// <returns>MetricTags.CIVisibilityTestFramework</returns>
    public static MetricTags.CIVisibilityTestFramework GetTelemetryTestingFrameworkEnum(string testingFramework)
    {
        return testingFramework switch
        {
            CommonTags.TestingFrameworkNameXUnit => MetricTags.CIVisibilityTestFramework.XUnit,
            CommonTags.TestingFrameworkNameNUnit => MetricTags.CIVisibilityTestFramework.NUnit,
            CommonTags.TestingFrameworkNameMsTestV2 => MetricTags.CIVisibilityTestFramework.MSTest,
            CommonTags.TestingFrameworkNameBenchmarkDotNet => MetricTags.CIVisibilityTestFramework.BenchmarkDotNet,
            _ => MetricTags.CIVisibilityTestFramework.Unknown
        };
    }

    /// <summary>
    /// Gets the CIVisibilityErrorType enum from the http response status code
    /// </summary>
    /// <param name="statusCode">Http status code</param>
    /// <returns>MetricTags.CIVisibilityErrorType</returns>
    public static MetricTags.CIVisibilityErrorType GetErrorTypeFromStatusCode(int statusCode)
    {
        return statusCode switch
        {
            >= 0 and < 200 => MetricTags.CIVisibilityErrorType.StatusCode,
            >= 300 and < 400 => MetricTags.CIVisibilityErrorType.StatusCode,
            >= 400 and < 500 => MetricTags.CIVisibilityErrorType.StatusCode4xx,
            >= 500 => MetricTags.CIVisibilityErrorType.StatusCode5xx,
            _ => MetricTags.CIVisibilityErrorType.Network,
        };
    }
}
