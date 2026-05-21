// <copyright file="ErrorHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.AutoInstrumentation;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers;

public enum RuntimeErrorOutcome
{
    /// <summary>No known fingerprint detected; proceed with normal validation.</summary>
    Proceed,

    /// <summary>Known fingerprint detected, retry budget remaining; caller should retry.</summary>
    Retry,

    /// <summary>Known fingerprint persisted past retry budget; caller should let the test fail.</summary>
    Persistent,
}

public static class ErrorHelpers
{
    private const string Runtime127957IssueTag = "runtime_issue:127957";
    private const string RuntimeMetadataRaceRetryMetric = "dd_trace_dotnet.ci.tests.retried_due_to_runtime_metadata_race";
    private const string RuntimeMetadataRacePersistentMetric = "dd_trace_dotnet.ci.tests.persistent_runtime_metadata_race";

    /// <summary>
    /// Dispatch helper for known transient runtime crashes. Returns whether the caller should
    /// retry, fail, or proceed normally. New fingerprints should be added as branches here.
    /// </summary>
    public static async Task<RuntimeErrorOutcome> HandleRuntimeSkippableErrorsAsync(
        int attempt, int maxAttempts, int exitCode, string stderr, TestHelper helper, Action<string> writeOutput)
    {
        if (!IsRuntime127957Race(exitCode, stderr))
        {
            return RuntimeErrorOutcome.Proceed;
        }

        if (attempt < maxAttempts)
        {
            writeOutput($"Detected dotnet/runtime#127957 race on attempt {attempt}/{maxAttempts}, retrying.");
            await helper.SendCIMetricAsync(RuntimeMetadataRaceRetryMetric, Runtime127957IssueTag);
            return RuntimeErrorOutcome.Retry;
        }

        writeOutput($"dotnet/runtime#127957 fingerprint persisted across {maxAttempts} attempts; letting the test fail.");
        await helper.SendCIMetricAsync(RuntimeMetadataRacePersistentMetric, Runtime127957IssueTag);
        return RuntimeErrorOutcome.Persistent;
    }

    public static bool IsRuntime127957Race(int exitCode, string standardError)
    {
        // Both fingerprints of dotnet/runtime#127957 surface as a SIGABRT (exit 134) caused by
        // an unhandled exception on a runtime worker thread that read corrupted metadata.
        if (exitCode != 134 || standardError is null)
        {
            return false;
        }

        // Fingerprint A — TypeLoadException with the "Undefined resource string ID" fallback:
        // garbage flowed into the IDS_* argument of the throw site, so the localization
        // layer was asked for a string ID that doesn't exist.
        if (standardError.Contains("System.TypeLoadException")
            && standardError.Contains("Undefined resource string ID"))
        {
            return true;
        }

        // Fingerprint B — MissingMethodException for ConcurrentDictionary.TryGetValue:
        // the canonical example in the runtime issue. A method signature read returns
        // garbage from a freed metadata buffer, so the runtime concludes the method doesn't exist.
        if (standardError.Contains("System.MissingMethodException: Method not found:")
            && standardError.Contains("ConcurrentDictionary")
            && standardError.Contains("TryGetValue"))
        {
            return true;
        }

        return false;
    }

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

        SkipKnownCrashes(environmentHelper.PathToCrashReport, output).Wait();
    }

    public static async Task SendMetric(ITestOutputHelper outputHelper, string metricName, EnvironmentHelper environmentHelper, params string[] extraTags)
    {
        const int maxTestFullNameLength = 200;

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
        var displayName = test?.TestCase.DisplayName ?? string.Empty;
        var testFullName = (displayName.StartsWith(type.FullName) ?
            displayName :
            $"{type.FullName}.{displayName}").Trim();

        testFullName = testFullName.Substring(0, Math.Min(testFullName.Length, maxTestFullNameLength));

        // In addition to logging, send a metric that will help us get more information through tags
        var srcBranch = Environment.GetEnvironmentVariable("DD_LOGGER_BUILD_SOURCEBRANCH");

        var tags = $$"""
                         "os.platform:{{SanitizeTagValue(FrameworkDescription.Instance.OSPlatform)}}",
                         "os.architecture:{{SanitizeTagValue(EnvironmentTools.GetPlatform())}}",
                         "target.framework:{{environmentHelper.GetTargetFramework()}}",
                         "test.name:{{SanitizeTagValue(testFullName)}}",
                         "git.branch:{{SanitizeTagValue(srcBranch)}}"
                     """;

        if (extraTags is { Length: > 0 })
        {
            tags += ", " + string.Join(", ", extraTags.Select(t => $"\"{t}\""));
        }

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

        try
        {
            var response = await client.PostAsync("https://api.datadoghq.com/api/v2/series", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.Accepted)
            {
                outputHelper.WriteLine($"Failed to submit metric {metricName}. Response was: Code: {response.StatusCode}. Response: {responseContent}. Payload sent was: \"{payload}\"");
            }
        }
        catch (Exception ex)
        {
            outputHelper.WriteLine($"Failed to submit metric {metricName}. Exception: {ex.ToString()} Payload: \"{payload}\"");
        }
    }

    private static string SanitizeTagValue(string tag)
    {
        SpanTagHelper.TryNormalizeTagName(tag, normalizeSpaces: true, out var normalizedTag);
        return normalizedTag;
    }

    private static async Task SkipKnownCrashes(string pathToCrashReport, ITestOutputHelper output)
    {
        if (pathToCrashReport == null || !File.Exists(pathToCrashReport))
        {
            return;
        }

        try
        {
            using var crashReport = new CrashReport(pathToCrashReport, output);
            var stacktrace = await crashReport.ResolveCrashStackTrace();

            output.WriteLine("Crash stacktrace:");

            foreach (var frame in stacktrace)
            {
                output.WriteLine(frame);
            }

            // TODO: Add logic to throw SkipException on known crashes
        }
        catch (Exception ex) when (ex is not SkipException)
        {
            output.WriteLine($"Unexpected exception while analyzing the crash report: {ex}");
        }
    }
}
