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
    public static MetricTags.CIVisibilityErrorType? GetErrorTypeFromStatusCode(int statusCode)
    {
        return statusCode switch
        {
            >= 0 and < 200 => MetricTags.CIVisibilityErrorType.StatusCode,
            >= 200 and < 300 => null,
            >= 300 and < 400 => MetricTags.CIVisibilityErrorType.StatusCode,
            >= 400 and < 500 => MetricTags.CIVisibilityErrorType.StatusCode4xx,
            >= 500 => MetricTags.CIVisibilityErrorType.StatusCode5xx,
            _ => MetricTags.CIVisibilityErrorType.Network,
        };
    }

    /// <summary>
    /// Gets the CIVisibilityExitCodes enum from the process exit code
    /// </summary>
    /// <param name="exitCode">Command exit code</param>
    /// <returns>MetricTags.CIVisibilityExitCodes</returns>
    public static MetricTags.CIVisibilityExitCodes GetTelemetryExitCodeFromExitCode(int exitCode)
    {
        return exitCode switch
        {
            -1 => MetricTags.CIVisibilityExitCodes.ECMinus1,
            1 => MetricTags.CIVisibilityExitCodes.EC1,
            2 => MetricTags.CIVisibilityExitCodes.EC2,
            127 => MetricTags.CIVisibilityExitCodes.EC127,
            128 => MetricTags.CIVisibilityExitCodes.EC128,
            129 => MetricTags.CIVisibilityExitCodes.EC129,
            _ => MetricTags.CIVisibilityExitCodes.Unknown,
        };
    }

    /// <summary>
    /// Gets the CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark enum from the current data
    /// </summary>
    /// <param name="eventType">Event Type</param>
    /// <param name="isBenchmark">True if is a benchmark event</param>
    /// <returns>MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark</returns>
    public static MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark? GetEventTypeWithCodeOwnerAndSupportedCiAndBenchmark(MetricTags.CIVisibilityTestingEventType eventType, bool isBenchmark)
    {
        switch (eventType)
        {
            case MetricTags.CIVisibilityTestingEventType.Test:
                return isBenchmark ?
                           MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Test_IsBenchmark :
                           MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Test;
            case MetricTags.CIVisibilityTestingEventType.Suite:
                return MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Suite;
            case MetricTags.CIVisibilityTestingEventType.Module:
                return MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Module;
            case MetricTags.CIVisibilityTestingEventType.Session:
            {
                return CIEnvironmentValues.Instance switch
                {
                    { CodeOwners: not null, IsCI: true } => MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Session_HasCodeOwner_IsSupportedCi,
                    { CodeOwners: not null, IsCI: false } => MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Session_HasCodeOwner_UnsupportedCi,
                    { CodeOwners: null, IsCI: true } => MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Session_NoCodeOwner_IsSupportedCi,
                    { CodeOwners: null, IsCI: false } => MetricTags.CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark.Session_NoCodeOwner_UnsupportedCi,
                };
            }
        }

        return null;
    }
}
