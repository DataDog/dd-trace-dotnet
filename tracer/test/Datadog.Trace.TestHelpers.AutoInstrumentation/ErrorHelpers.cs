// <copyright file="ErrorHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public static class ErrorHelpers
{
    public static void CheckForKnownSkipConditions(ITestOutputHelper output, int exitCode, string standardError, EnvironmentHelper environmentHelper)
    {
#if NETCOREAPP2_1
        if (exitCode == 139)
        {
            // Segmentation faults are expected on .NET Core because of a bug in the runtime: https://github.com/dotnet/runtime/issues/11885
            throw new SkipException("Segmentation fault on .NET Core 2.1");
        }
#endif
        if (exitCode == 134
         && standardError?.Contains("System.Threading.AbandonedMutexException: The wait completed due to an abandoned mutex") == true
         && standardError?.Contains("Coverlet.Core.Instrumentation.Tracker") == true)
        {
            // Coverlet occasionally throws AbandonedMutexException during clean up
            throw new SkipException("Coverlet threw AbandonedMutexException during cleanup");
        }

#if NETCOREAPP2_1
        if (exitCode == 134 && EnvironmentTools.IsLinux())
        {
            // We see SIGABRT relatively frequently on .NET Core 2.1 on Linux, but probably not worth investigating further
            throw new SkipException("SIGABRT on .NET Core 2.1");
        }
#endif

        if (exitCode == 13)
        {
            // This is an "expected" issue, e.g. timeout talking to a required service
            // strictly a failure, but skipping to avoid flake in CI etc
            SendMetric(output, "dd_trace_dotnet.ci.tests.skipped_due_to_flake", environmentHelper).ConfigureAwait(false).GetAwaiter().GetResult();
            throw new SkipException("Exit code (13) - anticipated flake");
        }
    }

    public static async Task SendMetric(ITestOutputHelper outputHelper, string metricName, EnvironmentHelper environmentHelper)
    {
        var envKey = Environment.GetEnvironmentVariable("DD_LOGGER_DD_API_KEY");
        if (string.IsNullOrEmpty(envKey))
        {
            // We're probably not in CI
            return;
        }

        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("DD-API-KEY", envKey);

        var type = outputHelper.GetType();
        var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic);
        var test = (ITest)testMember?.GetValue(outputHelper);
        var testFullName = type.FullName + test?.TestCase.DisplayName;

        // In addition to logging, send a metric that will help us get more information through tags
        var srcBranch = Environment.GetEnvironmentVariable("DD_LOGGER_BUILD_SOURCEBRANCH");

        var tags = $$"""
                         "os.platform:{{SanitizeTagValue(FrameworkDescription.Instance.OSPlatform)}}",
                         "os.architecture:{{SanitizeTagValue(EnvironmentTools.GetPlatform())}}",
                         "target.framework:{{SanitizeTagValue(environmentHelper.GetTargetFramework())}}",
                         "test.name:{{SanitizeTagValue(testFullName)}}",
                         "git.branch:{{SanitizeTagValue(srcBranch)}}"
                     """;

        var payload = $$"""
                            {
                                "series": [{
                                    "metric": "{{metricName}}",
                                    "type": 1,
                                    "points": [{
                                        "timestamp": {{((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds()}},
                                        "value": 1
                                        }],
                                    "tags": [
                                        {{tags}}
                                    ]
                                }]
                            }
                        """;

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.datadoghq.com/api/v2/series", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            outputHelper.WriteLine($"Failed to submit metric {metricName}. Response was: Code: {response.StatusCode}. Response: {responseContent}. Payload sent was: \"{payload}\"");
        }
    }

    private static string SanitizeTagValue(string tag)
    {
        SpanTagHelper.TryNormalizeTagName(tag, normalizeSpaces: true, out var normalizedTag);
        return normalizedTag;
    }
}
